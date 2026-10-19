using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Root.App.ViewModels;
using Root.Core.Models;

namespace Root.App.Views;

public partial class TreemapView : UserControl
{
    private Border? _previouslySelectedBorder;

    public TreemapView()
    {
        InitializeComponent();
    }

    private TreemapViewModel? ViewModel => DataContext as TreemapViewModel;

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

        // Tangani pemilihan kotak
        ViewModel.SelectNodeCommand.Execute(node);
        HighlightSelectedBorder(border);

        // Tangani klik dua kali untuk navigasi folder
        if (e.ClickCount == 2 && node.IsDirectory)
        {
            ViewModel.DrillDownCommand.Execute(node);
        }

        e.Handled = true;
    }

    private void TreemapBox_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TreemapNode node || ViewModel == null)
            return;

        ViewModel.HoveredNode = node;

        if (border != _previouslySelectedBorder)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Cyan hover
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
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(9, 13, 22)); // Normal border
            border.BorderThickness = new Thickness(1);
        }
    }

    private void HighlightSelectedBorder(Border newSelected)
    {
        if (_previouslySelectedBorder != null && _previouslySelectedBorder != newSelected)
        {
            _previouslySelectedBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(9, 13, 22));
            _previouslySelectedBorder.BorderThickness = new Thickness(1);
        }

        _previouslySelectedBorder = newSelected;
        if (_previouslySelectedBorder != null)
        {
            // Sorot kotak terpilih dengan border emerald terang dan tebal
            _previouslySelectedBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            _previouslySelectedBorder.BorderThickness = new Thickness(2.5);
        }
    }
}
