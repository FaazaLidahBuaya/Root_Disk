using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Root.App.Converters;

/// <summary>Menjaga kolom data tetap berada di posisi yang sama pada setiap tingkat pohon.</summary>
public sealed class TreeRowWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values[0] is not double treeWidth || treeWidth <= 0)
            return Binding.DoNothing;

        int depth = 0;
        if (values[2] is string rootPath && !string.IsNullOrWhiteSpace(rootPath) &&
            values[3] is string itemPath && !string.IsNullOrWhiteSpace(itemPath) &&
            !string.Equals(rootPath, itemPath, StringComparison.OrdinalIgnoreCase) &&
            values[1] is string parentPath && !string.IsNullOrWhiteSpace(parentPath))
        {
            try
            {
                string relativeParent = Path.GetRelativePath(rootPath, parentPath);
                depth = relativeParent == "."
                    ? 1
                    : relativeParent.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1;
            }
            catch (ArgumentException)
            {
                depth = 1;
            }
        }

        // Ruang untuk expander TreeView, indentasi tiap tingkat, dan padding baris.
        return Math.Max(0, treeWidth - 28 - (depth * 19));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
