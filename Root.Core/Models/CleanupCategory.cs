namespace Root.Core.Models;

/// <summary>
/// Kategori kandidat pembersihan sistem berkas yang diidentifikasi secara konservatif.
/// </summary>
public enum CleanupCategory
{
    /// <summary>
    /// Berkas sementara (.tmp, .bak, dll.) yang sudah lama tidak dimodifikasi.
    /// </summary>
    BerkasSementara,

    /// <summary>
    /// Berkas crash dump dan laporan diagnostik kegagalan (.dmp, .crash).
    /// </summary>
    CrashDump,

    /// <summary>
    /// Unduhan peramban yang terhenti atau terbengkalai (.crdownload, .part, .partial).
    /// </summary>
    UnduhanTerbengkalai,

    /// <summary>
    /// Berkas basis data thumbnail usang (Thumbs.db, ehthumbs.db) di direktori non-sistem.
    /// </summary>
    ThumbnailCacheUsang,

    /// <summary>
    /// Berkas log aplikasi kadaluarsa (.log, .log.old) di luar direktori inti sistem.
    /// </summary>
    LogKadaluarsa
}
