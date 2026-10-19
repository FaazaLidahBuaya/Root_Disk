using System;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Layanan analisis kandidat pembersihan sistem berkas yang berorientasi konservatif (safety-first).
/// </summary>
public interface ICleanupAnalyzerService
{
    /// <summary>
    /// Menganalisis pohon sistem berkas hasil pemindaian sebelumnya.
    /// </summary>
    Task<CleanupPreviewModel> AnalyzeFromScanTreeAsync(
        FileSystemItemModel rootItem,
        CleanupOptions? options = null,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Menganalisis direktori secara langsung dari penyimpanan disk.
    /// </summary>
    Task<CleanupPreviewModel> AnalyzeDirectoryAsync(
        string directoryPath,
        CleanupOptions? options = null,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}
