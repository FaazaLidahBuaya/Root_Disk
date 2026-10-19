using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Implementasi mesin analisis kandidat pembersihan yang konservatif.
/// Tidak menganggap semua cache atau berkas besar aman dihapus.
/// Menerapkan perlindungan ketat terhadap direktori sistem Windows.
/// </summary>
public class CleanupAnalyzerService : ICleanupAnalyzerService
{
    private readonly ISystemProtectionService _protectionService;
    private readonly ILoggerService? _logger;

    public CleanupAnalyzerService(
        ISystemProtectionService? protectionService = null,
        ILoggerService? logger = null)
    {
        _protectionService = protectionService ?? new SystemProtectionService();
        _logger = logger;
    }

    public async Task<CleanupPreviewModel> AnalyzeFromScanTreeAsync(
        FileSystemItemModel rootItem,
        CleanupOptions? options = null,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CleanupOptions();
        var candidates = new List<CleanupCandidateModel>();
        var progressReport = new CleanupProgressReport { Stage = "Mengumpulkan berkas dari pohon pemindaian..." };

        await Task.Run(() =>
        {
            var allFiles = new List<FileSystemItemModel>();
            CollectFiles(rootItem, allFiles, cancellationToken);

            progressReport.TotalCount = allFiles.Count;
            progressReport.Stage = "Menganalisis kriteria konservatif...";

            int processed = 0;
            DateTime nowUtc = DateTime.UtcNow;

            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processed++;
                if (processed % 200 == 0 || processed == allFiles.Count)
                {
                    progressReport.ProcessedCount = processed;
                    progressReport.CurrentFilePath = file.FullPath;
                    progress?.Report(progressReport);
                }

                var candidate = EvaluateFile(
                    file.FullPath,
                    file.Name,
                    file.Extension,
                    file.SizeBytes,
                    file.LastModified,
                    options,
                    nowUtc);

                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
        }, cancellationToken);

        _logger?.LogInfo($"Analisis pembersihan selesai dari pohon pemindaian: {candidates.Count:N0} kandidat konservatif ditemukan.");
        return new CleanupPreviewModel(candidates);
    }

    public async Task<CleanupPreviewModel> AnalyzeDirectoryAsync(
        string directoryPath,
        CleanupOptions? options = null,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CleanupOptions();
        var candidates = new List<CleanupCandidateModel>();
        var progressReport = new CleanupProgressReport { Stage = "Memindai direktori untuk kandidat pembersihan..." };

        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return new CleanupPreviewModel(candidates);
        }

        if (_protectionService.IsProtectedPath(directoryPath))
        {
            _logger?.LogWarning($"Direktori target berada dalam area sistem terproteksi: {directoryPath}. Analisis dibatalkan demi keamanan.");
            return new CleanupPreviewModel(candidates);
        }

        await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            var queue = new Queue<DirectoryInfo>();
            queue.Enqueue(dirInfo);

            int processed = 0;
            DateTime nowUtc = DateTime.UtcNow;

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDir = queue.Dequeue();

                // Perlindungan: jangan masuki folder sistem
                if (_protectionService.IsProtectedPath(currentDir.FullName))
                {
                    continue;
                }

                FileInfo[] files;
                DirectoryInfo[] subDirs;

