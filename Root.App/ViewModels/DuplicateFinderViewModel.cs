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

public class DuplicateFinderViewModel : ViewModelBase
{
    private readonly IDuplicateFinderService _duplicateService;
    private readonly ILoggerService _logger;
    private readonly Action<string, string, bool>? _alertAction;
    private readonly Action<string>? _fileOpener;

    private FileSystemItemModel? _lastScannedRootItem;
    private CancellationTokenSource? _searchCts;

    private string _targetDirectoryPath = string.Empty;
    private bool _useCurrentScanData = true;
    private bool _includeZeroByteFiles;
    private bool _isSearching;
    private bool _hasResults;
    private string _statusText = "Siap mencari berkas duplikat.";
    private string _currentProcessingFile = "-";
    private double _progressPercentage;
    private string _elapsedTime = "00:00";
    private string _throughputText = "-";
    private DuplicateGroupModel? _selectedGroup;
    private DuplicateSearchResult? _searchResult;

    public DuplicateFinderViewModel(
        IDuplicateFinderService duplicateService, 
        ILoggerService logger,
        Action<string, string, bool>? alertAction = null,
        Action<string>? fileOpener = null)
    {
        _duplicateService = duplicateService;
        _logger = logger;
        _alertAction = alertAction;
        _fileOpener = fileOpener;

        DuplicateGroups = [];
        Errors = [];

        MulaiPencarianCommand = new AsyncRelayCommand(MulaiPencarianAsync, () => !IsSearching);
        BatalPencarianCommand = new RelayCommand(BatalPencarian, () => IsSearching);
        PilihFolderCommand = new RelayCommand(PilihFolder, () => !IsSearching);
        SalinLaporanCommand = new RelayCommand(SalinLaporan, () => HasResults && SearchResult != null);
        BukaLokasiBerkasCommand = new RelayCommand(param => BukaLokasiBerkas(param as string));
    }

    public ObservableCollection<DuplicateGroupModel> DuplicateGroups { get; }
    public ObservableCollection<ScanErrorItem> Errors { get; }

