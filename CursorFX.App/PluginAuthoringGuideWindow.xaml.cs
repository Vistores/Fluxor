using System.IO;
using System.Windows;

namespace CursorFX.App;

public partial class PluginAuthoringGuideWindow : Window
{
    private readonly string _guideText;

    public PluginAuthoringGuideWindow(string guidePath)
    {
        InitializeComponent();
        _guideText = File.Exists(guidePath)
            ? File.ReadAllText(guidePath)
            : "Plugin authoring guide was not found.";
        GuideTextBox.Text = _guideText;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_guideText);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
