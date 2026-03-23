using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace CursorFX.App;

public partial class NewPluginProfileWindow : Window, INotifyPropertyChanged
{
    private static readonly Regex IdSanitizer = new("[^a-z0-9-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private string _pluginName;
    private string _pluginId;
    private string _pluginDescription;
    private bool _isEditingId;

    public NewPluginProfileWindow(string sourceName, string sourceId, string sourceDescription)
    {
        InitializeComponent();

        _pluginName = $"{sourceName} Copy";
        _pluginId = BuildId($"{sourceId}-copy");
        _pluginDescription = sourceDescription;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PluginName
    {
        get => _pluginName;
        set
        {
            if (!SetProperty(ref _pluginName, value))
            {
                return;
            }

            if (_isEditingId)
            {
                return;
            }

            _pluginId = BuildId(value);
            OnPropertyChanged(nameof(PluginId));
            OnPropertyChanged(nameof(HintText));
        }
    }

    public string PluginId
    {
        get => _pluginId;
        set
        {
            _isEditingId = true;
            var sanitized = BuildId(value);
            if (SetProperty(ref _pluginId, sanitized))
            {
                OnPropertyChanged(nameof(HintText));
            }
        }
    }

    public string PluginDescription
    {
        get => _pluginDescription;
        set => SetProperty(ref _pluginDescription, value);
    }

    public string HintText => $"File name: {PluginId}.cursorfx-plugin.json";

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PluginName))
        {
            System.Windows.MessageBox.Show("Profile name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PluginId))
        {
            System.Windows.MessageBox.Show("Profile id is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string BuildId(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "custom-profile" : value.Trim().ToLowerInvariant();
        normalized = IdSanitizer.Replace(normalized.Replace(' ', '-'), "-");
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "custom-profile" : normalized;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
