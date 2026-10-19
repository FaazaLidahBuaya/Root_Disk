using System.Collections.Generic;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Antarmuka layanan manipulasi, pengurutan, dan penyaringan hierarki sistem berkas dalam memori.
/// </summary>
public interface IFileTreeService
{
    /// <summary>
    /// Mengurutkan kumpulan item berdasarkan ukuran, nama, atau tanggal perubahan.
    /// </summary>
    IReadOnlyList<FileSystemItemModel> Sort(IEnumerable<FileSystemItemModel> items, SortField field, SortDirection direction, bool directoriesFirst = true);

    /// <summary>
    /// Menyaring hierarki pohon berkas secara rekursif berdasarkan ekstensi, ukuran minimum, dan teks pencarian tanpa pemindaian ulang disk.
    /// </summary>
    FileSystemItemModel? FilterHierarchy(FileSystemItemModel root, FileFilterCriteria criteria);

    /// <summary>
    /// Mengurutkan seluruh tingkat hierarki pohon berkas secara rekursif dalam memori.
    /// </summary>
    FileSystemItemModel SortHierarchy(FileSystemItemModel root, SortField field, SortDirection direction, bool directoriesFirst = true);

    /// <summary>
    /// Mengambil seluruh berkas dalam pohon secara mendatar (flat list), disaring dan diurutkan untuk eksplorasi berkas berukuran besar.
    /// </summary>
    IReadOnlyList<FileSystemItemModel> GetFlattenedFiles(FileSystemItemModel root, FileFilterCriteria? criteria, SortField sortField = SortField.Ukuran, SortDirection sortDirection = SortDirection.Menurun, int maxResults = 0);
}
