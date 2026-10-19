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

public class HierarchyViewModel : ViewModelBase
{
    private readonly IFileTreeService _fileTreeService;
    private readonly ILoggerService _logger;

    private FileSystemItemModel? _rawRoot;
    private FileSystemItemModel? _filteredRoot;
    private FileSystemItemModel? _selectedItem;

    private SortField _selectedSortField = SortField.Ukuran;
    private SortDirection _selectedSortDirection = SortDirection.Menurun;
    private string _filterExtension = "Semua";
    private SizeFilterOption _selectedSizeOption;
    private string _searchQuery = string.Empty;
    private bool _hasData;

    public event Action<FileSystemItemModel>? RequestNavigateToTreemap;

    public HierarchyViewModel(IFileTreeService fileTreeService, ILoggerService logger)
    {
        _fileTreeService = fileTreeService;
        _logger = logger;

        SizeOptions = new ObservableCollection<SizeFilterOption>(SizeFilterOption.GetDefaultPresets());
        _selectedSizeOption = SizeOptions[0]; // Semua Ukuran

        HierarchyItems = [];

        BukaDiExplorerCommand = new RelayCommand(BukaDiExplorer, () => SelectedItem != null);
        SalinJalurCommand = new RelayCommand(SalinJalur, () => SelectedItem != null);
        LihatDiTreemapCommand = new RelayCommand(LihatDiTreemap, () => SelectedItem != null);

        ResetFilterCommand = new RelayCommand(() =>
        {
            SelectedSortField = SortField.Ukuran;
            SelectedSortDirection = SortDirection.Menurun;
            FilterExtension = "Semua";
            SelectedSizeOption = SizeOptions[0];
            SearchQuery = string.Empty;
            ApplyFilterAndSort();
        });
    }

    public ObservableCollection<SizeFilterOption> SizeOptions { get; }
    public ObservableCollection<FileSystemItemModel> HierarchyItems { get; }

    public FileSystemItemModel? RawRoot
    {
        get => _rawRoot;
        private set => SetProperty(ref _rawRoot, value);
    }

    public FileSystemItemModel? FilteredRoot
    {
        get => _filteredRoot;
        private set => SetProperty(ref _filteredRoot, value);
    }

    public FileSystemItemModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
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
                ApplyFilterAndSort();
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
                ApplyFilterAndSort();
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
                ApplyFilterAndSort();
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
                ApplyFilterAndSort();
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
                ApplyFilterAndSort();
            }
        }
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
        _logger.LogInfo($"Memuat data pohon ke Tampilan Hierarkis: '{rootItem.FullPath}' ({FileFormatter.FormatBytes(rootItem.SizeBytes)}).");
        ApplyFilterAndSort();
    }

    public void ApplyFilterAndSort()
    {
        HierarchyItems.Clear();

        if (RawRoot == null)
        {
            HasData = false;
            return;
        }

        var criteria = new FileFilterCriteria
        {
            Extension = FilterExtension,
            MinimumSizeBytes = SelectedSizeOption?.Bytes ?? 0,
            SearchText = SearchQuery
        };

        // 1. Saring hierarki dalam memori tanpa pemindaian ulang disk
        var filtered = _fileTreeService.FilterHierarchy(RawRoot, criteria);

        if (filtered == null)
        {
            FilteredRoot = null;
            return;
        }

        // 2. Urutkan seluruh tingkat hierarki sesuai kriteria pilihan
        var sorted = _fileTreeService.SortHierarchy(filtered, SelectedSortField, SelectedSortDirection, directoriesFirst: true);

        FilteredRoot = sorted;
        HierarchyItems.Add(sorted);
    }

    private void BukaDiExplorer()
    {
        if (SelectedItem == null) return;

        try
        {
            string path = SelectedItem.FullPath;
            if (SelectedItem.IsDirectory && Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else if (File.Exists(path))
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
            _logger.LogError($"Gagal membuka Explorer untuk '{SelectedItem.FullPath}': {ex.Message}");
        }
    }

    private void SalinJalur()
    {
        if (SelectedItem == null) return;
        try
        {
            Clipboard.SetText(SelectedItem.FullPath);
            MessageBox.Show($"Jalur berhasil disalin:\n{SelectedItem.FullPath}", "ROOT - Informasi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Gagal menyalin jalur: {ex.Message}");
        }
    }

    private void LihatDiTreemap()
    {
        if (SelectedItem == null) return;
        RequestNavigateToTreemap?.Invoke(SelectedItem);
    }
}
