using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Root.Core.Utilities;

namespace Root.Core.Models;

/// <summary>
/// Model node visual treemap yang memuat posisi, dimensi geometris, dan referensi item sistem berkas nyata.
/// </summary>
public class TreemapNode : INotifyPropertyChanged
{
    private FileSystemItemModel _item;
    private bool _isAggregate;
    private int _aggregatedItemCount;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private string _colorHex = "#10B981";
    private double _percentageOfParent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public FileSystemItemModel Item => _item;
    public bool IsAggregate => _isAggregate;
    public int AggregatedItemCount => _aggregatedItemCount;
    public double X { get => _x; set => Set(ref _x, value); }
    public double Y { get => _y; set => Set(ref _y, value); }
    public double Width { get => _width; set { if (Set(ref _width, value)) OnPropertyChanged(nameof(Area)); } }
    public double Height { get => _height; set { if (Set(ref _height, value)) OnPropertyChanged(nameof(Area)); } }
    public string ColorHex { get => _colorHex; set => Set(ref _colorHex, value); }
    public double PercentageOfParent { get => _percentageOfParent; set { if (Set(ref _percentageOfParent, value)) OnPropertyChanged(nameof(FormattedPercentage)); } }

    public TreemapNode(FileSystemItemModel item, bool isAggregate = false, int aggregatedItemCount = 0)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _isAggregate = isAggregate;
        _aggregatedItemCount = aggregatedItemCount;
    }

    public void UpdateFrom(TreemapNode source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _item = source.Item;
        _isAggregate = source.IsAggregate;
        _aggregatedItemCount = source.AggregatedItemCount;
        foreach (var property in new[]
        {
            nameof(Item), nameof(IsAggregate), nameof(AggregatedItemCount), nameof(Name), nameof(FullPath),
            nameof(SizeBytes), nameof(FormattedSize), nameof(IsDirectory), nameof(TypeDescription),
            nameof(FileCount), nameof(DirectoryCount), nameof(TotalSubItems), nameof(LastModified)
        })
            OnPropertyChanged(property);

        X = source.X;
        Y = source.Y;
        Width = source.Width;
        Height = source.Height;
        ColorHex = source.ColorHex;
        PercentageOfParent = source.PercentageOfParent;
    }

    public double Area => Math.Max(0, Width * Height);
    public string Name => Item.Name;
    public string FullPath => Item.FullPath;
    public long SizeBytes => Item.SizeBytes;
    public string FormattedSize => FileFormatter.FormatBytes(SizeBytes);
    public bool IsDirectory => !IsAggregate && Item.IsDirectory;
    public string TypeDescription => IsAggregate ? "Ringkasan item" : Item.TypeDescription;
    public int FileCount => Item.FileCount;
    public int DirectoryCount => Item.DirectoryCount;
    public int TotalSubItems => Item.TotalSubItems;
    public DateTime? LastModified => Item.LastModified;

    public string FormattedPercentage => $"{PercentageOfParent:F1}%";

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public bool ContainsPoint(double px, double py)
    {
        return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
    }

    public override string ToString()
    {
        return $"{Name} [{X:F1}, {Y:F1}, {Width:F1}x{Height:F1}] - {FormattedSize} ({FormattedPercentage})";
    }
}
