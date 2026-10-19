using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.App.ViewModels;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class ScannerViewModelPhase4Tests
{
    private class FakeAdminService(bool isAdmin) : IAdminService
    {
        public bool RestartCalled { get; private set; }
        public bool IsRunningAsAdmin() => isAdmin;
        public AdminElevationResult RestartAsAdmin(string? arguments = null)
        {
            RestartCalled = true;
            return AdminElevationResult.Succeeded();
        }
    }

    private class FakeScanner(ScanResultReport reportToReturn) : IFileSystemScanner
    {
        public Task<ScanResultReport> ScanAsync(string rootPath, System.IProgress<ScanProgressReport>? progress, CancellationToken cancellationToken)
        {
            return Task.FromResult(reportToReturn);
        }
    }

    [Fact]
    public void ScannerViewModel_AdminProperties_ReflectAdminServiceState()
    {
        var logger = new LoggerService();
        var driveService = new DriveService(logger);
        var scanner = new FileSystemScanner(logger);

        var adminVm = new ScannerViewModel(scanner, driveService, logger, new FakeAdminService(true));
        Assert.True(adminVm.IsRunningAsAdmin);
        Assert.False(adminVm.CanElevate);

        var userVm = new ScannerViewModel(scanner, driveService, logger, new FakeAdminService(false));
        Assert.False(userVm.IsRunningAsAdmin);
        Assert.True(userVm.CanElevate);
    }

    [Fact]
    public void ScannerViewModel_ShowAdminBanner_TrueOnlyWhenStandardUserHasSkippedItems()
    {
        var logger = new LoggerService();
        var driveService = new DriveService(logger);
        var scanner = new FileSystemScanner(logger);

        var userVm = new ScannerViewModel(scanner, driveService, logger, new FakeAdminService(false));

        // Belum ada hasil
        Assert.False(userVm.ShowAdminBanner);

        // Hasil sukses tanpa berkas dilewati
        userVm.ScanResult = new ScanResultReport
        {
            Status = ScanStatus.Selesai,
            SkippedCount = 0
        };
        Assert.False(userVm.ShowAdminBanner);

        // Hasil parsial dengan berkas dilewati
        userVm.ScanResult = new ScanResultReport
        {
            Status = ScanStatus.SelesaiSebagian,
            SkippedCount = 4
        };
        Assert.True(userVm.ShowAdminBanner);

        // Jika berjalan sebagai admin, banner tidak perlu ditampilkan
        var adminVm = new ScannerViewModel(scanner, driveService, logger, new FakeAdminService(true));
        adminVm.ScanResult = new ScanResultReport
        {
            Status = ScanStatus.SelesaiSebagian,
            SkippedCount = 4
        };
        Assert.False(adminVm.ShowAdminBanner);
    }
}
