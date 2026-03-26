using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorFX.App.Services;
using CursorFX.Core.Models;

namespace CursorFX.App;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private bool _useSystemLanguage;
    private string _selectedLanguageCode = "en";

    public SettingsWindow(GeneralSettings settings, LocalizationSettings localizationSettings, LocalizationService localizationService)
    {
        InitializeComponent();

        AvailableLanguages = localizationService.AvailableLanguages;
        LaunchOnStartup = settings.LaunchOnStartup;
        RunInBackground = settings.RunInBackground;
        PauseWhenCursorHidden = settings.PauseWhenCursorHidden;
        UseSystemLanguage = localizationSettings.UseSystemLanguage;
        SelectedLanguageCode = localizationService.NormalizeLanguageCode(localizationSettings.LanguageCode);

        WindowTitle = localizationService.Get("settings.windowTitle");
        HeadingText = localizationService.Get("settings.heading");
        IntroText = localizationService.Get("settings.intro");
        StartupTitle = localizationService.Get("settings.startup");
        LaunchOnStartupText = localizationService.Get("settings.launchOnStartup");
        BackgroundModeTitle = localizationService.Get("settings.backgroundMode");
        RunInBackgroundText = localizationService.Get("settings.runInBackground");
        RenderGuardTitle = localizationService.Get("settings.renderGuard");
        PauseWhenCursorHiddenText = localizationService.Get("settings.pauseWhenCursorHidden");
        LanguageTitle = localizationService.Get("settings.language");
        UseSystemLanguageText = localizationService.Get("settings.useSystemLanguage");
        LanguageLabelText = localizationService.Get("settings.languageLabel");
        LanguageHintText = localizationService.Get("settings.languageHint");
        CancelButtonText = localizationService.Get("settings.cancel");
        ApplyButtonText = localizationService.Get("settings.apply");

        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LocalizationOption> AvailableLanguages { get; }

    public bool LaunchOnStartup { get; set; }

    public bool RunInBackground { get; set; }

    public bool PauseWhenCursorHidden { get; set; }

    public bool UseSystemLanguage
    {
        get => _useSystemLanguage;
        set
        {
            if (_useSystemLanguage == value)
            {
                return;
            }

            _useSystemLanguage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLanguageSelectionEnabled));
        }
    }

    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            if (string.Equals(_selectedLanguageCode, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedLanguageCode = value;
            OnPropertyChanged();
        }
    }

    public bool IsLanguageSelectionEnabled => !UseSystemLanguage;

    public string WindowTitle { get; }

    public string HeadingText { get; }

    public string IntroText { get; }

    public string StartupTitle { get; }

    public string LaunchOnStartupText { get; }

    public string BackgroundModeTitle { get; }

    public string RunInBackgroundText { get; }

    public string RenderGuardTitle { get; }

    public string PauseWhenCursorHiddenText { get; }

    public string LanguageTitle { get; }

    public string UseSystemLanguageText { get; }

    public string LanguageLabelText { get; }

    public string LanguageHintText { get; }

    public string CancelButtonText { get; }

    public string ApplyButtonText { get; }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
