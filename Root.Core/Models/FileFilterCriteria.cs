using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Kriteria penyaringan berkas berdasarkan ekstensi, batas ukuran minimum, dan teks pencarian.
/// </summary>
public class FileFilterCriteria
{
    private string? _extension;

    /// <summary>
    /// Ekstensi berkas yang disaring (mis. ".mp4", "zip", atau kosong/null untuk semua).
    /// </summary>
    public string? Extension
    {
        get => _extension;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("Semua", StringComparison.OrdinalIgnoreCase) || value.Equals("Semua Ekstensi", StringComparison.OrdinalIgnoreCase))
            {
                _extension = null;
            }
            else
            {
                var clean = value.Trim().ToLowerInvariant();
                _extension = clean.StartsWith('.') ? clean : "." + clean;
            }
        }
    }

    /// <summary>
    /// Batas ukuran bita minimum (berkas di bawah ukuran ini akan disaring).
    /// </summary>
    public long MinimumSizeBytes { get; set; } = 0;

    /// <summary>
    /// Teks pencarian kata kunci pada nama berkas atau direktori.
    /// </summary>
    public string? SearchText { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Extension) && MinimumSizeBytes <= 0 && string.IsNullOrWhiteSpace(SearchText);

    public string FormattedMinimumSize => MinimumSizeBytes > 0 ? FileFormatter.FormatBytes(MinimumSizeBytes) : "Semua Ukuran";

    /// <summary>
    /// Memeriksa apakah suatu berkas memenuhi kriteria penyaringan.
    /// </summary>
    public bool MatchesFile(FileSystemItemModel file)
    {
        if (file.IsDirectory)
            return false;

        // 1. Batas ukuran minimum
        if (MinimumSizeBytes > 0 && file.SizeBytes < MinimumSizeBytes)
            return false;

        // 2. Penyaringan ekstensi
        if (!string.IsNullOrWhiteSpace(Extension))
        {
            var fileExt = file.Extension.ToLowerInvariant();
            if (!fileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 3. Pencarian teks nama
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (!file.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
