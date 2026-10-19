using System;
using System.IO;

namespace Root.Core.Utilities;

/// <summary>
/// Pembantu penentuan warna palet visual ROOT untuk node treemap berdasarkan jenis ekstensi dan tipe direktori.
/// </summary>
public static class TreemapColorHelper
{
    // Warna untuk direktori (Slate & Cyan accents)
    public const string DirectoryColor = "#1E3A5F";
    public const string DirectoryBorder = "#38BDF8";

    // Kategori warna berkas
    public const string ExecutableColor = "#6366F1"; // Ungu indigo (.exe, .dll, .msi, .bat, .cmd, .ps1)
    public const string MediaVideoColor = "#06B6D4";  // Cyan terang (.mp4, .mkv, .avi, .mov, .wmv)
    public const string MediaAudioColor = "#14B8A6";  // Teal (.mp3, .wav, .flac, .aac, .ogg)
    public const string ImageColor = "#10B981";       // Emerald (.png, .jpg, .jpeg, .svg, .webp, .ico)
    public const string ArchiveColor = "#F59E0B";     // Amber (.zip, .rar, .7z, .tar, .gz, .iso)
    public const string DocumentColor = "#F43F5E";    // Rose/Merah (.pdf, .docx, .xlsx, .pptx, .txt, .md)
    public const string CodeColor = "#8B5CF6";        // Violet (.cs, .js, .ts, .py, .html, .css, .json, .xml)
    public const string DatabaseColor = "#EC4899";    // Pink (.db, .sqlite, .sql, .mdf)
    public const string SystemColor = "#9333EA";      // Purple (.sys, .bin, .dat, .log)
    public const string OtherColor = "#475569";       // Slate default

    public static string GetColorForExtension(string? extension, bool isDirectory)
    {
        if (isDirectory)
            return DirectoryColor;

        if (string.IsNullOrWhiteSpace(extension))
            return OtherColor;

        var ext = extension.TrimStart('.').ToLowerInvariant();

        return ext switch
        {
            // Eksekusi & Pustaka
            "exe" or "dll" or "msi" or "bat" or "cmd" or "ps1" or "com" or "vbs" => ExecutableColor,

            // Video
            "mp4" or "mkv" or "avi" or "mov" or "wmv" or "flv" or "webm" => MediaVideoColor,

            // Audio
            "mp3" or "wav" or "flac" or "aac" or "ogg" or "m4a" or "wma" => MediaAudioColor,

            // Gambar
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "svg" or "webp" or "ico" or "tiff" => ImageColor,

            // Arsip & Kompresi
            "zip" or "rar" or "7z" or "tar" or "gz" or "iso" or "bz2" or "xz" => ArchiveColor,

            // Dokumen & Teks
            "pdf" or "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "txt" or "rtf" or "md" => DocumentColor,

            // Kode Sumber & Data
            "cs" or "js" or "ts" or "py" or "html" or "css" or "json" or "xml" or "xaml" or "yaml" or "yml" or "cpp" or "c" or "h" => CodeColor,

            // Database
            "db" or "sqlite" or "sqlite3" or "sql" or "mdf" or "ldf" or "accdb" => DatabaseColor,

            // Sistem & Log
            "sys" or "bin" or "dat" or "log" or "bak" or "tmp" or "cab" => SystemColor,

            // Lainnya
            _ => OtherColor
        };
    }
}
