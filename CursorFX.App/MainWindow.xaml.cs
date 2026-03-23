using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CursorFX.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNestedScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindParentScrollViewer(e.OriginalSource as DependencyObject) ?? sender as ScrollViewer;
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? FindParentScrollViewer(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ScrollViewer viewer)
            {
                return viewer;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
