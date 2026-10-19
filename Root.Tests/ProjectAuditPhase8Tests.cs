using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

/// <summary>
/// Pengujian Audit Kualitas, Keamanan, Ketahanan Sumber Daya, dan Privasi Fase 8 ROOT.
/// </summary>
public class ProjectAuditPhase8Tests
{
    [Fact]
    public void PrivacyAudit_EnsuresZeroOutboundNetworkCallsOrTelemetry()
    {
        // Cari direktori solusi Root
        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Root.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        var codeFiles = Directory.EnumerateFiles(dir.FullName, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(dir.FullName, "*.xaml", SearchOption.AllDirectories))
            .Where(f => !f.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains("ProjectAuditPhase8Tests.cs", StringComparison.OrdinalIgnoreCase)) // abaikan pengujian ini sendiri
            .ToList();

        Assert.NotEmpty(codeFiles);

        // Pola pemicu jaringan, telemetri, atau transmisi data keluar
        var forbiddenPatterns = new[]
        {
            @"\bHttpClient\b",
            @"\bWebClient\b",
            @"\bHttpWebRequest\b",
            @"\bSystem\.Net\.Sockets\b",
            @"\bTelemetryClient\b",
            @"\bAppCenter\b",
            @"\bGoogleAnalytics\b",
            @"https?://(?!(schemas\.microsoft\.com|schemas\.openxmlformats\.org))" // Izinkan hanya skema standar XML WPF XAML
        };

        var violations = new List<string>();

        foreach (var file in codeFiles)
        {
            string content = File.ReadAllText(file);
            foreach (var pattern in forbiddenPatterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    violations.Add($"{Path.GetFileName(file)}: terdeteksi pola terlarang '{pattern}' ({matches.Count} kemunculan)");
                }
            }
        }

