using System;
using System.Collections.Generic;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Laporan hasil pemindaian berkas lengkap atau parsial.
/// </summary>
public class ScanResultReport
{
    public string TargetPath { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime >= StartTime ? EndTime - StartTime : TimeSpan.Zero;
    public ScanStatus Status { get; set; } = ScanStatus.Siap;
    public long TotalSizeBytes { get; set; }
    public long TotalFilesCount { get; set; }
    public long TotalDirectoriesCount { get; set; }
    public int SkippedCount { get; set; }

    private bool _isPartial;
    public bool IsPartial
    {
        get => _isPartial || SkippedCount > 0 || Status == ScanStatus.Dibatalkan || Status == ScanStatus.SelesaiSebagian || Status == ScanStatus.Gagal;
        set => _isPartial = value;
    }

    public FileSystemItemModel? RootItem { get; set; }
    public List<FileSystemItemModel> TopLargestFiles { get; set; } = [];
    public List<FileSystemItemModel> TopLargestDirectories { get; set; } = [];
    public List<ScanErrorItem> Errors { get; set; } = [];

    public string FormattedTotalSize => FileFormatter.FormatBytes(TotalSizeBytes);
    public string FormattedDuration => FileFormatter.FormatDuration(Duration);
    public string FormattedFilesCount => FileFormatter.FormatNumber(TotalFilesCount);
    public string FormattedDirectoriesCount => FileFormatter.FormatNumber(TotalDirectoriesCount);
    public string FormattedSkippedCount => FileFormatter.FormatNumber(SkippedCount);

    public string StatusDescription => Status switch
    {
        ScanStatus.Selesai => (SkippedCount > 0 || IsPartial)
            ? "Pemindaian Selesai Sebagian (Ada Berkas Dilewati)"
            : "Pemindaian Selesai",
        ScanStatus.Dibatalkan => "Pemindaian Dibatalkan (Laporan Parsial)",
        ScanStatus.SelesaiSebagian => "Pemindaian Selesai Sebagian (Ada Berkas Dilewati)",
        ScanStatus.Memindai => "Sedang Memindai...",
        ScanStatus.Gagal => "Pemindaian Gagal",
        _ => "Siap"
    };

    public string GenerateSummaryMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Laporan Pemindaian ROOT - {(IsPartial ? "Parsial" : "Lengkap")}");
        sb.AppendLine();
        sb.AppendLine($"- **Jalur Target**: `{TargetPath}`");
        sb.AppendLine($"- **Status**: {StatusDescription}");
        sb.AppendLine($"- **Waktu Mulai**: {StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **Waktu Selesai**: {EndTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **Durasi**: {FormattedDuration}");
        sb.AppendLine($"- **Total Ukuran Terpindai**: {FormattedTotalSize}");
        sb.AppendLine($"- **Jumlah Berkas**: {FormattedFilesCount}");
        sb.AppendLine($"- **Jumlah Direktori**: {FormattedDirectoriesCount}");
        sb.AppendLine($"- **Item Dilewati (Izin Ditolak / Akses Gagal)**: {FormattedSkippedCount}");
        sb.AppendLine();

        if (TopLargestFiles.Count > 0)
        {
            sb.AppendLine("## Berkas Terbesar (Top)");
            sb.AppendLine("| Nama Berkas | Ukuran | Jalur Lengkap |");
            sb.AppendLine("|---|---|---|");
            foreach (var file in TopLargestFiles)
            {
                sb.AppendLine($"| {file.Name} | {file.FormattedSize} | `{file.FullPath}` |");
            }
            sb.AppendLine();
        }

        if (TopLargestDirectories.Count > 0)
        {
            sb.AppendLine("## Direktori Terbesar (Top)");
            sb.AppendLine("| Nama Folder | Ukuran | Jumlah Berkas | Jalur Lengkap |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var dir in TopLargestDirectories)
            {
                sb.AppendLine($"| {dir.Name} | {dir.FormattedSize} | {dir.FileCount} | `{dir.FullPath}` |");
            }
            sb.AppendLine();
        }

        if (Errors.Count > 0)
        {
            sb.AppendLine($"## Daftar Izin Ditolak & Kegagalan Akses ({Errors.Count} item)");
            sb.AppendLine("| Waktu | Tipe Kesalahan | Pesan | Jalur |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var err in Errors)
            {
                sb.AppendLine($"| {err.FormattedTime} | {err.FriendlyType} | {err.ErrorMessage.Replace("\n", " ").Replace("\r", "")} | `{err.Path}` |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
