using System.Collections.Generic;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Antarmuka layanan pendeteksi dan informasi drive penyimpanan.
/// </summary>
public interface IDriveService
{
    IReadOnlyList<DriveInfoModel> GetAvailableDrives();
    DriveInfoModel? GetDriveForPath(string path);
}
