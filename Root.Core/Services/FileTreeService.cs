using System;
using System.Collections.Generic;
using System.Linq;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Layanan pemfilteran dan pengurutan hierarki berkas dalam memori berkecepatan tinggi.
/// Menghindari pemindaian ulang disk saat pengguna mengubah filter atau urutan.
/// </summary>
public class FileTreeService : IFileTreeService
{
    public IReadOnlyList<FileSystemItemModel> Sort(IEnumerable<FileSystemItemModel> items, SortField field, SortDirection direction, bool directoriesFirst = true)
    {
        if (items == null) return Array.Empty<FileSystemItemModel>();

        IOrderedEnumerable<FileSystemItemModel> ordered;

        if (directoriesFirst)
        {
            ordered = items.OrderByDescending(x => x.IsDirectory);

            ordered = field switch
            {
                SortField.Ukuran => direction == SortDirection.Menurun
                    ? ordered.ThenByDescending(x => x.SizeBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenBy(x => x.SizeBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),

                SortField.Nama => direction == SortDirection.Menurun
                    ? ordered.ThenByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),

                SortField.TanggalDiubah => direction == SortDirection.Menurun
                    ? ordered.ThenByDescending(x => x.LastModified ?? DateTime.MinValue).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenBy(x => x.LastModified ?? DateTime.MinValue).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),

                _ => ordered.ThenByDescending(x => x.SizeBytes)
            };
        }
        else
        {
            ordered = field switch
            {
                SortField.Ukuran => direction == SortDirection.Menurun
                    ? items.OrderByDescending(x => x.SizeBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderBy(x => x.SizeBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),

                SortField.Nama => direction == SortDirection.Menurun
                    ? items.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),

                SortField.TanggalDiubah => direction == SortDirection.Menurun
                    ? items.OrderByDescending(x => x.LastModified ?? DateTime.MinValue).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderBy(x => x.LastModified ?? DateTime.MinValue).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),

                _ => items.OrderByDescending(x => x.SizeBytes)
            };
        }

        return ordered.ToList();
    }

    public FileSystemItemModel? FilterHierarchy(FileSystemItemModel root, FileFilterCriteria criteria)
    {
        if (root == null) return null;

        if (criteria == null || criteria.IsEmpty)
        {
            return root;
        }

        return FilterNodeRecursive(root, criteria, isRoot: true);
    }

    private FileSystemItemModel? FilterNodeRecursive(FileSystemItemModel current, FileFilterCriteria criteria, bool isRoot)
    {
        if (!current.IsDirectory)
        {
            return criteria.MatchesFile(current) ? CloneItem(current) : null;
        }

        // Jika direktori: evaluasi setiap anak
        var matchingChildren = new List<FileSystemItemModel>();
        long totalSize = 0;
        int fileCount = 0;
        int dirCount = 0;

        foreach (var child in current.Children)
        {
            var filteredChild = FilterNodeRecursive(child, criteria, isRoot: false);
            if (filteredChild != null)
            {
                matchingChildren.Add(filteredChild);
                totalSize = SafeAdd(totalSize, filteredChild.SizeBytes);
                if (filteredChild.IsDirectory)
                {
                    dirCount += 1 + filteredChild.DirectoryCount;
                    fileCount += filteredChild.FileCount;
                }
                else
                {
                    fileCount++;
                }
            }
        }

        // Jika tidak ada anak yang cocok:
        // Jika ini adalah root, kembalikan root kosong agar UI tetap menampilkan kontainer akar
        if (matchingChildren.Count == 0 && !isRoot)
        {
            return null; // Pangkas direktori ini karena tidak ada berkas yang cocok
        }

        var clonedDir = CloneItem(current);
        clonedDir.Children = matchingChildren;
        clonedDir.SizeBytes = totalSize;
        clonedDir.FileCount = fileCount;
        clonedDir.DirectoryCount = dirCount;

        return clonedDir;
    }

    public FileSystemItemModel SortHierarchy(FileSystemItemModel root, SortField field, SortDirection direction, bool directoriesFirst = true)
    {
        if (root == null) return null!;

        var cloned = CloneItem(root);
        cloned.Children = Sort(root.Children.Select(c => SortHierarchy(c, field, direction, directoriesFirst)), field, direction, directoriesFirst).ToList();
        return cloned;
    }

    public IReadOnlyList<FileSystemItemModel> GetFlattenedFiles(
        FileSystemItemModel root,
        FileFilterCriteria? criteria,
        SortField sortField = SortField.Ukuran,
        SortDirection sortDirection = SortDirection.Menurun,
        int maxResults = 0)
    {
        if (root == null) return Array.Empty<FileSystemItemModel>();

        var flatList = new List<FileSystemItemModel>();

        void Flatten(FileSystemItemModel node)
        {
            if (!node.IsDirectory)
            {
                if (criteria == null || criteria.MatchesFile(node))
                {
                    flatList.Add(node);
                }
                return;
            }

            foreach (var child in node.Children)
            {
                Flatten(child);
            }
        }

        Flatten(root);

        var sorted = Sort(flatList, sortField, sortDirection, directoriesFirst: false);

        if (maxResults > 0 && sorted.Count > maxResults)
        {
            return sorted.Take(maxResults).ToList();
        }

        return sorted;
    }

    private static FileSystemItemModel CloneItem(FileSystemItemModel original)
    {
        return new FileSystemItemModel
        {
            FullPath = original.FullPath,
            Name = original.Name,
            Extension = original.Extension,
            IsDirectory = original.IsDirectory,
            SizeBytes = original.SizeBytes,
            FileCount = original.FileCount,
            DirectoryCount = original.DirectoryCount,
            LastModified = original.LastModified,
            ParentPath = original.ParentPath,
            Children = [] // Dikelola secara terpisah
        };
    }

    private static long SafeAdd(long a, long b)
    {
        try
        {
            checked
            {
                return a + b;
            }
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }
}
