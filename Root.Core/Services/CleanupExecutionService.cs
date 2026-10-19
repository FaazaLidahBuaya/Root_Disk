using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Implementasi eksekusi pembersihan berkas dengan pemeriksaan ulang pra-penghapusan (pre-deletion re-validation),
/// perlindungan direktori sistem operasi, dan audit menyeluruh.
/// </summary>
public class CleanupExecutionService : ICleanupExecutionService
{
    private readonly ISystemProtectionService _protectionService;
    private readonly ILoggerService? _logger;

    public CleanupExecutionService(
        ISystemProtectionService? protectionService = null,
        ILoggerService? logger = null)
    {
        _protectionService = protectionService ?? new SystemProtectionService();
        _logger = logger;
    }

    public async Task<CleanupExecutionResult> ExecuteCleanupAsync(
        IEnumerable<CleanupCandidateModel> selectedCandidates,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var candidateList = selectedCandidates.ToList();
        var result = new CleanupExecutionResult
        {
            StartedAt = DateTime.UtcNow,
            TotalCandidatesEvaluated = candidateList.Count,
            TotalSelectedCount = candidateList.Count
        };

        if (candidateList.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.IsCancelled = true;
            }
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }

        _logger?.LogInfo($"Memulai eksekusi pembersihan konservatif pada {candidateList.Count:N0} berkas terpilih...");

        var progressReport = new CleanupProgressReport
        {
            Stage = "Memulai pemeriksaan ulang target...",
            TotalCount = candidateList.Count,
            ProcessedCount = 0
        };

        try
        {
            await Task.Run(() =>
        {
            int index = 0;

            foreach (var candidate in candidateList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.IsCancelled = true;
                    _logger?.LogWarning("Eksekusi pembersihan dihentikan atas permintaan pembatalan pengguna.");
                    break;
                }

                index++;
                progressReport.ProcessedCount = index;
                progressReport.CurrentFilePath = candidate.FullPath;
                progressReport.Stage = $"Memeriksa & membersihkan ({index}/{candidateList.Count})...";
                progressReport.FreedBytesSoFar = result.TotalFreedSizeBytes;
                progress?.Report(progressReport);

                var itemResult = ProcessSingleCandidate(candidate);
                result.ItemResults.Add(itemResult);

                // Perbarui metrik agregat
                switch (itemResult.Status)
                {
                    case CleanupItemStatus.BerhasilDihapus:
                        result.TotalDeletedCount++;
                        result.TotalFreedSizeBytes += itemResult.FileSizeBytes;
                        _logger?.LogSuccess($"Berkas berhasil dibersihkan: {candidate.FullPath} ({candidate.FormattedSize}).");
                        break;

                    case CleanupItemStatus.DilewatiBerkasBerubah:
                        result.SkippedModifiedCount++;
                        _logger?.LogWarning($"Pemeriksaan ulang: Berkas dilewati karena telah berubah sejak analisis ({candidate.FullPath}).");
                        break;

                    case CleanupItemStatus.DilewatiBerkasHilang:
                        result.SkippedMissingCount++;
                        _logger?.LogWarning($"Pemeriksaan ulang: Berkas sudah tidak ditemukan di disk ({candidate.FullPath}).");
                        break;

                    case CleanupItemStatus.DitolakProteksiSistem:
                        result.BlockedProtectedCount++;
                        _logger?.LogError($"KEAMANAN: Percobaan penghapusan ditolak karena jalur berada di sistem terlindungi ({candidate.FullPath}).");
                        break;

                    case CleanupItemStatus.GagalIzinDitolak:
                        result.FailedPermissionCount++;
                        _logger?.LogError($"Gagal menghapus berkas karena izin akses ditolak ({candidate.FullPath}): {itemResult.ErrorMessage}");
                        break;

                    case CleanupItemStatus.GagalBerkasTerkunci:
                        result.FailedLockedCount++;
                        _logger?.LogError($"Gagal menghapus berkas karena sedang digunakan/terkunci proses lain ({candidate.FullPath}): {itemResult.ErrorMessage}");
                        break;

                    case CleanupItemStatus.GagalLainnya:
                    default:
                        result.FailedOtherCount++;
                        _logger?.LogError($"Gagal menghapus berkas ({candidate.FullPath}): {itemResult.ErrorMessage}");
                        break;
                }
            }
        }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result.IsCancelled = true;
            _logger?.LogWarning("Eksekusi pembersihan dihentikan atas permintaan pembatalan pengguna.");
        }

