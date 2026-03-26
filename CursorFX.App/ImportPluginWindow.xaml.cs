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
    private readonly LocalizationService _localizationService;

    public ImportPluginWindow(string pluginWorkspacePath, LocalizationService localizationService)
    {
        _pluginWorkspacePath = pluginWorkspacePath;
        _localizationService = localizationService;
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WindowTitle => _localizationService.Get("import.windowTitle");
    public string HeadingText => _localizationService.Get("import.heading");
    public string IntroText => _localizationService.Get("import.intro");
    public string AssemblyTitleText => _localizationService.Get("import.assemblyTitle");
    public string AssemblyHintText => _localizationService.Get("import.assemblyHint");
    public string BrowseDllButtonText => _localizationService.Get("import.browseDll");
    public string OpenWorkspaceButtonText => _localizationService.Get("import.openPluginsFolder");
    public string PluginTypeTitleText => _localizationService.Get("import.pluginTypeTitle");
    public string PluginTypeHintText => _localizationService.Get("import.pluginTypeHint");
    public string PreviewTitleText => _localizationService.Get("import.previewTitle");
    public string PreviewHintText => _localizationService.Get("import.previewHint");
    public string PreviewDisplayNameText => _localizationService.Get("import.preview.displayName");
    public string PreviewPluginIdText => _localizationService.Get("import.preview.pluginId");
    public string PreviewEntryTypeText => _localizationService.Get("import.preview.entryType");
    public string IconTitleText => _localizationService.Get("import.iconTitle");
    public string IconHintText => _localizationService.Get("import.iconHint");
    public string ChooseIconButtonText => _localizationService.Get("import.chooseIcon");
    public string ClearIconButtonText => _localizationService.Get("import.clearIcon");
    public string IconPlaceholderText => _localizationService.Get("import.iconPlaceholder");
    public string CancelButtonText => _localizationService.Get("import.cancel");
    public string ImportButtonText => _localizationService.Get("import.confirm");

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
        ? _localizationService.Get("import.iconNone")
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
            System.Windows.MessageBox.Show(_localizationService.Get("import.validation.chooseDll"), _localizationService.Get("import.validation.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (AvailablePlugins.Count > 1 && SelectedPluginCandidate is null)
        {
            System.Windows.MessageBox.Show(_localizationService.Get("import.validation.choosePluginType"), _localizationService.Get("import.validation.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
