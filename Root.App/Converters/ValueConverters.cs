using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Root.Core.Models;

namespace Root.App.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Invert) isNullOrEmpty = !isNullOrEmpty;
        return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class LogLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush InfoBrush = new(Color.FromRgb(56, 189, 248));      // Cyan-400
    private static readonly SolidColorBrush SuksesBrush = new(Color.FromRgb(52, 211, 153));    // Emerald-400
    private static readonly SolidColorBrush PeringatanBrush = new(Color.FromRgb(251, 191, 36)); // Amber-400
    private static readonly SolidColorBrush KesalahanBrush = new(Color.FromRgb(248, 113, 113)); // Red-400
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(156, 163, 175));   // Gray-400

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => InfoBrush,
                LogLevel.Sukses => SuksesBrush,
                LogLevel.Peringatan => PeringatanBrush,
                LogLevel.Kesalahan => KesalahanBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush SelesaiBrush = new(Color.FromRgb(16, 185, 129));   // Emerald-500
    private static readonly SolidColorBrush MemindaiBrush = new(Color.FromRgb(14, 165, 233));  // Sky-500
    private static readonly SolidColorBrush DibatalkanBrush = new(Color.FromRgb(245, 158, 11)); // Amber-500
    private static readonly SolidColorBrush ParsialBrush = new(Color.FromRgb(249, 115, 22));   // Orange-500
    private static readonly SolidColorBrush GagalBrush = new(Color.FromRgb(239, 68, 68));      // Rose-500
    private static readonly SolidColorBrush SiapBrush = new(Color.FromRgb(100, 116, 139));     // Slate-500

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScanStatus status)
        {
            return status switch
            {
                ScanStatus.Selesai => SelesaiBrush,
                ScanStatus.Memindai => MemindaiBrush,
                ScanStatus.Dibatalkan => DibatalkanBrush,
                ScanStatus.SelesaiSebagian => ParsialBrush,
                ScanStatus.Gagal => GagalBrush,
                _ => SiapBrush
            };
        }
        return SiapBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class HexToBrushConverter : IValueConverter
{
    private static readonly BrushConverter Converter = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var brush = (Brush?)Converter.ConvertFromString(hex);
                if (brush != null) return brush;
            }
            catch
            {
                // Fallback jika hex tidak valid
            }
        }
        return new SolidColorBrush(Color.FromRgb(71, 85, 105)); // Slate-600
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class DimensionToVisibilityConverter : IMultiValueConverter
{
    public double MinWidth { get; set; } = 45;
    public double MinHeight { get; set; } = 25;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double w && values[1] is double h)
        {
            return (w >= MinWidth && h >= MinHeight) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        var result = new object[targetTypes.Length];
        for (int i = 0; i < targetTypes.Length; i++)
        {
            result[i] = Binding.DoNothing;
        }
        return result;
    }
}

