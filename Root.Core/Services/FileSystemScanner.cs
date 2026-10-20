using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Utilities;

namespace Root.Core.Services;

/// <summary>
/// Pemindai sistem berkas rekursif asinkron dengan fitur perhitungan ukuran,
/// proteksi loop junction/symlink, penanganan izin ditolak, dan laporan parsial.
/// </summary>
public class FileSystemScanner : IFileSystemScanner
{
    private readonly ILoggerService? _logger;
    private const int MaxTopItems = 50;
    private const int ProgressReportIntervalMs = 300;
    private const int MaxDirectoryDepth = 256;
    private const int MaxPreviewChildren = 200;
    private const string AggregateMarker = "::<ROOT-TREEMAP-AGGREGATE>";

    public FileSystemScanner(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public async Task<ScanResultReport> ScanAsync(string rootPath, IProgress<ScanProgressReport>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Jalur target pemindaian tidak boleh kosong.", nameof(rootPath));

        return await Task.Run(() => PerformScan(rootPath, progress, cancellationToken));
    }

    private ScanResultReport PerformScan(string rootPath, IProgress<ScanProgressReport>? progress, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        _logger?.LogInfo($"Memulai pemindaian sistem berkas pada jalur: '{rootPath}'");

        var report = new ScanResultReport
        {
            TargetPath = rootPath,
            StartTime = startTime,
            Status = ScanStatus.Memindai
        };

        var rootDirInfo = new DirectoryInfo(rootPath);
        if (!rootDirInfo.Exists)
        {
            stopwatch.Stop();
            report.EndTime = DateTime.Now;
            report.Status = ScanStatus.Gagal;
            report.IsPartial = true;
            report.SkippedCount = 1;
            RecordError(report, rootPath, new DirectoryNotFoundException($"Direktori target '{rootPath}' tidak ditemukan atau tidak dapat diakses."), "Direktori target tidak ditemukan");
            _logger?.LogError($"Direktori target tidak ditemukan: '{rootPath}'");
            return report;
        }

        var rootItem = new FileSystemItemModel
        {
            FullPath = rootDirInfo.FullName,
            Name = string.IsNullOrEmpty(rootDirInfo.Name) ? rootDirInfo.FullName : rootDirInfo.Name,
            IsDirectory = true,
            Children = [],
            LastModified = TryGetLastWriteTime(rootDirInfo)
        };
        report.RootItem = rootItem;

        // Karena semua reparse point di bawah root dilewati, traversal tidak memerlukan
        // HashSet berisi setiap path. Menghindari retensi jutaan string pada scan drive besar.
        var liveRootChildSizes = new Dictionary<FileSystemItemModel, long>();
        var largestFiles = new PriorityQueue<FileSystemItemModel, long>();
        var largestDirectories = new PriorityQueue<FileSystemItemModel, long>();

        long scannedFilesCount = 0;
        long scannedDirectoriesCount = 0;
        long totalBytesScanned = 0;
        int skippedCount = 0;

        long lastReportTicks = 0;
        void ReportProgress(string currentPath, bool force = false)
        {
            if (progress == null) return;

            long currentTicks = stopwatch.ElapsedMilliseconds;
            if (!force && currentTicks - lastReportTicks < ProgressReportIntervalMs)
                return;

            lastReportTicks = currentTicks;
            double elapsedSec = stopwatch.Elapsed.TotalSeconds;

            var progressReport = new ScanProgressReport
            {
                CurrentPath = currentPath,
                ScannedFilesCount = scannedFilesCount,
                ScannedDirectoriesCount = scannedDirectoriesCount,
                TotalBytesScanned = totalBytesScanned,
                SkippedCount = skippedCount,
                ElapsedTime = stopwatch.Elapsed,
                FilesPerSecond = elapsedSec > 0.05 ? scannedFilesCount / elapsedSec : 0,
                BytesPerSecond = elapsedSec > 0.05 ? totalBytesScanned / elapsedSec : 0,
                RootPreview = CreateRootPreview(rootItem, liveRootChildSizes, totalBytesScanned, scannedFilesCount, scannedDirectoriesCount)
            };

            progress.Report(progressReport);
        }

        bool wasCancelled = false;

        // F-07: Penanganan reparse point root
        if ((rootDirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            _logger?.LogInfo($"Direktori target root merupakan reparse point / link: '{rootDirInfo.FullName}'");
        }
        // F-03: Traversal iteratif berbasis stack pada heap (mencegah StackOverflowException pada direktori sangat dalam)
        var stack = new Stack<DirectoryScanFrame>();
        stack.Push(new DirectoryScanFrame(rootDirInfo, rootItem, 0, null));
        scannedDirectoriesCount++;
        ReportProgress(rootDirInfo.FullName);

        try
        {
            while (stack.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCancelled = true;
                    break;
                }

                var currentFrame = stack.Peek();

                // 1. Baca berkas dalam direktori saat ini jika enumerator subdirektori belum diinisialisasi
                if (currentFrame.SubDirEnumerator == null)
                {
                    try
                    {
                        var files = currentFrame.DirInfo.EnumerateFiles();
                        foreach (var file in files)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                wasCancelled = true;
                                break;
                            }

                            try
                            {
                                long fileSize;
                                try
                                {
                                    fileSize = file.Length;
                                }
                                catch (FileNotFoundException ex)
                                {
                                    skippedCount++;
                                    RecordError(report, file.FullName, ex, "Berkas hilang atau target tautan simbolis tidak ditemukan");
                                    continue;
                                }
                                catch (DirectoryNotFoundException ex)
                                {
                                    skippedCount++;
                                    RecordError(report, file.FullName, ex, "Direktori induk hilang saat membaca berkas");
                                    continue;
                                }

                                var fileModel = new FileSystemItemModel
                                {
                                    FullPath = file.FullName,
                                    Name = file.Name,
                                    Extension = file.Extension,
                                    IsDirectory = false,
                                    Children = FileSystemItemModel.EmptyChildren,
                                    SizeBytes = fileSize,
                                    LastModified = TryGetLastWriteTime(file),
                                    ParentPath = currentFrame.DirInfo.FullName
                                };

                                currentFrame.Model.Children.Add(fileModel);
                                currentFrame.Model.SizeBytes = SafeAdd(currentFrame.Model.SizeBytes, fileSize);
                                currentFrame.Model.FileCount = SafeAddInt(currentFrame.Model.FileCount, 1);

                                scannedFilesCount = SafeAdd(scannedFilesCount, 1);
                                totalBytesScanned = SafeAdd(totalBytesScanned, fileSize);
                                if (currentFrame.RootChildModel != null &&
                                    liveRootChildSizes.TryGetValue(currentFrame.RootChildModel, out long liveRootChildSize))
                                {
                                    liveRootChildSizes[currentFrame.RootChildModel] = SafeAdd(liveRootChildSize, fileSize);
                                }
                                KeepLargest(largestFiles, fileModel, MaxTopItems);

                                if (scannedFilesCount % 50 == 0)
                                {
                                    ReportProgress(file.FullName);
                                }
                            }
                            catch (FileNotFoundException ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Berkas hilang atau telah dipindahkan selama pemindaian");
                            }
                            catch (DirectoryNotFoundException ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Direktori berkas hilang saat membaca berkas");
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Izin ditolak saat membaca atribut berkas");
                            }
                            catch (PathTooLongException ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Jalur berkas terlalu panjang");
                            }
                            catch (IOException ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Kesalahan I/O saat membaca berkas");
                            }
                            catch (SecurityException ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Pelanggaran keamanan saat membaca berkas");
                            }
                            catch (Exception ex)
                            {
                                skippedCount++;
                                RecordError(report, file.FullName, ex, "Kesalahan tidak terduga saat membaca berkas");
                            }
                        }
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Direktori hilang atau telah dihapus saat membaca berkas");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Izin ditolak saat membaca daftar berkas di direktori");
                    }
                    catch (PathTooLongException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Jalur direktori terlalu panjang");
                    }
                    catch (IOException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Kesalahan I/O saat membaca daftar berkas");
                    }
                    catch (SecurityException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Pelanggaran keamanan saat membaca direktori");
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Kesalahan tidak terduga pada direktori");
                    }

                    if (wasCancelled)
                    {
                        break;
                    }

                    // 2. Inisialisasi enumerasi subdirektori
                    try
                    {
                        currentFrame.SubDirEnumerator = currentFrame.DirInfo.EnumerateDirectories().GetEnumerator();
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Direktori induk hilang saat membaca daftar subdirektori");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Izin ditolak saat membuka subdirektori");
                    }
                    catch (PathTooLongException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Jalur direktori induk terlalu panjang");
                    }
                    catch (IOException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Kesalahan I/O saat membaca subdirektori");
                    }
                    catch (SecurityException ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Pelanggaran keamanan pada pembacaan direktori");
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        RecordError(report, currentFrame.DirInfo.FullName, ex, "Kesalahan umum direktori");
                    }
                }

