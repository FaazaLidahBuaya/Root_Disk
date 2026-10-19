using System.Collections.Generic;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Mewakili satu kelompok berkas yang memiliki ukuran dan isi hash SHA-256 identik.
/// </summary>
public class DuplicateGroupModel
{
    public string Sha256Hash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public List<DuplicateFileItem> Files { get; set; } = [];

    public int FileCount => Files.Count;
    public long TotalGroupSizeBytes => FileSizeBytes * FileCount;
    
    /// <summary>
    /// Estimasi pemborosan ruang penyimpanan (ukuran salinan berlebih jika 1 salinan dipertahankan).
    /// </summary>
    public long WasteSizeBytes => FileCount > 1 ? FileSizeBytes * (FileCount - 1) : 0;

    public string FormattedFileSize => FileFormatter.FormatBytes(FileSizeBytes);
    public string FormattedTotalGroupSize => FileFormatter.FormatBytes(TotalGroupSizeBytes);
    public string FormattedWasteSize => FileFormatter.FormatBytes(WasteSizeBytes);

    public string ShortHash => Sha256Hash.Length >= 12 ? Sha256Hash[..12] : Sha256Hash;
    public bool IsZeroByte => FileSizeBytes == 0;
}
