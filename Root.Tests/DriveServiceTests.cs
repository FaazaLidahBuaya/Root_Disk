using System.IO;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class DriveServiceTests
{
    [Fact]
    public void GetAvailableDrives_ReturnsSystemDrives()
    {
        var logger = new LoggerService();
        var driveService = new DriveService(logger);

        var drives = driveService.GetAvailableDrives();

        Assert.NotEmpty(drives);
        // Pastikan setidaknya satu drive sistem siap (biasanya C:\)
        var systemDrive = Path.GetPathRoot(Path.GetTempPath());
        Assert.NotNull(systemDrive);
        var matched = driveService.GetDriveForPath(systemDrive);
        Assert.NotNull(matched);
        Assert.True(matched?.TotalSizeBytes > 0);
    }

    [Fact]
    public void GetDriveForPath_InvalidOrEmptyPath_ReturnsNull()
    {
        var driveService = new DriveService();
        var result = driveService.GetDriveForPath("");
        Assert.Null(result);
    }
}