        result.CompletedAt = DateTime.UtcNow;
        _logger?.LogInfo($"Eksekusi pembersihan selesai: {result.TotalDeletedCount:N0} berkas dihapus, {result.FormattedTotalFreedSpace} ruang dibebaskan.");
        return result;
    }

    private CleanupItemResult ProcessSingleCandidate(CleanupCandidateModel candidate)
    {
        string path = candidate.FullPath;

        // 1. Pemeriksaan Ulang: Proteksi Direktori & Ekstensi Sistem
        if (_protectionService.IsProtectedPath(path) || _protectionService.IsProtectedExtension(path))
        {
            return new CleanupItemResult(
                path,
                candidate.Category,
                candidate.FileSizeBytes,
                CleanupItemStatus.DitolakProteksiSistem,
                "Jalur terdeteksi sebagai bagian dari sistem operasi terproteksi.");
        }

        // 2. Pemeriksaan Ulang: Keberadaan Berkas di Disk
        if (!File.Exists(path))
        {
            return new CleanupItemResult(
                path,
                candidate.Category,
                candidate.FileSizeBytes,
                CleanupItemStatus.DilewatiBerkasHilang,
                "Berkas tidak ditemukan saat verifikasi pra-penghapusan.");
        }

        // 3. Pemeriksaan Ulang: Verifikasi Integritas, Reparse Point, dan Modifikasi Berkas
        FileAttributes originalAttributes = default;
        bool attributesModified = false;

        try
        {
            var freshInfo = new FileInfo(path);
            if (!freshInfo.Exists)
            {
                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    candidate.FileSizeBytes,
                    CleanupItemStatus.DilewatiBerkasHilang,
                    "Berkas tidak ditemukan sesaat sebelum penghapusan.");
            }

            originalAttributes = freshInfo.Attributes;

            // F-02 Proteksi TOCTOU: Tolak jika berkas merupakan reparse point (symlink/junction)
            if ((originalAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    candidate.FileSizeBytes,
                    CleanupItemStatus.DitolakProteksiSistem,
                    "Jalur terdeteksi sebagai reparse point (tautan simbolis/junction) dan dilarang dihapus demi integritas sistem.");
            }

            // Periksa apakah ukuran berkas berubah sejak analisis
            if (freshInfo.Length != candidate.FileSizeBytes)
            {
                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    freshInfo.Length,
                    CleanupItemStatus.DilewatiBerkasBerubah,
                    $"Ukuran berkas berubah (Analisis: {candidate.FileSizeBytes} bita, Sekarang: {freshInfo.Length} bita).");
            }

            // Periksa apakah waktu modifikasi berubah signifikan (toleransi 2 detik)
            DateTime freshModifiedUtc = freshInfo.LastWriteTimeUtc;
            DateTime recordedModifiedUtc = candidate.LastModified.ToUniversalTime();
            if (Math.Abs((freshModifiedUtc - recordedModifiedUtc).TotalSeconds) > 2.0)
            {
                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    freshInfo.Length,
                    CleanupItemStatus.DilewatiBerkasBerubah,
                    $"Waktu modifikasi berkas berubah setelah analisis ({freshModifiedUtc:yyyy-MM-dd HH:mm:ss} vs {recordedModifiedUtc:yyyy-MM-dd HH:mm:ss}).");
            }

            // F-01: Jika berkas memiliki atribut ReadOnly, lepaskan sementara agar dapat diverifikasi dan dihapus dengan aman
            if ((originalAttributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, originalAttributes & ~FileAttributes.ReadOnly);
                attributesModified = true;
            }

            // F-02 Mitigasi TOCTOU: Verifikasi akses eksklusif / integritas isi sebelum penghapusan
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.None);
                if (fs.Length != candidate.FileSizeBytes)
                {
                    return new CleanupItemResult(
                        path,
                        candidate.Category,
                        fs.Length,
                        CleanupItemStatus.DilewatiBerkasBerubah,
                        $"Ukuran berkas berubah saat dibuka via handle ({fs.Length} vs {candidate.FileSizeBytes}).");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    candidate.FileSizeBytes,
                    CleanupItemStatus.GagalIzinDitolak,
                    ex.Message);
            }
            catch (IOException ex)
            {
                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    candidate.FileSizeBytes,
                    CleanupItemStatus.GagalBerkasTerkunci,
                    ex.Message);
            }

            // 4. Eksekusi Penghapusan Permanen Berkas Tunggal
            File.Delete(path);

            // Verifikasi pasca-penghapusan
            if (File.Exists(path))
            {
                // F-01 Rollback: Pulihkan atribut asli jika berkas masih ada
                if (attributesModified)
                {
                    try { File.SetAttributes(path, originalAttributes); } catch { }
                    attributesModified = false;
                }

                return new CleanupItemResult(
                    path,
                    candidate.Category,
                    candidate.FileSizeBytes,
                    CleanupItemStatus.GagalLainnya,
                    "Berkas masih terdeteksi di disk setelah operasi penghapusan.");
            }

            return new CleanupItemResult(
                path,
                candidate.Category,
                candidate.FileSizeBytes,
                CleanupItemStatus.BerhasilDihapus);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new CleanupItemResult(
                path,
                candidate.Category,
                candidate.FileSizeBytes,
                CleanupItemStatus.GagalIzinDitolak,
                ex.Message);
        }
        catch (IOException ex)
        {
            return new CleanupItemResult(
                path,
                candidate.Category,
                candidate.FileSizeBytes,
                CleanupItemStatus.GagalBerkasTerkunci,
                ex.Message);
        }
        catch (Exception ex)
        {
            return new CleanupItemResult(
                path,
                candidate.Category,
                candidate.FileSizeBytes,
                CleanupItemStatus.GagalLainnya,
                ex.Message);
        }
        finally
        {
            // F-01 Rollback: Jaminan pemulihan atribut asli jika berkas masih ada di disk
            if (attributesModified)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, originalAttributes);
                    }
                }
                catch
                {
                    // Abaikan kegagalan pemulihan atribut
                }
            }
        }
    }
}
