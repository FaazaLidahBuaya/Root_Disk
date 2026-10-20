using System;
using System.IO;

namespace Root.Core.Utilities;

/// <summary>
/// Pembantu penentuan warna palet visual ROOT untuk node treemap berdasarkan jenis ekstensi dan tipe direktori.
/// </summary>
public static class TreemapColorHelper
{
    // Warna untuk direktori (Slate & Cyan accents)
    public const string DirectoryColor = "#D9E4FF";
    public const string DirectoryBorder = "#7567F8";

    // Kategori warna berkas
    public const string ExecutableColor = "#DED9FF";
    public const string MediaVideoColor = "#D4E9FF";
    public const string MediaAudioColor = "#D1F0E8";
    public const string ImageColor = "#D7F1E1";
    public const string ArchiveColor = "#FBE9CB";
    public const string DocumentColor = "#F9DDE4";
    public const string CodeColor = "#E9DDF8";
    public const string DatabaseColor = "#F6DCEC";
    public const string SystemColor = "#E5E9F2";
    public const string OtherColor = "#E8ECF4";

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
