using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;
using Root.Core.Utilities;

namespace Root.App.ViewModels;

public class ScannerViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private readonly IDriveService _driveService;
    private readonly ILoggerService _logger;

    private CancellationTokenSource? _scanCts;
    private DriveInfoModel? _selectedDrive;
    private string _customPath = string.Empty;
    private bool _isCustomPathSelected;
    private bool _isScanning;
    private bool _hasResult;
    private string _statusText = "Siap melakukan pemindaian.";
    private string _currentScannedPath = "-";
    private long _scannedFiles;
    private long _scannedDirectories;
    private long _totalBytes;
    private string _formattedTotalBytes = "0 B";
    private string _elapsedTime = "00:00";
    private string _scanRate = "-";
    private int _skippedCount;
    private ScanResultReport? _scanResult;
    private int _selectedTabIndex;

    private readonly IAdminService? _adminService;

    public event Action<FileSystemItemModel?>? ScanCompleted;
    public event Action? RequestNavigateToTreemap;

    public ScannerViewModel(
        IFileSystemScanner scanner, 
        IDriveService driveService, 
        ILoggerService logger,
        IAdminService? adminService = null)
    {
        _scanner = scanner;
        _driveService = driveService;
        _logger = logger;
        _adminService = adminService;

        Drives = [];

        MulaiPemindaianCommand = new AsyncRelayCommand(MulaiPemindaianAsync, () => !IsScanning);
        BatalPemindaianCommand = new RelayCommand(BatalPemindaian, () => IsScanning);
        SegarkanDrivesCommand = new RelayCommand(SegarkanDrives, () => !IsScanning);
        PilihFolderCommand = new RelayCommand(PilihFolder, () => !IsScanning);
        SalinLaporanCommand = new RelayCommand(SalinLaporan, () => HasResult && ScanResult != null);
        BukaTreemapCommand = new RelayCommand(() => RequestNavigateToTreemap?.Invoke(), () => HasResult && ScanResult?.RootItem != null);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);

        SegarkanDrives();
    }

    public ObservableCollection<DriveInfoModel> Drives { get; }

    public DriveInfoModel? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                if (value != null)
                {
                    IsCustomPathSelected = false;
                }
                OnPropertyChanged(nameof(EffectiveTargetPath));
            }
        }
    }

    public string CustomPath
    {
        get => _customPath;
        set
        {
            if (SetProperty(ref _customPath, value))
            {
                OnPropertyChanged(nameof(EffectiveTargetPath));
            }
        }
    }

    public bool IsCustomPathSelected
    {
        get => _isCustomPathSelected;
        set
        {
            if (SetProperty(ref _isCustomPathSelected, value))
            {
                OnPropertyChanged(nameof(EffectiveTargetPath));
            }
        }
    }

    public string EffectiveTargetPath
    {
        get
        {
            if (IsCustomPathSelected && !string.IsNullOrWhiteSpace(CustomPath))
                return CustomPath.Trim();

            return SelectedDrive?.Name ?? string.Empty;
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanStartScan));
                MulaiPemindaianCommand.RaiseCanExecuteChanged();
                BatalPemindaianCommand.RaiseCanExecuteChanged();
                SegarkanDrivesCommand.RaiseCanExecuteChanged();
                PilihFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanStartScan => !IsScanning && !string.IsNullOrWhiteSpace(EffectiveTargetPath);

    public bool HasResult
    {
        get => _hasResult;
        set
        {
            if (SetProperty(ref _hasResult, value))
            {
                SalinLaporanCommand.RaiseCanExecuteChanged();
                BukaTreemapCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentScannedPath
    {
        get => _currentScannedPath;
        set => SetProperty(ref _currentScannedPath, value);
    }

    public long ScannedFiles
    {
        get => _scannedFiles;
        set => SetProperty(ref _scannedFiles, value);
    }

    public long ScannedDirectories
    {
        get => _scannedDirectories;
        set => SetProperty(ref _scannedDirectories, value);
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set => SetProperty(ref _totalBytes, value);
    }

    public string FormattedTotalBytes
    {
        get => _formattedTotalBytes;
        set => SetProperty(ref _formattedTotalBytes, value);
    }

    public string ElapsedTime
    {
        get => _elapsedTime;
        set => SetProperty(ref _elapsedTime, value);
    }

    public string ScanRate
    {
        get => _scanRate;
        set => SetProperty(ref _scanRate, value);
    }

    public int SkippedCount
    {
        get => _skippedCount;
        set => SetProperty(ref _skippedCount, value);
    }

    public ScanResultReport? ScanResult
    {
        get => _scanResult;
        set
        {
            if (SetProperty(ref _scanResult, value))
            {
                HasResult = value != null;
                OnPropertyChanged(nameof(ShowAdminBanner));
            }
        }
    }

    public bool IsRunningAsAdmin => _adminService?.IsRunningAsAdmin() ?? false;
    public bool CanElevate => !IsRunningAsAdmin;
    public bool ShowAdminBanner => !IsRunningAsAdmin && HasResult && (ScanResult?.SkippedCount ?? 0) > 0;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public AsyncRelayCommand MulaiPemindaianCommand { get; }
    public RelayCommand BatalPemindaianCommand { get; }
    public RelayCommand SegarkanDrivesCommand { get; }
    public RelayCommand PilihFolderCommand { get; }
    public RelayCommand SalinLaporanCommand { get; }
    public RelayCommand BukaTreemapCommand { get; }
    public RelayCommand RestartAsAdminCommand { get; }

    public void SegarkanDrives()
    {
        Drives.Clear();
        var drives = _driveService.GetAvailableDrives();
        foreach (var d in drives)
        {
            Drives.Add(d);
        }

        if (SelectedDrive == null || !Drives.Any(d => d.Name == SelectedDrive.Name))
        {
            SelectedDrive = Drives.FirstOrDefault(d => d.IsReady);
        }

        _logger.LogInfo($"Daftar drive diperbarui: {Drives.Count} drive terdeteksi.");
    }

    private void PilihFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder untuk Dipindai",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            CustomPath = dialog.FolderName;
            IsCustomPathSelected = true;
            _logger.LogInfo($"Folder target dipilih: '{CustomPath}'");
        }
    }

    private async Task MulaiPemindaianAsync()
    {
        string targetPath = EffectiveTargetPath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            StatusText = "Peringatan: Tentukan drive atau folder target terlebih dahulu.";
            _logger.LogWarning("Percobaan pemindaian dibatalkan: Jalur target belum ditentukan.");
            return;
        }

        if (!Directory.Exists(targetPath))
        {
            StatusText = $"Kesalahan: Jalur '{targetPath}' tidak ditemukan atau tidak dapat diakses.";
            _logger.LogError($"Direktori target tidak valid: '{targetPath}'");
            return;
        }

        // Reset metrik
        IsScanning = true;
        StatusText = $"Sedang memindai: {targetPath}...";
        CurrentScannedPath = targetPath;
        ScannedFiles = 0;
        ScannedDirectories = 0;
        TotalBytes = 0;
        FormattedTotalBytes = "0 B";
        ElapsedTime = "00:00";
        ScanRate = "-";
        SkippedCount = 0;
        ScanResult = null;

        _scanCts = new CancellationTokenSource();

        var progress = new Progress<ScanProgressReport>(report =>
        {
            CurrentScannedPath = report.CurrentPath;
            ScannedFiles = report.ScannedFilesCount;
            ScannedDirectories = report.ScannedDirectoriesCount;
            TotalBytes = report.TotalBytesScanned;
            FormattedTotalBytes = report.FormattedTotalBytes;
            ElapsedTime = report.FormattedElapsedTime;
            ScanRate = report.FormattedRate;
            SkippedCount = report.SkippedCount;
        });

        try
        {
            var result = await _scanner.ScanAsync(targetPath, progress, _scanCts.Token);
            ScanResult = result;

            if (result.RootItem != null)
            {
                ScanCompleted?.Invoke(result.RootItem);
            }

            // Perbarui ringkasan akhir
            ScannedFiles = result.TotalFilesCount;
            ScannedDirectories = result.TotalDirectoriesCount;
            TotalBytes = result.TotalSizeBytes;
            FormattedTotalBytes = result.FormattedTotalSize;
            ElapsedTime = result.FormattedDuration;
            SkippedCount = result.SkippedCount;
            StatusText = result.StatusDescription;
            CurrentScannedPath = result.IsPartial ? "Pemindaian dihentikan (Laporan parsial)" : "Pemindaian selesai lengkap";
        }
        catch (Exception ex)
        {
            StatusText = $"Kesalahan saat pemindaian: {ex.Message}";
            _logger.LogError($"Kesalahan tidak tertangani pada pemindai: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void BatalPemindaian()
    {
        if (_scanCts != null && !_scanCts.IsCancellationRequested)
        {
            StatusText = "Membatalkan pemindaian, mengumpulkan laporan parsial...";
            _logger.LogWarning("Pengguna menekan tombol pembatalan pemindaian.");
            _scanCts.Cancel();
        }
    }

    private void SalinLaporan()
    {
        if (ScanResult == null) return;

        try
        {
            var markdown = ScanResult.GenerateSummaryMarkdown();
            Clipboard.SetText(markdown);
            _logger.LogSuccess("Ringkasan laporan pemindaian berhasil disalin ke clipboard.");
            MessageBox.Show("Laporan pemindaian berhasil disalin ke clipboard!", "ROOT - Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal menyalin laporan ke clipboard: {ex.Message}");
        }
    }

    private void RestartAsAdmin()
    {
        if (_adminService == null) return;

        var confirm = MessageBox.Show(
            "Apakah Anda ingin memulai ulang ROOT dengan hak Administrator melalui UAC Windows?\n\n" +
            "Hak Administrator memungkinkan pemindaian berkas dan direktori yang dilindungi oleh sistem tanpa mengubah izin berkas.",
            "Mulai Ulang sebagai Administrator - ROOT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var result = _adminService.RestartAsAdmin();
        if (result.Success)
        {
            Application.Current?.Shutdown();
        }
        else if (result.CancelledByUser)
        {
            MessageBox.Show(
                "Permintaan elevasi UAC dibatalkan oleh pengguna. ROOT tetap berjalan dengan hak Pengguna Standar.",
                "Elevasi UAC Dibatalkan",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                $"Gagal meluncurkan aplikasi dengan hak Administrator: {result.ErrorMessage}",
                "Kesalahan Elevasi",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
