using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Root.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void DriveCapacityBar_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not ProgressBar progressBar)
            return;

        AnimateDriveCapacity(progressBar);
    }

    private void DriveSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => AnimateDriveCapacity(DriveCapacityBar)));
    }

    private static void AnimateDriveCapacity(ProgressBar progressBar)
    {
        var targetValue = progressBar.Value;
        progressBar.BeginAnimation(ProgressBar.ValueProperty, null);
        progressBar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(0, targetValue, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }
}
