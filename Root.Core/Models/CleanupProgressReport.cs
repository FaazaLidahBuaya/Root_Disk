using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Laporan kemajuan waktu nyata untuk analisis atau eksekusi pembersihan.
/// </summary>
public class CleanupProgressReport
{
    public string Stage { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public string CurrentFilePath { get; set; } = string.Empty;
    public double ProgressPercentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100.0 : 0.0;
    public long FreedBytesSoFar { get; set; }
    public string FormattedFreedBytesSoFar => FileFormatter.FormatBytes(FreedBytesSoFar);
}
