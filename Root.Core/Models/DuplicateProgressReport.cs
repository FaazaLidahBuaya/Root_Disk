using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Laporan kemajuan waktu nyata untuk pencarian berkas duplikat.
/// </summary>
public class DuplicateProgressReport
{
    public string CurrentStage { get; set; } = string.Empty;
    public string CurrentProcessingFile { get; set; } = string.Empty;

    public int TotalFilesDiscovered { get; set; }
    public int CandidateGroupsCount { get; set; }
    public int CandidateFilesCount { get; set; }
    public int ProcessedFilesCount { get; set; }
    
    public int DuplicateGroupsFound { get; set; }
    public int DuplicateFilesFound { get; set; }
    public long TotalWasteBytesFound { get; set; }

    public double PercentComplete { get; set; }
    public TimeSpan ElapsedTime { get; set; }

    public string FormattedElapsedTime => FileFormatter.FormatDuration(ElapsedTime);
    public string FormattedWasteBytes => FileFormatter.FormatBytes(TotalWasteBytesFound);
}
