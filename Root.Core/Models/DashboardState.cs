namespace Root.Core.Models;

/// <summary>
/// Status tampilan dasbor ROOT.
/// </summary>
public enum DashboardState
{
    /// <summary>
    /// Belum ada pemindaian yang dilakukan (kondisi awal).
    /// </summary>
    Kosong,

    /// <summary>
    /// Pemindaian sedang berlangsung (memuat data waktu nyata).
    /// </summary>
    Memuat,

    /// <summary>
    /// Pemindaian berhasil selesai secara penuh (100% lengkap).
    /// </summary>
    Selesai,

    /// <summary>
    /// Pemindaian selesai sebagian (ada folder yang dilewati karena izin akses / error).
    /// </summary>
    Parsial,

    /// <summary>
    /// Pemindaian mengalami kegagalan fatal atau dibatalkan.
    /// </summary>
    Gagal
}
