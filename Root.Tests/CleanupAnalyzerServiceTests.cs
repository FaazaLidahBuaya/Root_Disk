using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class CleanupAnalyzerServiceTests
{
    [Fact]
    public async Task AnalyzeFromScanTree_IdentifiesOldTempFiles_AndRejectsRecentTempFiles()
    {
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection);

        var root = new FileSystemItemModel { FullPath = @"D:\Testing", Name = "Testing", IsDirectory = true };

        // Berkas sementara tua (25 hari lalu)
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Testing\cache.tmp",
            Name = "cache.tmp",
            Extension = ".tmp",
            SizeBytes = 1024 * 1024 * 5, // 5 MB
            LastModified = DateTime.UtcNow.AddDays(-25),
            IsDirectory = false
        });

        // Berkas sementara baru (3 hari lalu) - TIDAK BOLEH DIHAPUS (bisa jadi sedang aktif)
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Testing\active_work.tmp",
            Name = "active_work.tmp",
            Extension = ".tmp",
            SizeBytes = 1024 * 1024 * 2,
            LastModified = DateTime.UtcNow.AddDays(-3),
            IsDirectory = false
        });

        var preview = await analyzer.AnalyzeFromScanTreeAsync(root);

        Assert.Equal(1, preview.TotalCandidates);
        var candidate = preview.Candidates[0];
        Assert.Equal("cache.tmp", candidate.FileName);
        Assert.Equal(CleanupCategory.BerkasSementara, candidate.Category);
        Assert.Equal(CleanupSafetyLevel.AmanKonservatif, candidate.SafetyLevel);
        Assert.Contains("konservatif", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeFromScanTree_IdentifiesCrashDumps_AndAbandonedDownloads()
    {
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection);

        var root = new FileSystemItemModel { FullPath = @"D:\Downloads", Name = "Downloads", IsDirectory = true };

        // Crash dump lama (30 hari lalu)
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Downloads\crash_dump.dmp",
            Name = "crash_dump.dmp",
            Extension = ".dmp",
            SizeBytes = 50 * 1024 * 1024, // 50 MB
            LastModified = DateTime.UtcNow.AddDays(-30),
            IsDirectory = false
        });

        // Unduhan terbengkalai lama (15 hari lalu)
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Downloads\bigfile.iso.crdownload",
            Name = "bigfile.iso.crdownload",
            Extension = ".crdownload",
            SizeBytes = 200 * 1024 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-15),
            IsDirectory = false
        });

        // Unduhan baru (1 hari lalu) - JANGAN HAPUS!
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Downloads\new_video.mp4.crdownload",
            Name = "new_video.mp4.crdownload",
            Extension = ".crdownload",
            SizeBytes = 80 * 1024 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-1),
            IsDirectory = false
        });

        var preview = await analyzer.AnalyzeFromScanTreeAsync(root);

        Assert.Equal(2, preview.TotalCandidates);
        Assert.Contains(preview.Candidates, c => c.Category == CleanupCategory.CrashDump && c.FileName == "crash_dump.dmp");
        Assert.Contains(preview.Candidates, c => c.Category == CleanupCategory.UnduhanTerbengkalai && c.FileName == "bigfile.iso.crdownload");
    }

    [Fact]
    public async Task AnalyzeFromScanTree_IdentifiesOrphanedThumbsAndOldLogs()
    {
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection);

        var root = new FileSystemItemModel { FullPath = @"D:\Photos", Name = "Photos", IsDirectory = true };

        // Thumbs.db lama (40 hari lalu)
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Photos\Thumbs.db",
            Name = "Thumbs.db",
            Extension = ".db",
            SizeBytes = 64 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-40),
            IsDirectory = false
        });

        // Log lama (60 hari lalu)
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Photos\sync.log",
            Name = "sync.log",
            Extension = ".log",
            SizeBytes = 512 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-60),
            IsDirectory = false
        });

        // Log baru (2 hari lalu) - jangan dihapus
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Photos\today.log",
            Name = "today.log",
            Extension = ".log",
            SizeBytes = 12 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-2),
            IsDirectory = false
        });

        var preview = await analyzer.AnalyzeFromScanTreeAsync(root);

        Assert.Equal(2, preview.TotalCandidates);
        Assert.Contains(preview.Candidates, c => c.Category == CleanupCategory.ThumbnailCacheUsang);
        var logCandidate = Assert.Single(preview.Candidates, c => c.Category == CleanupCategory.LogKadaluarsa);
        Assert.Equal(CleanupSafetyLevel.PerluPerhatian, logCandidate.SafetyLevel);
    }

    [Fact]
    public async Task AnalyzeFromScanTree_StrictlyRejectsUserDocumentsAndLargeFiles()
    {
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection);

        var root = new FileSystemItemModel { FullPath = @"D:\UserData", Name = "UserData", IsDirectory = true };

        // Berkas dokumen pengguna dan berkas besar TIDAK BOLEH dianggap aman dibersihkan
        root.Children.Add(new FileSystemItemModel { FullPath = @"D:\UserData\laporan.docx", Name = "laporan.docx", Extension = ".docx", SizeBytes = 1024 * 500, LastModified = DateTime.UtcNow.AddDays(-100), IsDirectory = false });
        root.Children.Add(new FileSystemItemModel { FullPath = @"D:\UserData\database.mdf", Name = "database.mdf", Extension = ".mdf", SizeBytes = 1024 * 1024 * 200, LastModified = DateTime.UtcNow.AddDays(-50), IsDirectory = false });
        root.Children.Add(new FileSystemItemModel { FullPath = @"D:\UserData\video_liburan.mkv", Name = "video_liburan.mkv", Extension = ".mkv", SizeBytes = 10L * 1024 * 1024 * 1024, LastModified = DateTime.UtcNow.AddDays(-200), IsDirectory = false });
        root.Children.Add(new FileSystemItemModel { FullPath = @"D:\UserData\windows11_setup.iso", Name = "windows11_setup.iso", Extension = ".iso", SizeBytes = 5L * 1024 * 1024 * 1024, LastModified = DateTime.UtcNow.AddDays(-300), IsDirectory = false });
        root.Children.Add(new FileSystemItemModel { FullPath = @"D:\UserData\archive.zip", Name = "archive.zip", Extension = ".zip", SizeBytes = 500 * 1024 * 1024, LastModified = DateTime.UtcNow.AddDays(-400), IsDirectory = false });

        var preview = await analyzer.AnalyzeFromScanTreeAsync(root);

        // Harus 0 kandidat: Berkas besar atau dokumen biasa BUKAN kandidat pembersihan!
        Assert.Equal(0, preview.TotalCandidates);
    }

    [Fact]
    public async Task AnalyzeFromScanTree_StrictlyRejectsProtectedSystemPaths()
    {
        string fakeSystemDir = @"C:\FakeSystemRoot";
        var protection = new SystemProtectionService([fakeSystemDir]);
        var analyzer = new CleanupAnalyzerService(protection);

        var root = new FileSystemItemModel { FullPath = fakeSystemDir, Name = "FakeSystemRoot", IsDirectory = true };

        // Berkas sementara tua di dalam direktori sistem yang terproteksi
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = Path.Combine(fakeSystemDir, "system_cache.tmp"),
            Name = "system_cache.tmp",
            Extension = ".tmp",
            SizeBytes = 1024 * 1024,
            LastModified = DateTime.UtcNow.AddDays(-50),
            IsDirectory = false
        });

        var preview = await analyzer.AnalyzeFromScanTreeAsync(root);

        // Direktori sistem terlindungi mutlak: harus 0 kandidat
        Assert.Equal(0, preview.TotalCandidates);
    }

    [Fact]
    public async Task AnalyzeDirectoryAsync_ScansDiskFilesConsistently()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_Scan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string oldTmp = Path.Combine(tempDir, "old_file.tmp");
            File.WriteAllText(oldTmp, "konten sementara lama");
            File.SetLastWriteTimeUtc(oldTmp, DateTime.UtcNow.AddDays(-20));

            string newTmp = Path.Combine(tempDir, "recent_file.tmp");
            File.WriteAllText(newTmp, "konten sementara baru");
            File.SetLastWriteTimeUtc(newTmp, DateTime.UtcNow.AddDays(-2));

            var protection = new SystemProtectionService();
            var analyzer = new CleanupAnalyzerService(protection);

            var preview = await analyzer.AnalyzeDirectoryAsync(tempDir);

            Assert.Equal(1, preview.TotalCandidates);
            Assert.Equal(oldTmp, preview.Candidates[0].FullPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task AnalyzeFromScanTree_HandlesCancellationGracefully()
    {
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection);

        var root = new FileSystemItemModel { FullPath = @"D:\BigTree", Name = "BigTree", IsDirectory = true };
        for (int i = 0; i < 50; i++)
        {
            root.Children.Add(new FileSystemItemModel
            {
                FullPath = $@"D:\BigTree\file_{i}.tmp",
                Name = $"file_{i}.tmp",
                Extension = ".tmp",
                SizeBytes = 1024,
                LastModified = DateTime.UtcNow.AddDays(-20),
                IsDirectory = false
            });
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Batalkan sebelum mulai

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await analyzer.AnalyzeFromScanTreeAsync(root, cancellationToken: cts.Token);
        });
    }
}
