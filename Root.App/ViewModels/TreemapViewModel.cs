using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;
using Root.Core.Utilities;

namespace Root.App.ViewModels;

public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public FileSystemItemModel Folder { get; set; } = null!;
    public bool IsLast { get; set; }
}

public class TreemapViewModel : ViewModelBase
{
    private readonly ITreemapEngine _treemapEngine;
    private readonly IFileTreeService _fileTreeService;
    private readonly ILoggerService _logger;

    private FileSystemItemModel? _rootFolder;
    private FileSystemItemModel? _currentFolder;
    private TreemapNode? _selectedNode;
    private TreemapNode? _hoveredNode;
    private double _containerWidth = 800;
    private double _containerHeight = 500;
    private bool _hasData;
    private bool _isEmptyFolder;

    // Filter & Urutan
    private SortField _selectedSortField = SortField.Ukuran;
    private SortDirection _selectedSortDirection = SortDirection.Menurun;
    private string _filterExtension = "Semua";
    private long _minimumSizeBytes = 0;
    private string _searchQuery = string.Empty;

    public TreemapViewModel(ITreemapEngine treemapEngine, ILoggerService logger, IFileTreeService? fileTreeService = null)
    {
        _treemapEngine = treemapEngine;
        _logger = logger;
        _fileTreeService = fileTreeService ?? new FileTreeService();

        Breadcrumbs = [];
        Nodes = [];

        DrillDownCommand = new RelayCommand(p =>
        {
            if (p is TreemapNode node && node.IsDirectory)
            {
                NavigateToFolder(node.Item);
            }
            else if (p is FileSystemItemModel item && item.IsDirectory)
            {
                NavigateToFolder(item);
            }
        });

        NavigateUpCommand = new RelayCommand(NavigateUp, () => CanNavigateUp);
        NavigateToBreadcrumbCommand = new RelayCommand(p =>
        {
            if (p is BreadcrumbItem b)
            {
                NavigateToFolder(b.Folder);
            }
        });

        SelectNodeCommand = new RelayCommand(p =>
        {
            if (p is TreemapNode node)
            {
                SelectedNode = node;
            }
        });

        BukaDiExplorerCommand = new RelayCommand(BukaDiExplorer, () => SelectedNode != null);

        ResetFilterCommand = new RelayCommand(() =>
        {
            SelectedSortField = SortField.Ukuran;
            SelectedSortDirection = SortDirection.Menurun;
            FilterExtension = "Semua";
            MinimumSizeBytes = 0;
            SearchQuery = string.Empty;
            RecalculateTreemap();
        });
    }

    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; }
    public ObservableCollection<TreemapNode> Nodes { get; }

    public FileSystemItemModel? RootFolder
    {
        get => _rootFolder;
        private set => SetProperty(ref _rootFolder, value);
    }

    public FileSystemItemModel? CurrentFolder
    {
        get => _currentFolder;
        private set
        {
            if (SetProperty(ref _currentFolder, value))
            {
                OnPropertyChanged(nameof(CanNavigateUp));
                NavigateUpCommand.RaiseCanExecuteChanged();
                UpdateBreadcrumbs();
                RecalculateTreemap();
            }
        }
    }

    public TreemapNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                BukaDiExplorerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public TreemapNode? HoveredNode
    {
        get => _hoveredNode;
        set => SetProperty(ref _hoveredNode, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    public bool IsEmptyFolder
    {
        get => _isEmptyFolder;
        private set => SetProperty(ref _isEmptyFolder, value);
    }

    public bool CanNavigateUp => CurrentFolder != null && RootFolder != null && CurrentFolder != RootFolder;

    // Filter & Urutan Properties
    public SortField SelectedSortField
    {
        get => _selectedSortField;
        set
        {
            if (SetProperty(ref _selectedSortField, value))
            {
                RecalculateTreemap();
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
                RecalculateTreemap();
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
                RecalculateTreemap();
            }
        }
    }

    public long MinimumSizeBytes
    {
        get => _minimumSizeBytes;
        set
        {
            if (SetProperty(ref _minimumSizeBytes, value))
            {
                RecalculateTreemap();
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
                RecalculateTreemap();
            }
        }
    }

    public RelayCommand DrillDownCommand { get; }
    public RelayCommand NavigateUpCommand { get; }
    public RelayCommand NavigateToBreadcrumbCommand { get; }
    public RelayCommand SelectNodeCommand { get; }
    public RelayCommand BukaDiExplorerCommand { get; }
    public RelayCommand ResetFilterCommand { get; }

    /// <summary>
    /// Memuat data pohon hasil pemindaian nyata ke dalam Treemap.
    /// Tidak menggunakan data tiruan (mock/dummy) tetap dalam fungsi produksi.
    /// </summary>
    public void LoadScanData(FileSystemItemModel rootItem)
    {
        if (rootItem == null) return;

        RootFolder = rootItem;
        _logger.LogInfo($"Memuat data hasil pemindaian nyata ke Treemap: '{rootItem.FullPath}' ({FileFormatter.FormatBytes(rootItem.SizeBytes)}).");
        NavigateToFolder(rootItem);
    }

    public void NavigateToFolder(FileSystemItemModel folder)
    {
        if (folder == null) return;
        SelectedNode = null;
        CurrentFolder = folder;
    }

    public void NavigateUp()
    {
        if (!CanNavigateUp || CurrentFolder == null || RootFolder == null)
            return;

        if (Breadcrumbs.Count >= 2)
        {
            var parentBreadcrumb = Breadcrumbs[Breadcrumbs.Count - 2];
            NavigateToFolder(parentBreadcrumb.Folder);
        }
        else
        {
            NavigateToFolder(RootFolder);
        }
    }

    public void OnDimensionsChanged(double newWidth, double newHeight)
    {
        if (newWidth <= 10 || newHeight <= 10) return;

        // F-05: Mencegah kalkulasi berulang pada perubahan ukuran mikro / drag halus (< 3px)
        if (Math.Abs(_containerWidth - newWidth) >= 3 || Math.Abs(_containerHeight - newHeight) >= 3)
        {
            _containerWidth = newWidth;
            _containerHeight = newHeight;
            RecalculateTreemap();
        }
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        if (RootFolder == null || CurrentFolder == null) return;

        var stack = new List<FileSystemItemModel>();
        var current = CurrentFolder;

        while (current != null)
        {
            stack.Insert(0, current);
            if (current == RootFolder) break;

            if (!string.IsNullOrEmpty(current.ParentPath))
            {
                current = FindNodeInTree(RootFolder, current.ParentPath);
            }
            else
            {
                break;
            }
        }

        for (int i = 0; i < stack.Count; i++)
        {
            var item = stack[i];
            Breadcrumbs.Add(new BreadcrumbItem
            {
                Name = string.IsNullOrEmpty(item.Name) ? item.FullPath : item.Name,
                Folder = item,
                IsLast = i == stack.Count - 1
            });
        }
    }

    private static FileSystemItemModel? FindNodeInTree(FileSystemItemModel current, string fullPath)
    {
        if (string.Equals(current.FullPath.TrimEnd('\\'), fullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return current;

        foreach (var child in current.Children)
        {
            if (child.IsDirectory)
            {
                var match = FindNodeInTree(child, fullPath);
                if (match != null) return match;
            }
        }

        return null;
    }

    public void RecalculateTreemap()
    {
        Nodes.Clear();

        if (CurrentFolder == null)
        {
            HasData = false;
            IsEmptyFolder = false;
            return;
        }

        var children = CurrentFolder.Children;
        if (children == null || children.Count == 0)
        {
            HasData = true;
            IsEmptyFolder = true;
            return;
        }

        // Terapkan kriteria penyaringan yang konsisten dengan tampilan daftar
        var criteria = new FileFilterCriteria
        {
            Extension = FilterExtension,
            MinimumSizeBytes = MinimumSizeBytes,
            SearchText = SearchQuery
        };

        IEnumerable<FileSystemItemModel> filteredChildren;

        if (criteria.IsEmpty)
        {
            filteredChildren = children;
        }
        else
        {
            var filteredCurrent = _fileTreeService.FilterHierarchy(CurrentFolder, criteria);
            filteredChildren = filteredCurrent?.Children ?? [];
        }

        // Terapkan pengurutan
        var sortedChildren = _fileTreeService.Sort(filteredChildren, SelectedSortField, SelectedSortDirection).ToList();

        if (sortedChildren.Count == 0)
        {
            HasData = true;
            IsEmptyFolder = true;
            return;
        }

        HasData = true;
        IsEmptyFolder = false;

        var calculatedNodes = _treemapEngine.CalculateLayout(sortedChildren, _containerWidth, _containerHeight);
        foreach (var node in calculatedNodes)
        {
            Nodes.Add(node);
        }

        // Pertahankan seleksi jika node masih ada di level ini
        if (SelectedNode != null)
        {
            SelectedNode = Nodes.FirstOrDefault(n => n.FullPath == SelectedNode.FullPath);
        }
    }

    private void BukaDiExplorer()
    {
        if (SelectedNode == null) return;

        try
        {
            string path = SelectedNode.FullPath;
            if (SelectedNode.IsDirectory && Directory.Exists(path))
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
            _logger.LogError($"Gagal membuka Explorer untuk '{SelectedNode.FullPath}': {ex.Message}");
        }
    }
}
