using System.Collections.Generic;
using System.Linq;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model pratinjau sebelum tindakan pembersihan dilakukan.
/// Menampilkan ringkasan dampak dan rincian berkas yang akan terpengaruh.
/// </summary>
public class CleanupPreviewModel
{
    public CleanupPreviewModel(IReadOnlyList<CleanupCandidateModel> candidates)
    {
        Candidates = candidates;
    }

    public IReadOnlyList<CleanupCandidateModel> Candidates { get; }

    public int TotalCandidates => Candidates.Count;
    public long TotalSizeBytes => Candidates.Sum(c => c.FileSizeBytes);
    public string FormattedTotalSize => FileFormatter.FormatBytes(TotalSizeBytes);

    public IEnumerable<CleanupCandidateModel> SelectedCandidates => Candidates.Where(c => c.IsSelected && !c.IsProtected);
    public int SelectedCount => SelectedCandidates.Count();
    public long SelectedFreedSizeBytes => SelectedCandidates.Sum(c => c.FileSizeBytes);
    public string FormattedSelectedFreedSpace => FileFormatter.FormatBytes(SelectedFreedSizeBytes);

    public int SafeCount => Candidates.Count(c => c.SafetyLevel == CleanupSafetyLevel.AmanKonservatif);
    public int NeedsCautionCount => Candidates.Count(c => c.SafetyLevel == CleanupSafetyLevel.PerluPerhatian);
    public int ProtectedCount => Candidates.Count(c => c.IsProtected);

    /// <summary>
    /// Menghasilkan teks konfirmasi eksplisit sebelum tindakan pembersihan.
    /// </summary>
    public string GenerateConfirmationMessage()
    {
        return $"Apakah Anda yakin ingin membersihkan {SelectedCount:N0} berkas terpilih?\n\n" +
               $"Perkiraan ruang yang akan dibebaskan: {FormattedSelectedFreedSpace}\n\n" +
               "Peringatan Keamanan:\n" +
               "• Tindakan ini akan menghapus berkas terpilih secara permanen.\n" +
               "• Seluruh berkas akan diperiksa ulang (re-validated) sesaat sebelum penghapusan.\n" +
               "• Berkas yang berubah atau berada di area sistem akan otomatis dilindungi dan dilewati.\n\n" +
               "Klik 'Ya' untuk melanjutkan pembersihan, atau 'Tidak' untuk membatalkan.";
    }
}
