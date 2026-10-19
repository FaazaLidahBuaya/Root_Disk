namespace Root.Core.Models;

/// <summary>
/// Status pemindaian sistem berkas.
/// </summary>
public enum ScanStatus
{
    Siap,
    Memindai,
    Selesai,
    Dibatalkan,
    SelesaiSebagian,
    Gagal
}
