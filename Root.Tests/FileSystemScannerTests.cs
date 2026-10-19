using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class FileSystemScannerTests : IDisposable
{
    private readonly string _testRootDirectory;
    private readonly LoggerService _logger;
    private readonly FileSystemScanner _scanner;

    public FileSystemScannerTests()
    {
        _testRootDirectory = Path.Combine(Path.GetTempPath(), "RootTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRootDirectory);
        _logger = new LoggerService();
        _scanner = new FileSystemScanner(_logger);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, true);
            }
        }
        catch
        {
            // Abaikan kesalahan pembersihan direktori sementara jika ada berkas yang terkunci
        }
    }

    [Fact]
    public async Task ScanAsync_ValidNestedDirectoryTree_CalculatesExactSizesAndCounts()
    {
        // Susun struktur direktori uji:
        // _testRootDirectory
        // ├── berkas_akar.dat (512 bytes)
        // ├── FolderA
        // │   ├── fileA1.txt (1024 bytes)
        // │   └── fileA2.bin (2048 bytes)
        // └── FolderB
        //     ├── fileB1.dat (4096 bytes)
        //     └── SubB1
        //         └── fileB_sub.bin (8192 bytes)
        // Total Berkas = 5
        // Total Subdirektori = 3 (FolderA, FolderB, SubB1)
        // Total Ukuran = 512 + 1024 + 2048 + 4096 + 8192 = 15.872 bytes

        string rootFile = Path.Combine(_testRootDirectory, "berkas_akar.dat");
        File.WriteAllBytes(rootFile, new byte[512]);

        string folderA = Path.Combine(_testRootDirectory, "FolderA");
        Directory.CreateDirectory(folderA);
        File.WriteAllBytes(Path.Combine(folderA, "fileA1.txt"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(folderA, "fileA2.bin"), new byte[2048]);

        string folderB = Path.Combine(_testRootDirectory, "FolderB");
        string subB1 = Path.Combine(folderB, "SubB1");
        Directory.CreateDirectory(subB1);
        File.WriteAllBytes(Path.Combine(folderB, "fileB1.dat"), new byte[4096]);
        File.WriteAllBytes(Path.Combine(subB1, "fileB_sub.bin"), new byte[8192]);

        var progressReports = new System.Collections.Concurrent.ConcurrentBag<ScanProgressReport>();
        var progress = new Progress<ScanProgressReport>(r => progressReports.Add(r));

        var result = await _scanner.ScanAsync(_testRootDirectory, progress, CancellationToken.None);

        Assert.Equal(ScanStatus.Selesai, result.Status);
        Assert.False(result.IsPartial);
        Assert.Equal(5, result.TotalFilesCount);
        // Direktori yang dipindai: FolderA, FolderB, SubB1
        Assert.True(result.TotalDirectoriesCount >= 3);
        Assert.Equal(15872, result.TotalSizeBytes);
        Assert.Equal(0, result.SkippedCount);

        // Periksa TopLargestFiles
        Assert.NotEmpty(result.TopLargestFiles);
        Assert.Equal(8192, result.TopLargestFiles[0].SizeBytes);
        Assert.Equal("fileB_sub.bin", result.TopLargestFiles[0].Name);
        Assert.Equal(4096, result.TopLargestFiles[1].SizeBytes);

        // Periksa TopLargestDirectories (FolderB seharusnya 4096 + 8192 = 12288 bytes)
        Assert.NotEmpty(result.TopLargestDirectories);
        var dirB = result.TopLargestDirectories.FirstOrDefault(d => d.Name == "FolderB");
        Assert.NotNull(dirB);
        Assert.Equal(12288, dirB?.SizeBytes);
        Assert.Equal(2, dirB?.FileCount);

        // Periksa RootItem
        Assert.NotNull(result.RootItem);
        Assert.Equal(15872, result.RootItem?.SizeBytes);
        Assert.Equal(5, result.RootItem?.FileCount);

        // Markdown summary
        var markdown = result.GenerateSummaryMarkdown();
        Assert.Contains("Pemindaian Selesai", markdown);
        Assert.Contains("fileB_sub.bin", markdown);
    }

    [Fact]
    public async Task ScanAsync_EmptyDirectory_ReturnsZeroCounts()
    {
        var result = await _scanner.ScanAsync(_testRootDirectory, null, CancellationToken.None);

        Assert.Equal(ScanStatus.Selesai, result.Status);
        Assert.False(result.IsPartial);
        Assert.Equal(0, result.TotalFilesCount);
        Assert.Equal(0, result.TotalSizeBytes);
    }

    [Fact]
    public async Task ScanAsync_NonExistentDirectory_ReturnsGagal()
    {
        string fakeDir = Path.Combine(_testRootDirectory, "folder_tidak_ada_123");
        var result = await _scanner.ScanAsync(fakeDir, null, CancellationToken.None);

        Assert.Equal(ScanStatus.Gagal, result.Status);
    }

    [Fact]
    public async Task ScanAsync_CancelledMidway_ReturnsPartialReport()
    {
        // Buat beberapa folder dengan banyak berkas
        for (int i = 0; i < 20; i++)
        {
            string subDir = Path.Combine(_testRootDirectory, $"Batch_{i}");
            Directory.CreateDirectory(subDir);
            for (int j = 0; j < 5; j++)
            {
                File.WriteAllBytes(Path.Combine(subDir, $"file_{j}.dat"), new byte[1024]);
            }
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Batalkan langsung

        var result = await _scanner.ScanAsync(_testRootDirectory, null, cts.Token);

        Assert.Equal(ScanStatus.Dibatalkan, result.Status);
        Assert.True(result.IsPartial);
        Assert.Contains("Dibatalkan", result.StatusDescription);
    }

    [Fact]
    public void ScanResultReport_GenerateSummaryMarkdown_ContainsKeySections()
    {
        var report = new ScanResultReport
        {
            TargetPath = @"C:\UjiCoba",
            StartTime = DateTime.Now.AddMinutes(-5),
            EndTime = DateTime.Now,
            Status = ScanStatus.SelesaiSebagian,
            TotalFilesCount = 100,
            TotalDirectoriesCount = 10,
            TotalSizeBytes = 50 * 1024 * 1024,
            SkippedCount = 2,
            IsPartial = true
        };

        report.TopLargestFiles.Add(new FileSystemItemModel
        {
            Name = "besar.iso",
            FullPath = @"C:\UjiCoba\besar.iso",
            SizeBytes = 40 * 1024 * 1024
        });

        report.Errors.Add(new ScanErrorItem
        {
            Path = @"C:\UjiCoba\Rahasia",
            ErrorMessage = "Akses ditolak oleh kebijakan keamanan",
            ExceptionType = nameof(UnauthorizedAccessException),
            Timestamp = DateTime.Now
        });

        var markdown = report.GenerateSummaryMarkdown();

        Assert.Contains("Laporan Pemindaian ROOT - Parsial", markdown);
        Assert.Contains("Izin Ditolak", markdown);
        Assert.Contains("besar.iso", markdown);
        Assert.Contains("Rahasia", markdown);
    }
}

