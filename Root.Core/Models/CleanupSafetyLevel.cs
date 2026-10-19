namespace Root.Core.Models;

/// <summary>
/// Tingkat keamanan / resiko kandidat pembersihan.
/// </summary>
public enum CleanupSafetyLevel
{
    /// <summary>
    /// Sangat aman dibersihkan secara konservatif (misalnya berkas temporary lama atau crash dump kadaluarsa).
    /// </summary>
    AmanKonservatif,

    /// <summary>
    /// Memerlukan perhatian pengguna sebelum dihapus (misalnya berkas log aplikasi lama atau unduhan terhenti).
    /// </summary>
    PerluPerhatian,

    /// <summary>
    /// Terproteksi sistem dan tidak boleh dihapus dalam kondisi apa pun.
    /// </summary>
    TerproteksiSistem
}
