using System;
using System.IO;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Root.App.ViewModels;
using Xunit;

namespace Root.Tests;

public class DashboardViewModelTests
{
    private static DashboardViewModel CreateTestViewModel(string? tempDbPath = null)
    {
        tempDbPath ??= Path.Combine(Path.GetTempPath(), "test_dash_" + Guid.NewGuid().ToString("N") + ".db");
        var logger = new LoggerService();
        var scanner = new FileSystemScanner(logger);
        var driveService = new DriveService(logger);
        var historyRepo = new SqliteScanHistoryRepository(tempDbPath);

        return new DashboardViewModel(
            scanner,
            driveService,
            historyRepo,
            logger,
            alertAction: (msg, title, isErr) => { },
            confirmAction: (msg, title) => true);
    }

    [Fact]
    public void InitialState_IsKosong_AndCalculatesDriveMetrics()
    {
        var vm = CreateTestViewModel();

        Assert.Equal(DashboardState.Kosong, vm.CurrentState);
        Assert.True(vm.IsStateKosong);
        Assert.False(vm.IsStateMemuat);
        Assert.False(vm.IsStateSelesai);
        Assert.False(vm.IsStateParsial);
        Assert.False(vm.IsStateGagal);
        Assert.False(vm.HasScanResult);

        Assert.NotEmpty(vm.Drives);
        Assert.NotNull(vm.SelectedDrive);
        Assert.True(vm.DriveCapacityBytes > 0);
        Assert.NotEmpty(vm.FormattedDriveCapacity);
        Assert.NotEmpty(vm.FormattedDriveUsed);
        Assert.NotEmpty(vm.FormattedDriveFree);
        Assert.True(vm.DriveUsedPercentage >= 0 && vm.DriveUsedPercentage <= 100);
    }

    [Fact]
    public void StateTransitions_CoverAllFiveDashboardStates()
    {
        var vm = CreateTestViewModel();

        vm.CurrentState = DashboardState.Memuat;
        Assert.True(vm.IsStateMemuat);
        Assert.False(vm.HasScanResult);

        vm.CurrentState = DashboardState.Selesai;
        Assert.True(vm.IsStateSelesai);
        Assert.True(vm.HasScanResult);

        vm.CurrentState = DashboardState.Parsial;
        Assert.True(vm.IsStateParsial);
        Assert.True(vm.HasScanResult);

        vm.CurrentState = DashboardState.Gagal;
        Assert.True(vm.IsStateGagal);
        Assert.False(vm.HasScanResult);

        vm.CurrentState = DashboardState.Kosong;
        Assert.True(vm.IsStateKosong);
        Assert.False(vm.HasScanResult);
    }

