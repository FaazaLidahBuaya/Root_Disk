using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Root.App.Services;

/// <summary>
/// Mengganti palet aplikasi dengan memperbarui resource brush bersama.
/// Kontrol aktif memakai DynamicResource agar pergantian tema langsung terlihat.
/// </summary>
public static class ThemeManager
{
    private static readonly IReadOnlyDictionary<string, string> LightPalette = new Dictionary<string, string>
    {
        ["BrushDarkBg"] = "#F7F8FC",
        ["BrushSidebarBg"] = "#FFFFFF",
        ["BrushCardBg"] = "#FFFFFF",
        ["BrushCardBorder"] = "#E3E6EF",
        ["BrushInputBg"] = "#FFFFFF",
        ["BrushTextPrimary"] = "#202B45",
        ["BrushTextSecondary"] = "#52617E",
        ["BrushTextMuted"] = "#7C879D",
        ["BrushAccentGreen"] = "#7567F8",
        ["BrushAccentGreenHover"] = "#857AFF",
        ["BrushAccentCyan"] = "#438BE8",
        ["BrushAccentAmber"] = "#D98B25",
        ["BrushAccentRose"] = "#DB5D72",
        ["BrushHoverSurface"] = "#F2F3FA",
        ["BrushHoverBorder"] = "#D9D6FC",
        ["BrushSelectedSurface"] = "#E9E6FF",
        ["BrushPopupSurface"] = "#FFFFFF",
        ["BrushDisabledSurface"] = "#E5E8F0",
        ["BrushDisabledText"] = "#8791A5",
        ["BrushDangerSurface"] = "#FFF0F2",
        ["BrushDangerText"] = "#B84258",
        ["BrushAdminSurface"] = "#F1EFFF",
        ["BrushAdminBorder"] = "#BDB5FF",
        ["BrushAdminText"] = "#5549C8",
        ["BrushAdminHoverSurface"] = "#E7E3FF",
        ["BrushAdminHoverBorder"] = "#9F95FF",
        ["BrushScrollThumb"] = "#C7C2F7",
        ["BrushScrollThumbHover"] = "#9B91F2",
        ["BrushSizeBar"] = "#E8E5FF",
        ["BrushSizeValue"] = "#18794E"
    };

    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
    {
        ["BrushDarkBg"] = "#171A24",
        ["BrushSidebarBg"] = "#1D2230",
        ["BrushCardBg"] = "#222938",
        ["BrushCardBorder"] = "#343D50",
        ["BrushInputBg"] = "#1A2030",
        ["BrushTextPrimary"] = "#E8ECF5",
        ["BrushTextSecondary"] = "#B7C0D2",
        ["BrushTextMuted"] = "#8B97AD",
        ["BrushAccentGreen"] = "#8B7CFF",
        ["BrushAccentGreenHover"] = "#998EFF",
        ["BrushAccentCyan"] = "#6AB8FF",
        ["BrushAccentAmber"] = "#F0B85A",
        ["BrushAccentRose"] = "#FF7D91",
        ["BrushHoverSurface"] = "#2B3040",
        ["BrushHoverBorder"] = "#4A456F",
        ["BrushSelectedSurface"] = "#343051",
        ["BrushPopupSurface"] = "#252C3B",
        ["BrushDisabledSurface"] = "#303747",
        ["BrushDisabledText"] = "#818BA0",
        ["BrushDangerSurface"] = "#3A2731",
        ["BrushDangerText"] = "#FF9AA9",
        ["BrushAdminSurface"] = "#302D45",
        ["BrushAdminBorder"] = "#6259A8",
        ["BrushAdminText"] = "#D5CFFF",
        ["BrushAdminHoverSurface"] = "#3A3555",
        ["BrushAdminHoverBorder"] = "#8A7EF0",
        ["BrushScrollThumb"] = "#515B70",
        ["BrushScrollThumbHover"] = "#8B7CFF",
        ["BrushSizeBar"] = "#38334F",
        ["BrushSizeValue"] = "#65D49A"
    };

    public static void Apply(bool useDarkTheme)
    {
        var application = Application.Current;
        if (application == null)
            return;

        var paletteDictionary = application.Resources.MergedDictionaries
            .FirstOrDefault(dictionary => dictionary.Contains("BrushDarkBg"));
        if (paletteDictionary == null)
            return;

        var palette = useDarkTheme ? DarkPalette : LightPalette;
        foreach (var (key, colorText) in palette)
        {
            var color = Color.FromRgb(
                Convert.ToByte(colorText.Substring(1, 2), 16),
                Convert.ToByte(colorText.Substring(3, 2), 16),
                Convert.ToByte(colorText.Substring(5, 2), 16));
            paletteDictionary[key] = new SolidColorBrush(color);
        }

        string logoName = useDarkTheme ? "RootDark.png" : "RootLight.png";
        paletteDictionary["AppLogo"] = new BitmapImage(new Uri($"pack://application:,,,/Assets/{logoName}", UriKind.Absolute));
    }
}