        // Asersi: Harus 100% bebas dari pemanggilan jaringan dan telemetri (Privasi Total & Offline First)
        Assert.Empty(violations);
    }

    [Fact]
    public async Task CancellationAudit_DuplicateFinder_CancelsPromptlyWithoutLeakingStreams()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Audit_Dup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Buat berkas tiruan
            for (int i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file_{i}.txt"), new string('A', 5000));
            }

            var service = new DuplicateFinderService();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Batalkan sebelum mulai

            var result = await service.FindDuplicatesInDirectoryAsync(tempDir, cancellationToken: cts.Token);

            Assert.True(result.IsCancelled);
            Assert.Empty(result.DuplicateGroups);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ResourceDisposalAudit_SqliteRepository_CanDeleteAndRecreateDbWithoutLocks()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), "Root_Audit_Sqlite_" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            var repo = new SqliteScanHistoryRepository(tempDb);
            await repo.InitializeAsync();

            var record = new ScanHistoryRecordModel
            {
                TargetPath = @"C:\Test",
                TargetName = "Test",
                ScannedAt = DateTime.UtcNow,
                Duration = TimeSpan.FromSeconds(2),
                TotalSizeBytes = 1024,
                TotalFiles = 5,
                TotalDirectories = 1,
                Status = ScanStatus.Selesai,
                DriveCapacityBytes = 1000000,
                DriveFreeBytes = 500000,
                DriveUsedBytes = 500000
            };

            await repo.SaveRecordAsync(record);
            var history = await repo.GetHistoryAsync(10);
            Assert.Single(history);

            // Microsoft.Data.Sqlite mengizinkan penghapusan setelah koneksi using selesai atau pool dibersihkan
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Assert.True(File.Exists(tempDb));

            File.Delete(tempDb);
            Assert.False(File.Exists(tempDb));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task SafetyAudit_CleanupExecution_ProtectsSystemDirectoryAndReportsBlocked()
    {
        var protection = new SystemProtectionService();
        var execution = new CleanupExecutionService(protection);

        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string criticalFile = Path.Combine(winDir, "System32", "kernel32.dll");

        var candidate = new CleanupCandidateModel(
            criticalFile,
            "kernel32.dll",
            Path.Combine(winDir, "System32"),
            ".dll",
            CleanupCategory.BerkasSementara,
            "Pembersihan uji coba",
            1048576,
            DateTime.Now.AddDays(-50),
            CleanupSafetyLevel.TerproteksiSistem,
            isProtected: true);

        var result = await execution.ExecuteCleanupAsync([candidate]);

        // Verifikasi mutlak: Tidak pernah terhapus, terblokir oleh proteksi sistem
        Assert.Equal(0, result.TotalDeletedCount);
        Assert.Equal(1, result.BlockedProtectedCount);
        Assert.Single(result.ItemResults);
        Assert.Equal(CleanupItemStatus.DitolakProteksiSistem, result.ItemResults[0].Status);
    }

    [Fact]
    public void TreemapLayoutAudit_ZeroOverlapAndStrictBoundingBox()
    {
        var engine = new SquarifiedTreemapEngine();
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "A", SizeBytes = 1000 },
            new() { Name = "B", SizeBytes = 2000 },
            new() { Name = "C", SizeBytes = 3000 },
            new() { Name = "D", SizeBytes = 4000 },
            new() { Name = "E", SizeBytes = 0 }, // 0-byte item
            new() { Name = "F", SizeBytes = 500 }
        };

        double width = 800;
        double height = 600;

        var layout = engine.CalculateLayout(items, width, height);

        Assert.Equal(items.Count, layout.Count);

        foreach (var node in layout)
        {
            // Periksa batas kanvas
            Assert.True(node.X >= 0, $"X ({node.X}) harus >= 0");
            Assert.True(node.Y >= 0, $"Y ({node.Y}) harus >= 0");
            Assert.True(node.X + node.Width <= width + 0.01, $"X+Width ({node.X + node.Width}) melebihi container ({width})");
            Assert.True(node.Y + node.Height <= height + 0.01, $"Y+Height ({node.Y + node.Height}) melebihi container ({height})");
            Assert.True(node.Width >= 0);
            Assert.True(node.Height >= 0);
        }

        // Periksa bahwa tidak ada overlap antar node
        for (int i = 0; i < layout.Count; i++)
        {
            for (int j = i + 1; j < layout.Count; j++)
            {
                var n1 = layout[i];
                var n2 = layout[j];

                bool horizontalOverlap = (n1.X < n2.X + n2.Width - 0.001) && (n1.X + n1.Width - 0.001 > n2.X);
                bool verticalOverlap = (n1.Y < n2.Y + n2.Height - 0.001) && (n1.Y + n1.Height - 0.001 > n2.Y);

                Assert.False(horizontalOverlap && verticalOverlap, $"Node '{n1.Name}' dan '{n2.Name}' mengalami tumpang tindih!");
            }
        }
    }

    [Fact]
    public async Task F01_ReadOnlyFileRollback_WhenCleanupFails_RestoresOriginalAttribute()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Audit_F01_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "readonly_locked.tmp");

        try
        {
            File.WriteAllText(testFile, "Temporary data for F-01 rollback test");
            File.SetAttributes(testFile, FileAttributes.ReadOnly);

            // Buka file stream eksklusif agar operasi File.Delete gagal karena terkunci
            using (var lockStream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var execution = new CleanupExecutionService();
                var fi = new FileInfo(testFile);
                var candidate = new CleanupCandidateModel(
                    testFile,
                    "readonly_locked.tmp",
                    tempDir,
                    ".tmp",
                    CleanupCategory.BerkasSementara,
                    "Uji rollback atribut",
                    fi.Length,
                    fi.LastWriteTime,
                    CleanupSafetyLevel.AmanKonservatif,
                    isProtected: false);

                var result = await execution.ExecuteCleanupAsync([candidate]);

                // Eksekusi harus gagal karena berkas terkunci
                Assert.Equal(0, result.TotalDeletedCount);
                Assert.Equal(1, result.FailedLockedCount);
                Assert.True(File.Exists(testFile));

                // F-01 Verifikasi: Atribut ReadOnly HARUS dipulihkan (tidak boleh ditinggalkan tanpa ReadOnly)
                var currentAttrs = File.GetAttributes(testFile);
                Assert.True((currentAttrs & FileAttributes.ReadOnly) != 0, "Atribut ReadOnly harus dipulihkan saat penghapusan gagal!");
            }
        }
        finally
        {
            if (File.Exists(testFile))
            {
                try
                {
                    File.SetAttributes(testFile, FileAttributes.Normal);
                    File.Delete(testFile);
                }
                catch { }
            }
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task F03_DeepDirectoryTraversal_IterativeHeapStack_PreventsStackOverflow()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Root_Audit_F03_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Buat struktur direktori bertingkat sangat dalam (40 level bertingkat)
            string currentPath = tempDir;
            const int depth = 40;
            for (int i = 0; i < depth; i++)
            {
                currentPath = Path.Combine(currentPath, $"level_{i}");
                Directory.CreateDirectory(currentPath);
                File.WriteAllText(Path.Combine(currentPath, $"file_{i}.txt"), $"Content {i}");
            }

            var scanner = new FileSystemScanner();
            var report = await scanner.ScanAsync(tempDir, null, CancellationToken.None);

            // Verifikasi pemindaian selesai sepenuhnya tanpa StackOverflow
            Assert.Equal(ScanStatus.Selesai, report.Status);
            Assert.False(report.IsPartial);
            Assert.Equal(depth, report.TotalFilesCount);
            Assert.Equal(depth + 1, report.TotalDirectoriesCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void F08_ValueConverters_ConvertBack_ReturnsBindingDoNothingWithoutException()
    {
        var nullConverter = new Root.App.Converters.NullToVisibilityConverter();
        var nullResult = nullConverter.ConvertBack(System.Windows.Visibility.Visible, typeof(string), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, nullResult);

        var logConverter = new Root.App.Converters.LogLevelToBrushConverter();
        var logResult = logConverter.ConvertBack(null, typeof(LogLevel), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, logResult);

        var statusConverter = new Root.App.Converters.StatusToBrushConverter();
        var statusResult = statusConverter.ConvertBack(null, typeof(ScanStatus), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, statusResult);

        var hexConverter = new Root.App.Converters.HexToBrushConverter();
        var hexResult = hexConverter.ConvertBack(null, typeof(string), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, hexResult);

        var dimConverter = new Root.App.Converters.DimensionToVisibilityConverter();
        var dimResult = dimConverter.ConvertBack(System.Windows.Visibility.Visible, new Type[] { typeof(double), typeof(double) }, string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2, dimResult.Length);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, dimResult[0]);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, dimResult[1]);
    }
}
