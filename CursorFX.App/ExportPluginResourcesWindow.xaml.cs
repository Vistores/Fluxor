using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;

namespace CursorFX.App;

public partial class ExportPluginResourcesWindow : Window, INotifyPropertyChanged
{
    private bool _exportJson = true;
    private bool _exportSource;
    private string _jsonPath;
    private string _sourcePath;

    public ExportPluginResourcesWindow(string defaultJsonPath, string? defaultSourcePath)
    {
        _jsonPath = defaultJsonPath;
        _sourcePath = defaultSourcePath ?? string.Empty;
        _exportSource = !string.IsNullOrWhiteSpace(defaultSourcePath);
        HasSourceFile = !string.IsNullOrWhiteSpace(defaultSourcePath);
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasSourceFile { get; }

    public double SourceSectionOpacity => HasSourceFile ? 1.0 : 0.65;

    public string SourceStatusText => HasSourceFile
        ? "CursorFX found a source file for this plugin. You can export it together with the JSON manifest."
        : "No source file is registered for this plugin, so only the JSON manifest can be exported.";

    public bool ExportJson
    {
        get => _exportJson;
        set => SetProperty(ref _exportJson, value);
    }

    public bool ExportSource
    {
        get => _exportSource;
        set => SetProperty(ref _exportSource, value);
    }

    public string JsonPath
    {
        get => _jsonPath;
        set => SetProperty(ref _jsonPath, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    private void OnBrowseJsonClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CursorFX plugins (*.cursorfx-plugin.json)|*.cursorfx-plugin.json",
            FileName = System.IO.Path.GetFileName(JsonPath),
            AddExtension = true
        };

        if (dialog.ShowDialog() == true)
        {
            JsonPath = dialog.FileName;
        }
    }

    private void OnBrowseSourceClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "C# files (*.cs)|*.cs",
            FileName = System.IO.Path.GetFileName(SourcePath),
            AddExtension = true
        };

        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FileName;
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (!ExportJson && !ExportSource)
        {
            System.Windows.MessageBox.Show("Choose at least one resource to export.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ExportJson && string.IsNullOrWhiteSpace(JsonPath))
        {
            System.Windows.MessageBox.Show("Choose a path for the JSON manifest.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ExportSource && string.IsNullOrWhiteSpace(SourcePath))
        {
            System.Windows.MessageBox.Show("Choose a path for the C# source file.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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
