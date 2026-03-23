using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;

namespace CursorFX.App;

public partial class ImportPluginWindow : Window, INotifyPropertyChanged
{
    private string _sourcePath = string.Empty;
    private string _manifestPath = string.Empty;
    private string _iconPath = string.Empty;

    public ImportPluginWindow()
    {
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public string ManifestPath
    {
        get => _manifestPath;
        set => SetProperty(ref _manifestPath, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (!SetProperty(ref _iconPath, value))
            {
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconFileName)));
        }
    }

    public string IconFileName => string.IsNullOrWhiteSpace(IconPath)
        ? "No icon selected."
        : Path.GetFileName(IconPath);

    private void OnBrowseSourceClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "C# files (*.cs)|*.cs",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FileName;
        }
    }

    private void OnBrowseManifestClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CursorFX plugins (*.cursorfx-plugin.json)|*.cursorfx-plugin.json|JSON files (*.json)|*.json",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            ManifestPath = dialog.FileName;
        }
    }

    private void OnBrowseIconClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.webp|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            IconPath = dialog.FileName;
        }
    }

    private void OnClearIconClick(object sender, RoutedEventArgs e)
    {
        IconPath = string.Empty;
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) && string.IsNullOrWhiteSpace(ManifestPath))
        {
            System.Windows.MessageBox.Show("Choose at least one file: C# source or JSON manifest.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
