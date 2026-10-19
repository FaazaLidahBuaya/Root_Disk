using System.Windows;
using Root.App.ViewModels;
using Root.Core.Services;

namespace Root.App;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var logger = new LoggerService();
        var adminService = new AdminService(logger);
        var driveService = new DriveService(logger);
        var scanner = new FileSystemScanner(logger);
        var treemapEngine = new SquarifiedTreemapEngine();
        var fileTreeService = new FileTreeService();
        var duplicateService = new DuplicateFinderService(logger);
        var protectionService = new SystemProtectionService();
        var cleanupAnalyzerService = new CleanupAnalyzerService(protectionService, logger);
        var cleanupExecutionService = new CleanupExecutionService(protectionService, logger);
        var historyRepository = new SqliteScanHistoryRepository();

        var dashboardViewModel = new DashboardViewModel(scanner, driveService, historyRepository, logger, adminService);
        var scannerViewModel = new ScannerViewModel(scanner, driveService, logger, adminService);
        var treemapViewModel = new TreemapViewModel(treemapEngine, logger, fileTreeService);
        var hierarchyViewModel = new HierarchyViewModel(fileTreeService, logger);
        var largeFilesViewModel = new LargeFilesViewModel(fileTreeService, logger);
        var duplicateFinderViewModel = new DuplicateFinderViewModel(duplicateService, logger);
        var cleanupViewModel = new CleanupViewModel(cleanupAnalyzerService, cleanupExecutionService, logger);
        var logViewModel = new ActivityLogViewModel(logger);
        var aboutViewModel = new AboutViewModel(adminService);

        var mainViewModel = new MainViewModel(
            dashboardViewModel,
            scannerViewModel,
            treemapViewModel,
            hierarchyViewModel,
            largeFilesViewModel,
            duplicateFinderViewModel,
            cleanupViewModel,
            logViewModel,
            aboutViewModel,
            adminService,
            logger);

        bool isAdmin = adminService.IsRunningAsAdmin();
        logger.LogInfo($"Aplikasi ROOT Fase 7 dimuat (Dasbor Utama, Kapasitas Drive, Riwayat SQLite, Komparasi Historis, Treemap, Hierarki, Berkas Besar, UAC, Pencari Duplikat SHA-256, & Pembersihan Konservatif). Status Hak Akses: {(isAdmin ? "Administrator (Elevasi Penuh)" : "Pengguna Standar (Hak Terbatas)")}.");

        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };

        mainWindow.Show();
    }
}
