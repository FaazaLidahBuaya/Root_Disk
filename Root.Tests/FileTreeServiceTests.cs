using System;
using System.Collections.Generic;
using System.Linq;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class FileTreeServiceTests
{
    private readonly FileTreeService _service = new();

    private FileSystemItemModel CreateTestTree()
    {
        // Struktur uji:
        // C:\Uji
        // ├── FolderVideo (70 MB)
        // │   ├── video1.mp4 (50 MB, 2026-01-01)
        // │   └── video2.mkv (20 MB, 2026-01-05)
        // ├── FolderDocs (5 MB)
        // │   ├── doc1.pdf (3 MB, 2026-02-01)
        // │   └── note.txt (2 MB, 2026-02-15)
        // ├── FolderKosong (0 B)
        // └── root_archive.zip (100 MB, 2026-03-01)
        // Total = 175 MB

        var root = new FileSystemItemModel
        {
            Name = "Uji",
            FullPath = @"C:\Uji",
            IsDirectory = true,
            SizeBytes = 175L * 1024L * 1024L
        };

        var folderVideo = new FileSystemItemModel
        {
            Name = "FolderVideo",
            FullPath = @"C:\Uji\FolderVideo",
            ParentPath = @"C:\Uji",
            IsDirectory = true,
            SizeBytes = 70L * 1024L * 1024L
        };
        folderVideo.Children.Add(new FileSystemItemModel
        {
            Name = "video1.mp4",
            Extension = ".mp4",
            FullPath = @"C:\Uji\FolderVideo\video1.mp4",
            SizeBytes = 50L * 1024L * 1024L,
            LastModified = new DateTime(2026, 1, 1)
        });
        folderVideo.Children.Add(new FileSystemItemModel
        {
            Name = "video2.mkv",
            Extension = ".mkv",
            FullPath = @"C:\Uji\FolderVideo\video2.mkv",
            SizeBytes = 20L * 1024L * 1024L,
            LastModified = new DateTime(2026, 1, 5)
        });

        var folderDocs = new FileSystemItemModel
        {
            Name = "FolderDocs",
            FullPath = @"C:\Uji\FolderDocs",
            ParentPath = @"C:\Uji",
            IsDirectory = true,
            SizeBytes = 5L * 1024L * 1024L
        };
        folderDocs.Children.Add(new FileSystemItemModel
        {
            Name = "doc1.pdf",
            Extension = ".pdf",
            FullPath = @"C:\Uji\FolderDocs\doc1.pdf",
            SizeBytes = 3L * 1024L * 1024L,
            LastModified = new DateTime(2026, 2, 1)
        });
        folderDocs.Children.Add(new FileSystemItemModel
        {
            Name = "note.txt",
            Extension = ".txt",
            FullPath = @"C:\Uji\FolderDocs\note.txt",
            SizeBytes = 2L * 1024L * 1024L,
            LastModified = new DateTime(2026, 2, 15)
        });

        var folderKosong = new FileSystemItemModel
        {
            Name = "FolderKosong",
            FullPath = @"C:\Uji\FolderKosong",
            ParentPath = @"C:\Uji",
            IsDirectory = true,
            SizeBytes = 0
        };

        var rootArchive = new FileSystemItemModel
        {
            Name = "root_archive.zip",
            Extension = ".zip",
            FullPath = @"C:\Uji\root_archive.zip",
            SizeBytes = 100L * 1024L * 1024L,
            LastModified = new DateTime(2026, 3, 1)
        };

        root.Children.Add(folderVideo);
        root.Children.Add(folderDocs);
        root.Children.Add(folderKosong);
        root.Children.Add(rootArchive);

        return root;
    }

    [Fact]
    public void Sort_BySize_SortsCorrectly()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "Sedang.bin", SizeBytes = 500 },
            new() { Name = "Besar.bin", SizeBytes = 2000 },
            new() { Name = "Kecil.bin", SizeBytes = 100 }
        };

        var descending = _service.Sort(items, SortField.Ukuran, SortDirection.Menurun, directoriesFirst: false);
        Assert.Equal("Besar.bin", descending[0].Name);
        Assert.Equal("Sedang.bin", descending[1].Name);
        Assert.Equal("Kecil.bin", descending[2].Name);

        var ascending = _service.Sort(items, SortField.Ukuran, SortDirection.Menaik, directoriesFirst: false);
        Assert.Equal("Kecil.bin", ascending[0].Name);
        Assert.Equal("Sedang.bin", ascending[1].Name);
        Assert.Equal("Besar.bin", ascending[2].Name);
    }

    [Fact]
    public void Sort_ByName_SortsAlphabetically()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "Charlie.txt" },
            new() { Name = "Alpha.txt" },
            new() { Name = "Bravo.txt" }
        };

        var asc = _service.Sort(items, SortField.Nama, SortDirection.Menaik, directoriesFirst: false);
        Assert.Equal("Alpha.txt", asc[0].Name);
        Assert.Equal("Bravo.txt", asc[1].Name);
        Assert.Equal("Charlie.txt", asc[2].Name);

        var desc = _service.Sort(items, SortField.Nama, SortDirection.Menurun, directoriesFirst: false);
        Assert.Equal("Charlie.txt", desc[0].Name);
        Assert.Equal("Bravo.txt", desc[1].Name);
        Assert.Equal("Alpha.txt", desc[2].Name);
    }

    [Fact]
    public void Sort_ByDateModified_SortsChronologically()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "Lama.txt", LastModified = new DateTime(2025, 1, 1) },
            new() { Name = "Baru.txt", LastModified = new DateTime(2026, 5, 1) },
            new() { Name = "Tengah.txt", LastModified = new DateTime(2026, 1, 1) }
        };

        var desc = _service.Sort(items, SortField.TanggalDiubah, SortDirection.Menurun, directoriesFirst: false);
        Assert.Equal("Baru.txt", desc[0].Name);
        Assert.Equal("Tengah.txt", desc[1].Name);
        Assert.Equal("Lama.txt", desc[2].Name);
    }

    [Fact]
    public void Sort_DirectoriesFirst_PlacesFoldersBeforeFiles()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "FileBesar.dat", IsDirectory = false, SizeBytes = 999999 },
            new() { Name = "FolderKecil", IsDirectory = true, SizeBytes = 100 }
        };

        var sorted = _service.Sort(items, SortField.Ukuran, SortDirection.Menurun, directoriesFirst: true);
        Assert.True(sorted[0].IsDirectory);
        Assert.Equal("FolderKecil", sorted[0].Name);
        Assert.False(sorted[1].IsDirectory);
        Assert.Equal("FileBesar.dat", sorted[1].Name);
    }

    [Fact]
    public void FilterHierarchy_ByExtension_PrunesNonMatchingFoldersAndRecalculatesSizes()
    {
        var tree = CreateTestTree();
        var criteria = new FileFilterCriteria { Extension = ".mp4" };

        var filtered = _service.FilterHierarchy(tree, criteria);

        Assert.NotNull(filtered);
        // FolderVideo harus ada karena memuat video1.mp4
        Assert.Contains(filtered!.Children, c => c.Name == "FolderVideo");
        // FolderDocs dan FolderKosong dan root_archive.zip harus dipangkas karena bukan .mp4
        Assert.DoesNotContain(filtered.Children, c => c.Name == "FolderDocs");
        Assert.DoesNotContain(filtered.Children, c => c.Name == "FolderKosong");
        Assert.DoesNotContain(filtered.Children, c => c.Name == "root_archive.zip");

        var videoDir = filtered.Children.First(c => c.Name == "FolderVideo");
        Assert.Single(videoDir.Children);
        Assert.Equal("video1.mp4", videoDir.Children[0].Name);

        // Ukuran terhitung ulang: hanya 50 MB (video1.mp4)
        Assert.Equal(50L * 1024L * 1024L, filtered.SizeBytes);
        Assert.Equal(50L * 1024L * 1024L, videoDir.SizeBytes);
    }

    [Fact]
    public void FilterHierarchy_ByMinimumSize_ExcludesFilesBelowThreshold()
    {
        var tree = CreateTestTree();
        // Saring berkas dengan batas minimum 25 MB
        var criteria = new FileFilterCriteria { MinimumSizeBytes = 25L * 1024L * 1024L };

        var filtered = _service.FilterHierarchy(tree, criteria);

        Assert.NotNull(filtered);
        // Yang lolos: video1.mp4 (50 MB) dan root_archive.zip (100 MB).
        // Total = 150 MB
        Assert.Equal(150L * 1024L * 1024L, filtered!.SizeBytes);

        // FolderDocs tidak memuat berkas >= 25 MB, maka harus dipangkas
        Assert.DoesNotContain(filtered.Children, c => c.Name == "FolderDocs");
    }

    [Fact]
    public void GetFlattenedFiles_ExtractsAllFilesSortedBySize()
    {
        var tree = CreateTestTree();

        var flat = _service.GetFlattenedFiles(tree, null, SortField.Ukuran, SortDirection.Menurun);

        // Total berkas: video1.mp4, video2.mkv, doc1.pdf, note.txt, root_archive.zip = 5 berkas
        Assert.Equal(5, flat.Count);
        Assert.All(flat, f => Assert.False(f.IsDirectory));

        // Urutan bita menurun:
        // 1. root_archive.zip (100 MB)
        // 2. video1.mp4 (50 MB)
        // 3. video2.mkv (20 MB)
        // 4. doc1.pdf (3 MB)
        // 5. note.txt (2 MB)
        Assert.Equal("root_archive.zip", flat[0].Name);
        Assert.Equal("video1.mp4", flat[1].Name);
        Assert.Equal("video2.mkv", flat[2].Name);
        Assert.Equal("doc1.pdf", flat[3].Name);
        Assert.Equal("note.txt", flat[4].Name);
    }

    [Fact]
    public void GetFlattenedFiles_WithFilterAndMaxResults_AppliesCorrectly()
    {
        var tree = CreateTestTree();
        var criteria = new FileFilterCriteria { MinimumSizeBytes = 10L * 1024L * 1024L }; // >= 10 MB

        var flat = _service.GetFlattenedFiles(tree, criteria, SortField.Ukuran, SortDirection.Menurun, maxResults: 2);

        // Berkas >= 10 MB ada 3 (root_archive 100MB, video1 50MB, video2 20MB)
        // Karena maxResults: 2, hanya ambil 2 teratas
        Assert.Equal(2, flat.Count);
        Assert.Equal("root_archive.zip", flat[0].Name);
        Assert.Equal("video1.mp4", flat[1].Name);
    }
}
