namespace Root.Core.Models;

/// <summary>
/// Opsi konfigurasi analisis kandidat pembersihan yang konservatif.
/// </summary>
public class CleanupOptions
{
    /// <summary>
    /// Usia minimum (hari) berkas sementara (.tmp, .bak) agar dianggap aman dianalisis.
    /// Standar konservatif: 14 hari untuk mencegah penghapusan berkas sementara aplikasi aktif.
    /// </summary>
    public int MinimumAgeDaysTemporaryFiles { get; set; } = 14;

    /// <summary>
    /// Usia minimum (hari) berkas crash dump (.dmp, .crash). Standar: 14 hari.
    /// </summary>
    public int MinimumAgeDaysCrashDumps { get; set; } = 14;

    /// <summary>
    /// Usia minimum (hari) berkas unduhan yang terhenti (.crdownload, .part). Standar: 7 hari.
    /// </summary>
    public int MinimumAgeDaysIncompleteDownloads { get; set; } = 7;

    /// <summary>
    /// Usia minimum (hari) log kadaluarsa (.log, .log.old). Standar: 30 hari.
    /// </summary>
    public int MinimumAgeDaysExpiredLogs { get; set; } = 30;

    /// <summary>
    /// Sertakan berkas sementara usang.
    /// </summary>
    public bool IncludeTemporaryFiles { get; set; } = true;

    /// <summary>
    /// Sertakan crash dump dan berkas diagnostik.
    /// </summary>
    public bool IncludeCrashDumps { get; set; } = true;

    /// <summary>
    /// Sertakan unduhan peramban terbengkalai.
    /// </summary>
    public bool IncludeIncompleteDownloads { get; set; } = true;

    /// <summary>
    /// Sertakan basis data thumbnail usang (Thumbs.db di folder pengguna).
    /// </summary>
    public bool IncludeOrphanedThumbnails { get; set; } = true;

    /// <summary>
    /// Sertakan berkas log aplikasi kadaluarsa.
    /// </summary>
    public bool IncludeExpiredLogs { get; set; } = true;

    /// <summary>
    /// Batas ukuran minimum berkas (bita).
    /// </summary>
    public long MinimumFileSizeBytes { get; set; } = 0;
}