                if (currentFrame.SubDirEnumerator == null)
                {
                    var finished = stack.Pop();
                    finished.Dispose();
                    continue;
                }

                bool hasNextSubDir = false;
                try
                {
                    hasNextSubDir = currentFrame.SubDirEnumerator.MoveNext();
                }
                catch (Exception ex)
                {
                    skippedCount++;
                    RecordError(report, currentFrame.DirInfo.FullName, ex, "Gagal menggeser enumerator subdirektori");
                    hasNextSubDir = false;
                }

                if (hasNextSubDir)
                {
                    var subDir = currentFrame.SubDirEnumerator.Current;

                    // F-03: Cek batas kedalaman direktori aman
                    if (currentFrame.Depth >= MaxDirectoryDepth)
                    {
                        skippedCount++;
                        RecordError(report, subDir.FullName, new InvalidOperationException($"Batas kedalaman aman direktori tercapai ({MaxDirectoryDepth})."), "Batas kedalaman direktori tercapai");
                        continue;
                    }

                    FileAttributes subDirAttrs;
                    try
                    {
                        if (!subDir.Exists)
                        {
                            skippedCount++;
                            RecordError(report, subDir.FullName, new DirectoryNotFoundException($"Subdirektori '{subDir.FullName}' hilang saat hendak dipindai."), "Subdirektori hilang");
                            continue;
                        }
                        subDirAttrs = subDir.Attributes;
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        RecordError(report, subDir.FullName, ex, "Gagal mengakses atribut subdirektori");
                        continue;
                    }

                    // F-07: Periksa reparse point / junction point / symbolic link untuk subdirektori
                    if ((subDirAttrs & FileAttributes.ReparsePoint) != 0 &&
                        !string.Equals(subDir.FullName.TrimEnd('\\'), rootDirInfo.FullName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInfo($"Melewati recursive junction / symbolic link: '{subDir.FullName}'");
                        continue;
                    }

                    var subDirModel = new FileSystemItemModel
                    {
                        FullPath = subDir.FullName,
                        Name = subDir.Name,
                        IsDirectory = true,
                        Children = [],
                        LastModified = TryGetLastWriteTime(subDir),
                        ParentPath = currentFrame.DirInfo.FullName
                    };

                    currentFrame.Model.Children.Add(subDirModel);
                    currentFrame.Model.DirectoryCount = SafeAddInt(currentFrame.Model.DirectoryCount, 1);

                    var rootChildModel = currentFrame.RootChildModel ?? (currentFrame.Depth == 0 ? subDirModel : null);
                    if (currentFrame.Depth == 0 && rootItem.Children.Count <= MaxPreviewChildren)
                        liveRootChildSizes[rootChildModel!] = 0;

                    scannedDirectoriesCount = SafeAdd(scannedDirectoriesCount, 1);
                    ReportProgress(subDir.FullName);

                    // Dorong subdirektori ke stack heap (iteratif tanpa alokasi call-stack CLR)
                    stack.Push(new DirectoryScanFrame(subDir, subDirModel, currentFrame.Depth + 1, rootChildModel));
                }
                else
                {
                    // Semua subdirektori telah diproses, pop frame ini dan akumulasikan ke frame induknya
                    var completedFrame = stack.Pop();
                    completedFrame.Dispose();

                    if (stack.Count > 0)
                    {
                        var parentFrame = stack.Peek();
                        parentFrame.Model.SizeBytes = SafeAdd(parentFrame.Model.SizeBytes, completedFrame.Model.SizeBytes);
                        parentFrame.Model.FileCount = SafeAddInt(parentFrame.Model.FileCount, completedFrame.Model.FileCount);
                        parentFrame.Model.DirectoryCount = SafeAddInt(parentFrame.Model.DirectoryCount, completedFrame.Model.DirectoryCount);
                        KeepLargest(largestDirectories, completedFrame.Model, MaxTopItems);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }
        catch (Exception ex)
        {
            skippedCount++;
            RecordError(report, rootPath, ex, "Kesalahan saat memindai direktori utama");
        }
        finally
        {
            while (stack.Count > 0)
            {
                stack.Pop().Dispose();
            }
        }

        stopwatch.Stop();
        report.EndTime = DateTime.Now;
        report.TotalSizeBytes = totalBytesScanned;
        report.TotalFilesCount = scannedFilesCount;
        report.TotalDirectoriesCount = scannedDirectoriesCount;
        report.SkippedCount = skippedCount;

        // Ambil Top berkas dan direktori terbesar
        report.TopLargestFiles = largestFiles.UnorderedItems
            .Select(entry => entry.Element)
            .OrderByDescending(f => f.SizeBytes)
            .ToList();

        report.TopLargestDirectories = largestDirectories.UnorderedItems
            .Select(entry => entry.Element)
            .OrderByDescending(d => d.SizeBytes)
            .ToList();

        // Tentukan status akhir - pastikan hasil parsial TIDAK PERNAH dilaporkan sebagai pemindaian lengkap
        if (wasCancelled || cancellationToken.IsCancellationRequested)
        {
            report.Status = ScanStatus.Dibatalkan;
            report.IsPartial = true;
            _logger?.LogWarning($"Pemindaian dibatalkan oleh pengguna. Laporan parsial dibuat ({scannedFilesCount} berkas, {FileFormatter.FormatBytes(totalBytesScanned)}).");
        }
        else if (skippedCount > 0)
        {
            report.Status = ScanStatus.SelesaiSebagian;
            report.IsPartial = true;
            _logger?.LogWarning($"Pemindaian selesai sebagian dengan {skippedCount} item dilewati karena izin/kesalahan. Laporan parsial diterbitkan.");
        }
        else
        {
            report.Status = ScanStatus.Selesai;
            report.IsPartial = false;
            _logger?.LogSuccess($"Pemindaian selesai sepenuhnya: {scannedFilesCount} berkas, {scannedDirectoriesCount} direktori, total {FileFormatter.FormatBytes(totalBytesScanned)} dalam {report.FormattedDuration}.");
        }

        // Laporan kemajuan terakhir
        ReportProgress(wasCancelled ? "Pemindaian Dibatalkan" : "Pemindaian Selesai", force: true);

        return report;
    }

    private void RecordError(ScanResultReport report, string path, Exception ex, string contextMessage)
    {
        var errorItem = new ScanErrorItem
        {
            Path = path,
            ErrorMessage = $"{contextMessage}: {ex.Message}",
            ExceptionType = ex.GetType().Name,
            Timestamp = DateTime.Now
        };

        report.Errors.Add(errorItem);
        _logger?.LogWarning($"[{errorItem.FriendlyType}] {contextMessage} pada '{path}': {ex.Message}");
    }

    private static FileSystemItemModel CreateRootPreview(
        FileSystemItemModel root,
        IReadOnlyDictionary<FileSystemItemModel, long> liveRootChildSizes,
        long totalBytes,
        long fileCount,
        long directoryCount)
    {
        var preview = new FileSystemItemModel
        {
            FullPath = root.FullPath,
            Name = root.Name,
            IsDirectory = true,
            SizeBytes = totalBytes,
            FileCount = fileCount > int.MaxValue ? int.MaxValue : (int)fileCount,
            DirectoryCount = directoryCount > int.MaxValue ? int.MaxValue : (int)directoryCount,
            LastModified = root.LastModified,
            ParentPath = root.ParentPath,
            Children = new List<FileSystemItemModel>(Math.Min(root.Children.Count, MaxPreviewChildren))
        };

        bool needsAggregate = root.Children.Count > MaxPreviewChildren;
        int count = Math.Min(root.Children.Count, needsAggregate ? MaxPreviewChildren - 1 : MaxPreviewChildren);
        long visibleBytes = 0;
        for (int i = 0; i < count; i++)
        {
            var child = root.Children[i];
            long childSize = child.IsDirectory && liveRootChildSizes.TryGetValue(child, out long liveSize)
                ? liveSize
                : child.SizeBytes;
            visibleBytes = SafeAdd(visibleBytes, childSize);
            preview.Children.Add(new FileSystemItemModel
            {
                FullPath = child.FullPath,
                Name = child.Name,
                Extension = child.Extension,
                IsDirectory = child.IsDirectory,
                SizeBytes = childSize,
                FileCount = child.FileCount,
                DirectoryCount = child.DirectoryCount,
                LastModified = child.LastModified,
                ParentPath = child.ParentPath,
                Children = FileSystemItemModel.EmptyChildren
            });
        }

        if (needsAggregate)
        {
            preview.Children.Add(new FileSystemItemModel
            {
                FullPath = root.FullPath + AggregateMarker,
                Name = "Lainnya",
                IsDirectory = false,
                SizeBytes = Math.Max(0, totalBytes - visibleBytes),
                DirectoryCount = Math.Max(0, root.Children.Count - count),
                Children = FileSystemItemModel.EmptyChildren
            });
        }

        return preview;
    }

    private static DateTime? TryGetLastWriteTime(FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTime;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// F-04: Penjumlahan aman terhadap batas long.MaxValue (mencegah wrap-around bilangan negatif).
    /// </summary>
    private static long SafeAdd(long a, long b)
    {
        try
        {
            checked
            {
                return a + b;
            }
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static void KeepLargest(PriorityQueue<FileSystemItemModel, long> queue, FileSystemItemModel item, int maximumCount)
    {
        if (queue.Count < maximumCount)
        {
            queue.Enqueue(item, item.SizeBytes);
            return;
        }

        queue.TryPeek(out _, out var smallestSize);
        if (item.SizeBytes > smallestSize)
        {
            queue.Dequeue();
            queue.Enqueue(item, item.SizeBytes);
        }
    }

    private static int SafeAddInt(int a, int b)
    {
        try
        {
            checked
            {
                return a + b;
            }
        }
        catch (OverflowException)
        {
            return int.MaxValue;
        }
    }

    /// <summary>
    /// F-03: Frame konteks direktori untuk traversal iteratif berbasis heap stack.
    /// </summary>
    private sealed class DirectoryScanFrame : IDisposable
    {
        public DirectoryInfo DirInfo { get; }
        public FileSystemItemModel Model { get; }
        public int Depth { get; }
        public FileSystemItemModel? RootChildModel { get; }
        public IEnumerator<DirectoryInfo>? SubDirEnumerator { get; set; }

        public DirectoryScanFrame(DirectoryInfo dirInfo, FileSystemItemModel model, int depth, FileSystemItemModel? rootChildModel)
        {
            DirInfo = dirInfo;
            Model = model;
            Depth = depth;
            RootChildModel = rootChildModel;
        }

        public void Dispose()
        {
            SubDirEnumerator?.Dispose();
            SubDirEnumerator = null;
        }
    }
}
