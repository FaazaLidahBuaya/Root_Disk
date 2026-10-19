namespace Root.Core.Models;

/// <summary>
/// Opsi konfigurasi pencarian berkas duplikat.
/// </summary>
public class DuplicateSearchOptions
{
    /// <summary>
    /// Menentukan apakah berkas kosong (0-byte) harus dimasukkan dalam pencarian duplikat.
    /// Default adalah false agar tidak membebani laporan dengan ratusan file 0-byte kosong.
    /// </summary>
    public bool IncludeZeroByteFiles { get; set; } = false;

    /// <summary>
    /// Batas ukuran minimum berkas yang akan diperiksa (dalam bita).
    /// </summary>
    public long MinimumSizeBytes { get; set; } = 1;

    /// <summary>
    /// Pola ekstensi berkas yang ingin dicari (misal: "*.jpg;*.png" atau null untuk semua berkas).
    /// </summary>
    public string? ExtensionFilter { get; set; }

    /// <summary>
    /// Ukuran batas untuk mengaktifkan pre-filter hash sebagian (dalam bita, default 64 KB).
    /// </summary>
    public long LargeFileThresholdBytes { get; set; } = 64 * 1024;
}
