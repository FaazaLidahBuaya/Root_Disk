using System;
using System.Collections.Generic;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model representasi item dalam sistem berkas (direktori atau berkas).
/// </summary>
public class FileSystemItemModel
{
    public static List<FileSystemItemModel> EmptyChildren { get; } = [];

    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public double RelativeSize { get; set; } = 1d;
    public DateTime? LastModified { get; set; }
    public string? ParentPath { get; set; }
    public List<FileSystemItemModel> Children { get; set; } = [];

    public string FormattedSize => FileFormatter.FormatBytes(SizeBytes);
    public int TotalSubItems => FileCount + DirectoryCount;
    public string TypeDescription => IsDirectory
        ? "Folder"
        : (string.IsNullOrWhiteSpace(Extension) ? "Berkas" : Extension.TrimStart('.').ToUpperInvariant());
}
