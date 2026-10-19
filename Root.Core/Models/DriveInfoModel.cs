using System.IO;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model data untuk drive penyimpanan yang terdeteksi.
/// </summary>
public class DriveInfoModel
{
    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public string DriveFormat { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public long FreeSpaceBytes { get; set; }
    public bool IsReady { get; set; }

    public long UsedSizeBytes => TotalSizeBytes > FreeSpaceBytes ? TotalSizeBytes - FreeSpaceBytes : 0;
    
    public double UsedPercentage => TotalSizeBytes > 0 
        ? Math.Round(((double)UsedSizeBytes / TotalSizeBytes) * 100.0, 1) 
        : 0;

    public string FormattedTotalSize => FileFormatter.FormatBytes(TotalSizeBytes);
    public string FormattedFreeSpace => FileFormatter.FormatBytes(FreeSpaceBytes);
    public string FormattedUsedSpace => FileFormatter.FormatBytes(UsedSizeBytes);

    public string DisplayName
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(VolumeLabel) ? "Drive Lokal" : VolumeLabel;
            return $"{Name} ({label}) - {FormattedFreeSpace} bebas dari {FormattedTotalSize}";
        }
    }
}
