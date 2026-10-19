using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Layanan eksekusi pembersihan sistem berkas dengan pemeriksaan ulang target ketat (pre-validation)
/// dan perlindungan direktori sistem.
/// </summary>
public interface ICleanupExecutionService
{
    /// <summary>
    /// Mengeksekusi pembersihan pada kandidat terpilih setelah melalui pemeriksaan ulang target.
    /// </summary>
    Task<CleanupExecutionResult> ExecuteCleanupAsync(
        IEnumerable<CleanupCandidateModel> selectedCandidates,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}
