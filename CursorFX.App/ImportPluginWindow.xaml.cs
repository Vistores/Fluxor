using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorFX.App.Services;

namespace CursorFX.App;

public partial class ImportPluginWindow : Window, INotifyPropertyChanged
{
    private string _assemblyPath = string.Empty;
    private string _iconPath = string.Empty;
    private PluginAssemblyCandidate? _selectedPluginCandidate;
    private readonly AssemblyPluginImporter _assemblyPluginImporter = new();
    private readonly string _pluginWorkspacePath;

    public ImportPluginWindow(string pluginWorkspacePath)
    {
        _pluginWorkspacePath = pluginWorkspacePath;
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AssemblyPath
    {
        get => _assemblyPath;
        set
        {
            if (!SetProperty(ref _assemblyPath, value))
            {
                return;
            }

            LoadPluginCandidates();
        }
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

    public ObservableCollection<PluginAssemblyCandidate> AvailablePlugins { get; } = [];

    public PluginAssemblyCandidate? SelectedPluginCandidate
    {
        get => _selectedPluginCandidate;
        set
        {
            if (!SetProperty(ref _selectedPluginCandidate, value))
            {
                return;
            }

            RaisePluginPreviewProperties();
        }
    }

    public string SelectedPluginSummary => SelectedPluginCandidate is null
        ? "No plugin type selected yet."
        : $"{SelectedPluginCandidate.DisplayName} - {SelectedPluginCandidate.PluginId}{Environment.NewLine}{SelectedPluginCandidate.Description}";

    public string SelectedPluginDisplayName => SelectedPluginCandidate?.DisplayName ?? "Not selected";

    public string SelectedPluginId => SelectedPluginCandidate?.PluginId ?? "Will be generated from the selected plugin.";

    public string SelectedPluginEntryType => SelectedPluginCandidate?.EntryTypeName ?? "Select a plugin type to preview it.";

    private void OnBrowseAssemblyClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DLL files (*.dll)|*.dll",
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(_pluginWorkspacePath) ? _pluginWorkspacePath : null
        };

        if (dialog.ShowDialog() == true)
        {
            AssemblyPath = dialog.FileName;
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

    private void OnOpenWorkspaceClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_pluginWorkspacePath);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{_pluginWorkspacePath}\"",
            UseShellExecute = true
        });
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AssemblyPath))
        {
            System.Windows.MessageBox.Show("Choose a DLL file.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (AvailablePlugins.Count > 1 && SelectedPluginCandidate is null)
        {
            System.Windows.MessageBox.Show("Choose a plugin type from the DLL.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void LoadPluginCandidates()
    {
        AvailablePlugins.Clear();
        SelectedPluginCandidate = null;

        if (string.IsNullOrWhiteSpace(AssemblyPath) || !File.Exists(AssemblyPath))
        {
            RaisePluginPreviewProperties();
            return;
        }

        try
        {
            foreach (var plugin in _assemblyPluginImporter.DiscoverPlugins(AssemblyPath))
            {
                AvailablePlugins.Add(plugin);
            }

            if (AvailablePlugins.Count == 1)
            {
                SelectedPluginCandidate = AvailablePlugins[0];
            }
        }
        catch
        {
            AvailablePlugins.Clear();
            SelectedPluginCandidate = null;
        }

        RaisePluginPreviewProperties();
    }

    private void RaisePluginPreviewProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginDisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginId)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginEntryType)));
    }
}
