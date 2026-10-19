using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Kontrak layanan pencarian berkas duplikat berbasis perbandingan ukuran dan verifikasi hash SHA-256.
/// </summary>
public interface IDuplicateFinderService
{
    /// <summary>
    /// Mencari berkas duplikat dari kumpulan FileSystemItemModel (misal dari hasil pemindaian sebelumnya).
    /// </summary>
    Task<DuplicateSearchResult> FindDuplicatesAsync(
        IEnumerable<FileSystemItemModel> fileItems,
        DuplicateSearchOptions? options = null,
        IProgress<DuplicateProgressReport>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mencari berkas duplikat langsung dari direktori pada sistem berkas.
    /// </summary>
    Task<DuplicateSearchResult> FindDuplicatesInDirectoryAsync(
        string directoryPath,
        DuplicateSearchOptions? options = null,
        IProgress<DuplicateProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}
