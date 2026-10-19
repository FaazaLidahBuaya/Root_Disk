using System;

namespace Root.Core.Models;

/// <summary>
/// Model untuk mencatat berkas atau direktori yang gagal diakses (misalnya izin ditolak / access denied).
/// </summary>
public class ScanErrorItem
{
    public string Path { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public string FriendlyType => ExceptionType switch
    {
        nameof(UnauthorizedAccessException) => "Izin Ditolak",
        nameof(System.IO.PathTooLongException) => "Jalur Terlalu Panjang",
        nameof(System.IO.DirectoryNotFoundException) => "Direktori Hilang / Terhapus",
        nameof(System.IO.FileNotFoundException) => "Berkas Hilang / Terhapus",
        nameof(System.IO.IOException) => "Kesalahan I/O",
        nameof(System.Security.SecurityException) => "Keamanan Sistem",
        _ => "Gagal Akses"
    };
}
