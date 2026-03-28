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
    private readonly LocalizationService _localizationService;
    private readonly AssemblyPluginImporter _assemblyPluginImporter;
    private readonly ProfileArchiveService _profileArchiveService;
    private readonly PluginWorkspaceService _pluginWorkspaceService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly DispatcherTimer _autosaveTimer;
    private ShaderTemplateDefinition? _selectedPlugin;
    private string _autosaveStatus;
    private QualityPresetOption? _selectedQualityPreset;
    private bool _isEffectOperationInProgress;
    private string _effectOperationTitle = "Updating Cursor Effects";
    private string _effectOperationMessage = "Please wait while Fluxor applies the current cursor effect.";

    public MainViewModel(
        AppSettings settings,
        TrailEffect trailEffect,
        GlowEffect glowEffect,
        ClickRippleEffect rippleEffect,
        TemplateEffect templateEffect,
        CustomPluginEffect customPluginEffect,
        CursorFxEngine engine,
        ISettingsStore settingsStore,
        IShaderTemplateCatalog templateCatalog,
        LocalizationService localizationService)
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
        _localizationService = localizationService;
        _autosaveStatus = _localizationService.Get("main.autosaveReady");
        _assemblyPluginImporter = new AssemblyPluginImporter();
        _profileArchiveService = new ProfileArchiveService();
        _pluginWorkspaceService = new PluginWorkspaceService();
        _pluginWorkspaceService.EnsureWorkspace();
        _startupRegistrationService = new StartupRegistrationService();
        QualityPresets =
        [
            new QualityPresetOption(EffectQualityPreset.Low, () => _localizationService.Get("main.quality.low")),
            new QualityPresetOption(EffectQualityPreset.Balanced, () => _localizationService.Get("main.quality.balanced")),
            new QualityPresetOption(EffectQualityPreset.High, () => _localizationService.Get("main.quality.high"))
        ];

        _autosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _autosaveTimer.Tick += OnAutosaveTick;

        AvailablePlugins = new ObservableCollection<ShaderTemplateDefinition>(_templateCatalog.LoadTemplates());
        RefreshPluginCollections();
        ImportTemplateCommand = new RelayCommand(ImportPlugin);
        OpenPluginFolderCommand = new RelayCommand(OpenPluginFolder);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        SaveProfileAsCommand = new RelayCommand(SaveSelectedProfileAs, () => SelectedPlugin is not null);
        ExportProfileArchiveCommand = new RelayCommand(ExportSelectedProfileArchive, () => SelectedPlugin is not null);
        ImportProfileArchiveCommand = new RelayCommand(ImportProfileArchive);
        ReloadPluginRuntimeCommand = new RelayCommand(ReloadSelectedPluginRuntime, () => IsExternalPluginSelected);
        DeletePluginCommand = new RelayCommand(DeleteSelectedPlugin, CanDeleteSelectedPlugin);
        ResetPluginSettingsCommand = new RelayCommand(ResetSelectedPluginSettingsWithFeedback, () => SelectedPlugin is not null);
        SaveSettingsCommand = new RelayCommand(SaveSettingsWithFeedback);
        ChoosePluginIconCommand = new RelayCommand(ChoosePluginIcon, () => SelectedPlugin is not null);
        ClearPluginIconCommand = new RelayCommand(ClearPluginIcon, () => SelectedPlugin is not null && !string.IsNullOrWhiteSpace(SelectedPlugin.ResolvedIconPath));
        OpenPluginAuthoringGuideCommand = new RelayCommand(OpenPluginAuthoringGuide);

        _selectedPlugin = AvailablePlugins.FirstOrDefault(plugin => plugin.Id == _settings.TemplateEffect.SelectedTemplateId)
            ?? AvailablePlugins.FirstOrDefault(plugin => plugin.Id == "neon-suite")
            ?? AvailablePlugins.FirstOrDefault();

        EnsurePluginValueState();
        BuildPluginCategories();
        ApplyPluginToSettings();
        ApplyRuntimeSettings();
        ApplyStartupRegistration();
        SyncSelectedQualityPreset();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ShaderTemplateDefinition> AvailablePlugins { get; }

    public ObservableCollection<ShaderTemplateDefinition> BuiltInPlugins { get; } = [];

    public ObservableCollection<ShaderTemplateDefinition> ImportedPlugins { get; } = [];

    public ObservableCollection<PluginCategoryViewModel> PluginCategories { get; } = [];

    public ObservableCollection<PluginCategoryViewModel> BasicPluginCategories { get; } = [];

    public ObservableCollection<PluginCategoryViewModel> AdvancedPluginCategories { get; } = [];

    public IReadOnlyList<QualityPresetOption> QualityPresets { get; }

    public RelayCommand ImportTemplateCommand { get; }

    public RelayCommand OpenPluginFolderCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand SaveProfileAsCommand { get; }

    public RelayCommand ExportProfileArchiveCommand { get; }

    public RelayCommand ImportProfileArchiveCommand { get; }

    public RelayCommand ReloadPluginRuntimeCommand { get; }

    public RelayCommand DeletePluginCommand { get; }

    public RelayCommand ResetPluginSettingsCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand ChoosePluginIconCommand { get; }

    public RelayCommand ClearPluginIconCommand { get; }

    public RelayCommand OpenPluginAuthoringGuideCommand { get; }

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
            RunEffectOperation(
                _localizationService.Get("main.effectOperation.switchTitle"),
                _localizationService.Get("main.effectOperation.switchMessage"),
                () =>
                {
                    ApplyPluginToSettings();
                    ApplyRuntimeSettings();
                });
            OnPropertyChanged(nameof(SelectedPluginName));
            OnPropertyChanged(nameof(SelectedPluginDescription));
            OnPropertyChanged(nameof(SelectedPluginResolvedIconPath));
            OnPropertyChanged(nameof(IsExternalPluginSelected));
            OnPropertyChanged(nameof(SelectedPluginRuntimeKindLabel));
            ScheduleAutosave(_localizationService.Get("main.status.profileChanged"));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SelectedPluginName => SelectedPlugin?.Name ?? _localizationService.Get("main.selectedPlugin.none");

    public string SelectedPluginDescription => SelectedPlugin?.Description ?? _localizationService.Get("main.selectedPlugin.prompt");

    public string SelectedPluginResolvedIconPath => SelectedPlugin?.ResolvedIconPath ?? string.Empty;

    public bool IsExternalPluginSelected => SelectedPlugin?.RuntimeKind == TemplateRuntimeKind.ExternalAssembly;

    public string SelectedPluginRuntimeKindLabel => SelectedPlugin?.RuntimeKind == TemplateRuntimeKind.ExternalAssembly
        ? _localizationService.Get("main.runtimeKind.imported")
        : _localizationService.Get("main.runtimeKind.builtIn");

    public string SelectedPluginDiagnosticsStatus => _customPluginEffect.Status switch
    {
        "Loaded" => _localizationService.Get("main.diag.status.loaded"),
        "Error" => _localizationService.Get("main.diag.status.error"),
        _ => _localizationService.Get("main.diag.status.idle")
    };

    public string SelectedPluginDiagnosticsMessage => _customPluginEffect.StatusDetails;

    public string SelectedPluginDiagnosticsAssembly => _customPluginEffect.RuntimeAssemblyFileName;

    public string SelectedPluginDiagnosticsAssemblyPath => _customPluginEffect.RuntimeAssemblyPath;

    public string SelectedPluginDiagnosticsEntryType => _customPluginEffect.RuntimeEntryTypeName;

    public string SelectedPluginDiagnosticsLoadedAt => _customPluginEffect.LoadedAtLabel;

    public string SelectedPluginDiagnosticsLastErrorAt => _customPluginEffect.LastErrorAtLabel;

    public string SelectedPluginDiagnosticsContext => string.Format(
        _localizationService.Get("main.diag.context"),
        _customPluginEffect.HasCursorSnapshot ? _localizationService.Get("main.diag.bool.yes") : _localizationService.Get("main.diag.bool.no"),
        _customPluginEffect.HasBackdropSample ? _localizationService.Get("main.diag.bool.yes") : _localizationService.Get("main.diag.bool.no"),
        _customPluginEffect.IsCursorVisibleInContext ? _localizationService.Get("main.diag.bool.yes") : _localizationService.Get("main.diag.bool.no"));

    public string DiagnosticsWarningText => _localizationService.Get("main.diag.warning");

    public string SelectedPluginDiagnosticsWarning
    {
        get
        {
            if (!_customPluginEffect.IsCursorVisibleInContext)
            {
                return _localizationService.Get("main.diag.warning.hiddenCursor");
            }

            if (!_customPluginEffect.HasCursorSnapshot)
            {
                return _localizationService.Get("main.diag.warning.noSnapshot");
            }

            if (!_customPluginEffect.HasBackdropSample)
            {
                return _localizationService.Get("main.diag.warning.noBackdrop");
            }

            return _localizationService.Get("main.diag.warning.none");
        }
    }

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
            ScheduleAutosave(_localizationService.Get("main.status.generalChanged"));
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
            ScheduleAutosave(_localizationService.Get("main.status.generalChanged"));
        }
    }

    public string EffectOpacityLabel => $"Master opacity: {_settings.General.MasterOpacity:P0}";

    public string FpsLabel => $"FPS cap: {_settings.General.TargetFps}";

    public double CursorAttachStrength
    {
        get => _settings.General.CursorAttachStrength;
        set
        {
            if (!SetNestedDouble(_settings.General.CursorAttachStrength, value, v => _settings.General.CursorAttachStrength = v))
            {
                return;
            }

            ApplyRuntimeSettings();
            OnPropertyChanged(nameof(CursorAttachStrengthLabel));
            ScheduleAutosave(_localizationService.Get("main.status.generalChanged"));
        }
    }

    public string CursorAttachStrengthLabel => $"Cursor attach: {_settings.General.CursorAttachStrength:0.0}x";

    public QualityPresetOption? SelectedQualityPreset
    {
        get => _selectedQualityPreset;
        set
        {
            if (value is null || _selectedQualityPreset?.Preset == value.Preset)
            {
                return;
            }

            _selectedQualityPreset = value;
            _settings.General.EffectQuality = value.Preset;
            OnPropertyChanged();
            ApplyRuntimeSettings();
            ScheduleAutosave(_localizationService.Get("main.status.generalChanged"));
        }
    }

    public string PluginFolderPath => _templateCatalog.CatalogDirectory;

    public string PluginAuthoringGuidePath => Path.Combine(AppContext.BaseDirectory, "Templates", "plugin-authoring-guide.txt");

    public string ApplicationVersion => "v0.0.5";

    public string ApplicationAuthor => "Dokzya_dev";

    public string ApplicationSignature => $"{ApplicationVersion} | {ApplicationAuthor}";

    public string HeroSubtitle => _localizationService.Get("main.heroSubtitle");

    public string ApplicationSettingsText => _localizationService.Get("main.applicationSettings");

    public string ImportPluginText => _localizationService.Get("main.importPlugin");

    public string ImportArchiveText => _localizationService.Get("main.importArchive");

    public string ImportMenuText => _localizationService.Get("main.importMenu");

    public string ColorPickText => _localizationService.Get("main.pickColor");

    public string EnabledToggleText => _localizationService.Get("main.enabledToggle");

    public string CurrentProfileText => _localizationService.Get("main.currentProfile");

    public string MoreActionsText => _localizationService.Get("main.moreActions");

    public string SaveSettingsText => _localizationService.Get("main.saveSettings");

    public string ResetProfileText => _localizationService.Get("main.resetProfile");

    public string ChooseIconText => _localizationService.Get("main.chooseIcon");

    public string ClearIconText => _localizationService.Get("main.clearIcon");

    public string OpenPluginFolderText => _localizationService.Get("main.openPluginFolder");

    public string SaveAsProfileText => _localizationService.Get("main.saveAsProfile");

    public string ExportProfileText => _localizationService.Get("main.exportProfile");

    public string DeletePluginText => _localizationService.Get("main.deletePlugin");

    public string StatusText => _localizationService.Get("main.status");

    public string GeneralControlsText => _localizationService.Get("main.generalControls");

    public string MasterOpacityText => _localizationService.Get("main.masterOpacity");

    public string FpsCapText => _localizationService.Get("main.fpsCap");

    public string CursorAttachText => _localizationService.Get("main.cursorAttach");

    public string EffectQualityText => _localizationService.Get("main.effectQuality");

    public string EffectQualityHintText => _localizationService.Get("main.effectQualityHint");

    public string CursorAttachHintText => _localizationService.Get("main.cursorAttachHint");

    public string PluginAuthoringText => _localizationService.Get("main.pluginAuthoring");

    public string PluginAuthoringHintText => _localizationService.Get("main.pluginAuthoringHint");

    public string OpenAuthoringGuideText => _localizationService.Get("main.openAuthoringGuide");

    public string ProfileControlsText => _localizationService.Get("main.profileControls");

    public string ProfileControlsHintText => _localizationService.Get("main.profileControlsHint");

    public string BasicText => _localizationService.Get("main.basic");

    public string BasicHintText => _localizationService.Get("main.basicHint");

    public string AdvancedText => _localizationService.Get("main.advanced");

    public string AdvancedHintText => _localizationService.Get("main.advancedHint");

    public string PluginDiagnosticsText => _localizationService.Get("main.pluginDiagnostics");

    public string DiagnosticsStatusText => _localizationService.Get("main.diag.status");

    public string DiagnosticsAssemblyText => _localizationService.Get("main.diag.assembly");

    public string DiagnosticsAssemblyPathText => _localizationService.Get("main.diag.assemblyPath");

    public string DiagnosticsEntryTypeText => _localizationService.Get("main.diag.entryType");

    public string DiagnosticsLoadedAtText => _localizationService.Get("main.diag.loadedAt");

    public string DiagnosticsLastErrorText => _localizationService.Get("main.diag.lastError");

    public string DiagnosticsReloadText => _localizationService.Get("main.diag.reload");

    public string ProfilesText => _localizationService.Get("main.profiles");

    public string ProfilesHintText => _localizationService.Get("main.profilesHint");

    public string BuiltInProfilesText => _localizationService.Get("main.builtInProfiles");

    public string ImportedPluginsText => _localizationService.Get("main.importedPlugins");

    public string ImportShortText => _localizationService.Get("main.importShort");

    public string IconPlaceholderText => _localizationService.Get("main.iconPlaceholder");

    public string BuiltInProfilesSummary => string.Format(_localizationService.Get("main.builtInProfilesSummary"), BuiltInPlugins.Count);

    public string ImportedProfilesSummary => ImportedPlugins.Count == 0
        ? _localizationService.Get("main.noImportedPlugins")
        : string.Format(_localizationService.Get("main.importedPluginsSummary"), ImportedPlugins.Count);

    public bool HasAdvancedParameters => AdvancedPluginCategories.Count > 0;

    public bool RunInBackgroundEnabled => _settings.General.RunInBackground;

    public string AutosaveStatus
    {
        get => _autosaveStatus;
        private set => SetProperty(ref _autosaveStatus, value);
    }

    public bool IsEffectOperationInProgress
    {
        get => _isEffectOperationInProgress;
        private set => SetProperty(ref _isEffectOperationInProgress, value);
    }

    public string EffectOperationTitle
    {
        get => _effectOperationTitle;
        private set => SetProperty(ref _effectOperationTitle, value);
    }

    public string EffectOperationMessage
    {
        get => _effectOperationMessage;
        private set => SetProperty(ref _effectOperationMessage, value);
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
        BasicPluginCategories.Clear();
        AdvancedPluginCategories.Clear();
        if (SelectedPlugin is null)
        {
            OnPropertyChanged(nameof(HasAdvancedParameters));
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
            if (category.Parameters.Any(parameter => !parameter.IsAdvanced))
            {
                var basicCategory = new PluginCategoryViewModel
                {
                    Name = category.Name
                };

                foreach (var parameter in category.Parameters.Where(parameter => !parameter.IsAdvanced))
                {
                    basicCategory.Parameters.Add(parameter);
                }

                BasicPluginCategories.Add(basicCategory);
            }

            if (category.Parameters.Any(parameter => parameter.IsAdvanced))
            {
                var advancedCategory = new PluginCategoryViewModel
                {
                    Name = category.Name
                };

                foreach (var parameter in category.Parameters.Where(parameter => parameter.IsAdvanced))
                {
                    advancedCategory.Parameters.Add(parameter);
                }

                AdvancedPluginCategories.Add(advancedCategory);
            }
        }

        OnPropertyChanged(nameof(HasAdvancedParameters));
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
        ScheduleAutosave(_localizationService.Get("main.status.pluginParametersChanged"));
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
        _settings.Trail.RenderMode = (TrailRenderMode)Math.Clamp((int)Math.Round(GetNumber(values, "trailMode", 0)), 0, 2);
        _settings.Trail.WaveAmplitude = GetNumber(values, "waveAmplitude", 0);
        _settings.Trail.WaveFrequency = GetNumber(values, "waveFrequency", 1.2);
        _settings.Trail.NoiseAmount = GetNumber(values, "noiseAmount", 0);
        _settings.Trail.RibbonSoftness = GetNumber(values, "ribbonSoftness", 0.45);

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
        var picker = new ColorPickerWindow(title, currentColor, _localizationService)
        {
            Owner = owner
        };

        return picker.ShowDialog() == true ? picker.SelectedColor : null;
    }

    private void ImportPlugin()
    {
        var importWindow = new ImportPluginWindow(_pluginWorkspaceService.WorkspaceDirectory, _localizationService)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (importWindow.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ShaderTemplateDefinition importedAssemblyPlugin = null!;
            RunEffectOperation(
                _localizationService.Get("main.effectOperation.importTitle"),
                _localizationService.Get("main.effectOperation.importMessage"),
                () =>
                {
                    importedAssemblyPlugin = _assemblyPluginImporter.Import(
                        importWindow.AssemblyPath,
                        importWindow.SelectedPluginCandidate?.EntryTypeName,
                        _templateCatalog.CatalogDirectory,
                        _templateCatalog,
                        string.IsNullOrWhiteSpace(importWindow.IconPath) ? null : importWindow.IconPath);
                    ReloadPlugins(importedAssemblyPlugin.Id);
                });
            AutosaveStatus = string.Format(_localizationService.Get("main.importedStatus"), importedAssemblyPlugin.Name);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.pluginImportFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportProfileArchive()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localizationService.Get("main.archiveImport.dialogTitle"),
            Filter = _localizationService.Get("main.archiveImport.dialogFilter"),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ShaderTemplateDefinition importedTemplate = null!;
            RunEffectOperation(
                _localizationService.Get("main.effectOperation.importArchiveTitle"),
                _localizationService.Get("main.effectOperation.importArchiveMessage"),
                () =>
                {
                    importedTemplate = _profileArchiveService.ImportArchive(dialog.FileName, _templateCatalog);
                    ReloadPlugins(importedTemplate.Id);
                });

            var importedStatus = string.Format(_localizationService.Get("main.archiveImport.success"), importedTemplate.Name);
            AutosaveStatus = importedStatus;
            ScheduleAutosave(importedStatus);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.archiveImportFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenPluginFolder()
    {
        OpenFolder(PluginFolderPath, string.Format(_localizationService.Get("main.status.folderOpened"), "Plugins"));
    }

    private void OpenPluginAuthoringGuide()
    {
        var guideWindow = new PluginAuthoringGuideWindow(PluginAuthoringGuidePath, _localizationService)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        guideWindow.ShowDialog();
    }

    private void SaveSelectedProfileAs()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        var dialog = new SaveProfileWindow(
            $"{SelectedPlugin.Name} {_localizationService.Get("saveProfile.copySuffix")}",
            SelectedPlugin.Description,
            _localizationService)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ShaderTemplateDefinition savedTemplate = null!;
            RunEffectOperation(
                _localizationService.Get("main.effectOperation.saveAsTitle"),
                _localizationService.Get("main.effectOperation.saveAsMessage"),
                () =>
                {
                    savedTemplate = CreateProfileSnapshot(dialog.ProfileName.Trim(), dialog.ProfileDescription.Trim());
                    savedTemplate = _templateCatalog.SaveTemplate(
                        savedTemplate,
                        string.IsNullOrWhiteSpace(SelectedPlugin.ResolvedIconPath) ? null : SelectedPlugin.ResolvedIconPath);
                    _settings.TemplateEffect.PluginParameterValues[savedTemplate.Id] = CreateParameterValueSnapshot(savedTemplate.Parameters);
                    ReloadPlugins(savedTemplate.Id);
                });

            var savedStatus = string.Format(_localizationService.Get("main.status.profileSavedAs"), savedTemplate.Name);
            AutosaveStatus = savedStatus;
            ScheduleAutosave(savedStatus);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.saveProfileFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportSelectedProfileArchive()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _localizationService.Get("main.archiveExport.dialogTitle"),
            Filter = _localizationService.Get("main.archiveExport.dialogFilter"),
            FileName = $"{ToKebabCase(SelectedPlugin.Name)}.fluxor-profile.zip",
            DefaultExt = ".zip",
            AddExtension = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            RunEffectOperation(
                _localizationService.Get("main.effectOperation.exportTitle"),
                _localizationService.Get("main.effectOperation.exportMessage"),
                () =>
                {
                    var snapshot = CreateProfileSnapshot(SelectedPlugin.Name, SelectedPlugin.Description, preserveIdentity: true);
                    _profileArchiveService.ExportArchive(snapshot, _templateCatalog.CatalogDirectory, dialog.FileName);
                });

            var exportedStatus = string.Format(_localizationService.Get("main.archiveExport.success"), Path.GetFileName(dialog.FileName));
            AutosaveStatus = exportedStatus;
            ScheduleAutosave(exportedStatus);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.archiveExportFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(
            _settings.General,
            _settings.Localization,
            _localizationService,
            PluginAuthoringGuidePath,
            PluginFolderPath)
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
        _settings.Localization.UseSystemLanguage = settingsWindow.UseSystemLanguage;
        _settings.Localization.LanguageCode = settingsWindow.SelectedLanguageCode;
        _localizationService.Apply(_settings.Localization);

        OnPropertyChanged(nameof(RunInBackgroundEnabled));
        RunEffectOperation(
            _localizationService.Get("main.effectOperation.settingsTitle"),
            _localizationService.Get("main.effectOperation.settingsMessage"),
            () =>
            {
                ApplyRuntimeSettings();
                ApplyStartupRegistration();
                SaveSettings();
            });
        AutosaveStatus = _localizationService.Get("settings.updated");
        RefreshLocalizedText();
    }

    private void DeleteSelectedPlugin()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        if (IsBuiltInPlugin(SelectedPlugin.Id))
        {
            AutosaveStatus = _localizationService.Get("main.status.builtInLocked");
            return;
        }

        var result = System.Windows.MessageBox.Show(
            string.Format(_localizationService.Get("main.dialog.deletePluginBody"), SelectedPlugin.Name),
            _localizationService.Get("main.dialog.deletePluginTitle"),
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
            var deletedStatus = string.Format(_localizationService.Get("main.status.pluginDeleted"), pluginToDelete.Name);
            AutosaveStatus = deletedStatus;
            ScheduleAutosave(deletedStatus);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.deletePluginFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReloadPlugins(string? selectedPluginId = null)
    {
        AvailablePlugins.Clear();
        foreach (var plugin in _templateCatalog.LoadTemplates())
        {
            AvailablePlugins.Add(plugin);
        }

        RefreshPluginCollections();

        SelectedPlugin = AvailablePlugins.FirstOrDefault(plugin => plugin.Id == selectedPluginId)
            ?? AvailablePlugins.FirstOrDefault(plugin => plugin.Id == _settings.TemplateEffect.SelectedTemplateId)
            ?? AvailablePlugins.FirstOrDefault();
    }

    private void RefreshPluginCollections()
    {
        BuiltInPlugins.Clear();
        foreach (var plugin in AvailablePlugins
                     .Where(plugin => plugin.RuntimeKind == TemplateRuntimeKind.BuiltInTemplate)
                     .OrderBy(plugin => GetBuiltInSortOrder(plugin.Id))
                     .ThenBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase))
        {
            BuiltInPlugins.Add(plugin);
        }

        ImportedPlugins.Clear();
        foreach (var plugin in AvailablePlugins
                     .Where(plugin => plugin.RuntimeKind == TemplateRuntimeKind.ExternalAssembly)
                     .OrderByDescending(plugin => plugin.DateAddedUtc)
                     .ThenBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase))
        {
            ImportedPlugins.Add(plugin);
        }

        OnPropertyChanged(nameof(BuiltInProfilesSummary));
        OnPropertyChanged(nameof(ImportedProfilesSummary));
    }

    private void SaveSettings()
    {
        PersistPluginValues();
        ApplyStartupRegistration();
        _settingsStore.Save(_settings);
        AutosaveStatus = string.Format(_localizationService.Get("main.status.savedAt"), DateTime.Now.ToString("T"));
    }

    private void SaveSettingsWithFeedback()
    {
        RunEffectOperation(
            _localizationService.Get("main.effectOperation.saveTitle"),
            _localizationService.Get("main.effectOperation.saveMessage"),
            SaveSettings);
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
        var resetStatus = string.Format(_localizationService.Get("main.status.pluginReset"), SelectedPlugin.Name);
        AutosaveStatus = resetStatus;
        ScheduleAutosave(resetStatus);
    }

    private void ResetSelectedPluginSettingsWithFeedback()
    {
        RunEffectOperation(
            _localizationService.Get("main.effectOperation.resetTitle"),
            _localizationService.Get("main.effectOperation.resetMessage"),
            ResetSelectedPluginSettings);
    }

    private void ChoosePluginIcon()
    {
        if (SelectedPlugin is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = string.Format(_localizationService.Get("main.dialog.chooseIconTitle"), SelectedPlugin.Name),
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
            AutosaveStatus = string.Format(_localizationService.Get("main.status.iconUpdated"), updated.Name);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.pluginIconFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
            AutosaveStatus = string.Format(_localizationService.Get("main.status.iconCleared"), updated.Name);
        }
        catch (Exception ex)
        {
            AutosaveStatus = ex.Message;
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.pluginIconFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReloadSelectedPluginRuntime()
    {
        if (SelectedPlugin is null || SelectedPlugin.RuntimeKind != TemplateRuntimeKind.ExternalAssembly)
        {
            return;
        }

        RunEffectOperation(
            _localizationService.Get("main.effectOperation.reloadTitle"),
            _localizationService.Get("main.effectOperation.reloadMessage"),
            ApplyRuntimeSettings);
        AutosaveStatus = _localizationService.Get("main.status.pluginReloaded");
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
                _settings.General.MasterOpacity,
                _settings.General.EffectQuality);

            _glowEffect.UpdateSettings(
                new GlowSettings
                {
                    IsEnabled = false,
                    Size = _settings.Glow.Size,
                    Opacity = _settings.Glow.Opacity,
                    Color = _settings.Glow.Color
                },
                _settings.General.MasterOpacity,
                _settings.General.CursorAttachStrength);

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
                _settings.General.MasterOpacity,
                _settings.General.EffectQuality);
        }
        else
        {
            _trailEffect.UpdateSettings(_settings.Trail, _settings.General.MasterOpacity, _settings.General.EffectQuality);
            _glowEffect.UpdateSettings(_settings.Glow, _settings.General.MasterOpacity, _settings.General.CursorAttachStrength);
            _rippleEffect.UpdateSettings(_settings.Ripple, _settings.General.MasterOpacity, _settings.General.EffectQuality);
        }

        _templateEffect.UpdateTemplate(
            SelectedPlugin?.RuntimeKind == TemplateRuntimeKind.BuiltInTemplate ? SelectedPlugin : null,
            SelectedPlugin is null ? new Dictionary<string, TemplateParameterValue>(StringComparer.OrdinalIgnoreCase) : GetOrCreatePluginValues(SelectedPlugin),
            _settings.TemplateEffect.IsEnabled,
            _settings.General.MasterOpacity,
            _settings.General.CursorAttachStrength,
            _settings.General.EffectQuality);
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
        RefreshPluginDiagnostics();
        _engine.SetTargetFps(_settings.General.TargetFps);
        _engine.SetPauseWhenCursorHidden(_settings.General.PauseWhenCursorHidden);
    }

    private void RefreshPluginDiagnostics()
    {
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsStatus));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsMessage));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsAssembly));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsAssemblyPath));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsEntryType));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsLoadedAt));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsLastErrorAt));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsContext));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsWarning));
    }

    private void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(HeroSubtitle));
        OnPropertyChanged(nameof(ApplicationSettingsText));
        OnPropertyChanged(nameof(ImportPluginText));
        OnPropertyChanged(nameof(ImportArchiveText));
        OnPropertyChanged(nameof(ImportMenuText));
        OnPropertyChanged(nameof(ColorPickText));
        OnPropertyChanged(nameof(EnabledToggleText));
        OnPropertyChanged(nameof(CurrentProfileText));
        OnPropertyChanged(nameof(MoreActionsText));
        OnPropertyChanged(nameof(SaveSettingsText));
        OnPropertyChanged(nameof(ResetProfileText));
        OnPropertyChanged(nameof(ChooseIconText));
        OnPropertyChanged(nameof(ClearIconText));
        OnPropertyChanged(nameof(OpenPluginFolderText));
        OnPropertyChanged(nameof(SaveAsProfileText));
        OnPropertyChanged(nameof(ExportProfileText));
        OnPropertyChanged(nameof(DeletePluginText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(GeneralControlsText));
        OnPropertyChanged(nameof(MasterOpacityText));
        OnPropertyChanged(nameof(FpsCapText));
        OnPropertyChanged(nameof(CursorAttachText));
        OnPropertyChanged(nameof(EffectQualityText));
        OnPropertyChanged(nameof(EffectQualityHintText));
        OnPropertyChanged(nameof(CursorAttachHintText));
        OnPropertyChanged(nameof(PluginAuthoringText));
        OnPropertyChanged(nameof(PluginAuthoringHintText));
        OnPropertyChanged(nameof(OpenAuthoringGuideText));
        OnPropertyChanged(nameof(ProfileControlsText));
        OnPropertyChanged(nameof(ProfileControlsHintText));
        OnPropertyChanged(nameof(BasicText));
        OnPropertyChanged(nameof(BasicHintText));
        OnPropertyChanged(nameof(AdvancedText));
        OnPropertyChanged(nameof(AdvancedHintText));
        OnPropertyChanged(nameof(PluginDiagnosticsText));
        OnPropertyChanged(nameof(DiagnosticsStatusText));
        OnPropertyChanged(nameof(DiagnosticsAssemblyText));
        OnPropertyChanged(nameof(DiagnosticsAssemblyPathText));
        OnPropertyChanged(nameof(DiagnosticsEntryTypeText));
        OnPropertyChanged(nameof(DiagnosticsLoadedAtText));
        OnPropertyChanged(nameof(DiagnosticsLastErrorText));
        OnPropertyChanged(nameof(DiagnosticsReloadText));
        OnPropertyChanged(nameof(DiagnosticsWarningText));
        OnPropertyChanged(nameof(ProfilesText));
        OnPropertyChanged(nameof(ProfilesHintText));
        OnPropertyChanged(nameof(BuiltInProfilesText));
        OnPropertyChanged(nameof(ImportedPluginsText));
        OnPropertyChanged(nameof(ImportShortText));
        OnPropertyChanged(nameof(IconPlaceholderText));
        OnPropertyChanged(nameof(SelectedPluginName));
        OnPropertyChanged(nameof(SelectedPluginDescription));
        OnPropertyChanged(nameof(SelectedPluginRuntimeKindLabel));
        OnPropertyChanged(nameof(BuiltInProfilesSummary));
        OnPropertyChanged(nameof(ImportedProfilesSummary));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsStatus));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsContext));
        OnPropertyChanged(nameof(SelectedPluginDiagnosticsWarning));
        OnPropertyChanged(nameof(AutosaveStatus));
        SyncSelectedQualityPreset();
    }

    private void SyncSelectedQualityPreset()
    {
        var match = QualityPresets.FirstOrDefault(option => option.Preset == _settings.General.EffectQuality)
            ?? QualityPresets.FirstOrDefault(option => option.Preset == EffectQualityPreset.Balanced)
            ?? QualityPresets.FirstOrDefault();

        if (!ReferenceEquals(_selectedQualityPreset, match))
        {
            _selectedQualityPreset = match;
            OnPropertyChanged(nameof(SelectedQualityPreset));
        }
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
        return pluginId is
            "neon-suite" or
            "minimal-suite" or
            "frost-halo" or
            "matrix-cascade" or
            "tap-cross" or
            "critical-spike";
    }

    private static int GetBuiltInSortOrder(string pluginId)
    {
        return pluginId switch
        {
            "minimal-suite" => 0,
            "neon-suite" => 1,
            "frost-halo" => 2,
            "matrix-cascade" => 3,
            "tap-cross" => 4,
            "critical-spike" => 5,
            _ => 100
        };
    }

    private ShaderTemplateDefinition CreateProfileSnapshot(string requestedName, string requestedDescription, bool preserveIdentity = false)
    {
        if (SelectedPlugin is null)
        {
            throw new InvalidOperationException("No profile is selected.");
        }

        var existingTemplates = _templateCatalog.LoadTemplates();
        var finalName = preserveIdentity
            ? requestedName
            : EnsureUniqueName(requestedName, existingTemplates.Select(template => template.Name));
        var finalId = preserveIdentity
            ? SelectedPlugin.Id
            : EnsureUniqueId(ToKebabCase(finalName), existingTemplates.Select(template => template.Id));
        var values = GetOrCreatePluginValues(SelectedPlugin);

        return new ShaderTemplateDefinition
        {
            Id = finalId,
            Name = finalName,
            Description = string.IsNullOrWhiteSpace(requestedDescription) ? SelectedPlugin.Description : requestedDescription,
            IconGlyph = SelectedPlugin.IconGlyph,
            IconPath = SelectedPlugin.IconPath,
            ResolvedIconPath = SelectedPlugin.ResolvedIconPath,
            AccentColor = SelectedPlugin.AccentColor,
            RuntimeKind = SelectedPlugin.RuntimeKind,
            AssemblyFileName = SelectedPlugin.AssemblyFileName,
            EntryTypeName = SelectedPlugin.EntryTypeName,
            Kind = SelectedPlugin.Kind,
            Trigger = SelectedPlugin.Trigger,
            Parameters = SelectedPlugin.Parameters
                .Select(parameter => CloneParameterWithCurrentDefaults(parameter, values))
                .ToList()
        };
    }

    private static TemplateParameterDefinition CloneParameterWithCurrentDefaults(
        TemplateParameterDefinition parameter,
        IReadOnlyDictionary<string, TemplateParameterValue> values)
    {
        values.TryGetValue(parameter.Key, out var value);
        return new TemplateParameterDefinition
        {
            Key = parameter.Key,
            DisplayName = parameter.DisplayName,
            Section = parameter.Section,
            SectionName = parameter.SectionName,
            Type = parameter.Type,
            Min = parameter.Min,
            Max = parameter.Max,
            Step = parameter.Step,
            DefaultNumber = value?.NumberValue ?? parameter.DefaultNumber,
            DefaultColor = string.IsNullOrWhiteSpace(value?.ColorValue) ? parameter.DefaultColor : value!.ColorValue!,
            DefaultBoolean = value?.BooleanValue ?? parameter.DefaultBoolean,
            IsAdvanced = parameter.IsAdvanced
        };
    }

    private static Dictionary<string, TemplateParameterValue> CreateParameterValueSnapshot(IEnumerable<TemplateParameterDefinition> parameters)
    {
        return parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => new TemplateParameterValue
            {
                NumberValue = parameter.DefaultNumber,
                ColorValue = parameter.DefaultColor,
                BooleanValue = parameter.DefaultBoolean
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static string EnsureUniqueName(string requestedName, IEnumerable<string> existingNames)
    {
        var usedNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "Profile" : requestedName.Trim();
        if (!usedNames.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (usedNames.Contains($"{baseName} {suffix}"))
        {
            suffix++;
        }

        return $"{baseName} {suffix}";
    }

    private static string EnsureUniqueId(string requestedId, IEnumerable<string> existingIds)
    {
        var usedIds = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var baseId = string.IsNullOrWhiteSpace(requestedId) ? "profile" : requestedId.Trim();
        if (!usedIds.Contains(baseId))
        {
            return baseId;
        }

        var suffix = 2;
        while (usedIds.Contains($"{baseId}-{suffix}"))
        {
            suffix++;
        }

        return $"{baseId}-{suffix}";
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "profile";
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        var needsDash = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (needsDash && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                needsDash = false;
            }
            else
            {
                needsDash = true;
            }
        }

        return builder.Length == 0 ? "profile" : builder.ToString();
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
                AutosaveStatus = string.Format(_localizationService.Get("main.status.pluginDeletedFilesBusy"), Path.GetFileName(filePath));
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
            System.Windows.MessageBox.Show(ex.Message, _localizationService.Get("main.dialog.openFolderFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void RunEffectOperation(string title, string message, Action action)
    {
        EffectOperationTitle = title;
        EffectOperationMessage = message;
        IsEffectOperationInProgress = true;
        FlushUi();

        try
        {
            action();
        }
        finally
        {
            IsEffectOperationInProgress = false;
            FlushUi();
        }
    }

    private static void FlushUi()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        dispatcher.Invoke(() => { }, DispatcherPriority.Render);
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

public sealed class QualityPresetOption(EffectQualityPreset preset, Func<string> displayFactory)
{
    public EffectQualityPreset Preset { get; } = preset;

    public string DisplayName => displayFactory();

    public override string ToString() => DisplayName;
}

