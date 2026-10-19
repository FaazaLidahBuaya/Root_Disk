using System;
using Root.Core.Mvvm;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model representasi berkas kandidat pembersihan yang diidentifikasi secara konservatif.
/// </summary>
public class CleanupCandidateModel : ViewModelBase
{
    private bool _isSelected;

    public CleanupCandidateModel(
        string fullPath,
        string fileName,
        string directoryPath,
        string extension,
        CleanupCategory category,
        string reason,
        long fileSizeBytes,
        DateTime lastModified,
        CleanupSafetyLevel safetyLevel = CleanupSafetyLevel.AmanKonservatif,
        bool isProtected = false)
    {
        FullPath = fullPath;
        FileName = fileName;
        DirectoryPath = directoryPath;
        Extension = extension;
        Category = category;
        Reason = reason;
        FileSizeBytes = fileSizeBytes;
        LastModified = lastModified;
        SafetyLevel = safetyLevel;
        IsProtected = isProtected;

        TimeSpan age = DateTime.UtcNow - lastModified.ToUniversalTime();
        AgeDays = Math.Max(0, (int)age.TotalDays);

        // Secara default tidak ada berkas yang terpilih otomatis
        _isSelected = false;
    }

    /// <summary>
    /// Lokasi lengkap (full path) berkas di sistem penyimpanan.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Nama berkas beserta ekstensinya.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Jalur direktori induk berkas.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Ekstensi berkas (misal .tmp, .dmp).
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Kategori klasifikasi kandidat pembersihan.
    /// </summary>
    public CleanupCategory Category { get; }

    /// <summary>
    /// Label kategori dalam bahasa Indonesia yang ramah pengguna.
    /// </summary>
    public string CategoryName => Category switch
    {
        CleanupCategory.BerkasSementara => "Berkas Sementara Usang",
        CleanupCategory.CrashDump => "Crash Dump & Diagnostik",
        CleanupCategory.UnduhanTerbengkalai => "Unduhan Terbengkalai",
        CleanupCategory.ThumbnailCacheUsang => "Thumbnail Cache Usang",
        CleanupCategory.LogKadaluarsa => "Log Aplikasi Kadaluarsa",
        _ => "Lainnya"
    };

    /// <summary>
    /// Alasan detail mengapa berkas ini teridentifikasi sebagai kandidat pembersihan.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Ukuran berkas saat analisis dalam bita.
    /// </summary>
    public long FileSizeBytes { get; }

    /// <summary>
    /// Format ukuran berkas yang mudah dibaca (misal 14,50 MB).
    /// </summary>
    public string FormattedSize => FileFormatter.FormatBytes(FileSizeBytes);

    /// <summary>
    /// Perkiraan ruang penyimpanan yang dapat dibebaskan jika berkas ini dibersihkan.
    /// </summary>
    public long EstimatedFreedSizeBytes => FileSizeBytes;

    /// <summary>
    /// Format perkiraan ruang yang dibebaskan (misal 14,50 MB).
    /// </summary>
    public string FormattedEstimatedFreedSpace => FileFormatter.FormatBytes(EstimatedFreedSizeBytes);

    /// <summary>
    /// Waktu terakhir berkas dimodifikasi.
    /// </summary>
    public DateTime LastModified { get; }

    /// <summary>
    /// Format tanggal modifikasi terakhir.
    /// </summary>
    public string FormattedLastModified => LastModified.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Usia berkas sejak terakhir dimodifikasi dalam satuan hari.
    /// </summary>
    public int AgeDays { get; }

    /// <summary>
    /// Tingkat keamanan pembersihan berkas ini.
    /// </summary>
    public CleanupSafetyLevel SafetyLevel { get; }

    /// <summary>
    /// Label tingkat keamanan dalam bahasa Indonesia.
    /// </summary>
    public string SafetyLevelName => SafetyLevel switch
    {
        CleanupSafetyLevel.AmanKonservatif => "Aman (Konservatif)",
        CleanupSafetyLevel.PerluPerhatian => "Perlu Perhatian",
        CleanupSafetyLevel.TerproteksiSistem => "Terproteksi Sistem",
        _ => "Tidak Diketahui"
    };

    /// <summary>
    /// Menandakan apakah berkas berada di area direktori sistem yang terproteksi.
    /// </summary>
    public bool IsProtected { get; }

    /// <summary>
    /// Menandakan apakah item ini diizinkan untuk dipilih oleh pengguna (tidak terproteksi sistem).
    /// </summary>
    public bool CanBeSelected => !IsProtected;

    /// <summary>
    /// Status apakah pengguna telah memilih item ini untuk dibersihkan.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (IsProtected && value)
            {
                // Berkas terproteksi sistem tidak boleh dipilih untuk dihapus
                return;
            }

            SetProperty(ref _isSelected, value);
        }
    }
}
