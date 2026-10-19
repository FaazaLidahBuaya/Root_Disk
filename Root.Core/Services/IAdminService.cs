using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Layanan untuk memeriksa status hak akses administrator dan melakukan elevasi melalui Windows UAC.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Memeriksa apakah aplikasi sedang berjalan dengan hak administrator (elevated process).
    /// </summary>
    bool IsRunningAsAdmin();

    /// <summary>
    /// Meminta Windows untuk meluncurkan instance baru aplikasi dengan hak administrator melalui dialog UAC.
    /// </summary>
    /// <param name="arguments">Argumen baris perintah opsional untuk instance baru.</param>
    /// <returns>Hasil elevasi termasuk status sukses, pembatalan oleh pengguna, atau kesalahan.</returns>
    AdminElevationResult RestartAsAdmin(string? arguments = null);
}
