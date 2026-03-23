using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CursorFX.App.Services;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;
using CursorFX.Effects;
using CursorFX.Rendering;
using Microsoft.Win32;

namespace CursorFX.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppSettings _settings;
    private readonly TrailEffect _trailEffect;
    private readonly GlowEffect _glowEffect;
    private readonly ClickRippleEffect _rippleEffect;
    private readonly TemplateEffect _templateEffect;
    private readonly CustomPluginEffect _customPluginEffect;
    private readonly CursorFxEngine _engine;
    private readonly ISettingsStore _settingsStore;
    private readonly IShaderTemplateCatalog _templateCatalog;
    private readonly SourcePluginCompiler _sourcePluginCompiler;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly DispatcherTimer _autosaveTimer;
    private ShaderTemplateDefinition? _selectedPlugin;
    private string _autosaveStatus = "Settings are synced automatically.";

    public MainViewModel(
        AppSettings settings,
        TrailEffect trailEffect,
        GlowEffect glowEffect,
        ClickRippleEffect rippleEffect,
        TemplateEffect templateEffect,
        CustomPluginEffect customPluginEffect,
        CursorFxEngine engine,
        ISettingsStore settingsStore,
        IShaderTemplateCatalog templateCatalog)
    {
        _settings = settings;
        _trailEffect = trailEffect;
        _glowEffect = glowEffect;
        _rippleEffect = rippleEffect;
        _templateEffect = templateEffect;
        _customPluginEffect = customPluginEffect;
        _engine = engine;
        _settingsStore = settingsStore;
        _templateCatalog = templateCatalog;
        _sourcePluginCompiler = new SourcePluginCompiler();
        _startupRegistrationService = new StartupRegistrationService();

        _autosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _autosaveTimer.Tick += OnAutosaveTick;

        AvailablePlugins = new ObservableCollection<ShaderTemplateDefinition>(_templateCatalog.LoadTemplates());
        ImportTemplateCommand = new RelayCommand(ImportPlugin);
        OpenPluginFolderCommand = new RelayCommand(OpenPluginFolder);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        DeletePluginCommand = new RelayCommand(DeleteSelectedPlugin, CanDeleteSelectedPlugin);
        ResetPluginSettingsCommand = new RelayCommand(ResetSelectedPluginSettings, () => SelectedPlugin is not null);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        ChoosePluginIconCommand = new RelayCommand(ChoosePluginIcon, () => SelectedPlugin is not null);
        ClearPluginIconCommand = new RelayCommand(ClearPluginIcon, () => SelectedPlugin is not null && !string.IsNullOrWhiteSpace(SelectedPlugin.ResolvedIconPath));

        _selectedPlugin = AvailablePlugins.FirstOrDefault(plugin => plugin.Id == _settings.TemplateEffect.SelectedTemplateId)
            ?? AvailablePlugins.FirstOrDefault(plugin => plugin.Id == "neon-suite")
            ?? AvailablePlugins.FirstOrDefault();

        EnsurePluginValueState();
        BuildPluginCategories();
        ApplyPluginToSettings();
        ApplyRuntimeSettings();
        ApplyStartupRegistration();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ShaderTemplateDefinition> AvailablePlugins { get; }

    public ObservableCollection<PluginCategoryViewModel> PluginCategories { get; } = [];

    public RelayCommand ImportTemplateCommand { get; }

    public RelayCommand OpenPluginFolderCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand DeletePluginCommand { get; }

    public RelayCommand ResetPluginSettingsCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand ChoosePluginIconCommand { get; }

    public RelayCommand ClearPluginIconCommand { get; }

    public ShaderTemplateDefinition? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (!SetProperty(ref _selectedPlugin, value))
            {
                return;
            }

            EnsurePluginValueState();
            BuildPluginCategories();
            ApplyPluginToSettings();
            ApplyRuntimeSettings();
            OnPropertyChanged(nameof(SelectedPluginName));
            OnPropertyChanged(nameof(SelectedPluginDescription));
            OnPropertyChanged(nameof(SelectedPluginResolvedIconPath));
            ScheduleAutosave("Plugin profile changed.");
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SelectedPluginName => SelectedPlugin?.Name ?? "No plugin selected";

    public string SelectedPluginDescription => SelectedPlugin?.Description ?? "Select a plugin profile.";

    public string SelectedPluginResolvedIconPath => SelectedPlugin?.ResolvedIconPath ?? string.Empty;

    public double MasterOpacity
    {
        get => _settings.General.MasterOpacity;
        set
        {
            if (!SetNestedDouble(_settings.General.MasterOpacity, value, v => _settings.General.MasterOpacity = v))
            {
                return;
            }

            ApplyRuntimeSettings();
            OnPropertyChanged(nameof(EffectOpacityLabel));
            ScheduleAutosave("General settings changed.");
        }
    }

    public double TargetFps
    {
        get => _settings.General.TargetFps;
        set
        {
            var normalized = (int)Math.Round(value);
            if (_settings.General.TargetFps == normalized)
            {
                return;
            }

            _settings.General.TargetFps = normalized;
            ApplyRuntimeSettings();
            OnPropertyChanged();
            OnPropertyChanged(nameof(FpsLabel));
            ScheduleAutosave("General settings changed.");
        }
    }

    public string EffectOpacityLabel => $"Master opacity: {_settings.General.MasterOpacity:P0}";

    public string FpsLabel => $"FPS cap: {_settings.General.TargetFps}";

    public string PluginFolderPath => _templateCatalog.CatalogDirectory;

    public string PluginAuthoringGuidePath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CursorFX.App", "Templates", "plugin-authoring-guide.txt"));

    public bool RunInBackgroundEnabled => _settings.General.RunInBackground;

    public string AutosaveStatus
    {
        get => _autosaveStatus;
        private set => SetProperty(ref _autosaveStatus, value);
    }

    public void Dispose()
    {
        _autosaveTimer.Stop();
        _autosaveTimer.Tick -= OnAutosaveTick;
    }

    private void EnsurePluginValueState()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        GetOrCreatePluginValues(SelectedPlugin);
    }

    private void BuildPluginCategories()
    {
        PluginCategories.Clear();
        if (SelectedPlugin is null)
        {
            return;
        }

        var storedValues = GetOrCreatePluginValues(SelectedPlugin);
        foreach (var group in SelectedPlugin.Parameters
                     .GroupBy(GetSectionName)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var category = new PluginCategoryViewModel
            {
                Name = group.Key
            };

            foreach (var parameter in group)
            {
                category.Parameters.Add(new ShaderTemplateParameterViewModel(
                    parameter,
                    GetStoredParameterValue(parameter, storedValues),
                    OnPluginParameterChanged,
                    PickPluginColor));
            }

            PluginCategories.Add(category);
        }
    }

    private static TemplateParameterValue GetStoredParameterValue(
        TemplateParameterDefinition parameter,
        IReadOnlyDictionary<string, TemplateParameterValue> storedValues)
    {
        if (storedValues.TryGetValue(parameter.Key, out var value))
        {
            return new TemplateParameterValue
            {
                NumberValue = value.NumberValue,
                ColorValue = value.ColorValue,
                BooleanValue = value.BooleanValue
            };
        }

        return new TemplateParameterValue
        {
            NumberValue = parameter.DefaultNumber,
            ColorValue = parameter.DefaultColor,
            BooleanValue = parameter.DefaultBoolean
        };
    }

    private void OnPluginParameterChanged()
    {
        PersistPluginValues();
        ApplyPluginToSettings();
        ApplyRuntimeSettings();
        ScheduleAutosave("Plugin parameters changed.");
    }

    private void PersistPluginValues()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        var values = new Dictionary<string, TemplateParameterValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in PluginCategories)
        {
            foreach (var parameter in category.Parameters)
            {
                values[parameter.Key] = parameter.ToValue();
            }
        }

        _settings.TemplateEffect.SelectedTemplateId = SelectedPlugin.Id;
        _settings.TemplateEffect.PluginParameterValues[SelectedPlugin.Id] = values;
    }

    private Dictionary<string, TemplateParameterValue> GetOrCreatePluginValues(ShaderTemplateDefinition plugin)
    {
        if (_settings.TemplateEffect.PluginParameterValues.TryGetValue(plugin.Id, out var values))
        {
            return values;
        }

        values = plugin.Parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => new TemplateParameterValue
            {
                NumberValue = parameter.DefaultNumber,
                ColorValue = parameter.DefaultColor,
                BooleanValue = parameter.DefaultBoolean
            },
            StringComparer.OrdinalIgnoreCase);

        _settings.TemplateEffect.PluginParameterValues[plugin.Id] = values;
        return values;
    }

    private void ApplyPluginToSettings()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        PersistPluginValues();
        var values = GetOrCreatePluginValues(SelectedPlugin);

        _settings.Trail.IsEnabled = GetBool(values, "trailEnabled", true);
        _settings.Trail.MaxPoints = (int)Math.Round(GetNumber(values, "trailLength", 32));
        _settings.Trail.Thickness = GetNumber(values, "trailThickness", 12);
        _settings.Trail.FadeSeconds = GetNumber(values, "trailFade", 0.55);
        _settings.Trail.Color = GetColor(values, "trailColor", "#22D3EE");

        _settings.Glow.IsEnabled = GetBool(values, "glowEnabled", true);
        _settings.Glow.Size = GetNumber(values, "glowSize", 32);
        _settings.Glow.Opacity = GetNumber(values, "glowOpacity", 0.42);
        _settings.Glow.Color = GetColor(values, "glowColor", "#67E8F9");

        _settings.Ripple.IsEnabled = GetBool(values, "rippleEnabled", true);
        _settings.Ripple.MaxRadius = GetNumber(values, "rippleRadius", 86);
        _settings.Ripple.LifetimeSeconds = GetNumber(values, "rippleLifetime", 0.7);
        _settings.Ripple.Opacity = GetNumber(values, "rippleOpacity", 0.75);
        _settings.Ripple.Thickness = GetNumber(values, "rippleThickness", 3);
        _settings.Ripple.Color = GetColor(values, "rippleColor", "#A5F3FC");

        _settings.TemplateEffect.SelectedTemplateId = SelectedPlugin.Id;
        _settings.TemplateEffect.IsEnabled = GetBool(values, "shaderEnabled", true);
    }

    private string? PickPluginColor(string title, string currentColor)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var picker = new ColorPickerWindow(title, currentColor)
        {
            Owner = owner
        };

        return picker.ShowDialog() == true ? picker.SelectedColor : null;
    }

    private void ImportPlugin()
    {
        var importWindow = new ImportPluginWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (importWindow.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(importWindow.SourcePath))
            {
                var result = _sourcePluginCompiler.CompileAndInstall(
                    importWindow.SourcePath,
                    _templateCatalog.CatalogDirectory,
                    string.IsNullOrWhiteSpace(importWindow.ManifestPath) ? null : importWindow.ManifestPath,
                    string.IsNullOrWhiteSpace(importWindow.IconPath) ? null : importWindow.IconPath);
                ReloadPlugins(result.Definition.Id);
                AutosaveStatus = $"Plugin {result.Definition.Name} compiled and imported.";
                return;
            }

            var imported = _templateCatalog.ImportTemplate(
                importWindow.ManifestPath,
                string.IsNullOrWhiteSpace(importWindow.IconPath) ? null : importWindow.IconPath);
            ReloadPlugins(imported.Id);
            AutosaveStatus = $"Plugin {imported.Name} imported.";
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, "Plugin Import Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenPluginFolder()
    {
        OpenFolder(PluginFolderPath, "Plugin folder opened.");
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_settings.General)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (settingsWindow.ShowDialog() != true)
        {
            return;
        }

        _settings.General.LaunchOnStartup = settingsWindow.LaunchOnStartup;
        _settings.General.RunInBackground = settingsWindow.RunInBackground;
        _settings.General.PauseWhenCursorHidden = settingsWindow.PauseWhenCursorHidden;

        OnPropertyChanged(nameof(RunInBackgroundEnabled));
        ApplyRuntimeSettings();
        ApplyStartupRegistration();
        SaveSettings();
        AutosaveStatus = "Application settings updated.";
    }

    private void DeleteSelectedPlugin()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        if (IsBuiltInPlugin(SelectedPlugin.Id))
        {
            AutosaveStatus = "Built-in profiles cannot be deleted.";
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Delete plugin '{SelectedPlugin.Name}'?\n\nThis removes its JSON profile from the plugin catalog. For external plugins CursorFX will also try to remove the DLL if no other profile references it.",
            "Delete Plugin",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var pluginToDelete = SelectedPlugin;
            var manifestPath = Path.Combine(_templateCatalog.CatalogDirectory, $"{pluginToDelete.Id}.cursorfx-plugin.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            _settings.TemplateEffect.PluginParameterValues.Remove(pluginToDelete.Id);
            TryDeletePluginAssembly(pluginToDelete);

            var fallbackId = AvailablePlugins
                .Where(plugin => !string.Equals(plugin.Id, pluginToDelete.Id, StringComparison.OrdinalIgnoreCase))
                .Select(plugin => plugin.Id)
                .FirstOrDefault();

            ReloadPlugins(fallbackId);
            AutosaveStatus = $"Plugin {pluginToDelete.Name} deleted.";
            ScheduleAutosave("Plugin deleted.");
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, "Delete Plugin Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReloadPlugins(string? selectedPluginId = null)
    {
        AvailablePlugins.Clear();
        foreach (var plugin in _templateCatalog.LoadTemplates())
        {
            AvailablePlugins.Add(plugin);
        }

        SelectedPlugin = AvailablePlugins.FirstOrDefault(plugin => plugin.Id == selectedPluginId)
            ?? AvailablePlugins.FirstOrDefault(plugin => plugin.Id == _settings.TemplateEffect.SelectedTemplateId)
            ?? AvailablePlugins.FirstOrDefault();
    }

    private void SaveSettings()
    {
        PersistPluginValues();
        ApplyStartupRegistration();
        _settingsStore.Save(_settings);
        AutosaveStatus = $"Saved at {DateTime.Now:T}";
    }

    private void ResetSelectedPluginSettings()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        _settings.TemplateEffect.PluginParameterValues[SelectedPlugin.Id] = SelectedPlugin.Parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => new TemplateParameterValue
            {
                NumberValue = parameter.DefaultNumber,
                ColorValue = parameter.DefaultColor,
                BooleanValue = parameter.DefaultBoolean
            },
            StringComparer.OrdinalIgnoreCase);

        BuildPluginCategories();
        ApplyPluginToSettings();
        ApplyRuntimeSettings();
        AutosaveStatus = $"Plugin {SelectedPlugin.Name} reset to defaults.";
        ScheduleAutosave("Plugin settings reset to defaults.");
    }

    private void ChoosePluginIcon()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Choose icon for {SelectedPlugin.Name}",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.webp|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var updated = _templateCatalog.SaveTemplate(SelectedPlugin, dialog.FileName);
            ReloadPlugins(updated.Id);
            AutosaveStatus = $"Icon updated for {updated.Name}.";
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, "Plugin Icon Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearPluginIcon()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        try
        {
            var updated = _templateCatalog.SaveTemplate(new ShaderTemplateDefinition
            {
                Id = SelectedPlugin.Id,
                Name = SelectedPlugin.Name,
                Description = SelectedPlugin.Description,
                IconGlyph = SelectedPlugin.IconGlyph,
                IconPath = string.Empty,
                ResolvedIconPath = string.Empty,
                AccentColor = SelectedPlugin.AccentColor,
                RuntimeKind = SelectedPlugin.RuntimeKind,
                AssemblyFileName = SelectedPlugin.AssemblyFileName,
                EntryTypeName = SelectedPlugin.EntryTypeName,
                Kind = SelectedPlugin.Kind,
                Trigger = SelectedPlugin.Trigger,
                Parameters = SelectedPlugin.Parameters
            });

            ReloadPlugins(updated.Id);
            AutosaveStatus = $"Icon cleared for {updated.Name}.";
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, "Plugin Icon Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyRuntimeSettings()
    {
        var useExternalPlugin = SelectedPlugin?.RuntimeKind == TemplateRuntimeKind.ExternalAssembly;

        if (useExternalPlugin)
        {
            _trailEffect.UpdateSettings(
                new TrailSettings
                {
                    IsEnabled = false,
                    MaxPoints = _settings.Trail.MaxPoints,
                    Thickness = _settings.Trail.Thickness,
                    FadeSeconds = _settings.Trail.FadeSeconds,
                    Color = _settings.Trail.Color
                },
                _settings.General.MasterOpacity);

            _glowEffect.UpdateSettings(
                new GlowSettings
                {
                    IsEnabled = false,
                    Size = _settings.Glow.Size,
                    Opacity = _settings.Glow.Opacity,
                    Color = _settings.Glow.Color
                },
                _settings.General.MasterOpacity);

            _rippleEffect.UpdateSettings(
                new RippleSettings
                {
                    IsEnabled = false,
                    MaxRadius = _settings.Ripple.MaxRadius,
                    LifetimeSeconds = _settings.Ripple.LifetimeSeconds,
                    Opacity = _settings.Ripple.Opacity,
                    Thickness = _settings.Ripple.Thickness,
                    Color = _settings.Ripple.Color
                },
                _settings.General.MasterOpacity);
        }
        else
        {
            _trailEffect.UpdateSettings(_settings.Trail, _settings.General.MasterOpacity);
            _glowEffect.UpdateSettings(_settings.Glow, _settings.General.MasterOpacity);
            _rippleEffect.UpdateSettings(_settings.Ripple, _settings.General.MasterOpacity);
        }

        _templateEffect.UpdateTemplate(
            SelectedPlugin?.RuntimeKind == TemplateRuntimeKind.BuiltInTemplate ? SelectedPlugin : null,
            SelectedPlugin is null ? new Dictionary<string, TemplateParameterValue>(StringComparer.OrdinalIgnoreCase) : GetOrCreatePluginValues(SelectedPlugin),
            _settings.TemplateEffect.IsEnabled,
            _settings.General.MasterOpacity);
        try
        {
            _customPluginEffect.UpdatePlugin(
                SelectedPlugin,
                SelectedPlugin is null ? new Dictionary<string, TemplateParameterValue>(StringComparer.OrdinalIgnoreCase) : GetOrCreatePluginValues(SelectedPlugin),
                _settings.General.MasterOpacity);
        }
        catch (Exception ex)
        {
            _customPluginEffect.IsEnabled = false;
            AutosaveStatus = ex.Message;
        }
        _engine.SetTargetFps(_settings.General.TargetFps);
        _engine.SetPauseWhenCursorHidden(_settings.General.PauseWhenCursorHidden);
    }

    private void ApplyStartupRegistration()
    {
        try
        {
            _startupRegistrationService.Apply(_settings.General.LaunchOnStartup);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
        }
    }

    private static string GetSectionName(TemplateParameterDefinition parameter)
    {
        return string.IsNullOrWhiteSpace(parameter.SectionName)
            ? parameter.Section.ToString()
            : parameter.SectionName;
    }

    private static IEnumerable<string> EnumeratePluginAssemblyFiles(string assemblyPath)
    {
        yield return assemblyPath;

        var directory = Path.GetDirectoryName(assemblyPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyPath);
        if (directory is null || fileNameWithoutExtension.Length == 0)
        {
            yield break;
        }

        foreach (var extension in new[] { ".pdb", ".deps.json", ".runtimeconfig.json" })
        {
            var companionPath = Path.Combine(directory, fileNameWithoutExtension + extension);
            if (File.Exists(companionPath))
            {
                yield return companionPath;
            }
        }
    }

    private bool CanDeleteSelectedPlugin()
    {
        return SelectedPlugin is not null && !IsBuiltInPlugin(SelectedPlugin.Id);
    }

    private static bool IsBuiltInPlugin(string pluginId)
    {
        return pluginId is "neon-suite" or "minimal-suite" or "gaming-suite";
    }

    private void TryDeletePluginAssembly(ShaderTemplateDefinition plugin)
    {
        if (plugin.RuntimeKind != TemplateRuntimeKind.ExternalAssembly || string.IsNullOrWhiteSpace(plugin.AssemblyFileName))
        {
            return;
        }

        var isSharedAssembly = _templateCatalog.LoadTemplates()
            .Any(other =>
                !string.Equals(other.Id, plugin.Id, StringComparison.OrdinalIgnoreCase) &&
                other.RuntimeKind == TemplateRuntimeKind.ExternalAssembly &&
                string.Equals(other.AssemblyFileName, plugin.AssemblyFileName, StringComparison.OrdinalIgnoreCase));
        if (isSharedAssembly)
        {
            return;
        }

        var assemblyPath = Path.Combine(_templateCatalog.CatalogDirectory, plugin.AssemblyFileName);
        foreach (var filePath in EnumeratePluginAssemblyFiles(assemblyPath))
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                AutosaveStatus = $"Plugin profile removed, but some runtime files are still in use: {Path.GetFileName(filePath)}";
            }
        }
    }

    private void OpenFolder(string folderPath, string successStatus)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
            AutosaveStatus = successStatus;
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, "Open Folder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ScheduleAutosave(string status)
    {
        AutosaveStatus = status;
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private void OnAutosaveTick(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();
        SaveSettings();
    }

    private static double GetNumber(IReadOnlyDictionary<string, TemplateParameterValue> values, string key, double fallback)
    {
        return values.TryGetValue(key, out var value) && value.NumberValue.HasValue
            ? value.NumberValue.Value
            : fallback;
    }

    private static bool GetBool(IReadOnlyDictionary<string, TemplateParameterValue> values, string key, bool fallback)
    {
        return values.TryGetValue(key, out var value) && value.BooleanValue.HasValue
            ? value.BooleanValue.Value
            : fallback;
    }

    private static string GetColor(IReadOnlyDictionary<string, TemplateParameterValue> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.ColorValue)
            ? value.ColorValue!
            : fallback;
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

    private bool SetNestedDouble(double currentValue, double newValue, Action<double> setter, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(currentValue - newValue) < 0.0001)
        {
            return false;
        }

        setter(newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
