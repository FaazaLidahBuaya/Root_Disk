using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class DuplicateFinderServiceTests : IDisposable
{
    private readonly string _testRoot;

    public DuplicateFinderServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Root_DuplicateTests_" + Guid.NewGuid().ToString("N"));
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
            // Abaikan kesalahan penghapusan direktori sementara
        }
    }

    [Fact]
    public async Task FindDuplicates_IdenticalFiles_DetectedInSameGroup()
    {
        // 1. Buat 3 berkas dengan isi dan ukuran yang benar-benar identik
        byte[] contentA = Encoding.UTF8.GetBytes("Dokumen Konten Identik ROOT Fase 5 1234567890!");
        string fileA1 = Path.Combine(_testRoot, "docA1.txt");
        string fileA2 = Path.Combine(_testRoot, "docA2.txt");
        string fileA3 = Path.Combine(_testRoot, "docA3.txt");
        await File.WriteAllBytesAsync(fileA1, contentA);
        await File.WriteAllBytesAsync(fileA2, contentA);
        await File.WriteAllBytesAsync(fileA3, contentA);

        // 2. Buat 1 berkas unik dengan ukuran berbeda
        byte[] contentB = Encoding.UTF8.GetBytes("Konten Berbeda Unik.");
        string fileB = Path.Combine(_testRoot, "docB.txt");
        await File.WriteAllBytesAsync(fileB, contentB);

        var logger = new LoggerService();
        var service = new DuplicateFinderService(logger);

        var result = await service.FindDuplicatesInDirectoryAsync(_testRoot);

        Assert.False(result.IsCancelled);
        Assert.Equal(4, result.TotalScannedFiles);
        Assert.Equal(1, result.TotalDuplicateGroups);
        Assert.Equal(3, result.TotalDuplicateFiles);

        var group = result.DuplicateGroups[0];
        Assert.Equal(contentA.Length, group.FileSizeBytes);
        Assert.Equal(3, group.FileCount);
        Assert.Equal(contentA.Length * 2, group.WasteSizeBytes); // 2 salinan berlebih

        // Verifikasi hash SHA-256 yang dihitung
        using var sha256 = SHA256.Create();
        string expectedHash = Convert.ToHexStringLower(sha256.ComputeHash(contentA));
        Assert.Equal(expectedHash, group.Sha256Hash);

        var groupPaths = group.Files.Select(f => f.FullPath).ToList();
        Assert.Contains(fileA1, groupPaths);
        Assert.Contains(fileA2, groupPaths);
        Assert.Contains(fileA3, groupPaths);
        Assert.DoesNotContain(fileB, groupPaths);
    }

    [Fact]
    public async Task FindDuplicates_SameSizeDifferentContent_NotDetectedAsDuplicates()
    {
        // Buat 2 berkas dengan ukuran bita PERSIS SAMA (32 bita), tetapi isi berbeda
        byte[] data1 = new byte[32];
        byte[] data2 = new byte[32];
        Array.Fill(data1, (byte)'A');
        Array.Fill(data2, (byte)'B');

        string file1 = Path.Combine(_testRoot, "same_size_A.dat");
        string file2 = Path.Combine(_testRoot, "same_size_B.dat");
        await File.WriteAllBytesAsync(file1, data1);
        await File.WriteAllBytesAsync(file2, data2);

        var service = new DuplicateFinderService();
        var result = await service.FindDuplicatesInDirectoryAsync(_testRoot);

        // Terdeteksi sebagai kandidat pada tahap ukuran, tetapi tereliminasi pada verifikasi SHA-256
        Assert.Equal(2, result.TotalScannedFiles);
        Assert.Equal(2, result.TotalCandidateFiles);
        Assert.Equal(0, result.TotalDuplicateGroups);
        Assert.Equal(0, result.TotalDuplicateFiles);
        Assert.Equal(0, result.TotalWasteBytes);
    }

    [Fact]
    public async Task FindDuplicates_ZeroByteFiles_HandledCorrectly_WhenIncludedAndExcluded()
    {
        string empty1 = Path.Combine(_testRoot, "empty1.tmp");
        string empty2 = Path.Combine(_testRoot, "empty2.tmp");
        string empty3 = Path.Combine(_testRoot, "empty3.tmp");
        await File.WriteAllBytesAsync(empty1, []);
        await File.WriteAllBytesAsync(empty2, []);
        await File.WriteAllBytesAsync(empty3, []);

        var service = new DuplicateFinderService();

        // Kasus A: IncludeZeroByteFiles = false (default)
        var optionsExclude = new DuplicateSearchOptions { IncludeZeroByteFiles = false };
        var resultExclude = await service.FindDuplicatesInDirectoryAsync(_testRoot, optionsExclude);
        Assert.Equal(0, resultExclude.TotalDuplicateGroups);

        // Kasus B: IncludeZeroByteFiles = true
        var optionsInclude = new DuplicateSearchOptions { IncludeZeroByteFiles = true };
        var resultInclude = await service.FindDuplicatesInDirectoryAsync(_testRoot, optionsInclude);
        Assert.Equal(1, resultInclude.TotalDuplicateGroups);
        Assert.Equal(3, resultInclude.TotalDuplicateFiles);
        Assert.Equal(0, resultInclude.DuplicateGroups[0].FileSizeBytes);
        Assert.Equal(0, resultInclude.TotalWasteBytes); // Berkas kosong tidak memboroskan ruang
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", resultInclude.DuplicateGroups[0].Sha256Hash);
    }

    [Fact]
    public async Task FindDuplicates_LargeFiles_PartialHashFilterAndStreaming()
    {
        // Buat 3 berkas 128 KB (di atas threshold 64 KB):
        // 2 berkas identik, 1 berkas dengan ukuran sama tetapi header berbeda
        int fileSize = 128 * 1024;
        byte[] largeA = new byte[fileSize];
        byte[] largeB = new byte[fileSize];
        var rand = new Random(42);
        rand.NextBytes(largeA);
        Array.Copy(largeA, largeB, fileSize);
        largeB[10] ^= 0xFF; // Ubah sedikit pada header sehingga partial hash berbeda

        string fileA1 = Path.Combine(_testRoot, "largeA1.bin");
        string fileA2 = Path.Combine(_testRoot, "largeA2.bin");
        string fileB = Path.Combine(_testRoot, "largeB.bin");
        await File.WriteAllBytesAsync(fileA1, largeA);
        await File.WriteAllBytesAsync(fileA2, largeA);
        await File.WriteAllBytesAsync(fileB, largeB);

        var service = new DuplicateFinderService();
        var options = new DuplicateSearchOptions { LargeFileThresholdBytes = 64 * 1024 };

        var result = await service.FindDuplicatesInDirectoryAsync(_testRoot, options);

        Assert.Equal(1, result.TotalDuplicateGroups);
        Assert.Equal(2, result.TotalDuplicateFiles);
        Assert.Equal(fileSize, result.DuplicateGroups[0].FileSizeBytes);
        Assert.Equal(fileSize, result.TotalWasteBytes);
        Assert.True(result.TotalHashedBytes > 0);
    }

    [Fact]
    public async Task FindDuplicates_FileChangesDuringInspection_HandledGracefully()
    {
        // Simulasikan berkas yang ukurannya berubah saat pemeriksaan
        string testFile = Path.Combine(_testRoot, "changing_file.txt");
        await File.WriteAllTextAsync(testFile, "Isi awal berkas 12345");

        var fileItem = new FileSystemItemModel
        {
            FullPath = testFile,
            Name = "changing_file.txt",
            SizeBytes = 99999, // Ukuran kandidat palsu/lama sebelum berkas berubah
            IsDirectory = false
        };

        var service = new DuplicateFinderService();
        var result = await service.FindDuplicatesAsync([fileItem, fileItem]);

        // Harus mendeteksi perubahan ukuran berkas dan mencatat error tanpa crash
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("berubah") || e.FriendlyType == "Gagal Akses");
        Assert.Equal(0, result.TotalDuplicateGroups);
    }

    [Fact]
    public async Task FindDuplicates_CancelledByToken_SetsIsCancelled()
    {
        for (int i = 0; i < 10; i++)
        {
            string f = Path.Combine(_testRoot, $"dup_{i}.txt");
            await File.WriteAllTextAsync(f, "Konten Sama");
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Batalkan sebelum proses dimulai

        var service = new DuplicateFinderService();
        var result = await service.FindDuplicatesInDirectoryAsync(_testRoot, null, null, cts.Token);

        Assert.True(result.IsCancelled);
    }

    [Fact]
    public async Task SafetyAudit_NeverDeletesAnyFilesAutomatically()
    {
        // Buat berkas duplikat
        string f1 = Path.Combine(_testRoot, "safe1.txt");
        string f2 = Path.Combine(_testRoot, "safe2.txt");
        await File.WriteAllTextAsync(f1, "Konten Duplikat Aman");
        await File.WriteAllTextAsync(f2, "Konten Duplikat Aman");

        var preScanTime1 = File.GetLastWriteTimeUtc(f1);
        var preScanTime2 = File.GetLastWriteTimeUtc(f2);

        var service = new DuplicateFinderService();
        var result = await service.FindDuplicatesInDirectoryAsync(_testRoot);

        Assert.Equal(1, result.TotalDuplicateGroups);

        // AUDIT KEAMANAN: Pastikan kedua berkas masih ada di disk dan tidak terhapus
        Assert.True(File.Exists(f1), "Berkas safe1.txt harus tetap ada.");
        Assert.True(File.Exists(f2), "Berkas safe2.txt harus tetap ada.");
        Assert.Equal(preScanTime1, File.GetLastWriteTimeUtc(f1));
        Assert.Equal(preScanTime2, File.GetLastWriteTimeUtc(f2));
    }

    [Fact]
    public async Task PerformanceMeasurement_TracksDurationAndThroughput()
    {
        byte[] chunk = new byte[32 * 1024];
        new Random(123).NextBytes(chunk);

        string f1 = Path.Combine(_testRoot, "bench1.bin");
        string f2 = Path.Combine(_testRoot, "bench2.bin");
        await File.WriteAllBytesAsync(f1, chunk);
        await File.WriteAllBytesAsync(f2, chunk);

        var service = new DuplicateFinderService();
        var result = await service.FindDuplicatesInDirectoryAsync(_testRoot);

        Assert.True(result.Duration >= TimeSpan.Zero);
        Assert.True(result.TotalHashedBytes >= chunk.Length * 2);
        Assert.NotNull(result.FormattedThroughput);
        Assert.Contains("/s", result.FormattedThroughput);

        // Verifikasi laporan ringkasan Markdown memuat metrik kinerja
        var markdown = result.GenerateSummaryMarkdown();
        Assert.Contains("Laporan Temuan Berkas Duplikat", markdown);
        Assert.Contains("SHA-256", markdown);
        Assert.Contains("Estimasi Ruang Boros", markdown);
    }
}
