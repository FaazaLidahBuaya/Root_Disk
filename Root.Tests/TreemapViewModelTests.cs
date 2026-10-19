using System.Collections.Generic;
using System.Linq;
using Root.App.ViewModels;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class TreemapViewModelTests
{
    private readonly SquarifiedTreemapEngine _engine = new();
    private readonly LoggerService _logger = new();

    private FileSystemItemModel CreateSampleRealTree()
    {
        // Struktur pohon nyata:
        // C:\Target
        // ├── FolderA (1500 B)
        // │   ├── A1.txt (500 B)
        // │   └── A2.dat (1000 B)
        // ├── FolderB (4000 B)
        // │   ├── SubB1 (3000 B)
        // │   │   └── deep.bin (3000 B)
        // │   └── B1.txt (1000 B)
        // └── berkas_akar.iso (2500 B)
        // Total = 8000 B

        var root = new FileSystemItemModel
        {
            Name = "Target",
            FullPath = @"C:\Target",
            IsDirectory = true,
            SizeBytes = 8000
        };

        var folderA = new FileSystemItemModel
        {
            Name = "FolderA",
            FullPath = @"C:\Target\FolderA",
            ParentPath = @"C:\Target",
            IsDirectory = true,
            SizeBytes = 1500
        };
        folderA.Children.Add(new FileSystemItemModel { Name = "A1.txt", FullPath = @"C:\Target\FolderA\A1.txt", ParentPath = folderA.FullPath, SizeBytes = 500 });
        folderA.Children.Add(new FileSystemItemModel { Name = "A2.dat", FullPath = @"C:\Target\FolderA\A2.dat", ParentPath = folderA.FullPath, SizeBytes = 1000 });

        var folderB = new FileSystemItemModel
        {
            Name = "FolderB",
            FullPath = @"C:\Target\FolderB",
            ParentPath = @"C:\Target",
            IsDirectory = true,
            SizeBytes = 4000
        };
        var subB1 = new FileSystemItemModel
        {
            Name = "SubB1",
            FullPath = @"C:\Target\FolderB\SubB1",
            ParentPath = folderB.FullPath,
            IsDirectory = true,
            SizeBytes = 3000
        };
        subB1.Children.Add(new FileSystemItemModel { Name = "deep.bin", FullPath = @"C:\Target\FolderB\SubB1\deep.bin", ParentPath = subB1.FullPath, SizeBytes = 3000 });
        folderB.Children.Add(subB1);
        folderB.Children.Add(new FileSystemItemModel { Name = "B1.txt", FullPath = @"C:\Target\FolderB\B1.txt", ParentPath = folderB.FullPath, SizeBytes = 1000 });

        var fileRoot = new FileSystemItemModel
        {
            Name = "berkas_akar.iso",
            FullPath = @"C:\Target\berkas_akar.iso",
            ParentPath = @"C:\Target",
            SizeBytes = 2500
        };

        root.Children.Add(folderA);
        root.Children.Add(folderB);
        root.Children.Add(fileRoot);

        return root;
    }

    [Fact]
    public void LoadScanData_PopulatesRootAndInitialTreemapNodes()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var tree = CreateSampleRealTree();

        Assert.False(vm.HasData);
        vm.LoadScanData(tree);

        Assert.True(vm.HasData);
        Assert.False(vm.IsEmptyFolder);
        Assert.Equal("Target", vm.RootFolder?.Name);
        Assert.Equal("Target", vm.CurrentFolder?.Name);
        Assert.Single(vm.Breadcrumbs);
        Assert.Equal("Target", vm.Breadcrumbs[0].Name);

        // Di tingkat akar, terdapat 3 item: FolderA, FolderB, berkas_akar.iso
        Assert.Equal(3, vm.Nodes.Count);
        Assert.False(vm.CanNavigateUp);
    }

    [Fact]
    public void DrillDown_NavigatesIntoSubdirectoryAndUpdatesBreadcrumbs()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var tree = CreateSampleRealTree();
        vm.LoadScanData(tree);

        // Pilih FolderB untuk masuk (drill down)
        var folderBNode = vm.Nodes.First(n => n.Name == "FolderB");
        vm.DrillDownCommand.Execute(folderBNode);

        Assert.Equal("FolderB", vm.CurrentFolder?.Name);
        Assert.Equal(2, vm.Breadcrumbs.Count);
        Assert.Equal("Target", vm.Breadcrumbs[0].Name);
        Assert.Equal("FolderB", vm.Breadcrumbs[1].Name);
        Assert.True(vm.CanNavigateUp);

        // Di dalam FolderB terdapat 2 item: SubB1 dan B1.txt
        Assert.Equal(2, vm.Nodes.Count);
        Assert.Contains(vm.Nodes, n => n.Name == "SubB1");
        Assert.Contains(vm.Nodes, n => n.Name == "B1.txt");

        // Masuk lebih dalam ke SubB1
        var subB1Node = vm.Nodes.First(n => n.Name == "SubB1");
        vm.DrillDownCommand.Execute(subB1Node);

        Assert.Equal("SubB1", vm.CurrentFolder?.Name);
        Assert.Equal(3, vm.Breadcrumbs.Count);
        Assert.Single(vm.Nodes);
        Assert.Equal("deep.bin", vm.Nodes[0].Name);
    }

    [Fact]
    public void NavigateUp_MovesBackToParentDirectory()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var tree = CreateSampleRealTree();
        vm.LoadScanData(tree);

        var folderANode = vm.Nodes.First(n => n.Name == "FolderA");
        vm.DrillDownCommand.Execute(folderANode);

        Assert.Equal("FolderA", vm.CurrentFolder?.Name);
        Assert.True(vm.CanNavigateUp);

        // Naik satu tingkat
        vm.NavigateUpCommand.Execute(null);

        Assert.Equal("Target", vm.CurrentFolder?.Name);
        Assert.Single(vm.Breadcrumbs);
        Assert.False(vm.CanNavigateUp);
        Assert.Equal(3, vm.Nodes.Count);
    }

    [Fact]
    public void NavigateToBreadcrumb_JumpsDirectlyToAncestorFolder()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var tree = CreateSampleRealTree();
        vm.LoadScanData(tree);

        // Masuk ke FolderB -> SubB1
        var folderBNode = vm.Nodes.First(n => n.Name == "FolderB");
        vm.DrillDownCommand.Execute(folderBNode);
        var subB1Node = vm.Nodes.First(n => n.Name == "SubB1");
        vm.DrillDownCommand.Execute(subB1Node);

        Assert.Equal(3, vm.Breadcrumbs.Count);

        // Klik breadcrumb "Target" (akar)
        var rootBreadcrumb = vm.Breadcrumbs[0];
        vm.NavigateToBreadcrumbCommand.Execute(rootBreadcrumb);

        Assert.Equal("Target", vm.CurrentFolder?.Name);
        Assert.Single(vm.Breadcrumbs);
        Assert.Equal(3, vm.Nodes.Count);
    }

    [Fact]
    public void OnDimensionsChanged_RecalculatesNodeDimensionsToFitNewBounds()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var tree = CreateSampleRealTree();
        vm.LoadScanData(tree);

        // Awalnya default ukuran
        double initialTotalArea = vm.Nodes.Sum(n => n.Area);

        // Simulasikan resize jendela (misal perbesar ke 1200 x 900 = 1.080.000)
        vm.OnDimensionsChanged(1200, 900);

        Assert.Equal(3, vm.Nodes.Count);
        double newTotalArea = vm.Nodes.Sum(n => n.Area);
        Assert.True(newTotalArea > initialTotalArea);
        Assert.InRange(newTotalArea, 1200 * 900 - 10, 1200 * 900 + 10);

        foreach (var n in vm.Nodes)
        {
            Assert.True(n.X + n.Width <= 1200 + 0.1);
            Assert.True(n.Y + n.Height <= 900 + 0.1);
        }
    }

    [Fact]
    public void SelectNode_UpdatesSelectedNodeProperty()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var tree = CreateSampleRealTree();
        vm.LoadScanData(tree);

        Assert.Null(vm.SelectedNode);

        var targetNode = vm.Nodes[0];
        vm.SelectNodeCommand.Execute(targetNode);

        Assert.NotNull(vm.SelectedNode);
        Assert.Equal(targetNode.Name, vm.SelectedNode?.Name);
        Assert.True(vm.BukaDiExplorerCommand.CanExecute(null));
    }

    [Fact]
    public void EmptyDirectory_SetsIsEmptyFolderTrue()
    {
        var vm = new TreemapViewModel(_engine, _logger);
        var emptyRoot = new FileSystemItemModel
        {
            Name = "EmptyRoot",
            FullPath = @"C:\EmptyRoot",
            IsDirectory = true,
            SizeBytes = 0
        };

        vm.LoadScanData(emptyRoot);

        Assert.True(vm.HasData);
        Assert.True(vm.IsEmptyFolder);
        Assert.Empty(vm.Nodes);
    }
}
