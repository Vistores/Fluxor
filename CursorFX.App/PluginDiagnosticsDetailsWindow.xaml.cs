using System.Windows;
using CursorFX.App.Services;

namespace CursorFX.App;

public partial class PluginDiagnosticsDetailsWindow : Window
{
    public PluginDiagnosticsDetailsWindow(string summaryText, string detailsText, LocalizationService localizationService)
    {
        _summaryText = summaryText;
        _detailsText = detailsText;
        _localizationService = localizationService;
        DataContext = this;
        InitializeComponent();
    }

    private readonly string _summaryText;
    private readonly string _detailsText;
    private readonly LocalizationService _localizationService;

    public string WindowTitle => _localizationService.Get("diagDetails.windowTitle");
    public string HeadingText => _localizationService.Get("diagDetails.heading");
    public string IntroText => _localizationService.Get("diagDetails.intro");
    public string SummaryTitleText => _localizationService.Get("diagDetails.summary");
    public string DetailsTitleText => _localizationService.Get("diagDetails.details");
    public string CloseText => _localizationService.Get("diagDetails.close");
    public string SummaryText => _summaryText;
    public string DetailsText => _detailsText;

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
