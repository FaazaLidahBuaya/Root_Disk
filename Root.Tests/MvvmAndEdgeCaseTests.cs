using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Mvvm;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class MvvmAndEdgeCaseTests : IDisposable
{
    private readonly string _testRoot;

    public MvvmAndEdgeCaseTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "RootEdgeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch
        {
            // Abaikan kesalahan penghapusan folder sementara
        }
    }

    [Fact]
    public void RelayCommand_ExecutesActionAndReportsCanExecute()
    {
        bool executed = false;
        bool canRun = true;

        var cmd = new RelayCommand(() => executed = true, () => canRun);

        Assert.True(cmd.CanExecute(null));
        cmd.Execute(null);
        Assert.True(executed);

        canRun = false;
        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public async Task AsyncRelayCommand_PreventsConcurrentExecution()
    {
        int runCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        var cmd = new AsyncRelayCommand(async () =>
        {
            runCount++;
            await tcs.Task;
        });

        Assert.True(cmd.CanExecute(null));
        var runTask = cmd.ExecuteAsync(null);

        // Saat masih berjalan, CanExecute harus false
        Assert.False(cmd.CanExecute(null));

        tcs.SetResult(true);
        await runTask;

        // Setelah selesai, CanExecute harus kembali true
        Assert.True(cmd.CanExecute(null));
        Assert.Equal(1, runCount);
    }

    [Fact]
    public void DriveInfoModel_ComputesUsedSizeAndPercentageProperly()
    {
        var model = new DriveInfoModel
        {
            Name = @"C:\",
            VolumeLabel = "Sistem",
            DriveType = "Fixed",
            DriveFormat = "NTFS",
            TotalSizeBytes = 1000L * 1024L * 1024L, // 1000 MB
            FreeSpaceBytes = 250L * 1024L * 1024L,  // 250 MB
            IsReady = true
        };

        Assert.Equal(750L * 1024L * 1024L, model.UsedSizeBytes);
        Assert.Equal(75.0, model.UsedPercentage);
        Assert.Contains("Sistem", model.DisplayName);
    }

    [Fact]
    public async Task FileSystemScanner_DeepHierarchy_CalculatesAllLevelsCorrectly()
    {
        // Susun struktur dalam (5 level)
        string current = _testRoot;
        for (int i = 1; i <= 5; i++)
        {
            current = Path.Combine(current, $"Level_{i}");
            Directory.CreateDirectory(current);
            File.WriteAllBytes(Path.Combine(current, $"file_lvl_{i}.dat"), new byte[100 * i]);
        }

        var scanner = new FileSystemScanner();
        var result = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);

        Assert.Equal(ScanStatus.Selesai, result.Status);
        Assert.Equal(5, result.TotalFilesCount);
        // 100 + 200 + 300 + 400 + 500 = 1500 bytes
        Assert.Equal(1500, result.TotalSizeBytes);
    }

    [Fact]
    public async Task FileSystemScanner_UnicodeFilenames_HandledWithoutError()
    {
        string specialName = "Laporan_Keuangan_Pengujian_äöü_日本語_2026.dat";
        string filePath = Path.Combine(_testRoot, specialName);
        File.WriteAllBytes(filePath, new byte[256]);

        var scanner = new FileSystemScanner();
        var result = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);

        Assert.Equal(ScanStatus.Selesai, result.Status);
        Assert.Equal(1, result.TotalFilesCount);
        Assert.Equal(256, result.TotalSizeBytes);
        Assert.Equal(specialName, result.TopLargestFiles[0].Name);
    }

    [Fact]
    public async Task FileSystemScanner_MultipleConsecutiveScans_YieldConsistentResults()
    {
        File.WriteAllBytes(Path.Combine(_testRoot, "file1.txt"), new byte[500]);

        var scanner = new FileSystemScanner();
        
        var result1 = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);
        Assert.Equal(1, result1.TotalFilesCount);
        Assert.Equal(500, result1.TotalSizeBytes);

        // Tambah berkas kedua
        File.WriteAllBytes(Path.Combine(_testRoot, "file2.txt"), new byte[300]);

        var result2 = await scanner.ScanAsync(_testRoot, null, CancellationToken.None);
        Assert.Equal(2, result2.TotalFilesCount);
        Assert.Equal(800, result2.TotalSizeBytes);
    }
}
