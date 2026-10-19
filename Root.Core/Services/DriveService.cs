using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Implementasi layanan pendeteksi drive fisik dan logis pada sistem operasi Windows.
/// </summary>
public class DriveService : IDriveService
{
    private readonly ILoggerService? _logger;

    public DriveService(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public IReadOnlyList<DriveInfoModel> GetAvailableDrives()
    {
        var result = new List<DriveInfoModel>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var d in drives)
            {
                try
                {
                    bool isReady = d.IsReady;
                    var model = new DriveInfoModel
                    {
                        Name = d.Name,
                        DriveType = d.DriveType.ToString(),
                        IsReady = isReady
                    };

                    if (isReady)
                    {
                        model.VolumeLabel = d.VolumeLabel;
                        model.DriveFormat = d.DriveFormat;
                        model.TotalSizeBytes = d.TotalSize;
                        model.FreeSpaceBytes = d.AvailableFreeSpace;
                    }

                    result.Add(model);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Gagal membaca rincian drive {d.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Gagal mendata drive sistem: {ex.Message}");
        }

        return result;
    }

    public DriveInfoModel? GetDriveForPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
                return null;

            var drives = GetAvailableDrives();
            return drives.FirstOrDefault(d => string.Equals(d.Name.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Gagal menentukan drive untuk jalur '{path}': {ex.Message}");
            return null;
        }
    }
}
