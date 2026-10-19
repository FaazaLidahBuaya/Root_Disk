using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Utilities;

namespace Root.Core.Services;

/// <summary>
/// Layanan pencarian berkas duplikat berbasis perbandingan ukuran (size-grouping)
/// dan verifikasi hash kriptografi SHA-256.
/// Mengoptimalkan kinerja untuk berkas besar dengan streaming async dan partial-hash filter,
/// serta menangani berkas 0-byte, kegagalan izin/baca, dan pembatalan tanpa modifikasi sistem berkas.
/// </summary>
public class DuplicateFinderService : IDuplicateFinderService
{
    private const string ZeroByteSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const int StreamBufferSize = 64 * 1024; // 64 KB stream buffer
    private const int PartialHashHeaderSize = 4 * 1024; // 4 KB partial header check
    private const int ProgressReportIntervalMs = 80;

    private readonly ILoggerService? _logger;

    public DuplicateFinderService(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public async Task<DuplicateSearchResult> FindDuplicatesAsync(
        IEnumerable<FileSystemItemModel> fileItems,
        DuplicateSearchOptions? options = null,
        IProgress<DuplicateProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DuplicateSearchOptions();
        var allFiles = FlattenFiles(fileItems).ToList();
        return await ProcessCandidateFilesAsync(allFiles, options, progress, cancellationToken);
    }

    public async Task<DuplicateSearchResult> FindDuplicatesInDirectoryAsync(
        string directoryPath,
        DuplicateSearchOptions? options = null,
        IProgress<DuplicateProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DuplicateSearchOptions();

        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Jalur direktori tidak boleh kosong.", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Direktori target '{directoryPath}' tidak ditemukan.");

        var files = new List<FileSystemItemModel>();
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    files.Add(new FileSystemItemModel
                    {
                        FullPath = file.FullName,
                        Name = file.Name,
                        Extension = file.Extension,
                        SizeBytes = file.Length,
                        LastModified = file.LastWriteTime,
                        IsDirectory = false
                    });
                }
                catch
                {
                    // Abaikan berkas yang tidak bisa dibaca saat enumerasi awal; akan dicatat saat proses utama jika relevan
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Peringatan saat mengenumerasi direktori '{directoryPath}': {ex.Message}");
        }

