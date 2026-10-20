using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Root.App.ViewModels;
using Root.Core.Models;

namespace Root.App.Views;

public partial class TreemapView : UserControl
{
    private const int MaximumStaggeredTiles = 24;
    private const int MaximumLayoutAnimatedTiles = 80;
    private static readonly DropShadowEffect HoverShadow = CreateFrozenShadow(Colors.Black, 0.05, 12, 4);
    private static readonly DropShadowEffect SelectedShadow = CreateFrozenShadow(Color.FromRgb(59, 153, 252), 0.18, 8, 0);
    private Border? _previouslySelectedBorder;
    private TreemapViewModel? _observedViewModel;
    private Dictionary<string, int>? _entryRanks;
    private Dictionary<string, Rect> _lastLayout = new(StringComparer.Ordinal);
    private Dictionary<string, Rect> _transitionFrom = new(StringComparer.Ordinal);
    private HashSet<string> _layoutTransitionPaths = new(StringComparer.Ordinal);
    private bool _layoutTransitionQueued;
    private bool _zoomInProgress;

    public TreemapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ObserveViewModel(DataContext as TreemapViewModel);
    }

    private TreemapViewModel? ViewModel => DataContext as TreemapViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        ObserveViewModel(e.NewValue as TreemapViewModel);

    private void ObserveViewModel(TreemapViewModel? viewModel)
    {
        if (_observedViewModel != null)
        {
            _observedViewModel.Nodes.CollectionChanged -= OnNodesCollectionChanged;
            _observedViewModel.LayoutUpdated -= OnLayoutUpdated;
        }

        _observedViewModel = viewModel;
        _entryRanks = null;
        _lastLayout.Clear();
        _transitionFrom.Clear();
        if (_observedViewModel != null)
        {
            _observedViewModel.Nodes.CollectionChanged += OnNodesCollectionChanged;
            _observedViewModel.LayoutUpdated += OnLayoutUpdated;
        }
    }

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _entryRanks = null;
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            if (_previouslySelectedBorder != null)
                _previouslySelectedBorder.Effect = null;
            _previouslySelectedBorder = null;
        }
    }

    private void OnLayoutUpdated()
    {
        _transitionFrom = new Dictionary<string, Rect>(_lastLayout, StringComparer.Ordinal);
        _layoutTransitionPaths = new HashSet<string>(_transitionFrom.Keys, StringComparer.Ordinal);
        if (_layoutTransitionQueued)
            return;

        _layoutTransitionQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AnimateLayoutChanges));
    }

    private void AnimateLayoutChanges()
    {
        _layoutTransitionQueued = false;
        var viewModel = ViewModel;
        if (viewModel == null)
            return;

        TreemapItems.UpdateLayout();
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        foreach (var node in viewModel.Nodes
                     .Where(node => _transitionFrom.ContainsKey(node.FullPath))
                     .OrderByDescending(node => node.SizeBytes)
                     .Take(MaximumLayoutAnimatedTiles))
        {
            if (TreemapItems.ItemContainerGenerator.ContainerFromItem(node) is not ContentPresenter presenter)
                continue;

            var old = _transitionFrom[node.FullPath];
            AnimateLayoutProperty(presenter, Canvas.LeftProperty, old.X, node.X, easing);
            AnimateLayoutProperty(presenter, Canvas.TopProperty, old.Y, node.Y, easing);
            AnimateLayoutProperty(presenter, FrameworkElement.WidthProperty, old.Width, node.Width, easing);
            AnimateLayoutProperty(presenter, FrameworkElement.HeightProperty, old.Height, node.Height, easing);
        }

        _lastLayout = viewModel.Nodes.ToDictionary(
            node => node.FullPath,
            node => new Rect(node.X, node.Y, node.Width, node.Height),
            StringComparer.Ordinal);
        _transitionFrom.Clear();
    }

    private static void AnimateLayoutProperty(DependencyObject target, DependencyProperty property, double from, double to, IEasingFunction easing)
    {
        if (Math.Abs(from - to) < 0.5)
            return;

        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        if (target is UIElement element)
            element.BeginAnimation(property, animation);
    }

    private void TreemapBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TreemapNode node || _layoutTransitionPaths.Contains(node.FullPath))
            return;

        var viewModel = ViewModel;
        if (viewModel == null)
            return;

        _entryRanks ??= viewModel.Nodes
            .OrderByDescending(item => item.SizeBytes)
            .Take(MaximumStaggeredTiles)
            .Select((item, rank) => new { item.FullPath, rank })
            .ToDictionary(item => item.FullPath, item => item.rank, StringComparer.Ordinal);

        if (!_entryRanks.TryGetValue(node.FullPath, out var rank))
            return;

        var scale = GetMutableScaleTransform(border);
        if (scale == null)
            return;

        border.Opacity = 0;
        scale.ScaleX = 0.96;
        scale.ScaleY = 0.96;
        var delay = TimeSpan.FromMilliseconds(rank * 35);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
        {
            BeginTime = delay,
            EasingFunction = easing
        };
        opacityAnimation.Completed += (_, _) =>
        {
            border.BeginAnimation(OpacityProperty, null);
            border.Opacity = 1;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        };
        border.BeginAnimation(OpacityProperty, opacityAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(400))
        {
            BeginTime = delay,
            EasingFunction = easing
        });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(400))
        {
            BeginTime = delay,
            EasingFunction = easing
        });
    }

    private void TreemapBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel == null) return;

        double width = e.NewSize.Width - 6;
        double height = e.NewSize.Height - 6;

        if (width > 20 && height > 20)
        {
            ViewModel.OnDimensionsChanged(width, height);
        }
    }

    private void TreemapBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TreemapNode node || ViewModel == null)
            return;

        if (node.IsAggregate)
        {
            e.Handled = true;
            return;
        }

        // Tangani pemilihan kotak
        ViewModel.SelectNodeCommand.Execute(node);
        HighlightSelectedBorder(border);

        // Tangani klik dua kali untuk navigasi folder
        if (e.ClickCount == 2 && node.IsDirectory && !ViewModel.IsScanInProgress)
        {
            if (!_zoomInProgress)
                AnimateDrillDown(border, node);
        }
        else
        {
            AnimatePress(border);
        }

        e.Handled = true;
    }

    private void TreemapBox_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TreemapNode node || ViewModel == null)
            return;

        ViewModel.HoveredNode = node;
        AnimateScale(border, 1.006, TimeSpan.FromMilliseconds(200));
        AnimateTileIcon(border, 1.08);
        border.Effect = HoverShadow;

        if (border != _previouslySelectedBorder)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(117, 103, 248)); // Aksen ungu pada tema terang
            border.BorderThickness = new Thickness(1.5);
        }
    }

    private void TreemapBox_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TreemapNode node || ViewModel == null)
            return;

        if (ViewModel.HoveredNode == node)
        {
            ViewModel.HoveredNode = null;
        }

        if (border != _previouslySelectedBorder)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(227, 230, 239)); // Garis netral tema terang
            border.BorderThickness = new Thickness(1);
            border.Effect = null;
        }
        AnimateScale(border, 1, TimeSpan.FromMilliseconds(200));
        AnimateTileIcon(border, 1);
    }

    private void HighlightSelectedBorder(Border newSelected)
    {
        if (_previouslySelectedBorder != null && _previouslySelectedBorder != newSelected)
        {
            _previouslySelectedBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(227, 230, 239));
            _previouslySelectedBorder.BorderThickness = new Thickness(1);
            _previouslySelectedBorder.Effect = null;
        }

        _previouslySelectedBorder = newSelected;
        if (_previouslySelectedBorder != null)
        {
            // Sorot kotak terpilih dengan aksen ungu tema aplikasi.
            _previouslySelectedBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 153, 252));
            _previouslySelectedBorder.BorderThickness = new Thickness(2);
            _previouslySelectedBorder.Effect = SelectedShadow;
        }
    }

    private static DropShadowEffect CreateFrozenShadow(Color color, double opacity, double blurRadius, double depth)
    {
        var effect = new DropShadowEffect
        {
            Color = color,
            Opacity = opacity,
            BlurRadius = blurRadius,
            ShadowDepth = depth
        };
        effect.Freeze();
        return effect;
    }

    private static void AnimateScale(Border border, double target, TimeSpan duration)
    {
        var scale = GetMutableScaleTransform(border);
        if (scale == null)
            return;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, duration) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(target, duration) { EasingFunction = easing });
    }

    private static void AnimateTileIcon(Border border, double target)
    {
        foreach (var textBlock in FindVisualChildren<TextBlock>(border))
        {
            if (!textBlock.FontFamily.Source.Equals("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase))
                continue;

            var scale = GetMutableScaleTransform(textBlock);
            if (scale == null)
                continue;

            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T found)
                yield return found;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private void AnimatePress(Border border)
    {
        var scale = GetMutableScaleTransform(border);
        if (scale == null)
            return;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var pressX = new DoubleAnimation(0.992, TimeSpan.FromMilliseconds(100)) { EasingFunction = easing };
        pressX.Completed += (_, _) =>
        {
            if (border.IsMouseOver && border != _previouslySelectedBorder)
                AnimateScale(border, 1.006, TimeSpan.FromMilliseconds(180));
            else if (border != _previouslySelectedBorder)
                AnimateScale(border, 1, TimeSpan.FromMilliseconds(180));
        };
        var pressY = new DoubleAnimation(0.992, TimeSpan.FromMilliseconds(100)) { EasingFunction = easing };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pressX);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pressY);
    }

    private void AnimateDrillDown(Border border, TreemapNode node)
    {
        var scale = GetMutableScaleTransform(border);
        if (scale == null || ViewModel == null)
            return;

        _zoomInProgress = true;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scaleAnimation = new DoubleAnimation(scale.ScaleX, 1.06, TimeSpan.FromMilliseconds(300)) { EasingFunction = easing };
        var opacityAnimation = new DoubleAnimation(border.Opacity, 0.18, TimeSpan.FromMilliseconds(300)) { EasingFunction = easing };
        opacityAnimation.Completed += (_, _) =>
        {
            if (ViewModel?.SelectedNode == node)
                ViewModel.DrillDownCommand.Execute(node);

            _zoomInProgress = false;
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        border.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private static ScaleTransform? GetMutableScaleTransform(UIElement element)
    {
        if (element.RenderTransform is not ScaleTransform scale)
            return null;

        // WPF membekukan Freezable yang dideklarasikan di dalam DataTemplate sebagai
        // optimasi. SetValue maupun BeginAnimation pada transform yang beku melempar
        // InvalidOperationException ketika kotak treemap pertama kali dimuat.
        if (scale.IsFrozen)
        {
            scale = scale.CloneCurrentValue();
            element.RenderTransform = scale;
        }

        return scale;
    }
}
