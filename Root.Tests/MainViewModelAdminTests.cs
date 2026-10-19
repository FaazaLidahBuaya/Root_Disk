using System;
using Root.App.ViewModels;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class MainViewModelAdminTests
{
    private class FakeAdminService : IAdminService
    {
        public bool RunningAsAdmin { get; set; }
        public AdminElevationResult ResultToReturn { get; set; } = AdminElevationResult.Succeeded();
        public bool RestartCalled { get; private set; }

        public bool IsRunningAsAdmin() => RunningAsAdmin;

        public AdminElevationResult RestartAsAdmin(string? arguments = null)
        {
            RestartCalled = true;
            return ResultToReturn;
        }
    }

    private MainViewModel CreateMainViewModel(
        FakeAdminService fakeAdmin,
        Func<string, string, bool>? confirm = null,
        Action<string, string, bool>? alert = null,
        Action? shutdown = null)
    {
        var logger = new LoggerService();
        var driveService = new DriveService(logger);
        var scanner = new FileSystemScanner(logger);
        var treemap = new SquarifiedTreemapEngine();
        var fileTree = new FileTreeService();

        var scannerVm = new ScannerViewModel(scanner, driveService, logger, fakeAdmin);
        var treemapVm = new TreemapViewModel(treemap, logger, fileTree);
        var hierarchyVm = new HierarchyViewModel(fileTree, logger);
        var largeFilesVm = new LargeFilesViewModel(fileTree, logger);
        var duplicateService = new DuplicateFinderService(logger);
        var duplicateFinderVm = new DuplicateFinderViewModel(duplicateService, logger);
        var protectionService = new SystemProtectionService();
        var cleanupAnalyzer = new CleanupAnalyzerService(protectionService, logger);
        var cleanupExecutor = new CleanupExecutionService(protectionService, logger);
        var cleanupVm = new CleanupViewModel(cleanupAnalyzer, cleanupExecutor, logger, confirm, alert);
        var historyRepo = new SqliteScanHistoryRepository(Path.Combine(Path.GetTempPath(), "test_main_" + Guid.NewGuid().ToString("N") + ".db"));
        var dashboardVm = new DashboardViewModel(scanner, driveService, historyRepo, logger, fakeAdmin);
        var logVm = new ActivityLogViewModel(logger);
        var aboutVm = new AboutViewModel(fakeAdmin);

        return new MainViewModel(
            dashboardVm,
            scannerVm,
            treemapVm,
            hierarchyVm,
            largeFilesVm,
            duplicateFinderVm,
            cleanupVm,
            logVm,
            aboutVm,
            fakeAdmin,
            logger,
            confirm,
            alert,
            shutdown);
    }

    [Fact]
    public void MainViewModel_ExposesAdministratorStatusAndRoleText_WhenAdmin()
    {
        var fakeAdmin = new FakeAdminService { RunningAsAdmin = true };
        var vm = CreateMainViewModel(fakeAdmin);

        Assert.True(vm.IsRunningAsAdmin);
        Assert.False(vm.CanElevate);
        Assert.Equal("Administrator", vm.AdminRoleText);
    }

    [Fact]
    public void MainViewModel_ExposesAdministratorStatusAndRoleText_WhenStandardUser()
    {
        var fakeAdmin = new FakeAdminService { RunningAsAdmin = false };
        var vm = CreateMainViewModel(fakeAdmin);

        Assert.False(vm.IsRunningAsAdmin);
        Assert.True(vm.CanElevate);
        Assert.Equal("Pengguna Standar", vm.AdminRoleText);
    }

    [Fact]
    public void ExecuteRestartAsAdmin_WhenUserRejectsConfirmation_DoesNotCallRestart()
    {
        var fakeAdmin = new FakeAdminService();
        var vm = CreateMainViewModel(fakeAdmin, confirm: (msg, title) => false);

        vm.ExecuteRestartAsAdmin();

        Assert.False(fakeAdmin.RestartCalled);
    }

    [Fact]
    public void ExecuteRestartAsAdmin_WhenElevationSucceeds_CallsShutdown()
    {
        bool shutdownCalled = false;
        var fakeAdmin = new FakeAdminService { ResultToReturn = AdminElevationResult.Succeeded() };
        var vm = CreateMainViewModel(
            fakeAdmin, 
            confirm: (msg, title) => true, 
            shutdown: () => shutdownCalled = true);

        vm.ExecuteRestartAsAdmin();

        Assert.True(fakeAdmin.RestartCalled);
        Assert.True(shutdownCalled);
    }

    [Fact]
    public void ExecuteRestartAsAdmin_WhenUserCancelsUac_AlertsCancellationWithoutShutdown()
    {
        bool shutdownCalled = false;
        string? alertMsg = null;
        bool isAlertError = true;

        var fakeAdmin = new FakeAdminService { ResultToReturn = AdminElevationResult.Cancelled() };
        var vm = CreateMainViewModel(
            fakeAdmin,
            confirm: (msg, title) => true,
            alert: (msg, title, isErr) =>
            {
                alertMsg = msg;
                isAlertError = isErr;
            },
            shutdown: () => shutdownCalled = true);

        vm.ExecuteRestartAsAdmin();

        Assert.True(fakeAdmin.RestartCalled);
        Assert.False(shutdownCalled);
        Assert.False(isAlertError);
        Assert.NotNull(alertMsg);
        Assert.Contains("dibatalkan oleh pengguna", alertMsg);
    }

    [Fact]
    public void ExecuteRestartAsAdmin_WhenElevationFails_AlertsFailureWithoutShutdown()
    {
        bool shutdownCalled = false;
        string? alertMsg = null;
        bool isAlertError = false;

        var fakeAdmin = new FakeAdminService { ResultToReturn = AdminElevationResult.Failed("Akses ditolak pada biner aplikasi.", 5) };
        var vm = CreateMainViewModel(
            fakeAdmin,
            confirm: (msg, title) => true,
            alert: (msg, title, isErr) =>
            {
                alertMsg = msg;
                isAlertError = isErr;
            },
            shutdown: () => shutdownCalled = true);

        vm.ExecuteRestartAsAdmin();

        Assert.True(fakeAdmin.RestartCalled);
        Assert.False(shutdownCalled);
        Assert.True(isAlertError);
        Assert.NotNull(alertMsg);
        Assert.Contains("Gagal meluncurkan aplikasi", alertMsg);
    }
}
