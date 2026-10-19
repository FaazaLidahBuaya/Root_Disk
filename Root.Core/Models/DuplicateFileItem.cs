using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Representasi satu berkas dalam kelompok duplikat.
/// </summary>
public class DuplicateFileItem
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime? LastModified { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;

    public string FormattedSize => FileFormatter.FormatBytes(SizeBytes);
    public string FormattedLastModified => LastModified.HasValue ? LastModified.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
    public bool IsZeroByte => SizeBytes == 0;
}
