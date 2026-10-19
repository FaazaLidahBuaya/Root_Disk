using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;
using Root.Core.Utilities;

namespace Root.App.ViewModels;

public class LargeFileItemModel
{
    public int Rank { get; set; }
    public FileSystemItemModel Item { get; set; } = null!;

    public string Name => Item.Name;
    public string Extension => Item.Extension;
    public long SizeBytes => Item.SizeBytes;
    public string FormattedSize => Item.FormattedSize;
    public string FullPath => Item.FullPath;
    public string? ParentPath => Item.ParentPath;
    public DateTime? LastModified => Item.LastModified;
    public string TypeDescription => Item.TypeDescription;
}

public class LargeFilesViewModel : ViewModelBase
{
    private readonly IFileTreeService _fileTreeService;
    private readonly ILoggerService _logger;

    private FileSystemItemModel? _rawRoot;
    private LargeFileItemModel? _selectedFile;

    private SortField _selectedSortField = SortField.Ukuran;
    private SortDirection _selectedSortDirection = SortDirection.Menurun;
    private string _filterExtension = "Semua";
    private SizeFilterOption _selectedSizeOption;
    private string _searchQuery = string.Empty;
    private int _selectedMaxResults = 100;
    private bool _hasData;

    // Statistik Cepat
    private int _totalFilesCount;
    private string _totalSizeFormatted = "0 B";
    private string _largestFileName = "-";
    private string _largestFileSize = "0 B";

    public event Action<FileSystemItemModel>? RequestNavigateToTreemap;

    public LargeFilesViewModel(IFileTreeService fileTreeService, ILoggerService logger)
    {
        _fileTreeService = fileTreeService;
        _logger = logger;

        SizeOptions = new ObservableCollection<SizeFilterOption>(SizeFilterOption.GetDefaultPresets());
        _selectedSizeOption = SizeOptions[2]; // Default > 10 MB

        MaxResultsOptions = [50, 100, 250, 500, 1000];

        Files = [];

        BukaDiExplorerCommand = new RelayCommand(BukaDiExplorer, () => SelectedFile != null);
        SalinJalurCommand = new RelayCommand(SalinJalur, () => SelectedFile != null);
        LihatDiTreemapCommand = new RelayCommand(LihatDiTreemap, () => SelectedFile != null);

        ResetFilterCommand = new RelayCommand(() =>
        {
            SelectedSortField = SortField.Ukuran;
            SelectedSortDirection = SortDirection.Menurun;
            FilterExtension = "Semua";
            SelectedSizeOption = SizeOptions[2];
            SearchQuery = string.Empty;
            SelectedMaxResults = 100;
            RefreshFilesList();
        });
    }

    public ObservableCollection<SizeFilterOption> SizeOptions { get; }
    public IReadOnlyList<int> MaxResultsOptions { get; }
    public ObservableCollection<LargeFileItemModel> Files { get; }

    public FileSystemItemModel? RawRoot
    {
        get => _rawRoot;
        private set => SetProperty(ref _rawRoot, value);
    }

    public LargeFileItemModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                BukaDiExplorerCommand.RaiseCanExecuteChanged();
                SalinJalurCommand.RaiseCanExecuteChanged();
                LihatDiTreemapCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    public SortField SelectedSortField
    {
        get => _selectedSortField;
        set
        {
            if (SetProperty(ref _selectedSortField, value))
            {
                RefreshFilesList();
            }
        }
    }

    public SortDirection SelectedSortDirection
    {
        get => _selectedSortDirection;
        set
        {
            if (SetProperty(ref _selectedSortDirection, value))
            {
                RefreshFilesList();
            }
        }
    }

    public string FilterExtension
    {
        get => _filterExtension;
        set
        {
            if (SetProperty(ref _filterExtension, value))
            {
                RefreshFilesList();
            }
        }
    }

    public SizeFilterOption SelectedSizeOption
    {
        get => _selectedSizeOption;
        set
        {
            if (SetProperty(ref _selectedSizeOption, value))
            {
                RefreshFilesList();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshFilesList();
            }
        }
    }

    public int SelectedMaxResults
    {
        get => _selectedMaxResults;
        set
        {
            if (SetProperty(ref _selectedMaxResults, value))
            {
                RefreshFilesList();
            }
        }
    }

    // Statistik Properties
    public int TotalFilesCount
    {
        get => _totalFilesCount;
        private set => SetProperty(ref _totalFilesCount, value);
    }

    public string TotalSizeFormatted
    {
        get => _totalSizeFormatted;
        private set => SetProperty(ref _totalSizeFormatted, value);
    }

    public string LargestFileName
    {
        get => _largestFileName;
        private set => SetProperty(ref _largestFileName, value);
    }

    public string LargestFileSize
    {
        get => _largestFileSize;
        private set => SetProperty(ref _largestFileSize, value);
    }

    public RelayCommand BukaDiExplorerCommand { get; }
    public RelayCommand SalinJalurCommand { get; }
    public RelayCommand LihatDiTreemapCommand { get; }
    public RelayCommand ResetFilterCommand { get; }

    public void LoadScanData(FileSystemItemModel rootItem)
    {
        if (rootItem == null) return;

        RawRoot = rootItem;
        HasData = true;
        _logger.LogInfo($"Memuat data ke Halaman Berkas Berukuran Besar: '{rootItem.FullPath}'.");
        RefreshFilesList();
    }

    public void RefreshFilesList()
    {
        Files.Clear();

        if (RawRoot == null)
        {
            HasData = false;
            TotalFilesCount = 0;
            TotalSizeFormatted = "0 B";
            LargestFileName = "-";
            LargestFileSize = "0 B";
            return;
        }

        var criteria = new FileFilterCriteria
        {
            Extension = FilterExtension,
            MinimumSizeBytes = SelectedSizeOption?.Bytes ?? 0,
            SearchText = SearchQuery
        };

        // Ekstraksi berkas secara in-memory tanpa pemindaian ulang disk
        var flattened = _fileTreeService.GetFlattenedFiles(
            RawRoot,
            criteria,
            SelectedSortField,
            SelectedSortDirection,
            SelectedMaxResults);

        long sumBytes = 0;
        int rank = 1;

        foreach (var file in flattened)
        {
            sumBytes += file.SizeBytes;
            Files.Add(new LargeFileItemModel
            {
                Rank = rank++,
                Item = file
            });
        }

        TotalFilesCount = Files.Count;
        TotalSizeFormatted = FileFormatter.FormatBytes(sumBytes);

        if (Files.Count > 0)
        {
            var largest = Files.OrderByDescending(f => f.SizeBytes).First();
            LargestFileName = largest.Name;
            LargestFileSize = largest.FormattedSize;
        }
        else
        {
            LargestFileName = "-";
            LargestFileSize = "0 B";
        }
    }

    private void BukaDiExplorer()
    {
        if (SelectedFile == null) return;

        try
        {
            string path = SelectedFile.FullPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            _logger.LogInfo($"Membuka di Explorer: '{path}'");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal membuka Explorer untuk '{SelectedFile.FullPath}': {ex.Message}");
        }
    }

    private void SalinJalur()
    {
        if (SelectedFile == null) return;
        try
        {
            Clipboard.SetText(SelectedFile.FullPath);
            MessageBox.Show($"Jalur berhasil disalin:\n{SelectedFile.FullPath}", "ROOT - Informasi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal menyalin jalur: {ex.Message}");
        }
    }

    private void LihatDiTreemap()
    {
        if (SelectedFile == null) return;
        RequestNavigateToTreemap?.Invoke(SelectedFile.Item);
    }
}
