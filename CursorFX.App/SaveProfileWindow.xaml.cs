using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorFX.App.Services;

namespace CursorFX.App;

public partial class SaveProfileWindow : Window, INotifyPropertyChanged
{
    private readonly LocalizationService _localizationService;
    private readonly string _windowTitle;
    private readonly string _headingText;
    private readonly string _introText;
    private readonly string _confirmText;
    private string _profileName;
    private string _profileDescription;

    public SaveProfileWindow(
        string defaultName,
        string defaultDescription,
        LocalizationService localizationService,
        string? windowTitle = null,
        string? headingText = null,
        string? introText = null,
        string? confirmText = null)
    {
        _localizationService = localizationService;
        _windowTitle = string.IsNullOrWhiteSpace(windowTitle) ? _localizationService.Get("saveProfile.windowTitle") : windowTitle;
        _headingText = string.IsNullOrWhiteSpace(headingText) ? _localizationService.Get("saveProfile.heading") : headingText;
        _introText = string.IsNullOrWhiteSpace(introText) ? _localizationService.Get("saveProfile.intro") : introText;
        _confirmText = string.IsNullOrWhiteSpace(confirmText) ? _localizationService.Get("saveProfile.confirm") : confirmText;
        _profileName = defaultName;
        _profileDescription = defaultDescription;
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WindowTitle => _windowTitle;
    public string HeadingText => _headingText;
    public string IntroText => _introText;
    public string NameLabelText => _localizationService.Get("saveProfile.name");
    public string DescriptionLabelText => _localizationService.Get("saveProfile.description");
    public string CancelText => _localizationService.Get("saveProfile.cancel");
    public string ConfirmText => _confirmText;

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string ProfileDescription
    {
        get => _profileDescription;
        set => SetProperty(ref _profileDescription, value);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            System.Windows.MessageBox.Show(
                _localizationService.Get("saveProfile.validation.nameRequired"),
                _localizationService.Get("saveProfile.validation.title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