    public DuplicateSearchResult? SearchResult
    {
        get => _searchResult;
        set
        {
            if (SetProperty(ref _searchResult, value))
            {
                HasResults = value != null;
                SalinLaporanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DuplicateGroupModel? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    public string TargetDirectoryPath
    {
        get => _targetDirectoryPath;
        set
        {
            if (SetProperty(ref _targetDirectoryPath, value))
            {
                OnPropertyChanged(nameof(CanStartSearch));
                MulaiPencarianCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool UseCurrentScanData
    {
        get => _useCurrentScanData;
        set
        {
            if (SetProperty(ref _useCurrentScanData, value))
            {
                OnPropertyChanged(nameof(CanStartSearch));
                MulaiPencarianCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasScanDataAvailable => _lastScannedRootItem != null;

    public bool IncludeZeroByteFiles
    {
        get => _includeZeroByteFiles;
        set => SetProperty(ref _includeZeroByteFiles, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (SetProperty(ref _isSearching, value))
            {
                OnPropertyChanged(nameof(CanStartSearch));
                MulaiPencarianCommand.RaiseCanExecuteChanged();
                BatalPencarianCommand.RaiseCanExecuteChanged();
                PilihFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    public bool CanStartSearch => !IsSearching && (UseCurrentScanData ? HasScanDataAvailable : !string.IsNullOrWhiteSpace(TargetDirectoryPath));

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentProcessingFile
    {
        get => _currentProcessingFile;
        set => SetProperty(ref _currentProcessingFile, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string ElapsedTime
    {
        get => _elapsedTime;
        set => SetProperty(ref _elapsedTime, value);
    }

    public string ThroughputText
    {
        get => _throughputText;
        set => SetProperty(ref _throughputText, value);
    }

    public AsyncRelayCommand MulaiPencarianCommand { get; }
    public RelayCommand BatalPencarianCommand { get; }
    public RelayCommand PilihFolderCommand { get; }
    public RelayCommand SalinLaporanCommand { get; }
    public RelayCommand BukaLokasiBerkasCommand { get; }

    public void LoadScanData(FileSystemItemModel rootItem)
    {
        _lastScannedRootItem = rootItem;
        TargetDirectoryPath = rootItem.FullPath;
        UseCurrentScanData = true;
        OnPropertyChanged(nameof(HasScanDataAvailable));
        OnPropertyChanged(nameof(CanStartSearch));
        MulaiPencarianCommand.RaiseCanExecuteChanged();

        StatusText = $"Data pemindaian aktif dimuat: {rootItem.Name} ({rootItem.FileCount:N0} berkas). Siap menganalisis duplikat.";
        _logger.LogInfo($"DuplicateFinderViewModel: Menerima data hasil pemindaian '{rootItem.FullPath}' ({rootItem.FileCount:N0} berkas).");
    }

    private void PilihFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Direktori untuk Mencari Berkas Duplikat",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            TargetDirectoryPath = dialog.FolderName;
            UseCurrentScanData = false;
            _logger.LogInfo($"Direktori pencarian duplikat dipilih: '{TargetDirectoryPath}'");
        }
    }

    private async Task MulaiPencarianAsync()
    {
        if (UseCurrentScanData && _lastScannedRootItem == null)
        {
            StatusText = "Peringatan: Belum ada data pemindaian aktif. Lakukan pemindaian terlebih dahulu atau pilih folder manual.";
            _logger.LogWarning("Percobaan pencarian duplikat gagal: Belum ada data pemindaian aktif.");
            return;
        }

        if (!UseCurrentScanData && !Directory.Exists(TargetDirectoryPath))
        {
            StatusText = $"Kesalahan: Direktori '{TargetDirectoryPath}' tidak valid atau tidak ditemukan.";
            _logger.LogError($"Direktori target tidak valid: '{TargetDirectoryPath}'");
            return;
        }

        IsSearching = true;
        StatusText = "Mengelompokkan berkas kandidat berdasarkan ukuran...";
        CurrentProcessingFile = "-";
        ProgressPercentage = 0;
        ElapsedTime = "00:00";
        ThroughputText = "-";
        DuplicateGroups.Clear();
        Errors.Clear();
        SelectedGroup = null;
        SearchResult = null;

        _searchCts = new CancellationTokenSource();

        var options = new DuplicateSearchOptions
        {
            IncludeZeroByteFiles = IncludeZeroByteFiles,
            MinimumSizeBytes = IncludeZeroByteFiles ? 0 : 1
        };

        var progress = new Progress<DuplicateProgressReport>(report =>
        {
            StatusText = report.CurrentStage;
            CurrentProcessingFile = report.CurrentProcessingFile;
            ProgressPercentage = report.PercentComplete;
            ElapsedTime = report.FormattedElapsedTime;
        });

        try
        {
            DuplicateSearchResult result;

            if (UseCurrentScanData && _lastScannedRootItem != null)
            {
                _logger.LogInfo($"Menjalankan pencarian duplikat dari data pemindaian memori '{_lastScannedRootItem.FullPath}'...");
                result = await _duplicateService.FindDuplicatesAsync([_lastScannedRootItem], options, progress, _searchCts.Token);
            }
            else
            {
                _logger.LogInfo($"Menjalankan pencarian duplikat langsung dari direktori '{TargetDirectoryPath}'...");
                result = await _duplicateService.FindDuplicatesInDirectoryAsync(TargetDirectoryPath, options, progress, _searchCts.Token);
            }

            SearchResult = result;

            foreach (var group in result.DuplicateGroups.OrderByDescending(g => g.WasteSizeBytes))
            {
                DuplicateGroups.Add(group);
            }

            foreach (var err in result.Errors)
            {
                Errors.Add(err);
            }

            SelectedGroup = DuplicateGroups.FirstOrDefault();
            ElapsedTime = result.FormattedDuration;
            ThroughputText = result.FormattedThroughput;
            ProgressPercentage = 100;

            if (result.IsCancelled)
            {
                StatusText = $"Pencarian dibatalkan oleh pengguna ({result.TotalDuplicateGroups:N0} kelompok ditemukan).";
            }
            else if (result.TotalDuplicateGroups == 0)
            {
                StatusText = "Pencarian selesai. Tidak ditemukan berkas duplikat yang identik.";
            }
            else
            {
                StatusText = $"Pencarian selesai: {result.TotalDuplicateGroups:N0} kelompok duplikat ditemukan (Pemborosan: {result.FormattedTotalWaste}).";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Terjadi kesalahan saat mencari duplikat: {ex.Message}";
            _logger.LogError($"Kesalahan tidak terduga pada pencarian duplikat: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
            _searchCts?.Dispose();
            _searchCts = null;
        }
    }

    private void BatalPencarian()
    {
        if (_searchCts != null && !_searchCts.IsCancellationRequested)
        {
            StatusText = "Membatalkan pencarian duplikat, menyelesaikan hasil saat ini...";
            _logger.LogWarning("Pengguna membatalkan proses pencarian berkas duplikat.");
            _searchCts.Cancel();
        }
    }

    private void SalinLaporan()
    {
        if (SearchResult == null) return;

        try
        {
            var markdown = SearchResult.GenerateSummaryMarkdown();
            Clipboard.SetText(markdown);
            _logger.LogSuccess("Laporan temuan berkas duplikat berhasil disalin ke clipboard.");

            if (_alertAction != null)
            {
                _alertAction("Laporan berkas duplikat berhasil disalin ke clipboard!", "ROOT - Sukses", false);
            }
            else
            {
                MessageBox.Show("Laporan berkas duplikat berhasil disalin ke clipboard!", "ROOT - Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal menyalin laporan duplikat: {ex.Message}");
        }
    }

    private void BukaLokasiBerkas(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            if (_alertAction != null)
            {
                _alertAction($"Berkas '{filePath}' tidak ditemukan pada sistem.", "Peringatan", false);
            }
            else
            {
                MessageBox.Show($"Berkas '{filePath}' tidak ditemukan pada sistem.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        try
        {
            if (_fileOpener != null)
            {
                _fileOpener(filePath);
            }
            else
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            _logger.LogInfo($"Membuka berkas di Windows Explorer: '{filePath}'");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal membuka Explorer untuk berkas '{filePath}': {ex.Message}");
        }
    }
}
