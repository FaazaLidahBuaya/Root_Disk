namespace Root.Core.Models;

/// <summary>
/// Bidang kriteria pengurutan item sistem berkas.
/// </summary>
public enum SortField
{
    Ukuran,
    Nama,
    TanggalDiubah
}

/// <summary>
/// Arah pengurutan item.
/// </summary>
public enum SortDirection
{
    Menurun, // Descending
    Menaik   // Ascending
}
