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
    private readonly IReadOnlyList<PluginImportMatch> _existingImportedPlugins;
    private readonly AssemblyPluginImporter _assemblyPluginImporter = new();
    private readonly string _pluginWorkspacePath;
    private readonly LocalizationService _localizationService;
    private PluginImportMatch? _matchedExistingPlugin;
    private bool _replaceExistingPlugin;

    public ImportPluginWindow(
        string pluginWorkspacePath,
        LocalizationService localizationService,
        IReadOnlyList<PluginImportMatch>? existingImportedPlugins = null)
    {
        _pluginWorkspacePath = pluginWorkspacePath;
        _localizationService = localizationService;
        _existingImportedPlugins = existingImportedPlugins ?? [];
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
    public string InstallModeTitleText => _localizationService.Get("import.installModeTitle");
    public string InstallModeHintText => _localizationService.Get("import.installModeHint");
    public string ReplaceExistingText => _localizationService.Get("import.replaceExisting");
    public string ImportAsNewText => _localizationService.Get("import.importAsNew");

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
        ? _localizationService.Get("import.preview.noSelection")
        : string.Format(
            _localizationService.Get("import.preview.summary"),
            SelectedPluginCandidate.DisplayName,
            SelectedPluginCandidate.PluginId,
            SelectedPluginCandidate.Description);

    public string SelectedPluginDisplayName => SelectedPluginCandidate?.DisplayName ?? _localizationService.Get("import.preview.notSelected");

    public string SelectedPluginId => SelectedPluginCandidate?.PluginId ?? _localizationService.Get("import.preview.generatedId");

    public string SelectedPluginEntryType => SelectedPluginCandidate?.EntryTypeName ?? _localizationService.Get("import.preview.chooseType");

    public PluginImportMatch? MatchedExistingPlugin
    {
        get => _matchedExistingPlugin;
        private set
        {
            if (!SetProperty(ref _matchedExistingPlugin, value))
            {
                return;
            }

            if (_matchedExistingPlugin is null)
            {
                ReplaceExistingPlugin = false;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMatchedExistingPlugin)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MatchedExistingPluginSummary)));
        }
    }

    public bool HasMatchedExistingPlugin => MatchedExistingPlugin is not null;

    public bool ReplaceExistingPlugin
    {
        get => _replaceExistingPlugin;
        set => SetProperty(ref _replaceExistingPlugin, value);
    }

    public string MatchedExistingPluginSummary => MatchedExistingPlugin is null
        ? _localizationService.Get("import.match.none")
        : string.Format(
            _localizationService.Get("import.match.summary"),
            MatchedExistingPlugin.Name,
            MatchedExistingPlugin.Id,
            MatchedExistingPlugin.MatchReason);

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
        MatchedExistingPlugin = null;

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
        UpdateMatchedExistingPlugin();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginDisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginId)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPluginEntryType)));
    }

    private void UpdateMatchedExistingPlugin()
    {
        if (SelectedPluginCandidate is null)
        {
            MatchedExistingPlugin = null;
            return;
        }

        var idMatch = _existingImportedPlugins.FirstOrDefault(match =>
            string.Equals(match.Id, SelectedPluginCandidate.PluginId, StringComparison.OrdinalIgnoreCase));
        if (idMatch is not null)
        {
            MatchedExistingPlugin = idMatch with { MatchReason = _localizationService.Get("import.match.reason.pluginId") };
            return;
        }

        var entryTypeMatch = _existingImportedPlugins.FirstOrDefault(match =>
            !string.IsNullOrWhiteSpace(match.EntryTypeName) &&
            string.Equals(match.EntryTypeName, SelectedPluginCandidate.EntryTypeName, StringComparison.Ordinal));
        MatchedExistingPlugin = entryTypeMatch is null
            ? null
            : entryTypeMatch with { MatchReason = _localizationService.Get("import.match.reason.entryType") };
    }
}

public sealed record PluginImportMatch(
    string Id,
    string Name,
    string EntryTypeName,
    string MatchReason);
