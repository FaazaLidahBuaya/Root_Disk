using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Laporan kemajuan pemindaian secara berkala (real-time progress).
/// </summary>
public class ScanProgressReport
{
    public string CurrentPath { get; set; } = string.Empty;
    public long ScannedFilesCount { get; set; }
    public long ScannedDirectoriesCount { get; set; }
    public long TotalBytesScanned { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public double FilesPerSecond { get; set; }
    public double BytesPerSecond { get; set; }
    public int SkippedCount { get; set; }
    /// <summary>Snapshot dangkal dan terbatas untuk menampilkan peta selama pemindaian.</summary>
    public FileSystemItemModel? RootPreview { get; set; }

    public string FormattedTotalBytes => FileFormatter.FormatBytes(TotalBytesScanned);
    public string FormattedFilesCount => FileFormatter.FormatNumber(ScannedFilesCount);
    public string FormattedDirectoriesCount => FileFormatter.FormatNumber(ScannedDirectoriesCount);
    public string FormattedElapsedTime => FileFormatter.FormatDuration(ElapsedTime);
    public string FormattedRate => FileFormatter.FormatRate(FilesPerSecond, BytesPerSecond);
}