    [Fact]
    public async Task MulaiPemindaianAsync_OnCleanTarget_TransitionsToSelesai_AndSavesToSqlite()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Dash_Test1_" + Guid.NewGuid().ToString("N"));
        string tempDb = Path.Combine(Path.GetTempPath(), "test_db1_" + Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "file1.txt"), "Halo ROOT Dashboard!");
            File.WriteAllText(Path.Combine(tempDir, "file2.txt"), "Uji coba fase 7");

            var vm = CreateTestViewModel(tempDb);
            vm.CustomPath = tempDir;
            vm.IsCustomPathSelected = true;

            await vm.MulaiPemindaianAsync();

            Assert.Equal(DashboardState.Selesai, vm.CurrentState);
            Assert.True(vm.IsStateSelesai);
            Assert.Equal(2, vm.ScannedFiles);
            Assert.NotNull(vm.CurrentScanResult);
            Assert.NotNull(vm.CurrentRootItem);

            // Verifikasi catatan tersimpan ke SQLite
            Assert.NotEmpty(vm.ScanHistory);
            Assert.Equal(tempDir, vm.ScanHistory[0].TargetPath);
            Assert.Equal(2, vm.ScanHistory[0].TotalFiles);

            // Pemindaian pertama belum memiliki komparasi sebelumnya
            Assert.NotNull(vm.Comparison);
            Assert.False(vm.Comparison.HasPreviousComparison);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
            if (File.Exists(tempDb)) try { File.Delete(tempDb); } catch { }
        }
    }

    [Fact]
    public async Task MulaiPemindaianAsync_SecondScan_CalculatesDeltaComparisonFromSqlite()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Dash_Test2_" + Guid.NewGuid().ToString("N"));
        string tempDb = Path.Combine(Path.GetTempPath(), "test_db2_" + Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Pemindaian 1: 1 berkas (10 bytes)
            File.WriteAllText(Path.Combine(tempDir, "doc1.txt"), "0123456789");

            var vm = CreateTestViewModel(tempDb);
            vm.CustomPath = tempDir;
            vm.IsCustomPathSelected = true;

            await vm.MulaiPemindaianAsync();
            Assert.Equal(1, vm.ScannedFiles);
            Assert.Single(vm.ScanHistory);

            // Tambahkan berkas untuk pemindaian 2
            File.WriteAllText(Path.Combine(tempDir, "doc2.txt"), "Tambahan berkas kedua yang lebih besar!");
            File.WriteAllText(Path.Combine(tempDir, "doc3.txt"), "Tambahan berkas ketiga!");

            // Pemindaian 2 pada target yang sama
            await vm.MulaiPemindaianAsync();

            Assert.Equal(3, vm.ScannedFiles);
            Assert.Equal(2, vm.ScanHistory.Count);

            // Verifikasi Komparasi Baru vs Lama dari SQLite
            Assert.NotNull(vm.Comparison);
            Assert.True(vm.Comparison.HasPreviousComparison);
            Assert.True(vm.Comparison.DeltaSizeBytes > 0);
            Assert.Equal(2, vm.Comparison.DeltaFiles); // +2 berkas baru
            Assert.Equal("+2 berkas", vm.Comparison.FormattedDeltaFiles);
            Assert.Equal("Ukuran Bertambah", vm.Comparison.Trend);
            Assert.Contains("Data Riwayat", vm.Comparison.HistoricalComparisonLabel);
            Assert.Contains("meningkat sebesar", vm.Comparison.ComparisonSummary);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
            if (File.Exists(tempDb)) try { File.Delete(tempDb); } catch { }
        }
    }

    [Fact]
    public async Task HistoryCommands_DeleteAndClear_WorkWithSqliteAndUI()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), "test_db3_" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            var vm = CreateTestViewModel(tempDb);

            var r1 = new ScanHistoryRecordModel { Id = 1, TargetPath = @"C:\", TargetName = "C" };
            var r2 = new ScanHistoryRecordModel { Id = 2, TargetPath = @"D:\", TargetName = "D" };
            vm.ScanHistory.Add(r1);
            vm.ScanHistory.Add(r2);

            // Hapus satu
            vm.SelectedHistoryRecord = r1;
            await vm.HapusRiwayatItemAsync();

            Assert.Single(vm.ScanHistory);
            Assert.Equal(2, vm.ScanHistory[0].Id);

            // Bersihkan semua
            await vm.BersihkanSemuaRiwayatAsync();
            Assert.Empty(vm.ScanHistory);
        }
        finally
        {
            if (File.Exists(tempDb)) try { File.Delete(tempDb); } catch { }
        }
    }

    [Fact]
    public void NavigationCommands_FireRegisteredEvents()
    {
        var vm = CreateTestViewModel();
        vm.CurrentRootItem = new FileSystemItemModel { FullPath = @"C:\", Name = "C:" };

        bool treemapInvoked = false;
        bool hierarchyInvoked = false;
        bool cleanupInvoked = false;

        vm.RequestNavigateToTreemap += () => treemapInvoked = true;
        vm.RequestNavigateToHierarchy += () => hierarchyInvoked = true;
        vm.RequestNavigateToCleanup += () => cleanupInvoked = true;

        vm.BukaTreemapCommand.Execute(null);
        vm.BukaHierarkiCommand.Execute(null);
        vm.BukaPembersihanCommand.Execute(null);

        Assert.True(treemapInvoked);
        Assert.True(hierarchyInvoked);
        Assert.True(cleanupInvoked);
    }
}
