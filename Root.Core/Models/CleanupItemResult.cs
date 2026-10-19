namespace Root.Core.Models;

/// <summary>
/// Hasil penanganan satu berkas target pembersihan.
/// </summary>
public class CleanupItemResult
{
    public CleanupItemResult(
        string fullPath,
        CleanupCategory category,
        long fileSizeBytes,
        CleanupItemStatus status,
        string? errorMessage = null)
    {
        FullPath = fullPath;
        Category = category;
        FileSizeBytes = fileSizeBytes;
        Status = status;
        ErrorMessage = errorMessage;
    }

    public string FullPath { get; }
    public CleanupCategory Category { get; }
    public long FileSizeBytes { get; }
    public CleanupItemStatus Status { get; }
    public string? ErrorMessage { get; }

    public string StatusDescription => Status switch
    {
        CleanupItemStatus.BerhasilDihapus => "Berhasil Dihapus",
        CleanupItemStatus.DilewatiBerkasBerubah => "Dilewati: Berkas berubah sejak analisis (Keamanan Terjaga)",
        CleanupItemStatus.DilewatiBerkasHilang => "Dilewati: Berkas sudah tidak ditemukan",
        CleanupItemStatus.DitolakProteksiSistem => "Ditolak: Jalur terproteksi sistem operasi",
        CleanupItemStatus.GagalIzinDitolak => $"Gagal: Izin akses ditolak ({ErrorMessage ?? "Akses Ditolak"})",
        CleanupItemStatus.GagalBerkasTerkunci => $"Gagal: Berkas sedang digunakan proses lain ({ErrorMessage ?? "Terkunci"})",
        CleanupItemStatus.GagalLainnya => $"Gagal: {ErrorMessage ?? "Kesalahan tak terduga"}",
        _ => "Menunggu"
    };
}
