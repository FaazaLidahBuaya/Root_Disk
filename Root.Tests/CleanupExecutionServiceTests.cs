using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class CleanupExecutionServiceTests
{
    [Fact]
    public async Task ExecuteCleanupAsync_SafelyDeletesValidatedCandidateFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_Exec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string file1 = Path.Combine(tempDir, "temp1.tmp");
            string file2 = Path.Combine(tempDir, "temp2.tmp");
            File.WriteAllText(file1, "konten sementara satu");
            File.WriteAllText(file2, "konten sementara dua");

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            var candidate1 = new CleanupCandidateModel(
                file1, info1.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji pembersihan",
                info1.Length, info1.LastWriteTimeUtc);

            var candidate2 = new CleanupCandidateModel(
                file2, info2.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji pembersihan",
                info2.Length, info2.LastWriteTimeUtc);

            var service = new CleanupExecutionService();
            var result = await service.ExecuteCleanupAsync([candidate1, candidate2]);

            // Verifikasi bahwa berkas benar-benar terhapus secara aman
            Assert.False(File.Exists(file1));
            Assert.False(File.Exists(file2));
            Assert.Equal(2, result.TotalDeletedCount);
            Assert.Equal(info1.Length + info2.Length, result.TotalFreedSizeBytes);
            Assert.All(result.ItemResults, item => Assert.Equal(CleanupItemStatus.BerhasilDihapus, item.Status));
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
    public async Task ExecuteCleanupAsync_PreValidation_SkipsFileIfModifiedSinceAnalysis()
    {
        // PENGUJIAN KEAMANAN KRITIS: Jika berkas dimodifikasi setelah analisis, ROOT DILARANG menghapusnya!
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_Mod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string file = Path.Combine(tempDir, "important_autosave.tmp");
            File.WriteAllText(file, "versi awal saat analisis");

            var info = new FileInfo(file);
            var candidate = new CleanupCandidateModel(
                file, info.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji berkas berubah",
                info.Length, info.LastWriteTimeUtc);

            // Simulasi proses eksternal/pengguna mengedit atau menulis ke berkas setelah analisis dijalankan
            await Task.Delay(50);
            File.AppendAllText(file, "\nDATA TAMBAHAN PENTING DARI PENGGUNA!");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);

            var service = new CleanupExecutionService();
            var result = await service.ExecuteCleanupAsync([candidate]);

            // Berkas HARUS TETAP ADA dan TIDAK DIHAPUS demi keamanan data!
            Assert.True(File.Exists(file));
            Assert.Equal(0, result.TotalDeletedCount);
            Assert.Equal(1, result.SkippedModifiedCount);
            var item = Assert.Single(result.ItemResults);
            Assert.Equal(CleanupItemStatus.DilewatiBerkasBerubah, item.Status);
            Assert.Contains("berubah", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task ExecuteCleanupAsync_PreValidation_HandlesDisappearedFileGracefully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_Lost_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string file = Path.Combine(tempDir, "deleted_by_app.tmp");
            File.WriteAllText(file, "data");

            var info = new FileInfo(file);
            var candidate = new CleanupCandidateModel(
                file, info.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji berkas hilang",
                info.Length, info.LastWriteTimeUtc);

            // Berkas dihapus oleh aplikasi eksternal sebelum tindakan pembersihan dijalankan
            File.Delete(file);

            var service = new CleanupExecutionService();
            var result = await service.ExecuteCleanupAsync([candidate]);

            Assert.Equal(0, result.TotalDeletedCount);
            Assert.Equal(1, result.SkippedMissingCount);
            var item = Assert.Single(result.ItemResults);
            Assert.Equal(CleanupItemStatus.DilewatiBerkasHilang, item.Status);
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
    public async Task ExecuteCleanupAsync_SystemProtection_StrictlyBlocksProtectedPaths()
    {
        // PENGUJIAN KEAMANAN KRITIS: Mencoba menghapus berkas di jalur sistem terlindungi
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_SysProt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string protectedFile = Path.Combine(tempDir, "protected_os_file.tmp");
            File.WriteAllText(protectedFile, "data terlindungi sistem");

            var info = new FileInfo(protectedFile);
            var candidate = new CleanupCandidateModel(
                protectedFile, info.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji proteksi sistem",
                info.Length, info.LastWriteTimeUtc);

            // Konfigurasikan service proteksi sistem dengan tempDir sebagai protected root
            var protection = new SystemProtectionService([tempDir]);
            var service = new CleanupExecutionService(protection);

            var result = await service.ExecuteCleanupAsync([candidate]);

            // Berkas sistem terlindungi TIDAK BOLEH dihapus!
            Assert.True(File.Exists(protectedFile));
            Assert.Equal(0, result.TotalDeletedCount);
            Assert.Equal(1, result.BlockedProtectedCount);
            var item = Assert.Single(result.ItemResults);
            Assert.Equal(CleanupItemStatus.DitolakProteksiSistem, item.Status);
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
    public async Task ExecuteCleanupAsync_SystemProtection_BlocksExecutableAndSystemBinaryExtensions()
    {
        // PENGUJIAN KEAMANAN KRITIS: Mencoba menghapus berkas biner eksekutabel (.dll / .exe)
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_BinProt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string dllFile = Path.Combine(tempDir, "plugin.dll");
            File.WriteAllText(dllFile, "binary code");

            var info = new FileInfo(dllFile);
            var candidate = new CleanupCandidateModel(
                dllFile, info.Name, tempDir, ".dll",
                CleanupCategory.BerkasSementara, "Uji ekstensi eksekutabel",
                info.Length, info.LastWriteTimeUtc);

            var service = new CleanupExecutionService();
            var result = await service.ExecuteCleanupAsync([candidate]);

            // Ekstensi biner dilarang keras dihapus oleh modul pembersihan
            Assert.True(File.Exists(dllFile));
            Assert.Equal(0, result.TotalDeletedCount);
            Assert.Equal(1, result.BlockedProtectedCount);
            var item = Assert.Single(result.ItemResults);
            Assert.Equal(CleanupItemStatus.DitolakProteksiSistem, item.Status);
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
    public async Task ExecuteCleanupAsync_HandlesLockedOrInUseFileWithoutCrashingBatch()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_Lock_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string lockedFile = Path.Combine(tempDir, "active_process.tmp");
            string normalFile = Path.Combine(tempDir, "free_file.tmp");
            File.WriteAllText(lockedFile, "berkas terkunci");
            File.WriteAllText(normalFile, "berkas bebas");

            var infoLocked = new FileInfo(lockedFile);
            var infoNormal = new FileInfo(normalFile);

            var candidateLocked = new CleanupCandidateModel(
                lockedFile, infoLocked.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji terkunci",
                infoLocked.Length, infoLocked.LastWriteTimeUtc);

            var candidateNormal = new CleanupCandidateModel(
                normalFile, infoNormal.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji normal",
                infoNormal.Length, infoNormal.LastWriteTimeUtc);

            var service = new CleanupExecutionService();

            // Kunci berkas pertama secara eksklusif menggunakan FileStream
            using (var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = await service.ExecuteCleanupAsync([candidateLocked, candidateNormal]);

                // Berkas terkunci gagal tanpa merusak berkas lainnya
                Assert.True(File.Exists(lockedFile));
                Assert.Equal(1, result.FailedLockedCount);

                // Berkas normal tetap berhasil dihapus
                Assert.False(File.Exists(normalFile));
                Assert.Equal(1, result.TotalDeletedCount);
            }
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
    public async Task ExecuteCleanupAsync_HandlesCancellationGracefully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Cleanup_Cancel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string file1 = Path.Combine(tempDir, "f1.tmp");
            File.WriteAllText(file1, "1");
            var info1 = new FileInfo(file1);
            var candidate1 = new CleanupCandidateModel(file1, info1.Name, tempDir, ".tmp", CleanupCategory.BerkasSementara, "1", info1.Length, info1.LastWriteTimeUtc);

            var service = new CleanupExecutionService();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Batalkan sebelum eksekusi

            var result = await service.ExecuteCleanupAsync([candidate1], cancellationToken: cts.Token);

            Assert.True(result.IsCancelled);
            Assert.True(File.Exists(file1)); // Berkas tetap utuh
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
    public void CleanupExecutionResult_GenerateAuditReportMarkdown_ProducesStructuredReport()
    {
        var result = new CleanupExecutionResult
        {
            TotalCandidatesEvaluated = 5,
            TotalSelectedCount = 3,
            TotalDeletedCount = 2,
            TotalFreedSizeBytes = 1024 * 1024 * 15,
            SkippedModifiedCount = 1,
            BlockedProtectedCount = 0
        };

        result.ItemResults.Add(new CleanupItemResult(@"D:\Temp\a.tmp", CleanupCategory.BerkasSementara, 1024 * 1024 * 5, CleanupItemStatus.BerhasilDihapus));
        result.ItemResults.Add(new CleanupItemResult(@"D:\Temp\b.tmp", CleanupCategory.BerkasSementara, 1024 * 1024 * 10, CleanupItemStatus.BerhasilDihapus));
        result.ItemResults.Add(new CleanupItemResult(@"D:\Temp\c.tmp", CleanupCategory.BerkasSementara, 1024 * 1024 * 2, CleanupItemStatus.DilewatiBerkasBerubah, "Ukuran berkas berubah."));

        string report = result.GenerateAuditReportMarkdown();

        Assert.Contains("Laporan Audit Pembersihan Sistem Berkas ROOT (Fase 6)", report);
        Assert.Contains("15,00 MB", report);
        Assert.Contains("Dilewati karena Berkas Dimodifikasi", report);
        Assert.Contains("D:\\Temp\\a.tmp", report);
        Assert.Contains("Dilewati: Berkas berubah", report);
    }
}
