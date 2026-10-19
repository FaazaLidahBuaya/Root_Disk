namespace Root.Core.Models;

/// <summary>
/// Hasil dari upaya elevasi atau memulai ulang aplikasi dengan hak Administrator melalui UAC Windows.
/// </summary>
public class AdminElevationResult
{
    /// <summary>
    /// Menunjukkan apakah proses baru berhasil dimulai dengan hak yang diminta.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Menunjukkan apakah pengguna secara sengaja membatalkan/menolak dialog UAC Windows (Win32 Error 1223).
    /// </summary>
    public bool CancelledByUser { get; set; }

    /// <summary>
    /// Pesan rincian status atau kesalahan.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Kode kesalahan Win32 asli jika terjadi kegagalan sistem.
    /// </summary>
    public int? NativeErrorCode { get; set; }

    public static AdminElevationResult Succeeded() => new()
    {
        Success = true,
        CancelledByUser = false,
        ErrorMessage = null,
        NativeErrorCode = null
    };

    public static AdminElevationResult Cancelled(string message = "Pengguna membatalkan dialog elevasi Windows UAC.") => new()
    {
        Success = false,
        CancelledByUser = true,
        ErrorMessage = message,
        NativeErrorCode = 1223
    };

    public static AdminElevationResult Failed(string message, int? nativeErrorCode = null) => new()
    {
        Success = false,
        CancelledByUser = false,
        ErrorMessage = message,
        NativeErrorCode = nativeErrorCode
    };
}
