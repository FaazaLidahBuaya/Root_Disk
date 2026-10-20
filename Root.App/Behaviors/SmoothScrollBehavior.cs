using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Root.App.Behaviors;

public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private const double PixelsPerWheelNotch = 34;

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer scrollViewer)
            return;

        if ((bool)e.NewValue)
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
        else
            scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Delta == 0)
            return;

        var direction = e.Delta > 0 ? -1 : 1;
        var step = scrollViewer.CanContentScroll ? 2 : PixelsPerWheelNotch;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && scrollViewer.ScrollableWidth > 0)
        {
            if ((direction < 0 && scrollViewer.HorizontalOffset <= 0) ||
                (direction > 0 && scrollViewer.HorizontalOffset >= scrollViewer.ScrollableWidth))
                return;

            var nextOffset = Math.Clamp(
                scrollViewer.HorizontalOffset + direction * step,
                0,
                scrollViewer.ScrollableWidth);
            scrollViewer.ScrollToHorizontalOffset(nextOffset);
            e.Handled = true;
            return;
        }

        if ((direction < 0 && scrollViewer.VerticalOffset <= 0) ||
            (direction > 0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight))
            return;

        var verticalOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * step,
            0,
            scrollViewer.ScrollableHeight);
        scrollViewer.ScrollToVerticalOffset(verticalOffset);
        e.Handled = true;
    }
}
