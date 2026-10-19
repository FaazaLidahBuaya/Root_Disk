using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model perbandingan hasil pemindaian terkini dengan pemindaian lama (sebelumnya).
/// </summary>
public class ScanComparisonModel
{
    public ScanComparisonModel(ScanHistoryRecordModel currentScan, ScanHistoryRecordModel? previousScan = null)
    {
        CurrentScan = currentScan;
        PreviousScan = previousScan;

        if (previousScan != null)
        {
            HasPreviousComparison = true;
            DeltaSizeBytes = currentScan.TotalSizeBytes - previousScan.TotalSizeBytes;
            DeltaFiles = currentScan.TotalFiles - previousScan.TotalFiles;
            DeltaDirectories = currentScan.TotalDirectories - previousScan.TotalDirectories;
            DeltaDuration = currentScan.Duration - previousScan.Duration;

            if (previousScan.TotalSizeBytes > 0)
            {
                SizeGrowthPercentage = (double)DeltaSizeBytes / previousScan.TotalSizeBytes * 100.0;
            }
        }
    }

    public bool HasPreviousComparison { get; }
    public ScanHistoryRecordModel CurrentScan { get; }
    public ScanHistoryRecordModel? PreviousScan { get; }

    public long DeltaSizeBytes { get; }
    public long DeltaFiles { get; }
    public long DeltaDirectories { get; }
    public TimeSpan DeltaDuration { get; }
    public double SizeGrowthPercentage { get; }

    public string FormattedDeltaSize
    {
        get
        {
            if (!HasPreviousComparison) return "-";
            string formatted = FileFormatter.FormatBytes(Math.Abs(DeltaSizeBytes));
            return DeltaSizeBytes > 0 ? $"+{formatted}" : (DeltaSizeBytes < 0 ? $"-{formatted}" : "0 B");
        }
    }

    public string FormattedDeltaFiles
    {
        get
        {
            if (!HasPreviousComparison) return "-";
            return DeltaFiles > 0 ? $"+{DeltaFiles:N0} berkas" : (DeltaFiles < 0 ? $"{DeltaFiles:N0} berkas" : "0 berkas");
        }
    }

    public string FormattedDeltaDirectories
    {
        get
        {
            if (!HasPreviousComparison) return "-";
            return DeltaDirectories > 0 ? $"+{DeltaDirectories:N0} folder" : (DeltaDirectories < 0 ? $"{DeltaDirectories:N0} folder" : "0 folder");
        }
    }

    public string FormattedGrowthPercentage
    {
        get
        {
            if (!HasPreviousComparison) return "-";
            return SizeGrowthPercentage > 0 ? $"+{SizeGrowthPercentage:F1}%" : $"{SizeGrowthPercentage:F1}%";
        }
    }

    public string Trend
    {
        get
        {
            if (!HasPreviousComparison) return "Pertama Kali Dipindai";
            if (DeltaSizeBytes > 0) return "Ukuran Bertambah";
            if (DeltaSizeBytes < 0) return "Ukuran Berkurang";
            return "Ukuran Tetap";
        }
    }

    public string HistoricalComparisonLabel => PreviousScan != null
        ? $"Perbandingan terhadap {PreviousScan.HistoricalLabel}"
        : "Belum ada data pemindaian sebelumnya untuk target ini.";

    public string ComparisonSummary
    {
        get
        {
            if (!HasPreviousComparison || PreviousScan == null)
            {
                return "Ini adalah pemindaian pertama yang tercatat untuk target ini. Data perbandingan akan tersedia pada pemindaian berikutnya.";
            }

            string changeDirection = DeltaSizeBytes > 0 ? "meningkat" : (DeltaSizeBytes < 0 ? "berkurang" : "tidak berubah");
            return $"Sejak pemindaian sebelumnya ({PreviousScan.FormattedScannedAt}), ukuran penyimpanan {changeDirection} sebesar {FormattedDeltaSize} ({FormattedGrowthPercentage}), dengan perubahan {FormattedDeltaFiles} dan {FormattedDeltaDirectories}.";
        }
    }
}
