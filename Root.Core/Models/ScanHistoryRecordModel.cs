using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model catatan riwayat pemindaian sistem berkas yang disimpan ke basis data SQLite.
/// </summary>
public class ScanHistoryRecordModel
{
    public long Id { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public long TotalSizeBytes { get; set; }
    public long TotalFiles { get; set; }
    public long TotalDirectories { get; set; }
    public int SkippedCount { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Selesai;
    public bool IsPartial { get; set; }

    public long DriveCapacityBytes { get; set; }
    public long DriveFreeBytes { get; set; }
    public long DriveUsedBytes { get; set; }

    /// <summary>
    /// Menandakan apakah data ini merupakan data historis (lama/arsip).
    /// </summary>
    public bool IsStale { get; set; }

    public string FormattedScannedAt => ScannedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
    public string FormattedDuration => FileFormatter.FormatDuration(Duration);
    public string FormattedTotalSize => FileFormatter.FormatBytes(TotalSizeBytes);
    public string FormattedDriveCapacity => FileFormatter.FormatBytes(DriveCapacityBytes);
    public string FormattedDriveUsed => FileFormatter.FormatBytes(DriveUsedBytes);
    public string FormattedDriveFree => FileFormatter.FormatBytes(DriveFreeBytes);

    public double DriveUsedPercentage => DriveCapacityBytes > 0 
        ? Math.Min(100.0, Math.Max(0.0, (double)DriveUsedBytes / DriveCapacityBytes * 100.0)) 
        : 0.0;

    public double DriveFreePercentage => DriveCapacityBytes > 0 
        ? Math.Min(100.0, Math.Max(0.0, (double)DriveFreeBytes / DriveCapacityBytes * 100.0)) 
        : 0.0;

    public string FormattedDriveUsedPercentage => $"{DriveUsedPercentage:F1}%";
    public string FormattedDriveFreePercentage => $"{DriveFreePercentage:F1}%";

    public string StatusDescription => Status switch
    {
        ScanStatus.Selesai => "Selesai Penuh (100%)",
        ScanStatus.SelesaiSebagian => "Selesai Sebagian (Parsial)",
        ScanStatus.Dibatalkan => "Dibatalkan Pengguna",
        ScanStatus.Gagal => "Gagal",
        _ => "Memproses"
    };

    public string HistoricalLabel => IsStale 
        ? $"Data Riwayat ({ScannedAt.ToLocalTime():dd/MM/yyyy HH:mm})" 
        : "Pemindaian Terkini";
}
