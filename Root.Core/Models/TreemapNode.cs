using System;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model node visual treemap yang memuat posisi, dimensi geometris, dan referensi item sistem berkas nyata.
/// </summary>
public class TreemapNode
{
    public FileSystemItemModel Item { get; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string ColorHex { get; set; } = "#10B981";
    public double PercentageOfParent { get; set; }

    public TreemapNode(FileSystemItemModel item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public double Area => Math.Max(0, Width * Height);
    public string Name => Item.Name;
    public string FullPath => Item.FullPath;
    public long SizeBytes => Item.SizeBytes;
    public string FormattedSize => FileFormatter.FormatBytes(SizeBytes);
    public bool IsDirectory => Item.IsDirectory;
    public string TypeDescription => Item.TypeDescription;
    public int FileCount => Item.FileCount;
    public int DirectoryCount => Item.DirectoryCount;
    public int TotalSubItems => Item.TotalSubItems;
    public DateTime? LastModified => Item.LastModified;

    public string FormattedPercentage => $"{PercentageOfParent:F1}%";

    public bool ContainsPoint(double px, double py)
    {
        return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
    }

    public override string ToString()
    {
        return $"{Name} [{X:F1}, {Y:F1}, {Width:F1}x{Height:F1}] - {FormattedSize} ({FormattedPercentage})";
    }
}
