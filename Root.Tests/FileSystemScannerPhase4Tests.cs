using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class FileSystemScannerPhase4Tests : IDisposable
{
    private readonly string _testRoot;

    public FileSystemScannerPhase4Tests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Root_Phase4_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch
        {
            // Abaikan kegagalan pembersihan direktori sementara
        }
    }

    [Fact]
    public async Task ScanAsync_WhenDirectoryDoesNotExist_StatusIsGagal_AndIsPartialIsTrue()
    {
        var scanner = new FileSystemScanner();
        var nonExistentPath = Path.Combine(_testRoot, "Folder_Tidak_Ada_" + Guid.NewGuid().ToString("N"));

        var report = await scanner.ScanAsync(nonExistentPath, null, CancellationToken.None);

        Assert.Equal(ScanStatus.Gagal, report.Status);
        Assert.True(report.IsPartial, "Status gagal harus selalu ditandai sebagai laporan parsial.");
        Assert.NotEmpty(report.Errors);
        Assert.Equal("Direktori Hilang / Terhapus", report.Errors[0].FriendlyType);
        Assert.NotEqual("Pemindaian Selesai", report.StatusDescription);
    }

    [Fact]
    public async Task ScanAsync_WhenCancelledMidway_StatusIsDibatalkan_AndIsPartialIsTrue()
    {
        // Buat struktur dengan beberapa subfolder
        for (int i = 0; i < 15; i++)
        {
            var subDir = Path.Combine(_testRoot, $"SubDir_{i}");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, $"File_{i}.dat"), "Uji Pembatalan");
        }

        using var cts = new CancellationTokenSource();
        var logger = new LoggerService();
        logger.LogAdded += entry =>
        {
            if (entry.Message.Contains("Memulai pemindaian"))
            {
                cts.Cancel();
            }
        };

        var scanner = new FileSystemScanner(logger);
        var report = await scanner.ScanAsync(_testRoot, null, cts.Token);

        Assert.Equal(ScanStatus.Dibatalkan, report.Status);
        Assert.True(report.IsPartial, "Pemindaian yang dibatalkan tidak boleh dianggap selesai lengkap.");
        Assert.Contains("Laporan Parsial", report.StatusDescription);
    }

    [Fact]
    public void ScanResultReport_PartialInvariant_GuaranteesPartialWhenSkippedCountPositive()
    {
        var report = new ScanResultReport
        {
            Status = ScanStatus.Selesai,
            SkippedCount = 3,
            TotalFilesCount = 100
        };

        // Bahkan jika secara eksplisit diset IsPartial = false:
        report.IsPartial = false;

        // Invariant harus tetap mengembalikan true karena SkippedCount > 0
        Assert.True(report.IsPartial, "Hasil dengan item yang dilewati (SkippedCount > 0) harus selalu parsial.");
        Assert.Contains("Selesai Sebagian", report.StatusDescription);
    }

    [Fact]
    public async Task ScanAsync_WhenSkippedCountOccurs_StatusIsSelesaiSebagian_AndNeverSelesai()
    {
        // Buat folder dengan berkas normal
        var dir1 = Path.Combine(_testRoot, "Folder1");
        Directory.CreateDirectory(dir1);
        File.WriteAllText(Path.Combine(dir1, "Normal.txt"), "Berkas Biasa");

        var logger = new LoggerService();
        var scanner = new FileSystemScanner(logger);

        var report = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);

        Assert.Equal(ScanStatus.Selesai, report.Status);
        Assert.False(report.IsPartial);

        // Uji simulasi bila ada error item
        report.SkippedCount = 1;
        Assert.True(report.IsPartial);
        Assert.Equal("Pemindaian Selesai Sebagian (Ada Berkas Dilewati)", report.StatusDescription);
    }

    [Fact]
    public async Task ScanAsync_WhenDirectoryDisappears_CatchesException_RecordsError_DoesNotCrash()
    {
        // Buat folder induk dan subfolder
        var subDir = Path.Combine(_testRoot, "SubFolderToDelete");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "SubFile.txt"), "Akan dihapus");

        var scanner = new FileSystemScanner();

        // Lakukan pemindaian pada direktori yang valid
        var report = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);
        Assert.Equal(ScanStatus.Selesai, report.Status);

        // Sekarang kita uji jika subdirektori dihapus dan scanner membaca path tersebut
        Directory.Delete(subDir, true);

        // Jika subfolder dihapus dan dipindai langsung, scanner menangani DirectoryNotFoundException dengan elegan
        var missingDirReport = await scanner.ScanAsync(subDir, null, CancellationToken.None);
        Assert.True(missingDirReport.IsPartial);
        Assert.Equal(ScanStatus.Gagal, missingDirReport.Status);
        Assert.True(missingDirReport.SkippedCount > 0);
        Assert.Equal("Direktori Hilang / Terhapus", missingDirReport.Errors[0].FriendlyType);
    }

    [Fact]
    public async Task SecurityAudit_ScannerNeverModifiesFileAttributesOrPermissions()
    {
        // Buat berkas dengan atribut ReadOnly untuk menguji integritas keamanan
        var testFilePath = Path.Combine(_testRoot, "SecureReadOnlyFile.txt");
        File.WriteAllText(testFilePath, "Konten Terproteksi untuk Audit Keamanan");
        File.SetAttributes(testFilePath, FileAttributes.ReadOnly | FileAttributes.Archive);

        var originalAttributes = File.GetAttributes(testFilePath);
        var originalWriteTime = File.GetLastWriteTimeUtc(testFilePath);

        var scanner = new FileSystemScanner();
        var report = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);

        // Verifikasi hasil pemindaian normal
        Assert.Equal(ScanStatus.Selesai, report.Status);
        Assert.False(report.IsPartial);

        // AUDIT KEAMANAN: Pastikan scanner tidak mengubah atribut, hak akses, atau stempel waktu berkas
        var postScanAttributes = File.GetAttributes(testFilePath);
        var postScanWriteTime = File.GetLastWriteTimeUtc(testFilePath);

        Assert.Equal(originalAttributes, postScanAttributes);
        Assert.Equal(originalWriteTime, postScanWriteTime);
    }

    [Fact]
    public void ScanErrorItem_FriendlyTypeMapping_CoversAllPhase4Exceptions()
    {
        var dirNotFound = new ScanErrorItem { ExceptionType = nameof(DirectoryNotFoundException) };
        Assert.Equal("Direktori Hilang / Terhapus", dirNotFound.FriendlyType);

        var fileNotFound = new ScanErrorItem { ExceptionType = nameof(FileNotFoundException) };
        Assert.Equal("Berkas Hilang / Terhapus", fileNotFound.FriendlyType);

        var unauth = new ScanErrorItem { ExceptionType = nameof(UnauthorizedAccessException) };
        Assert.Equal("Izin Ditolak", unauth.FriendlyType);

        var pathTooLong = new ScanErrorItem { ExceptionType = nameof(PathTooLongException) };
        Assert.Equal("Jalur Terlalu Panjang", pathTooLong.FriendlyType);

        var io = new ScanErrorItem { ExceptionType = nameof(IOException) };
        Assert.Equal("Kesalahan I/O", io.FriendlyType);

        var security = new ScanErrorItem { ExceptionType = nameof(System.Security.SecurityException) };
        Assert.Equal("Keamanan Sistem", security.FriendlyType);

        var other = new ScanErrorItem { ExceptionType = "UnknownException" };
        Assert.Equal("Gagal Akses", other.FriendlyType);
    }
}
