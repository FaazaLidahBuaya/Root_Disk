using System;
using System.Windows;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;

namespace Root.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAdminService _adminService;
    private readonly ILoggerService? _logger;
    private readonly Func<string, string, bool>? _confirmAction;
    private readonly Action<string, string, bool>? _alertAction;
    private readonly Action? _shutdownAction;

    private ViewModelBase _currentView;
    private string _currentTitle = "Dasbor Analisis Penyimpanan";

    public string AppVersionText => "Versi 8.0 - Windows x64";

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        ScannerViewModel scannerViewModel,
        TreemapViewModel treemapViewModel,
        HierarchyViewModel hierarchyViewModel,
        LargeFilesViewModel largeFilesViewModel,
        DuplicateFinderViewModel duplicateFinderViewModel,
        CleanupViewModel cleanupViewModel,
        ActivityLogViewModel activityLogViewModel,
        AboutViewModel aboutViewModel,
        IAdminService? adminService = null,
        ILoggerService? logger = null,
        Func<string, string, bool>? confirmAction = null,
        Action<string, string, bool>? alertAction = null,
        Action? shutdownAction = null)
    {
        Dashboard = dashboardViewModel;
        Scanner = scannerViewModel;
        Treemap = treemapViewModel;
        Hierarchy = hierarchyViewModel;
        LargeFiles = largeFilesViewModel;
        DuplicateFinder = duplicateFinderViewModel;
        Cleanup = cleanupViewModel;
        ActivityLog = activityLogViewModel;
        About = aboutViewModel;
        _adminService = adminService ?? new AdminService(logger);
        _logger = logger;
        _confirmAction = confirmAction;
        _alertAction = alertAction;
        _shutdownAction = shutdownAction;

        _currentView = Dashboard;

        NavigateToDashboardCommand = new RelayCommand(() =>
        {
            CurrentView = Dashboard;
            CurrentTitle = "Dasbor Analisis Penyimpanan";
        });

        NavigateToScannerCommand = new RelayCommand(() =>
        {
            CurrentView = Scanner;
            CurrentTitle = "Pemindai Berkas & Drive";
        });

        NavigateToTreemapCommand = new RelayCommand(() =>
        {
            CurrentView = Treemap;
            CurrentTitle = "Treemap Interaktif Penyimpanan";
        });

        NavigateToHierarchyCommand = new RelayCommand(() =>
        {
            CurrentView = Hierarchy;
            CurrentTitle = "Tampilan Daftar Hierarkis";
        });

        NavigateToLargeFilesCommand = new RelayCommand(() =>
        {
            CurrentView = LargeFiles;
            CurrentTitle = "Eksplorasi Berkas Berukuran Besar";
        });

        NavigateToDuplicateFinderCommand = new RelayCommand(() =>
        {
            CurrentView = DuplicateFinder;
            CurrentTitle = "Pencari Berkas Duplikat (SHA-256)";
        });

        NavigateToCleanupCommand = new RelayCommand(() =>
        {
            CurrentView = Cleanup;
            CurrentTitle = "Analisis Kandidat Pembersihan Konservatif";
        });

        NavigateToLogsCommand = new RelayCommand(() =>
        {
            CurrentView = ActivityLog;
            CurrentTitle = "Catatan Aktivitas Sistem";
        });

        NavigateToAboutCommand = new RelayCommand(() =>
        {
            CurrentView = About;
            CurrentTitle = "Tentang ROOT";
        });

        RestartAsAdminCommand = new RelayCommand(ExecuteRestartAsAdmin);

        // Hubungkan event hasil pemindaian langsung dari Scanner dan Dashboard ke modul-modul lain
        Dashboard.ScanCompleted += OnScanCompleted;
        Scanner.ScanCompleted += OnScanCompleted;

        Dashboard.RequestNavigateToTreemap += () => NavigateToTreemapCommand.Execute(null);
        Dashboard.RequestNavigateToHierarchy += () => NavigateToHierarchyCommand.Execute(null);
        Dashboard.RequestNavigateToCleanup += () => NavigateToCleanupCommand.Execute(null);

        Scanner.RequestNavigateToTreemap += () =>
        {
            NavigateToTreemapCommand.Execute(null);
        };

        Hierarchy.RequestNavigateToTreemap += targetItem =>
        {
            if (targetItem.IsDirectory)
            {
                Treemap.NavigateToFolder(targetItem);
            }
            NavigateToTreemapCommand.Execute(null);
        };

        LargeFiles.RequestNavigateToTreemap += targetFile =>
        {
            NavigateToTreemapCommand.Execute(null);
        };
    }

    private void OnScanCompleted(FileSystemItemModel? rootItem)
    {
        if (rootItem != null)
        {
            Treemap.LoadScanData(rootItem);
            Hierarchy.LoadScanData(rootItem);
            LargeFiles.LoadScanData(rootItem);
            DuplicateFinder.LoadScanData(rootItem);
            Cleanup.LoadScanData(rootItem);
        }
    }

    public DashboardViewModel Dashboard { get; }
    public ScannerViewModel Scanner { get; }
    public TreemapViewModel Treemap { get; }
    public HierarchyViewModel Hierarchy { get; }
    public LargeFilesViewModel LargeFiles { get; }
    public DuplicateFinderViewModel DuplicateFinder { get; }
    public CleanupViewModel Cleanup { get; }
    public ActivityLogViewModel ActivityLog { get; }
    public AboutViewModel About { get; }

    public bool IsRunningAsAdmin => _adminService.IsRunningAsAdmin();
    public bool CanElevate => !IsRunningAsAdmin;
    public string AdminRoleText => IsRunningAsAdmin ? "Administrator" : "Pengguna Standar";

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        set => SetProperty(ref _currentTitle, value);
    }

    public RelayCommand NavigateToDashboardCommand { get; }
    public RelayCommand NavigateToScannerCommand { get; }
    public RelayCommand NavigateToTreemapCommand { get; }
    public RelayCommand NavigateToHierarchyCommand { get; }
    public RelayCommand NavigateToLargeFilesCommand { get; }
    public RelayCommand NavigateToDuplicateFinderCommand { get; }
    public RelayCommand NavigateToCleanupCommand { get; }
    public RelayCommand NavigateToLogsCommand { get; }
    public RelayCommand NavigateToAboutCommand { get; }
    public RelayCommand RestartAsAdminCommand { get; }

    public void ExecuteRestartAsAdmin()
    {
        bool confirmed = _confirmAction != null
            ? _confirmAction(
                "Apakah Anda ingin memulai ulang ROOT dengan hak Administrator melalui UAC Windows?\n\n" +
                "Hak Administrator memungkinkan pemindaian berkas dan folder terproteksi sistem operasi tanpa memodifikasi izin berkas atau mengambil kepemilikan.",
                "Mulai Ulang sebagai Administrator - ROOT")
            : (MessageBox.Show(
                "Apakah Anda ingin memulai ulang ROOT dengan hak Administrator melalui UAC Windows?\n\n" +
                "Hak Administrator memungkinkan pemindaian berkas dan folder terproteksi sistem operasi tanpa memodifikasi izin berkas atau mengambil kepemilikan.",
                "Mulai Ulang sebagai Administrator - ROOT",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes);

        if (!confirmed)
        {
            _logger?.LogInfo("Pengguna membatalkan dialog konfirmasi elevasi administrator.");
            return;
        }

        var result = _adminService.RestartAsAdmin();
        if (result.Success)
        {
            _logger?.LogSuccess("Instans aplikasi dengan hak Administrator berhasil diluncurkan. Menutup instans saat ini...");
            if (_shutdownAction != null)
            {
                _shutdownAction();
            }
            else
            {
                Application.Current?.Shutdown();
            }
        }
        else if (result.CancelledByUser)
        {
            _logger?.LogWarning("Permintaan elevasi hak Administrator dibatalkan oleh pengguna pada dialog UAC.");
            if (_alertAction != null)
            {
                _alertAction("Permintaan elevasi UAC dibatalkan oleh pengguna. ROOT tetap berjalan dengan hak Pengguna Standar.", "Elevasi UAC Dibatalkan", false);
            }
            else
            {
                MessageBox.Show(
                    "Permintaan elevasi UAC dibatalkan oleh pengguna. ROOT tetap berjalan dengan hak Pengguna Standar.",
                    "Elevasi UAC Dibatalkan - ROOT",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        else
        {
            _logger?.LogError($"Kegagalan elevasi administrator: {result.ErrorMessage}");
            if (_alertAction != null)
            {
                _alertAction($"Gagal meluncurkan aplikasi dengan hak Administrator.\n\nDetail: {result.ErrorMessage}", "Kegagalan Elevasi", true);
            }
            else
            {
                MessageBox.Show(
                    $"Gagal meluncurkan aplikasi dengan hak Administrator.\n\nDetail: {result.ErrorMessage}",
                    "Kegagalan Elevasi - ROOT",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
