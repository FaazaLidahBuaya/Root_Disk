using System;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Antarmuka pemindai sistem berkas rekursif asinkron.
/// </summary>
public interface IFileSystemScanner
{
    /// <summary>
    /// Menjalankan pemindaian rekursif pada direktori target dengan pelaporan kemajuan dan dukungan pembatalan.
    /// </summary>
    Task<ScanResultReport> ScanAsync(string rootPath, IProgress<ScanProgressReport>? progress, CancellationToken cancellationToken);
}
