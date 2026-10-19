using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

public class DashboardViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private readonly IDriveService _driveService;
    private readonly IScanHistoryRepository _historyRepository;
    private readonly ILoggerService _logger;
    private readonly IAdminService? _adminService;
    private readonly Action<string, string, bool>? _alertAction;
    private readonly Func<string, string, bool>? _confirmAction;

    private CancellationTokenSource? _scanCts;
    private DashboardState _currentState = DashboardState.Kosong;
    private DriveInfoModel? _selectedDrive;
    private string _customPath = string.Empty;
    private bool _isCustomPathSelected;
    private bool _isScanning;
    private string _statusText = "Siap melakukan analisis penyimpanan.";
    private string _currentScannedPath = "-";
    private long _scannedFiles;
    private long _scannedDirectories;
    private long _totalSizeBytes;
    private string _formattedTotalSize = "0 B";
    private string _elapsedTime = "00:00";
    private string _scanRate = "-";
    private int _skippedCount;
    private ScanResultReport? _currentScanResult;
    private FileSystemItemModel? _currentRootItem;
    private ScanComparisonModel? _comparison;
    private ScanHistoryRecordModel? _selectedHistoryRecord;
    private string _errorMessage = string.Empty;

    public event Action<FileSystemItemModel?>? ScanCompleted;
    public event Action? RequestNavigateToTreemap;
    public event Action? RequestNavigateToHierarchy;
    public event Action? RequestNavigateToCleanup;

    public DashboardViewModel(
        IFileSystemScanner scanner,
        IDriveService driveService,
        IScanHistoryRepository historyRepository,
        ILoggerService logger,
        IAdminService? adminService = null,
        Action<string, string, bool>? alertAction = null,
        Func<string, string, bool>? confirmAction = null)
    {
        _scanner = scanner;
        _driveService = driveService;
        _historyRepository = historyRepository;
        _logger = logger;
        _adminService = adminService;
        _alertAction = alertAction;
        _confirmAction = confirmAction;

        Drives = [];
        ScanHistory = [];

        MulaiPemindaianCommand = new AsyncRelayCommand(MulaiPemindaianAsync, () => !IsScanning);
        BatalPemindaianCommand = new RelayCommand(BatalPemindaian, () => IsScanning);
        SegarkanDrivesCommand = new RelayCommand(SegarkanDrives, () => !IsScanning);
        PilihFolderCommand = new RelayCommand(PilihFolder, () => !IsScanning);
        BukaTreemapCommand = new RelayCommand(() => RequestNavigateToTreemap?.Invoke(), () => CurrentRootItem != null);
        BukaHierarkiCommand = new RelayCommand(() => RequestNavigateToHierarchy?.Invoke(), () => CurrentRootItem != null);
        BukaPembersihanCommand = new RelayCommand(() => RequestNavigateToCleanup?.Invoke(), () => CurrentRootItem != null);
        HapusRiwayatItemCommand = new AsyncRelayCommand(HapusRiwayatItemAsync);
        BersihkanSemuaRiwayatCommand = new AsyncRelayCommand(BersihkanSemuaRiwayatAsync);
        TerapkanRiwayatUntukKomparasiCommand = new RelayCommand(param => TerapkanRiwayatUntukKomparasi(param as ScanHistoryRecordModel));

        SegarkanDrives();
        _ = MuatRiwayatDariDbAsync();
    }

    public ObservableCollection<DriveInfoModel> Drives { get; }
    public ObservableCollection<ScanHistoryRecordModel> ScanHistory { get; }

    public DashboardState CurrentState
    {
        get => _currentState;
        set
        {
            if (SetProperty(ref _currentState, value))
            {
                OnPropertyChanged(nameof(IsStateKosong));
                OnPropertyChanged(nameof(IsStateMemuat));
                OnPropertyChanged(nameof(IsStateSelesai));
                OnPropertyChanged(nameof(IsStateParsial));
                OnPropertyChanged(nameof(IsStateGagal));
                OnPropertyChanged(nameof(HasScanResult));
            }
        }
    }

    public bool IsStateKosong => CurrentState == DashboardState.Kosong;
    public bool IsStateMemuat => CurrentState == DashboardState.Memuat;
    public bool IsStateSelesai => CurrentState == DashboardState.Selesai;
    public bool IsStateParsial => CurrentState == DashboardState.Parsial;
    public bool IsStateGagal => CurrentState == DashboardState.Gagal;
    public bool HasScanResult => CurrentState == DashboardState.Selesai || CurrentState == DashboardState.Parsial;

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
                OnPropertyChanged(nameof(TargetDisplayName));
                UpdateDriveCapacityMetrics();
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
                OnPropertyChanged(nameof(TargetDisplayName));
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
                OnPropertyChanged(nameof(TargetDisplayName));
                UpdateDriveCapacityMetrics();
            }
        }
    }

    public string EffectiveTargetPath
    {
        get
        {
            if (IsCustomPathSelected)
            {
                return CustomPath;
            }
            return SelectedDrive?.Name ?? string.Empty;
        }
    }

    public string TargetDisplayName
    {
        get
        {
            if (IsCustomPathSelected)
            {
                return string.IsNullOrWhiteSpace(CustomPath) ? "Folder Kustom" : Path.GetFileName(CustomPath);
            }
            return SelectedDrive?.DisplayName ?? "Drive Sistem";
        }
    }

    // Metrik Kapasitas Drive
    public long DriveCapacityBytes => SelectedDrive?.TotalSizeBytes ?? 0;
    public long DriveUsedBytes => SelectedDrive?.UsedSizeBytes ?? 0;
    public long DriveFreeBytes => SelectedDrive?.FreeSpaceBytes ?? 0;
    public string FormattedDriveCapacity => FileFormatter.FormatBytes(DriveCapacityBytes);
    public string FormattedDriveUsed => FileFormatter.FormatBytes(DriveUsedBytes);
    public string FormattedDriveFree => FileFormatter.FormatBytes(DriveFreeBytes);
    public double DriveUsedPercentage => DriveCapacityBytes > 0 ? (double)DriveUsedBytes / DriveCapacityBytes * 100.0 : 0.0;
    public double DriveFreePercentage => DriveCapacityBytes > 0 ? (double)DriveFreeBytes / DriveCapacityBytes * 100.0 : 0.0;
    public string FormattedDriveUsedPercentage => $"{DriveUsedPercentage:F1}%";
    public string FormattedDriveFreePercentage => $"{DriveFreePercentage:F1}%";

    // Metrik Pemindaian Terkini
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                MulaiPemindaianCommand.RaiseCanExecuteChanged();
                BatalPemindaianCommand.RaiseCanExecuteChanged();
                SegarkanDrivesCommand.RaiseCanExecuteChanged();
                PilihFolderCommand.RaiseCanExecuteChanged();
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

    public long TotalSizeBytes
    {
        get => _totalSizeBytes;
        set
        {
            if (SetProperty(ref _totalSizeBytes, value))
            {
                FormattedTotalSize = FileFormatter.FormatBytes(value);
            }
        }
    }

    public string FormattedTotalSize
    {
        get => _formattedTotalSize;
        set => SetProperty(ref _formattedTotalSize, value);
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

    public ScanResultReport? CurrentScanResult
    {
        get => _currentScanResult;
        set => SetProperty(ref _currentScanResult, value);
    }

    public FileSystemItemModel? CurrentRootItem
    {
        get => _currentRootItem;
        set
        {
            if (SetProperty(ref _currentRootItem, value))
            {
                BukaTreemapCommand.RaiseCanExecuteChanged();
                BukaHierarkiCommand.RaiseCanExecuteChanged();
                BukaPembersihanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ScanComparisonModel? Comparison
    {
        get => _comparison;
        set
        {
            if (SetProperty(ref _comparison, value))
            {
                OnPropertyChanged(nameof(HasComparison));
            }
        }
    }

    public bool HasComparison => Comparison?.HasPreviousComparison ?? false;

    public ScanHistoryRecordModel? SelectedHistoryRecord
    {
        get => _selectedHistoryRecord;
        set => SetProperty(ref _selectedHistoryRecord, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    // Commands
    public AsyncRelayCommand MulaiPemindaianCommand { get; }
    public RelayCommand BatalPemindaianCommand { get; }
    public RelayCommand SegarkanDrivesCommand { get; }
    public RelayCommand PilihFolderCommand { get; }
    public RelayCommand BukaTreemapCommand { get; }
    public RelayCommand BukaHierarkiCommand { get; }
    public RelayCommand BukaPembersihanCommand { get; }
    public AsyncRelayCommand HapusRiwayatItemCommand { get; }
    public AsyncRelayCommand BersihkanSemuaRiwayatCommand { get; }
    public RelayCommand TerapkanRiwayatUntukKomparasiCommand { get; }

    public void SegarkanDrives()
    {
        Drives.Clear();
        var drives = _driveService.GetAvailableDrives();
        foreach (var drive in drives)
        {
            Drives.Add(drive);
        }

        if (SelectedDrive == null && Drives.Count > 0)
        {
            SelectedDrive = Drives[0];
        }

        UpdateDriveCapacityMetrics();
    }

    private void UpdateDriveCapacityMetrics()
    {
        OnPropertyChanged(nameof(DriveCapacityBytes));
        OnPropertyChanged(nameof(DriveUsedBytes));
        OnPropertyChanged(nameof(DriveFreeBytes));
        OnPropertyChanged(nameof(FormattedDriveCapacity));
        OnPropertyChanged(nameof(FormattedDriveUsed));
        OnPropertyChanged(nameof(FormattedDriveFree));
        OnPropertyChanged(nameof(DriveUsedPercentage));
        OnPropertyChanged(nameof(DriveFreePercentage));
        OnPropertyChanged(nameof(FormattedDriveUsedPercentage));
        OnPropertyChanged(nameof(FormattedDriveFreePercentage));
    }

    public void PilihFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Target Pemindaian - ROOT"
        };

        if (dialog.ShowDialog() == true)
        {
            CustomPath = dialog.FolderName;
            IsCustomPathSelected = true;
        }
    }

    public async Task MuatRiwayatDariDbAsync()
    {
        try
        {
            var records = await _historyRepository.GetHistoryAsync(50);
            ScanHistory.Clear();
            foreach (var rec in records)
            {
                ScanHistory.Add(rec);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal memuat riwayat pemindaian dari SQLite: {ex.Message}");
        }
    }

    public async Task MulaiPemindaianAsync()
    {
        if (IsScanning) return;

        string target = EffectiveTargetPath;
        if (string.IsNullOrWhiteSpace(target))
        {
            ShowAlert("Silakan tentukan drive atau folder target pemindaian terlebih dahulu.", "Target Kosong", true);
            return;
        }

        if (!Directory.Exists(target))
        {
            ShowAlert($"Target pemindaian tidak valid atau tidak ditemukan:\n{target}", "Target Tidak Valid", true);
            return;
        }

        IsScanning = true;
        CurrentState = DashboardState.Memuat;
        StatusText = "Memulai pemindaian asinkron...";
        CurrentScannedPath = target;
        ScannedFiles = 0;
        ScannedDirectories = 0;
        TotalSizeBytes = 0;
        SkippedCount = 0;
        ScanRate = "Menghitung...";
        ErrorMessage = string.Empty;

        _scanCts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        using var timer = new System.Timers.Timer(500);
        timer.Elapsed += (_, _) =>
        {
            ElapsedTime = $"{sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
        };
        timer.Start();

        var progress = new Progress<ScanProgressReport>(report =>
        {
            ScannedFiles = report.ScannedFilesCount;
            ScannedDirectories = report.ScannedDirectoriesCount;
            TotalSizeBytes = report.TotalBytesScanned;
            SkippedCount = report.SkippedCount;
            CurrentScannedPath = report.CurrentPath;
            StatusText = $"Memindai: {report.FormattedFilesCount} berkas ({report.FormattedTotalBytes})";
            ScanRate = report.FormattedRate;
        });

        try
        {
            var result = await _scanner.ScanAsync(target, progress, _scanCts.Token);
            CurrentScanResult = result;
            CurrentRootItem = result.RootItem;

            // Simpan catatan ke SQLite
            var historyRecord = new ScanHistoryRecordModel
            {
                TargetPath = target,
                TargetName = TargetDisplayName,
                ScannedAt = result.EndTime,
                Duration = result.Duration,
                TotalSizeBytes = result.TotalSizeBytes,
                TotalFiles = result.TotalFilesCount,
                TotalDirectories = result.TotalDirectoriesCount,
                SkippedCount = result.SkippedCount,
                Status = result.Status,
                IsPartial = result.IsPartial,
                DriveCapacityBytes = DriveCapacityBytes,
                DriveFreeBytes = DriveFreeBytes,
                DriveUsedBytes = DriveUsedBytes,
                IsStale = false
            };

            // Ambil catatan pemindaian sebelumnya pada target yang sama untuk komparasi delta
            var previousRecord = await _historyRepository.GetPreviousScanAsync(target);
            var savedRecord = await _historyRepository.SaveRecordAsync(historyRecord);

            // Buat model perbandingan (Komparasi Baru vs Lama)
            Comparison = new ScanComparisonModel(savedRecord, previousRecord);

            // Perbarui daftar riwayat di memori
            ScanHistory.Insert(0, savedRecord);

            // Perbarui status tampilan
            if (result.Status == ScanStatus.Selesai)
            {
                CurrentState = DashboardState.Selesai;
                StatusText = $"Pemindaian sukses penuh (100% lengkap): {result.TotalFilesCount:N0} berkas, {result.FormattedTotalSize}.";
            }
            else if (result.Status == ScanStatus.SelesaiSebagian || result.IsPartial || result.Status == ScanStatus.Dibatalkan)
            {
                CurrentState = DashboardState.Parsial;
                StatusText = result.Status == ScanStatus.Dibatalkan
                    ? $"Pemindaian dibatalkan pengguna ({result.TotalFilesCount:N0} berkas terpindai, hasil parsial)."
                    : $"Pemindaian selesai sebagian ({result.SkippedCount:N0} item dilewati karena izin sistem): {result.TotalFilesCount:N0} berkas.";
            }
            else
            {
                CurrentState = DashboardState.Gagal;
                StatusText = "Pemindaian gagal atau dihentikan.";
            }

            // Kirim event hasil pemindaian ke Treemap, Hierarki, dan modul lainnya
            ScanCompleted?.Invoke(result.RootItem);
        }
        catch (OperationCanceledException)
        {
            CurrentState = DashboardState.Parsial;
            StatusText = "Pemindaian dibatalkan oleh pengguna (laporan parsial).";
            ErrorMessage = "Pemindaian dihentikan sebelum selesai.";
            _logger.LogWarning("Pemindaian dari dasbor dibatalkan oleh pengguna.");
        }
        catch (Exception ex)
        {
            CurrentState = DashboardState.Gagal;
            StatusText = $"Pemindaian gagal: {ex.Message}";
            ErrorMessage = ex.Message;
            _logger.LogError($"Kesalahan pemindaian di dasbor: {ex.Message}");
            ShowAlert($"Terjadi kesalahan saat memindai:\n\n{ex.Message}", "Kesalahan Pemindaian", true);
        }
        finally
        {
            timer.Stop();
            sw.Stop();
            ElapsedTime = $"{sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    public void BatalPemindaian()
    {
        if (_scanCts != null && !_scanCts.IsCancellationRequested)
        {
            _scanCts.Cancel();
            StatusText = "Membatalkan pemindaian...";
        }
    }

    public async Task HapusRiwayatItemAsync()
    {
        if (SelectedHistoryRecord == null) return;

        long id = SelectedHistoryRecord.Id;
        await _historyRepository.DeleteRecordAsync(id);
        ScanHistory.Remove(SelectedHistoryRecord);
        SelectedHistoryRecord = null;
        _logger.LogInfo($"Catatan riwayat pemindaian #{id} dihapus dari basis data SQLite.");
    }

    public async Task BersihkanSemuaRiwayatAsync()
    {
        bool confirmed = _confirmAction != null
            ? _confirmAction("Apakah Anda yakin ingin menghapus seluruh riwayat pemindaian di basis data SQLite?", "Bersihkan Riwayat - ROOT")
            : (MessageBox.Show(
                "Apakah Anda yakin ingin menghapus seluruh riwayat pemindaian di basis data SQLite?",
                "Bersihkan Riwayat - ROOT",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes);

        if (!confirmed) return;

        await _historyRepository.ClearHistoryAsync();
        ScanHistory.Clear();
        Comparison = null;
        _logger.LogInfo("Seluruh riwayat pemindaian di basis data SQLite telah dibersihkan.");
    }

    public void TerapkanRiwayatUntukKomparasi(ScanHistoryRecordModel? record)
    {
        if (record == null) return;

        SelectedHistoryRecord = record;

        // Jika ada pemindaian saat ini, bandingkan dengan catatan riwayat terpilih
        if (CurrentScanResult != null)
        {
            var currentAsHistory = new ScanHistoryRecordModel
            {
                TargetPath = CurrentScanResult.TargetPath,
                TargetName = TargetDisplayName,
                ScannedAt = CurrentScanResult.EndTime,
                Duration = CurrentScanResult.Duration,
                TotalSizeBytes = CurrentScanResult.TotalSizeBytes,
                TotalFiles = CurrentScanResult.TotalFilesCount,
                TotalDirectories = CurrentScanResult.TotalDirectoriesCount,
                SkippedCount = CurrentScanResult.SkippedCount,
                Status = CurrentScanResult.Status,
                IsPartial = CurrentScanResult.IsPartial,
                IsStale = false
            };

            Comparison = new ScanComparisonModel(currentAsHistory, record);
        }
    }

    private void ShowAlert(string message, string title, bool isError)
    {
        if (_alertAction != null)
        {
            _alertAction(message, title, isError);
        }
        else
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                isError ? MessageBoxImage.Error : MessageBoxImage.Information);
        }
    }
}
