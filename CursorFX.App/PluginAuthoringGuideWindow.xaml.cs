using System.IO;
using System.Windows;
using CursorFX.App.Services;

namespace CursorFX.App;

public partial class PluginAuthoringGuideWindow : Window
{
    private readonly string _guideText;

    public PluginAuthoringGuideWindow(string guidePath, LocalizationService localizationService)
    {
        InitializeComponent();

        WindowTitle = localizationService.Get("guide.windowTitle");
        HeadingText = localizationService.Get("guide.heading");
        IntroText = localizationService.Get("guide.intro");
        CopyButtonText = localizationService.Get("guide.copy");
        CloseButtonText = localizationService.Get("guide.close");

        _guideText = File.Exists(guidePath)
            ? File.ReadAllText(guidePath)
            : $"{localizationService.Get("guide.missing")}{Environment.NewLine}{Environment.NewLine}Expected path:{Environment.NewLine}{guidePath}";

        DataContext = this;
        GuideTextBox.Text = _guideText;
    }

    public string WindowTitle { get; }

    public string HeadingText { get; }

    public string IntroText { get; }

    public string CopyButtonText { get; }

    public string CloseButtonText { get; }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_guideText);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
