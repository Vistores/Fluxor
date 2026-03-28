using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorFX.App.Services;

namespace CursorFX.App;

public partial class SaveProfileWindow : Window, INotifyPropertyChanged
{
    private readonly LocalizationService _localizationService;
    private string _profileName;
    private string _profileDescription;

    public SaveProfileWindow(string defaultName, string defaultDescription, LocalizationService localizationService)
    {
        _localizationService = localizationService;
        _profileName = defaultName;
        _profileDescription = defaultDescription;
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WindowTitle => _localizationService.Get("saveProfile.windowTitle");
    public string HeadingText => _localizationService.Get("saveProfile.heading");
    public string IntroText => _localizationService.Get("saveProfile.intro");
    public string NameLabelText => _localizationService.Get("saveProfile.name");
    public string DescriptionLabelText => _localizationService.Get("saveProfile.description");
    public string CancelText => _localizationService.Get("saveProfile.cancel");
    public string ConfirmText => _localizationService.Get("saveProfile.confirm");

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
