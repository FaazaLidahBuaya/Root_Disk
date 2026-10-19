using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.App.ViewModels;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class DuplicateFinderViewModelTests
{
    private class MockDuplicateService(DuplicateSearchResult resultToReturn) : IDuplicateFinderService
    {
        public bool FindCalled { get; private set; }

        public Task<DuplicateSearchResult> FindDuplicatesAsync(
            IEnumerable<FileSystemItemModel> fileItems,
            DuplicateSearchOptions? options = null,
            IProgress<DuplicateProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            FindCalled = true;
            return Task.FromResult(resultToReturn);
        }

        public Task<DuplicateSearchResult> FindDuplicatesInDirectoryAsync(
            string directoryPath,
            DuplicateSearchOptions? options = null,
            IProgress<DuplicateProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            FindCalled = true;
            return Task.FromResult(resultToReturn);
        }
    }

    [Fact]
    public void LoadScanData_UpdatesPropertiesAndEnablesSearch()
    {
        var logger = new LoggerService();
        var mockService = new MockDuplicateService(new DuplicateSearchResult());
        var vm = new DuplicateFinderViewModel(mockService, logger);

        Assert.False(vm.HasScanDataAvailable);
        Assert.False(vm.CanStartSearch);

        var rootItem = new FileSystemItemModel
        {
            FullPath = @"C:\TestFolder",
            Name = "TestFolder",
            IsDirectory = true,
            FileCount = 10
        };

        vm.LoadScanData(rootItem);

        Assert.True(vm.HasScanDataAvailable);
        Assert.True(vm.CanStartSearch);
        Assert.True(vm.UseCurrentScanData);
        Assert.Equal(@"C:\TestFolder", vm.TargetDirectoryPath);
    }

    [Fact]
    public async Task MulaiPencarianAsync_PopulatesGroupsAndSelectsFirst()
    {
        var group = new DuplicateGroupModel
        {
            Sha256Hash = "abc123456789",
            FileSizeBytes = 1024,
            Files =
            [
                new() { FullPath = @"C:\dup1.txt", FileName = "dup1.txt", SizeBytes = 1024 },
                new() { FullPath = @"C:\dup2.txt", FileName = "dup2.txt", SizeBytes = 1024 }
            ]
        };

        var expectedResult = new DuplicateSearchResult
        {
            DuplicateGroups = [group],
            TotalScannedFiles = 5,
            TotalCandidateFiles = 2,
            Duration = TimeSpan.FromSeconds(1)
        };

        var logger = new LoggerService();
        var mockService = new MockDuplicateService(expectedResult);
        var vm = new DuplicateFinderViewModel(mockService, logger);

        vm.LoadScanData(new FileSystemItemModel { FullPath = @"C:\Test", Name = "Test", IsDirectory = true, FileCount = 5 });

        await vm.MulaiPencarianCommand.ExecuteAsync(null);

        Assert.True(mockService.FindCalled);
        Assert.True(vm.HasResults);
        Assert.Single(vm.DuplicateGroups);
        Assert.NotNull(vm.SelectedGroup);
        Assert.Equal("abc123456789", vm.SelectedGroup?.Sha256Hash);
        Assert.Equal(2, vm.SelectedGroup?.FileCount);
    }

    [Fact]
    public void BukaLokasiBerkas_WithEmptyOrMissingPath_DoesNotThrow()
    {
        var logger = new LoggerService();
        var mockService = new MockDuplicateService(new DuplicateSearchResult());
        var vm = new DuplicateFinderViewModel(mockService, logger, alertAction: (m, t, e) => { });

        var ex1 = Record.Exception(() => vm.BukaLokasiBerkasCommand.Execute(null));
        Assert.Null(ex1);

        var ex2 = Record.Exception(() => vm.BukaLokasiBerkasCommand.Execute(@"C:\FolderPastiTidakAda_123456\berkas.bin"));
        Assert.Null(ex2);
    }
}
