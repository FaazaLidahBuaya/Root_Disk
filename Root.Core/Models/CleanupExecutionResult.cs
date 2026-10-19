using System;
using System.Collections.Generic;
using System.Text;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Laporan hasil audit dan eksekusi tindakan pembersihan berkas.
/// </summary>
public class CleanupExecutionResult
{
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration => CompletedAt - StartedAt;

    public int TotalCandidatesEvaluated { get; set; }
    public int TotalSelectedCount { get; set; }
    public int TotalDeletedCount { get; set; }
    public long TotalFreedSizeBytes { get; set; }
    public string FormattedTotalFreedSpace => FileFormatter.FormatBytes(TotalFreedSizeBytes);

    public int SkippedModifiedCount { get; set; }
    public int SkippedMissingCount { get; set; }
    public int BlockedProtectedCount { get; set; }
    public int FailedPermissionCount { get; set; }
    public int FailedLockedCount { get; set; }
    public int FailedOtherCount { get; set; }

    public bool IsCancelled { get; set; }

    public List<CleanupItemResult> ItemResults { get; } = [];

    /// <summary>
    /// Menghasilkan ringkasan laporan audit eksekusi pembersihan dalam format Markdown berbahasa Indonesia.
    /// </summary>
    public string GenerateAuditReportMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Laporan Audit Pembersihan Sistem Berkas ROOT (Fase 6)");
        sb.AppendLine($"Waktu Eksekusi : {CompletedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Durasi         : {FileFormatter.FormatDuration(Duration)}");
        sb.AppendLine($"Status Akhir   : {(IsCancelled ? "Dibatalkan oleh Pengguna" : "Selesai")}");
        sb.AppendLine();
        sb.AppendLine("## Ringkasan Eksekusi");
        sb.AppendLine($"- **Kandidat Terpilih**: {TotalSelectedCount:N0} berkas");
        sb.AppendLine($"- **Berhasil Dihapus**: {TotalDeletedCount:N0} berkas");
        sb.AppendLine($"- **Ruang Dibebaskan**: {FormattedTotalFreedSpace} ({TotalFreedSizeBytes:N0} bita)");
        sb.AppendLine();
        sb.AppendLine("## Audit Keamanan & Pencegahan Risiko");
        sb.AppendLine($"- **Dilewati karena Berkas Dimodifikasi**: {SkippedModifiedCount:N0} berkas (Integritas data terlindungi)");
        sb.AppendLine($"- **Dilewati karena Berkas Hilang**: {SkippedMissingCount:N0} berkas");
        sb.AppendLine($"- **Ditolak oleh Proteksi Direktori Sistem**: {BlockedProtectedCount:N0} berkas (Keamanan OS terjamin)");
        sb.AppendLine($"- **Gagal Izin Akses (Unauthorized)**: {FailedPermissionCount:N0} berkas");
        sb.AppendLine($"- **Gagal Berkas Terkunci (File In Use)**: {FailedLockedCount:N0} berkas");
        sb.AppendLine($"- **Kegagalan Lainnya**: {FailedOtherCount:N0} berkas");
        sb.AppendLine();

        if (ItemResults.Count > 0)
        {
            sb.AppendLine("## Rincian Berkas");
            sb.AppendLine("| Lokasi Lengkap | Kategori | Ukuran | Status | Catatan |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var item in ItemResults)
            {
                sb.AppendLine($"| `{item.FullPath}` | {item.Category} | {FileFormatter.FormatBytes(item.FileSizeBytes)} | {item.StatusDescription} | {item.ErrorMessage ?? "-"} |");
            }
        }

        return sb.ToString();
    }
}
