using System;
using System.Globalization;
using System.Windows.Data;

namespace Root.App.Converters;

public sealed class RelativeBarWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double availableWidth || values[1] is not double fraction)
            return 0d;

        return Math.Max(0, availableWidth * Math.Clamp(fraction, 0d, 1d));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
