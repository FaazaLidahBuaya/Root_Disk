using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Hasil akhir dari operasi pencarian berkas duplikat.
/// </summary>
public class DuplicateSearchResult
{
    public List<DuplicateGroupModel> DuplicateGroups { get; set; } = [];
    public List<ScanErrorItem> Errors { get; set; } = [];

    public int TotalScannedFiles { get; set; }
    public int TotalCandidateFiles { get; set; }
    public int TotalDuplicateFiles => DuplicateGroups.Sum(g => g.FileCount);
    public int TotalDuplicateGroups => DuplicateGroups.Count;
    public long TotalWasteBytes => DuplicateGroups.Sum(g => g.WasteSizeBytes);

    public TimeSpan Duration { get; set; }
    public bool IsCancelled { get; set; }

    public long TotalHashedBytes { get; set; }
    public int TotalHashedFiles { get; set; }
    public double HashThroughputBytesPerSecond => Duration.TotalSeconds > 0.05 ? TotalHashedBytes / Duration.TotalSeconds : 0;

    public string FormattedTotalWaste => FileFormatter.FormatBytes(TotalWasteBytes);
    public string FormattedDuration => FileFormatter.FormatDuration(Duration);
    public string FormattedThroughput => $"{FileFormatter.FormatBytes((long)HashThroughputBytesPerSecond)}/s";

    public string GenerateSummaryMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Laporan Temuan Berkas Duplikat ROOT");
        sb.AppendLine();
        sb.AppendLine($"- **Status**: {(IsCancelled ? "Dibatalkan oleh Pengguna (Hasil Parsial)" : "Selesai Penuh")}");
        sb.AppendLine($"- **Total Berkas Terpindai**: {TotalScannedFiles:N0} berkas");
        sb.AppendLine($"- **Kandidat Berukuran Sama**: {TotalCandidateFiles:N0} berkas");
        sb.AppendLine($"- **Kelompok Duplikat Ditemukan**: {TotalDuplicateGroups:N0} kelompok");
        sb.AppendLine($"- **Total Berkas Duplikat**: {TotalDuplicateFiles:N0} berkas");
        sb.AppendLine($"- **Estimasi Ruang Boros (Potensi Penghematan)**: {FormattedTotalWaste}");
        sb.AppendLine($"- **Durasi Pencarian**: {FormattedDuration}");
        sb.AppendLine($"- **Data Ter-hash SHA-256**: {FileFormatter.FormatBytes(TotalHashedBytes)} ({TotalHashedFiles:N0} berkas)");
        sb.AppendLine($"- **Kecepatan Hash Rata-rata**: {FormattedThroughput}");
        sb.AppendLine();

        if (DuplicateGroups.Count > 0)
        {
            sb.AppendLine("## Rincian Kelompok Duplikat Identik");
            sb.AppendLine();

            int groupIndex = 1;
            foreach (var group in DuplicateGroups.OrderByDescending(g => g.WasteSizeBytes))
            {
                sb.AppendLine($"### Kelompok #{groupIndex} — Ukuran per Berkas: {group.FormattedFileSize} | Pemborosan: {group.FormattedWasteSize} | SHA-256: `{group.Sha256Hash}`");
                sb.AppendLine("| Nama Berkas | Jalur Lengkap | Terakhir Diubah |");
                sb.AppendLine("|---|---|---|");
                foreach (var file in group.Files)
                {
                    sb.AppendLine($"| {file.FileName} | `{file.FullPath}` | {file.FormattedLastModified} |");
                }
                sb.AppendLine();
                groupIndex++;
            }
        }
        else
        {
            sb.AppendLine("Tidak ditemukan berkas duplikat yang identik pada kumpulan berkas yang diperiksa.");
            sb.AppendLine();
        }

        if (Errors.Count > 0)
        {
            sb.AppendLine($"## Kendala Pembacaan & Akses ({Errors.Count:N0} berkas)");
            sb.AppendLine("| Jenis Kesalahan | Pesan | Jalur |");
            sb.AppendLine("|---|---|---|");
            foreach (var err in Errors)
            {
                sb.AppendLine($"| {err.FriendlyType} | {err.ErrorMessage.Replace("\n", " ").Replace("\r", "")} | `{err.Path}` |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
