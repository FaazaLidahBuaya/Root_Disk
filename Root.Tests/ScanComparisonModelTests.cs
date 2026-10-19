using System;
using Root.Core.Models;
using Xunit;

namespace Root.Tests;

public class ScanComparisonModelTests
{
    [Fact]
    public void Comparison_WithPreviousScan_CalculatesGrowthTrendAndDeltaMetrics()
    {
        var prev = new ScanHistoryRecordModel
        {
            Id = 1,
            TargetPath = @"D:\Media",
            TargetName = "Media",
            ScannedAt = DateTime.UtcNow.AddDays(-3),
            Duration = TimeSpan.FromSeconds(30),
            TotalSizeBytes = 1024L * 1024 * 100, // 100 MB
            TotalFiles = 1000,
            TotalDirectories = 50,
            IsStale = true
        };

        var curr = new ScanHistoryRecordModel
        {
            Id = 2,
            TargetPath = @"D:\Media",
            TargetName = "Media",
            ScannedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(35),
            TotalSizeBytes = 1024L * 1024 * 150, // 150 MB
            TotalFiles = 1200,
            TotalDirectories = 55,
            IsStale = false
        };

        var comparison = new ScanComparisonModel(curr, prev);

        Assert.True(comparison.HasPreviousComparison);
        Assert.Equal(1024L * 1024 * 50, comparison.DeltaSizeBytes);
        Assert.Equal("+50,00 MB", comparison.FormattedDeltaSize);
        Assert.Equal("+200 berkas", comparison.FormattedDeltaFiles);
        Assert.Equal("+5 folder", comparison.FormattedDeltaDirectories);
        Assert.Equal("+50,0%", comparison.FormattedGrowthPercentage);
        Assert.Equal("Ukuran Bertambah", comparison.Trend);
        Assert.Contains("Data Riwayat", comparison.HistoricalComparisonLabel);
        Assert.Contains("meningkat sebesar +50,00 MB (+50,0%)", comparison.ComparisonSummary);
    }

    [Fact]
    public void Comparison_WithDecreasedSize_CalculatesNegativeDeltaMetrics()
    {
        var prev = new ScanHistoryRecordModel
        {
            Id = 1,
            TargetPath = @"D:\Temp",
            TotalSizeBytes = 1024L * 1024 * 200, // 200 MB
            TotalFiles = 1500,
            TotalDirectories = 80,
            ScannedAt = DateTime.UtcNow.AddDays(-1),
            IsStale = true
        };

        var curr = new ScanHistoryRecordModel
        {
            Id = 2,
            TargetPath = @"D:\Temp",
            TotalSizeBytes = 1024L * 1024 * 120, // 120 MB (berkurang setelah pembersihan)
            TotalFiles = 1100,
            TotalDirectories = 70,
            ScannedAt = DateTime.UtcNow,
            IsStale = false
        };

        var comparison = new ScanComparisonModel(curr, prev);

        Assert.True(comparison.HasPreviousComparison);
        Assert.Equal(-1024L * 1024 * 80, comparison.DeltaSizeBytes);
        Assert.Equal("-80,00 MB", comparison.FormattedDeltaSize);
        Assert.Equal("-400 berkas", comparison.FormattedDeltaFiles);
        Assert.Equal("-10 folder", comparison.FormattedDeltaDirectories);
        Assert.Equal("-40,0%", comparison.FormattedGrowthPercentage);
        Assert.Equal("Ukuran Berkurang", comparison.Trend);
        Assert.Contains("berkurang sebesar -80,00 MB", comparison.ComparisonSummary);
    }

    [Fact]
    public void Comparison_WithoutPreviousScan_ExposesFirstScanState()
    {
        var curr = new ScanHistoryRecordModel
        {
            Id = 1,
            TargetPath = @"E:\NewDrive",
            TotalSizeBytes = 1024 * 1024 * 50,
            TotalFiles = 200,
            TotalDirectories = 20
        };

        var comparison = new ScanComparisonModel(curr, null);

        Assert.False(comparison.HasPreviousComparison);
        Assert.Equal("-", comparison.FormattedDeltaSize);
        Assert.Equal("-", comparison.FormattedDeltaFiles);
        Assert.Equal("Pertama Kali Dipindai", comparison.Trend);
        Assert.Contains("pemindaian pertama", comparison.ComparisonSummary);
    }
}
