namespace Root.Core.Models;

/// <summary>
/// Status hasil pemeriksaan ulang atau eksekusi pada suatu target pembersihan.
/// </summary>
public enum CleanupItemStatus
{
    /// <summary>
    /// Menunggu validasi dan eksekusi.
    /// </summary>
    Menunggu,

    /// <summary>
    /// Berhasil dihapus secara aman dari sistem berkas.
    /// </summary>
    BerhasilDihapus,

    /// <summary>
    /// Dilewati karena berkas berubah (ukuran atau waktu modifikasi berbeda dengan saat analisis).
    /// </summary>
    DilewatiBerkasBerubah,

    /// <summary>
    /// Dilewati karena berkas sudah tidak ditemukan (terhapus atau dipindahkan pihak luar).
    /// </summary>
    DilewatiBerkasHilang,

    /// <summary>
    /// Ditolak karena berada dalam direktori sistem yang terproteksi.
    /// </summary>
    DitolakProteksiSistem,

    /// <summary>
    /// Gagal karena izin akses ditolak (UnauthorizedAccessException).
    /// </summary>
    GagalIzinDitolak,

    /// <summary>
    /// Gagal karena berkas sedang dikunci atau digunakan proses lain (IOException).
    /// </summary>
    GagalBerkasTerkunci,

    /// <summary>
    /// Gagal karena kesalahan umum lainnya.
    /// </summary>
    GagalLainnya
}
