using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Root.App.ViewModels;
using Xunit;

namespace Root.Tests;

public class CleanupViewModelTests
{
    [Fact]
    public void LoadScanData_EnablesAnalysisAndUpdatesStatus()
    {
        var logger = new LoggerService();
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection, logger);
        var executor = new CleanupExecutionService(protection, logger);
        var vm = new CleanupViewModel(analyzer, executor, logger);

        Assert.False(vm.HasScanData);
        Assert.False(vm.CanStartAnalysis);

        var root = new FileSystemItemModel { FullPath = @"D:\Projects", Name = "Projects", IsDirectory = true };
        vm.LoadScanData(root);

        Assert.True(vm.HasScanData);
        Assert.True(vm.CanStartAnalysis);
        Assert.Contains("Projects", vm.StatusText);
    }

    [Fact]
    public async Task MulaiAnalisisAsync_PopulatesCandidatesAndPreview()
    {
        var logger = new LoggerService();
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection, logger);
        var executor = new CleanupExecutionService(protection, logger);
        var vm = new CleanupViewModel(analyzer, executor, logger);

        var root = new FileSystemItemModel { FullPath = @"D:\Data", Name = "Data", IsDirectory = true };
        root.Children.Add(new FileSystemItemModel
        {
            FullPath = @"D:\Data\old_dump.dmp",
            Name = "old_dump.dmp",
            Extension = ".dmp",
            SizeBytes = 1024 * 1024 * 20, // 20 MB
            LastModified = DateTime.UtcNow.AddDays(-20),
            IsDirectory = false
        });

        vm.LoadScanData(root);
        await vm.MulaiAnalisisAsync();

        Assert.True(vm.HasResults);
        Assert.Equal(1, vm.TotalCandidatesCount);
        Assert.NotNull(vm.PreviewModel);
        Assert.Equal("old_dump.dmp", vm.Candidates[0].FileName);
        Assert.False(vm.Candidates[0].IsSelected); // Default konservatif: tidak ada yang otomatis dicentang!
    }

    [Fact]
    public void SelectionCommands_SelectAllSafe_AndDeselectAll_WorkAccurately()
    {
        var logger = new LoggerService();
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection, logger);
        var executor = new CleanupExecutionService(protection, logger);
        var vm = new CleanupViewModel(analyzer, executor, logger);

        var itemSafe = new CleanupCandidateModel(
            @"D:\temp.tmp", "temp.tmp", @"D:\", ".tmp",
            CleanupCategory.BerkasSementara, "Alasan", 1024,
            DateTime.UtcNow.AddDays(-20), CleanupSafetyLevel.AmanKonservatif);

        var itemCaution = new CleanupCandidateModel(
            @"D:\app.log", "app.log", @"D:\", ".log",
            CleanupCategory.LogKadaluarsa, "Alasan", 2048,
            DateTime.UtcNow.AddDays(-50), CleanupSafetyLevel.PerluPerhatian);

        vm.Candidates.Add(itemSafe);
        vm.Candidates.Add(itemCaution);

        // Pilih Semua Aman
        vm.PilihSemuaAman();
        Assert.True(itemSafe.IsSelected);
        Assert.False(itemCaution.IsSelected);
        Assert.Equal(1, vm.SelectedCount);

        // Batalkan Semua Pilihan
        vm.BatalkanSemuaPilihan();
        Assert.False(itemSafe.IsSelected);
        Assert.False(itemCaution.IsSelected);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public async Task BersihkanTerpilihAsync_ExplicitConfirmation_CancelledByUser_DoesNotDeleteFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Vm_Cancel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string testFile = Path.Combine(tempDir, "sample.tmp");
            File.WriteAllText(testFile, "data");
            var info = new FileInfo(testFile);

            var logger = new LoggerService();
            var protection = new SystemProtectionService();
            var analyzer = new CleanupAnalyzerService(protection, logger);
            var executor = new CleanupExecutionService(protection, logger);

            // Pengguna menolak konfirmasi eksplisit (klik 'Tidak')
            bool confirmCalled = false;
            Func<string, string, bool> fakeConfirm = (msg, title) =>
            {
                confirmCalled = true;
                return false; // TOLAK
            };

            var vm = new CleanupViewModel(analyzer, executor, logger, confirmAction: fakeConfirm);

            var candidate = new CleanupCandidateModel(
                testFile, info.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji pembatalan",
                info.Length, info.LastWriteTimeUtc);
            candidate.IsSelected = true;

            vm.Candidates.Add(candidate);

            await vm.BersihkanTerpilihAsync();

            Assert.True(confirmCalled);
            Assert.True(File.Exists(testFile)); // Berkas tetap utuh!
            Assert.Contains("dibatalkan oleh pengguna", vm.StatusText, StringComparison.OrdinalIgnoreCase);
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
    public async Task BersihkanTerpilihAsync_ExplicitConfirmation_ConfirmedByUser_DeletesFilesAndUpdatesUI()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Vm_Confirm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string testFile = Path.Combine(tempDir, "to_delete.tmp");
            File.WriteAllText(testFile, "data untuk dihapus");
            var info = new FileInfo(testFile);

            var logger = new LoggerService();
            var protection = new SystemProtectionService();
            var analyzer = new CleanupAnalyzerService(protection, logger);
            var executor = new CleanupExecutionService(protection, logger);

            // Pengguna menyetujui konfirmasi eksplisit (klik 'Ya')
            bool confirmCalled = false;
            Func<string, string, bool> fakeConfirm = (msg, title) =>
            {
                confirmCalled = true;
                return true; // SETUJU
            };

            string? alertMsg = null;
            Action<string, string, bool> fakeAlert = (msg, title, isErr) =>
            {
                alertMsg = msg;
            };

            var vm = new CleanupViewModel(analyzer, executor, logger, confirmAction: fakeConfirm, alertAction: fakeAlert);

            var candidate = new CleanupCandidateModel(
                testFile, info.Name, tempDir, ".tmp",
                CleanupCategory.BerkasSementara, "Uji eksekusi",
                info.Length, info.LastWriteTimeUtc);
            candidate.IsSelected = true;

            vm.Candidates.Add(candidate);

            await vm.BersihkanTerpilihAsync();

            Assert.True(confirmCalled);
            Assert.False(File.Exists(testFile)); // Berkas terhapus!
            Assert.Empty(vm.Candidates); // Berkas yang terhapus dikeluarkan dari list UI
            Assert.NotNull(vm.ExecutionResult);
            Assert.Equal(1, vm.ExecutionResult.TotalDeletedCount);
            Assert.NotNull(alertMsg);
            Assert.Contains("HASIL AUDIT", alertMsg);
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
    public void BukaLokasiBerkas_And_SalinLaporan_WorkWithInjectedDelegates()
    {
        var logger = new LoggerService();
        var protection = new SystemProtectionService();
        var analyzer = new CleanupAnalyzerService(protection, logger);
        var executor = new CleanupExecutionService(protection, logger);

        string? openedPath = null;
        string? copiedReport = null;

        var vm = new CleanupViewModel(
            analyzer,
            executor,
            logger,
            alertAction: (msg, title, isErr) => { },
            fileOpener: path => openedPath = path,
            clipboardAction: text => copiedReport = text);

        vm.BukaLokasiBerkas(@"D:\Folder\file.tmp");
        Assert.Equal(@"D:\Folder\file.tmp", openedPath);

        vm.ExecutionResult = new CleanupExecutionResult { TotalDeletedCount = 1 };
        vm.SalinLaporanAudit();
        Assert.NotNull(copiedReport);
        Assert.Contains("Laporan Audit Pembersihan", copiedReport);
    }
}
