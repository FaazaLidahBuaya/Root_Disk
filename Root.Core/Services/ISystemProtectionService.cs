namespace Root.Core.Services;

/// <summary>
/// Layanan proteksi direktori dan berkas sistem operasi Windows.
/// Memastikan ROOT tidak pernah menandai atau menghapus berkas sistem kritis.
/// </summary>
public interface ISystemProtectionService
{
    /// <summary>
    /// Memeriksa apakah suatu jalur direktori atau berkas merupakan bagian dari sistem terproteksi.
    /// </summary>
    bool IsProtectedPath(string path);

    /// <summary>
    /// Memeriksa apakah ekstensi berkas merupakan komponen sistem eksekutabel atau driver yang dilarang disentuh.
    /// </summary>
    bool IsProtectedExtension(string path);

    /// <summary>
    /// Mendapatkan alasan proteksi dalam bahasa Indonesia jika jalur terproteksi, atau null jika aman.
    /// </summary>
    string? GetProtectionReason(string path);
}
