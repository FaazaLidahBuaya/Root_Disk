using System.Windows;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Root.App.ViewModels;

namespace Root.App;

/// <summary>
/// Interaksi jendela utama ROOT.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => UpdateMaximizeGlyph();
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeGlyph();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void MainContent_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        var scale = MainContent.RenderTransform as ScaleTransform;
        if (scale is null)
            return;

        MainContent.Opacity = 0;
        scale.ScaleX = 0.97;
        scale.ScaleY = 0.97;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        MainContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });

        if (DataContext is MainViewModel viewModel && ViewModeIndicator.RenderTransform is TranslateTransform indicatorOffset)
        {
            if (indicatorOffset.IsFrozen)
            {
                indicatorOffset = indicatorOffset.CloneCurrentValue();
                ViewModeIndicator.RenderTransform = indicatorOffset;
            }

            var targetOffset = ReferenceEquals(viewModel.CurrentView, viewModel.Treemap) ? 53 : 0;
            indicatorOffset.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(indicatorOffset.X, targetOffset, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        }
    }

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeGlyph is null)
            return;

        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Pulihkan" : "Maksimalkan";
        AutomationProperties.SetName(MaximizeButton, WindowState == WindowState.Maximized ? "Pulihkan ukuran jendela" : "Maksimalkan jendela");
    }
}
