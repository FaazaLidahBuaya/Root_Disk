using System;
using System.IO;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class SqliteScanHistoryRepositoryTests
{
    [Fact]
    public async Task InitializeAndSaveRecord_InsertsRecordAndAssignsAutoIncrementId()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), "test_repo_" + Guid.NewGuid().ToString("N") + ".db");
        var repo = new SqliteScanHistoryRepository(dbPath);

        try
        {
            var record = new ScanHistoryRecordModel
            {
                TargetPath = @"C:\Testing",
                TargetName = "Testing Target",
                ScannedAt = DateTime.UtcNow,
                Duration = TimeSpan.FromSeconds(45),
                TotalSizeBytes = 1024L * 1024 * 1024 * 2, // 2 GB
                TotalFiles = 15000,
                TotalDirectories = 1200,
                SkippedCount = 3,
                Status = ScanStatus.SelesaiSebagian,
                IsPartial = true,
                DriveCapacityBytes = 1024L * 1024 * 1024 * 500, // 500 GB
                DriveFreeBytes = 1024L * 1024 * 1024 * 200,     // 200 GB
                DriveUsedBytes = 1024L * 1024 * 1024 * 300      // 300 GB
            };

            var saved = await repo.SaveRecordAsync(record);

            Assert.True(saved.Id > 0);
            Assert.Equal(@"C:\Testing", saved.TargetPath);
            Assert.True(saved.IsPartial);

            var history = await repo.GetHistoryAsync(10);
            Assert.Single(history);
            var item = history[0];
            Assert.Equal(saved.Id, item.Id);
            Assert.Equal(15000, item.TotalFiles);
            Assert.Equal(3, item.SkippedCount);
            Assert.Equal(ScanStatus.SelesaiSebagian, item.Status);
            Assert.Equal("Selesai Sebagian (Parsial)", item.StatusDescription);
            Assert.Equal("60,0%", item.FormattedDriveUsedPercentage);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task GetPreviousScanAsync_RetrievesMostRecentPreviousScanExcludingCurrent()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), "test_prev_" + Guid.NewGuid().ToString("N") + ".db");
        var repo = new SqliteScanHistoryRepository(dbPath);

        try
        {
            string target = @"D:\Projects";

            // Catatan 1 (tertua)
            var rec1 = await repo.SaveRecordAsync(new ScanHistoryRecordModel
            {
                TargetPath = target,
                TargetName = "Projects",
                ScannedAt = DateTime.UtcNow.AddDays(-2),
                Duration = TimeSpan.FromSeconds(30),
                TotalSizeBytes = 1024 * 1024 * 100, // 100 MB
                TotalFiles = 500,
                TotalDirectories = 50,
                Status = ScanStatus.Selesai
            });

            // Catatan 2 (tengah)
            var rec2 = await repo.SaveRecordAsync(new ScanHistoryRecordModel
            {
                TargetPath = target,
                TargetName = "Projects",
                ScannedAt = DateTime.UtcNow.AddDays(-1),
                Duration = TimeSpan.FromSeconds(35),
                TotalSizeBytes = 1024 * 1024 * 150, // 150 MB
                TotalFiles = 600,
                TotalDirectories = 55,
                Status = ScanStatus.Selesai
            });

            // Catatan 3 (terbaru)
            var rec3 = await repo.SaveRecordAsync(new ScanHistoryRecordModel
            {
                TargetPath = target,
                TargetName = "Projects",
                ScannedAt = DateTime.UtcNow,
                Duration = TimeSpan.FromSeconds(40),
                TotalSizeBytes = 1024 * 1024 * 200, // 200 MB
                TotalFiles = 750,
                TotalDirectories = 60,
                Status = ScanStatus.Selesai
            });

            // Cari pemindaian sebelum rec3
            var previous = await repo.GetPreviousScanAsync(target, rec3.Id);

            Assert.NotNull(previous);
            Assert.Equal(rec2.Id, previous.Id);
            Assert.Equal(600, previous.TotalFiles);
            Assert.True(previous.IsStale); // Ditandai sebagai data historis/lama
            Assert.Contains("Data Riwayat", previous.HistoricalLabel);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task DeleteRecordAndClearHistory_RemovesRecordsPermanently()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), "test_del_" + Guid.NewGuid().ToString("N") + ".db");
        var repo = new SqliteScanHistoryRepository(dbPath);

        try
        {
            var r1 = await repo.SaveRecordAsync(new ScanHistoryRecordModel { TargetPath = @"C:\", TargetName = "C", TotalFiles = 10 });
            var r2 = await repo.SaveRecordAsync(new ScanHistoryRecordModel { TargetPath = @"D:\", TargetName = "D", TotalFiles = 20 });

            var listBefore = await repo.GetHistoryAsync();
            Assert.Equal(2, listBefore.Count);

            // Hapus r1
            await repo.DeleteRecordAsync(r1.Id);
            var listAfterOne = await repo.GetHistoryAsync();
            Assert.Single(listAfterOne);
            Assert.Equal(r2.Id, listAfterOne[0].Id);

            // Bersihkan semua
            await repo.ClearHistoryAsync();
            var listEmpty = await repo.GetHistoryAsync();
            Assert.Empty(listEmpty);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}