        return await ProcessCandidateFilesAsync(files, options, progress, cancellationToken);
    }

    private async Task<DuplicateSearchResult> ProcessCandidateFilesAsync(
        List<FileSystemItemModel> files,
        DuplicateSearchOptions options,
        IProgress<DuplicateProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger?.LogInfo($"Memulai pencarian berkas duplikat pada {files.Count:N0} berkas terdaftar.");

        var result = new DuplicateSearchResult
        {
            TotalScannedFiles = files.Count
        };

        // 1. TAHAP 1: Penyaringan awal & Pengelompokan Berdasarkan Ukuran (Size Grouping)
        var filteredFiles = files.Where(f => !f.IsDirectory);

        if (!options.IncludeZeroByteFiles)
        {
            filteredFiles = filteredFiles.Where(f => f.SizeBytes > 0);
        }

        if (options.MinimumSizeBytes > 0)
        {
            filteredFiles = filteredFiles.Where(f => f.SizeBytes >= options.MinimumSizeBytes || (options.IncludeZeroByteFiles && f.SizeBytes == 0));
        }

        if (!string.IsNullOrWhiteSpace(options.ExtensionFilter))
        {
            var extensions = options.ExtensionFilter.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filteredFiles = filteredFiles.Where(f => extensions.Any(ext => f.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)));
        }

        // Kelompokkan berkas berdasarkan ukuran bita yang sama
        var sizeGroups = filteredFiles
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1) // Hanya berkas dengan ukuran identik yang menjadi kandidat
            .OrderByDescending(g => g.Key) // Periksa berkas terbesar terlebih dahulu
            .ToList();

        int totalCandidates = sizeGroups.Sum(g => g.Count());
        result.TotalCandidateFiles = totalCandidates;

        _logger?.LogInfo($"Tahap 1 selesai: {sizeGroups.Count:N0} kelompok ukuran identik ditemukan dengan total {totalCandidates:N0} berkas kandidat.");

        if (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            result.IsCancelled = true;
            result.Duration = stopwatch.Elapsed;
            _logger?.LogWarning("Pencarian duplikat dibatalkan oleh pengguna.");
            return result;
        }

        if (totalCandidates == 0)
        {
            stopwatch.Stop();
            result.IsCancelled = cancellationToken.IsCancellationRequested;
            result.Duration = stopwatch.Elapsed;
            _logger?.LogInfo("Tidak ada kandidat berkas berukuran sama. Tidak perlu verifikasi hash SHA-256.");
            return result;
        }

        var progressReport = new DuplicateProgressReport
        {
            TotalFilesDiscovered = files.Count,
            CandidateGroupsCount = sizeGroups.Count,
            CandidateFilesCount = totalCandidates,
            CurrentStage = "Memeriksa hash berkas kandidat..."
        };

        long lastReportTicks = 0;
        void ReportProgress(string currentFile, bool force = false)
        {
            if (progress == null) return;
            long currentTicks = stopwatch.ElapsedMilliseconds;
            if (!force && currentTicks - lastReportTicks < ProgressReportIntervalMs)
                return;

            lastReportTicks = currentTicks;
            progressReport.CurrentProcessingFile = currentFile;
            progressReport.ElapsedTime = stopwatch.Elapsed;
            progressReport.PercentComplete = totalCandidates > 0
                ? Math.Min(100.0, (double)progressReport.ProcessedFilesCount / totalCandidates * 100.0)
                : 100.0;
            progress.Report(progressReport);
        }

        // 2. TAHAP 2: Verifikasi Konten dengan SHA-256 (Streaming Hash & Pre-filter Parsial)
        long totalHashedBytes = 0;
        int totalHashedFiles = 0;

        foreach (var group in sizeGroups)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.IsCancelled = true;
                _logger?.LogWarning("Pencarian duplikat dibatalkan oleh pengguna saat pengujian kelompok ukuran.");
                break;
            }

            long currentSize = group.Key;
            var groupFiles = group.ToList();

            // Kasus Khusus: Berkas 0-byte (File Kosong)
            if (currentSize == 0)
            {
                var zeroByteGroup = new DuplicateGroupModel
                {
                    Sha256Hash = ZeroByteSha256,
                    FileSizeBytes = 0
                };

                foreach (var item in groupFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.IsCancelled = true;
                        break;
                    }

                    // Verifikasi berkas masih ada di disk dan benar-benar 0 byte
                    try
                    {
                        var fi = new FileInfo(item.FullPath);
                        if (!fi.Exists)
                        {
                            RecordError(result, item.FullPath, new FileNotFoundException($"Berkas '{item.FullPath}' tidak ditemukan saat verifikasi duplikat."), "Berkas Hilang / Terhapus");
                            continue;
                        }
                        if (fi.Length != 0)
                        {
                            RecordError(result, item.FullPath, new InvalidOperationException($"Ukuran berkas '{item.FullPath}' berubah saat diperiksa (menjadi {fi.Length} bita)."), "Berkas Berubah Saat Diperiksa");
                            continue;
                        }

                        zeroByteGroup.Files.Add(new DuplicateFileItem
                        {
                            FullPath = item.FullPath,
                            FileName = item.Name,
                            Extension = item.Extension,
                            SizeBytes = 0,
                            LastModified = fi.LastWriteTime,
                            Sha256Hash = ZeroByteSha256
                        });
                    }
                    catch (Exception ex)
                    {
                        RecordError(result, item.FullPath, ex, "Gagal memverifikasi berkas 0-byte");
                    }
                    finally
                    {
                        progressReport.ProcessedFilesCount++;
                        ReportProgress(item.FullPath);
                    }
                }

                if (zeroByteGroup.Files.Count > 1)
                {
                    result.DuplicateGroups.Add(zeroByteGroup);
                    progressReport.DuplicateGroupsFound++;
                    progressReport.DuplicateFilesFound += zeroByteGroup.Files.Count;
                }
                continue;
            }

            // TAHAP 2A: Pre-filter Parsial untuk Berkas Besar (> LargeFileThresholdBytes)
            // Jika ada lebih dari 2 berkas berukuran besar, bandingkan header hash (4 KB awal) dahulu.
            IEnumerable<List<FileSystemItemModel>> candidateSubGroups;
            if (currentSize > options.LargeFileThresholdBytes && groupFiles.Count > 2)
            {
                var partialHashGroups = new Dictionary<string, List<FileSystemItemModel>>(StringComparer.OrdinalIgnoreCase);

                foreach (var candidate in groupFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.IsCancelled = true;
                        break;
                    }

                    string? partialHash = await TryComputePartialHashAsync(candidate.FullPath, currentSize, result, cancellationToken);
                    if (partialHash != null)
                    {
                        if (!partialHashGroups.TryGetValue(partialHash, out var list))
                        {
                            list = [];
                            partialHashGroups[partialHash] = list;
                        }
                        list.Add(candidate);
                    }

                    progressReport.ProcessedFilesCount++;
                    ReportProgress(candidate.FullPath);
                }

                // Hanya sub-kelompok yang masih memiliki > 1 berkas yang dilanjutkan ke hash penuh
                candidateSubGroups = partialHashGroups.Values.Where(sub => sub.Count > 1).ToList();
            }
            else
            {
                candidateSubGroups = [groupFiles];
            }

            if (result.IsCancelled) break;

            // TAHAP 2B: Hash SHA-256 Penuh untuk memastikan isi 100% identik
            foreach (var subGroup in candidateSubGroups)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.IsCancelled = true;
                    break;
                }

                var fullHashDict = new Dictionary<string, List<DuplicateFileItem>>(StringComparer.OrdinalIgnoreCase);

                foreach (var candidate in subGroup)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.IsCancelled = true;
                        break;
                    }

                    var hashResult = await TryComputeFullSha256HashAsync(candidate.FullPath, currentSize, result, cancellationToken);
                    if (hashResult != null)
                    {
                        totalHashedBytes += currentSize;
                        totalHashedFiles++;

                        if (!fullHashDict.TryGetValue(hashResult.Value.Hash, out var list))
                        {
                            list = [];
                            fullHashDict[hashResult.Value.Hash] = list;
                        }

                        list.Add(new DuplicateFileItem
                        {
                            FullPath = candidate.FullPath,
                            FileName = candidate.Name,
                            Extension = candidate.Extension,
                            SizeBytes = currentSize,
                            LastModified = hashResult.Value.LastModified,
                            Sha256Hash = hashResult.Value.Hash
                        });
                    }

                    // Jika belum dihitung saat pre-filter parsial
                    if (currentSize <= options.LargeFileThresholdBytes || groupFiles.Count <= 2)
                    {
                        progressReport.ProcessedFilesCount++;
                    }

                    ReportProgress(candidate.FullPath);
                }

                // Daftarkan kelompok yang benar-benar memiliki duplikat (>= 2 berkas dengan hash identik)
                foreach (var kvp in fullHashDict.Where(k => k.Value.Count > 1))
                {
                    var dupGroup = new DuplicateGroupModel
                    {
                        Sha256Hash = kvp.Key,
                        FileSizeBytes = currentSize,
                        Files = kvp.Value
                    };

                    result.DuplicateGroups.Add(dupGroup);
                    progressReport.DuplicateGroupsFound++;
                    progressReport.DuplicateFilesFound += dupGroup.FileCount;
                    progressReport.TotalWasteBytesFound += dupGroup.WasteSizeBytes;
                }
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.TotalHashedBytes = totalHashedBytes;
        result.TotalHashedFiles = totalHashedFiles;

        progressReport.CurrentStage = result.IsCancelled ? "Pencarian Dibatalkan" : "Pencarian Selesai";
        ReportProgress(result.IsCancelled ? "Dibatalkan" : "Selesai", force: true);

        _logger?.LogSuccess($"Pencarian duplikat selesai dalam {result.FormattedDuration}: " +
            $"{result.TotalDuplicateGroups:N0} kelompok duplikat ditemukan, " +
            $"{result.TotalDuplicateFiles:N0} total berkas, " +
            $"pemborosan ruang {result.FormattedTotalWaste}, " +
            $"throughput hash: {result.FormattedThroughput}.");

        return result;
    }

    /// <summary>
    /// Menghitung hash parsial (header) untuk eliminasi cepat berkas besar yang memiliki ukuran sama tetapi isi awal berbeda.
    /// </summary>
    private async Task<string?> TryComputePartialHashAsync(
        string path, 
        long expectedSize, 
        DuplicateSearchResult result, 
        CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                RecordError(result, path, new FileNotFoundException($"Berkas '{path}' hilang saat hendak dibaca."), "Berkas Hilang / Terhapus");
                return null;
            }

            if (fileInfo.Length != expectedSize)
            {
                RecordError(result, path, new InvalidOperationException($"Ukuran berkas '{path}' berubah dari {expectedSize} menjadi {fileInfo.Length}."), "Berkas Berubah Saat Diperiksa");
                return null;
            }

            int bytesToRead = (int)Math.Min(PartialHashHeaderSize, expectedSize);
            byte[] buffer = new byte[bytesToRead];

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: bytesToRead,
                useAsync: true);

            if (stream.Length != expectedSize)
            {
                RecordError(result, path, new InvalidOperationException($"Ukuran stream berkas '{path}' tidak sesuai dengan ukuran yang diharapkan."), "Berkas Berubah Saat Diperiksa");
                return null;
            }

            int read = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(buffer, 0, read);
            return Convert.ToHexStringLower(hash);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            RecordError(result, path, ex, "Izin ditolak saat membaca header berkas");
            return null;
        }
        catch (FileNotFoundException ex)
        {
            RecordError(result, path, ex, "Berkas tidak ditemukan saat membaca header");
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            RecordError(result, path, ex, "Direktori berkas tidak ditemukan");
            return null;
        }
        catch (IOException ex)
        {
            RecordError(result, path, ex, "Kesalahan I/O saat membaca header berkas");
            return null;
        }
        catch (SecurityException ex)
        {
            RecordError(result, path, ex, "Pelanggaran keamanan sistem");
            return null;
        }
        catch (Exception ex)
        {
            RecordError(result, path, ex, "Gagal membaca header berkas");
            return null;
        }
    }

    /// <summary>
    /// Menghitung hash SHA-256 lengkap berkas secara streaming asinkron aman memori.
    /// </summary>
    private async Task<(string Hash, DateTime? LastModified)?> TryComputeFullSha256HashAsync(
        string path, 
        long expectedSize, 
        DuplicateSearchResult result, 
        CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                RecordError(result, path, new FileNotFoundException($"Berkas '{path}' hilang sebelum penghitungan hash."), "Berkas Hilang / Terhapus");
                return null;
            }

            if (fileInfo.Length != expectedSize)
            {
                RecordError(result, path, new InvalidOperationException($"Ukuran berkas '{path}' berubah dari {expectedSize} bita menjadi {fileInfo.Length} bita."), "Berkas Berubah Saat Diperiksa");
                return null;
            }

            DateTime lastModified = fileInfo.LastWriteTime;

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: StreamBufferSize,
                useAsync: true);

            if (stream.Length != expectedSize)
            {
                RecordError(result, path, new InvalidOperationException($"Panjang stream berkas '{path}' berubah saat dibuka ({stream.Length} != {expectedSize})."), "Berkas Berubah Saat Diperiksa");
                return null;
            }

            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            string hashHex = Convert.ToHexStringLower(hashBytes);

            return (hashHex, lastModified);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            RecordError(result, path, ex, "Izin ditolak saat membaca seluruh isi berkas");
            return null;
        }
        catch (FileNotFoundException ex)
        {
            RecordError(result, path, ex, "Berkas tidak ditemukan saat penghitungan hash SHA-256");
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            RecordError(result, path, ex, "Direktori berkas hilang saat penghitungan hash");
            return null;
        }
        catch (IOException ex)
        {
            RecordError(result, path, ex, "Kesalahan I/O saat membaca berkas untuk hash SHA-256");
            return null;
        }
        catch (SecurityException ex)
        {
            RecordError(result, path, ex, "Pelanggaran keamanan sistem pada berkas");
            return null;
        }
        catch (Exception ex)
        {
            RecordError(result, path, ex, "Kesalahan tidak terduga saat menghitung hash SHA-256");
            return null;
        }
    }

    private void RecordError(DuplicateSearchResult result, string path, Exception ex, string context)
    {
        var errorItem = new ScanErrorItem
        {
            Path = path,
            ErrorMessage = $"{context}: {ex.Message}",
            ExceptionType = ex.GetType().Name,
            Timestamp = DateTime.Now
        };

        result.Errors.Add(errorItem);
        _logger?.LogWarning($"[{errorItem.FriendlyType}] {context} pada '{path}': {ex.Message}");
    }

    private static IEnumerable<FileSystemItemModel> FlattenFiles(IEnumerable<FileSystemItemModel> items)
    {
        foreach (var item in items)
        {
            if (!item.IsDirectory)
            {
                yield return item;
            }

            if (item.Children.Count > 0)
            {
                foreach (var childFile in FlattenFiles(item.Children))
                {
                    yield return childFile;
                }
            }
        }
    }
}
