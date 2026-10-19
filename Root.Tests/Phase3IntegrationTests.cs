using System;
using System.Collections.Generic;
using System.Linq;
using Root.App.ViewModels;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class Phase3IntegrationTests
{
    private readonly FileTreeService _treeService = new();
    private readonly SquarifiedTreemapEngine _treemapEngine = new();
    private readonly LoggerService _logger = new();

    private FileSystemItemModel CreateSampleTree()
    {
        // C:\Data
        // ├── Media (120 MB)
        // │   ├── clip.mp4 (100 MB, 2026-03-01)
        // │   └── song.mp3 (20 MB, 2026-02-01)
        // ├── Docs (15 MB)
        // │   ├── report.pdf (10 MB, 2026-01-15)
        // │   └── readme.txt (5 MB, 2026-01-01)
        // └── backup.iso (500 MB, 2026-04-01)
        // Total = 635 MB

        var root = new FileSystemItemModel
        {
            Name = "Data",
            FullPath = @"C:\Data",
            IsDirectory = true,
            SizeBytes = 635L * 1024L * 1024L
        };

        var media = new FileSystemItemModel
        {
            Name = "Media",
            FullPath = @"C:\Data\Media",
            ParentPath = @"C:\Data",
            IsDirectory = true,
            SizeBytes = 120L * 1024L * 1024L
        };
        media.Children.Add(new FileSystemItemModel
        {
            Name = "clip.mp4",
            Extension = ".mp4",
            FullPath = @"C:\Data\Media\clip.mp4",
            SizeBytes = 100L * 1024L * 1024L,
            LastModified = new DateTime(2026, 3, 1)
        });
        media.Children.Add(new FileSystemItemModel
        {
            Name = "song.mp3",
            Extension = ".mp3",
            FullPath = @"C:\Data\Media\song.mp3",
            SizeBytes = 20L * 1024L * 1024L,
            LastModified = new DateTime(2026, 2, 1)
        });

        var docs = new FileSystemItemModel
        {
            Name = "Docs",
            FullPath = @"C:\Data\Docs",
            ParentPath = @"C:\Data",
            IsDirectory = true,
            SizeBytes = 15L * 1024L * 1024L
        };
        docs.Children.Add(new FileSystemItemModel
        {
            Name = "report.pdf",
            Extension = ".pdf",
            FullPath = @"C:\Data\Docs\report.pdf",
            SizeBytes = 10L * 1024L * 1024L,
            LastModified = new DateTime(2026, 1, 15)
        });
        docs.Children.Add(new FileSystemItemModel
        {
            Name = "readme.txt",
            Extension = ".txt",
            FullPath = @"C:\Data\Docs\readme.txt",
            SizeBytes = 5L * 1024L * 1024L,
            LastModified = new DateTime(2026, 1, 1)
        });

        var backup = new FileSystemItemModel
        {
            Name = "backup.iso",
            Extension = ".iso",
            FullPath = @"C:\Data\backup.iso",
            ParentPath = @"C:\Data",
            SizeBytes = 500L * 1024L * 1024L,
            LastModified = new DateTime(2026, 4, 1)
        };

        root.Children.Add(media);
        root.Children.Add(docs);
        root.Children.Add(backup);

        return root;
    }

    [Fact]
    public void HierarchyViewModel_SortingAndFiltering_UpdatesFilteredTreeWithoutRescanning()
    {
        var vm = new HierarchyViewModel(_treeService, _logger);
        var tree = CreateSampleTree();

        vm.LoadScanData(tree);

        Assert.True(vm.HasData);
        Assert.NotNull(vm.FilteredRoot);
        Assert.Equal(3, vm.FilteredRoot?.Children.Count);

        // 1. Uji Penyaringan Ekstensi (.mp4)
        vm.FilterExtension = ".mp4";

        Assert.NotNull(vm.FilteredRoot);
        // Docs dan backup.iso harus terfilter keluar, hanya Media yang tersisa
        Assert.Single(vm.FilteredRoot?.Children ?? []);
        Assert.Equal("Media", vm.FilteredRoot?.Children[0].Name);
        Assert.Equal(100L * 1024L * 1024L, vm.FilteredRoot?.SizeBytes);

        // 2. Uji Batas Ukuran Minimum (> 50 MB)
        vm.FilterExtension = "Semua";
        vm.SelectedSizeOption = vm.SizeOptions.First(o => o.Label == "> 50 MB");

        // Yang lolos > 50 MB: clip.mp4 (100 MB) dan backup.iso (500 MB)
        // Total = 600 MB
        Assert.Equal(600L * 1024L * 1024L, vm.FilteredRoot?.SizeBytes);
        Assert.Contains(vm.FilteredRoot?.Children ?? [], c => c.Name == "Media");
        Assert.Contains(vm.FilteredRoot?.Children ?? [], c => c.Name == "backup.iso");
        Assert.DoesNotContain(vm.FilteredRoot?.Children ?? [], c => c.Name == "Docs");

        // 3. Uji Reset Filter
        vm.ResetFilterCommand.Execute(null);
        Assert.Equal(635L * 1024L * 1024L, vm.FilteredRoot?.SizeBytes);
        Assert.Equal(3, vm.FilteredRoot?.Children.Count);
    }

    [Fact]
    public void LargeFilesViewModel_ExtractsAndRanksLargeFilesCorrectly()
    {
        var vm = new LargeFilesViewModel(_treeService, _logger);
        var tree = CreateSampleTree();

        // Atur ambang batas ke Semua Ukuran untuk pengujian seluruh berkas
        vm.SelectedSizeOption = vm.SizeOptions.First(o => o.Label == "Semua Ukuran");
        vm.LoadScanData(tree);

        Assert.True(vm.HasData);
        Assert.Equal(5, vm.Files.Count);

        // Peringkat 1: backup.iso (500 MB)
        Assert.Equal(1, vm.Files[0].Rank);
        Assert.Equal("backup.iso", vm.Files[0].Name);
        Assert.Equal("backup.iso", vm.LargestFileName);

        // Peringkat 2: clip.mp4 (100 MB)
        Assert.Equal(2, vm.Files[1].Rank);
        Assert.Equal("clip.mp4", vm.Files[1].Name);

        // Peringkat 3: song.mp3 (20 MB)
        Assert.Equal(3, vm.Files[2].Rank);

        // Peringkat 4: report.pdf (10 MB)
        Assert.Equal(4, vm.Files[3].Rank);

        // Peringkat 5: readme.txt (5 MB)
        Assert.Equal(5, vm.Files[4].Rank);

        // Uji Filter Batas Ukuran > 50 MB
        vm.SelectedSizeOption = vm.SizeOptions.First(o => o.Label == "> 50 MB");
        Assert.Equal(2, vm.Files.Count);
        Assert.Equal("backup.iso", vm.Files[0].Name);
        Assert.Equal("clip.mp4", vm.Files[1].Name);

        // Uji Seleksi Item
        vm.SelectedFile = vm.Files[0];
        Assert.NotNull(vm.SelectedFile);
        Assert.True(vm.BukaDiExplorerCommand.CanExecute(null));
    }

    [Fact]
    public void Consistency_TreemapAndLists_ShowConsistentResultsUnderSameFilters()
    {
        var tree = CreateSampleTree();

        var treemapVm = new TreemapViewModel(_treemapEngine, _logger, _treeService);
        var hierarchyVm = new HierarchyViewModel(_treeService, _logger);
        var largeFilesVm = new LargeFilesViewModel(_treeService, _logger);

        treemapVm.LoadScanData(tree);
        hierarchyVm.LoadScanData(tree);
        largeFilesVm.LoadScanData(tree);

        // Terapkan filter yang sama ke seluruh modul: Ekstensi ".mp4"
        treemapVm.FilterExtension = ".mp4";
        hierarchyVm.FilterExtension = ".mp4";
        largeFilesVm.FilterExtension = ".mp4";
        largeFilesVm.SelectedSizeOption = largeFilesVm.SizeOptions.First(o => o.Label == "Semua Ukuran");

        // 1. LargeFiles harus memuat tepat 1 berkas: clip.mp4 (100 MB)
        Assert.Single(largeFilesVm.Files);
        Assert.Equal("clip.mp4", largeFilesVm.Files[0].Name);
        Assert.Equal(100L * 1024L * 1024L, largeFilesVm.Files[0].SizeBytes);

        // 2. Hierarchy harus menyisakan hanya Media yang memuat clip.mp4 (100 MB)
        Assert.Single(hierarchyVm.FilteredRoot?.Children ?? []);
        var filteredFolder = hierarchyVm.FilteredRoot?.Children[0];
        Assert.Equal("Media", filteredFolder?.Name);
        Assert.Equal(100L * 1024L * 1024L, filteredFolder?.SizeBytes);

        // 3. Treemap pada tingkat akar harus menyisakan hanya Media (100 MB)
        Assert.Single(treemapVm.Nodes);
        Assert.Equal("Media", treemapVm.Nodes[0].Name);
        Assert.Equal(100L * 1024L * 1024L, treemapVm.Nodes[0].SizeBytes);

        // Masuk (drill down) ke Media pada Treemap
        treemapVm.DrillDownCommand.Execute(treemapVm.Nodes[0]);
        Assert.Single(treemapVm.Nodes);
        Assert.Equal("clip.mp4", treemapVm.Nodes[0].Name);
        Assert.Equal(100L * 1024L * 1024L, treemapVm.Nodes[0].SizeBytes);

        // Seluruh komponen menghasilkan nilai bita dan struktur yang 100% konsisten!
    }

    [Fact]
    public void Sorting_NameAndDate_OrdersCorrectlyInLargeFiles()
    {
        var tree = CreateSampleTree();
        var vm = new LargeFilesViewModel(_treeService, _logger);
        vm.SelectedSizeOption = vm.SizeOptions.First(o => o.Label == "Semua Ukuran");
        vm.LoadScanData(tree);

        // Urutkan berdasarkan Nama Menaik (A-Z)
        vm.SelectedSortField = SortField.Nama;
        vm.SelectedSortDirection = SortDirection.Menaik;

        Assert.Equal("backup.iso", vm.Files[0].Name);
        Assert.Equal("clip.mp4", vm.Files[1].Name);
        Assert.Equal("readme.txt", vm.Files[2].Name);
        Assert.Equal("report.pdf", vm.Files[3].Name);
        Assert.Equal("song.mp3", vm.Files[4].Name);

        // Urutkan berdasarkan Tanggal Diubah Menurun (Terbaru ke Terlama)
        vm.SelectedSortField = SortField.TanggalDiubah;
        vm.SelectedSortDirection = SortDirection.Menurun;

        // 2026-04-01 (backup.iso), 2026-03-01 (clip.mp4), 2026-02-01 (song.mp3), 2026-01-15 (report.pdf), 2026-01-01 (readme.txt)
        Assert.Equal("backup.iso", vm.Files[0].Name);
        Assert.Equal("clip.mp4", vm.Files[1].Name);
        Assert.Equal("song.mp3", vm.Files[2].Name);
        Assert.Equal("report.pdf", vm.Files[3].Name);
        Assert.Equal("readme.txt", vm.Files[4].Name);
    }
}
