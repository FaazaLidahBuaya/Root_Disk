using System.Windows;
using System.Windows.Controls;
using Root.App.ViewModels;
using Root.Core.Models;

namespace Root.App.Views;

public partial class HierarchyView : UserControl
{
    public HierarchyView()
    {
        InitializeComponent();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is HierarchyViewModel vm)
        {
            vm.SelectedItem = e.NewValue as FileSystemItemModel;
        }
    }
}