                try
                {
                    files = currentDir.GetFiles();
                    subDirs = currentDir.GetDirectories();
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    try
                    {
                        // Lewati reparse points (symlinks/junctions) dan direktori sistem
                        if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        if (!_protectionService.IsProtectedPath(subDir.FullName))
                        {
                            queue.Enqueue(subDir);
                        }
                    }
                    catch
                    {
                        // Abaikan folder bermasalah
                    }
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    processed++;

                    if (processed % 100 == 0)
                    {
                        progressReport.ProcessedCount = processed;
                        progressReport.CurrentFilePath = file.FullName;
                        progressReport.Stage = $"Menganalisis {processed:N0} berkas...";
                        progress?.Report(progressReport);
                    }

                    var candidate = EvaluateFile(
                        file.FullName,
                        file.Name,
                        file.Extension,
                        file.Length,
                        file.LastWriteTimeUtc,
                        options,
                        nowUtc);

                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }
        }, cancellationToken);

        _logger?.LogInfo($"Analisis pembersihan direktori selesai: {candidates.Count:N0} kandidat konservatif ditemukan.");
        return new CleanupPreviewModel(candidates);
    }

    private void CollectFiles(FileSystemItemModel item, List<FileSystemItemModel> list, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!item.IsDirectory)
        {
            list.Add(item);
            return;
        }

        foreach (var child in item.Children)
        {
            CollectFiles(child, list, ct);
        }
    }

    private CleanupCandidateModel? EvaluateFile(
        string fullPath,
        string fileName,
        string extension,
        long sizeBytes,
        DateTime? lastModifiedUtc,
        CleanupOptions options,
        DateTime nowUtc)
    {
        if (!lastModifiedUtc.HasValue)
        {
            return null;
        }

        // 1. Perlindungan Ketat: Jika jalur atau ekstensi terproteksi, JANGAN PERNAH masukkan sebagai kandidat
        if (_protectionService.IsProtectedPath(fullPath) || _protectionService.IsProtectedExtension(fullPath))
        {
            return null;
        }

        if (sizeBytes < options.MinimumFileSizeBytes)
        {
            return null;
        }

        string ext = (extension ?? string.Empty).ToLowerInvariant();
        string name = fileName.ToLowerInvariant();
        TimeSpan age = nowUtc - lastModifiedUtc.Value;
        int ageDays = Math.Max(0, (int)age.TotalDays);

        // 2. Evaluasi Konservatif Kategori 1: Berkas Sementara (.tmp, .bak, dll.)
        if (options.IncludeTemporaryFiles && (ext == ".tmp" || ext == ".bak" || ext == ".swp" || name.EndsWith("~") || name.StartsWith("~")))
        {
            if (ageDays >= options.MinimumAgeDaysTemporaryFiles)
            {
                return new CleanupCandidateModel(
                    fullPath: fullPath,
                    fileName: fileName,
                    directoryPath: Path.GetDirectoryName(fullPath) ?? string.Empty,
                    extension: ext,
                    category: CleanupCategory.BerkasSementara,
                    reason: $"Berkas sementara ({ext}) tidak dimodifikasi selama {ageDays} hari (konservatif > {options.MinimumAgeDaysTemporaryFiles} hari).",
                    fileSizeBytes: sizeBytes,
                    lastModified: lastModifiedUtc.Value.ToLocalTime(),
                    safetyLevel: CleanupSafetyLevel.AmanKonservatif,
                    isProtected: false);
            }

            // Jika berkas sementara masih baru (< 14 hari), berkas mungkin aktif dipakai aplikasi. JANGAN HAPUS!
            return null;
        }

        // 3. Evaluasi Konservatif Kategori 2: Crash Dump & Diagnostik (.dmp, .mdmp, .crash)
        if (options.IncludeCrashDumps && (ext == ".dmp" || ext == ".mdmp" || ext == ".crash"))
        {
            if (ageDays >= options.MinimumAgeDaysCrashDumps)
            {
                return new CleanupCandidateModel(
                    fullPath: fullPath,
                    fileName: fileName,
                    directoryPath: Path.GetDirectoryName(fullPath) ?? string.Empty,
                    extension: ext,
                    category: CleanupCategory.CrashDump,
                    reason: $"Crash dump/diagnostik ({ext}) lama yang tersisa dari kegagalan aplikasi ({ageDays} hari lalu).",
                    fileSizeBytes: sizeBytes,
                    lastModified: lastModifiedUtc.Value.ToLocalTime(),
                    safetyLevel: CleanupSafetyLevel.AmanKonservatif,
                    isProtected: false);
            }

            return null;
        }

        // 4. Evaluasi Konservatif Kategori 3: Unduhan Peramban Terbengkalai (.crdownload, .part, .partial)
        if (options.IncludeIncompleteDownloads && (ext == ".crdownload" || ext == ".part" || ext == ".partial"))
        {
            if (ageDays >= options.MinimumAgeDaysIncompleteDownloads)
            {
                return new CleanupCandidateModel(
                    fullPath: fullPath,
                    fileName: fileName,
                    directoryPath: Path.GetDirectoryName(fullPath) ?? string.Empty,
                    extension: ext,
                    category: CleanupCategory.UnduhanTerbengkalai,
                    reason: $"Unduhan terhenti/terbengkalai ({ext}) yang tidak dilanjutkan selama {ageDays} hari.",
                    fileSizeBytes: sizeBytes,
                    lastModified: lastModifiedUtc.Value.ToLocalTime(),
                    safetyLevel: CleanupSafetyLevel.AmanKonservatif,
                    isProtected: false);
            }

            return null;
        }

        // 5. Evaluasi Konservatif Kategori 4: Basis Data Thumbnail Usang Lokal
        if (options.IncludeOrphanedThumbnails && (name == "thumbs.db" || name == "ehthumbs.db"))
        {
            if (ageDays >= 14)
            {
                return new CleanupCandidateModel(
                    fullPath: fullPath,
                    fileName: fileName,
                    directoryPath: Path.GetDirectoryName(fullPath) ?? string.Empty,
                    extension: ext,
                    category: CleanupCategory.ThumbnailCacheUsang,
                    reason: $"Basis data thumbnail lokal ({fileName}) lama ({ageDays} hari). Windows akan membuat ulang jika dibutuhkan.",
                    fileSizeBytes: sizeBytes,
                    lastModified: lastModifiedUtc.Value.ToLocalTime(),
                    safetyLevel: CleanupSafetyLevel.AmanKonservatif,
                    isProtected: false);
            }

            return null;
        }

        // 6. Evaluasi Konservatif Kategori 5: Log Aplikasi Kadaluarsa (.log, .log.old)
        if (options.IncludeExpiredLogs && (ext == ".log" || name.EndsWith(".log.old") || ext == ".old"))
        {
            if (ageDays >= options.MinimumAgeDaysExpiredLogs)
            {
                return new CleanupCandidateModel(
                    fullPath: fullPath,
                    fileName: fileName,
                    directoryPath: Path.GetDirectoryName(fullPath) ?? string.Empty,
                    extension: ext,
                    category: CleanupCategory.LogKadaluarsa,
                    reason: $"Log aplikasi kadaluarsa ({fileName}) berusia {ageDays} hari (konservatif > {options.MinimumAgeDaysExpiredLogs} hari).",
                    fileSizeBytes: sizeBytes,
                    lastModified: lastModifiedUtc.Value.ToLocalTime(),
                    safetyLevel: CleanupSafetyLevel.PerluPerhatian,
                    isProtected: false);
            }

            return null;
        }

        // Semua berkas lainnya (dokumen, foto, video, kode sumber, arsip zip, executable, file baru, dll.) DIABAIKAN.
        // Tidak menganggap semua cache atau file besar aman dihapus!
        return null;
    }
}
