using System.Collections.Generic;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Antarmuka repositori riwayat pemindaian berbasis SQLite.
/// </summary>
public interface IScanHistoryRepository
{
    /// <summary>
    /// Menginisialisasi skema tabel SQLite jika belum ada.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Menyimpan catatan hasil pemindaian baru ke basis data SQLite.
    /// </summary>
    Task<ScanHistoryRecordModel> SaveRecordAsync(ScanHistoryRecordModel record);

    /// <summary>
    /// Mengambil daftar riwayat pemindaian terbaru.
    /// </summary>
    Task<List<ScanHistoryRecordModel>> GetHistoryAsync(int limit = 50);

    /// <summary>
    /// Mengambil catatan pemindaian sebelumnya pada target yang sama untuk keperluan komparasi delta.
    /// </summary>
    Task<ScanHistoryRecordModel?> GetPreviousScanAsync(string targetPath, long currentRecordId = 0);

    /// <summary>
    /// Menghapus satu catatan riwayat pemindaian berdasarkan ID.
    /// </summary>
    Task DeleteRecordAsync(long id);

    /// <summary>
    /// Membersihkan seluruh catatan riwayat pemindaian.
    /// </summary>
    Task ClearHistoryAsync();
}
