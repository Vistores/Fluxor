using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorFX.App.Services;
using CursorFX.Core.Models;

namespace CursorFX.App;

public partial class ArchiveImportPreviewWindow : Window, INotifyPropertyChanged
{
    private readonly LocalizationService _localizationService;

    public ArchiveImportPreviewWindow(ProfileArchiveInspectionResult inspection, LocalizationService localizationService)
    {
        Inspection = inspection;
        _localizationService = localizationService;
        Warnings = inspection.Warnings.Count == 0
            ? [_localizationService.Get("archivePreview.warnings.none")]
            : inspection.Warnings;
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProfileArchiveInspectionResult Inspection { get; }

    public ArchiveImportDecision SelectedDecision { get; private set; } = ArchiveImportDecision.Cancel;

    public string WindowTitle => _localizationService.Get("archivePreview.windowTitle");

    public string HeadingText => _localizationService.Get("archivePreview.heading");

    public string IntroText => Inspection.ExistingById is null
        ? _localizationService.Get("archivePreview.introImport")
        : _localizationService.Get("archivePreview.introReplace");

    public string NameLabelText => _localizationService.Get("archivePreview.name");

    public string IdLabelText => _localizationService.Get("archivePreview.id");

    public string RuntimeLabelText => _localizationService.Get("archivePreview.runtime");

    public string ParametersLabelText => _localizationService.Get("archivePreview.parameters");

    public string IconLabelText => _localizationService.Get("archivePreview.icon");

    public string AssemblyLabelText => _localizationService.Get("archivePreview.assembly");

    public string ConflictTitleText => _localizationService.Get("archivePreview.conflictTitle");

    public string WarningsTitleText => _localizationService.Get("archivePreview.warningsTitle");

    public string CancelText => _localizationService.Get("archivePreview.cancel");

    public string ImportCopyText => Inspection.ExistingById is null
        ? _localizationService.Get("archivePreview.import")
        : _localizationService.Get("archivePreview.importCopy");

    public string ReplaceText => _localizationService.Get("archivePreview.replace");

    public string ProfileName => Inspection.Template.Name;

    public string ProfileId => Inspection.Template.Id;

    public string RuntimeLabel => Inspection.Template.RuntimeKind == TemplateRuntimeKind.ExternalAssembly
        ? _localizationService.Get("archivePreview.runtimeImported")
        : _localizationService.Get("archivePreview.runtimeBuiltIn");

    public string ParameterCount => Inspection.Template.Parameters.Count.ToString();

    public string HasIconText => Inspection.HasIcon ? _localizationService.Get("archivePreview.yes") : _localizationService.Get("archivePreview.no");

    public string HasAssemblyText => Inspection.Template.RuntimeKind == TemplateRuntimeKind.ExternalAssembly && Inspection.HasAssembly
        ? _localizationService.Get("archivePreview.yes")
        : _localizationService.Get("archivePreview.no");

    public string Description => string.IsNullOrWhiteSpace(Inspection.Template.Description)
        ? _localizationService.Get("archivePreview.noDescription")
        : Inspection.Template.Description;

    public bool CanReplace => Inspection.ExistingById is not null;

    public string ConflictSummary => Inspection.ExistingById is null
        ? _localizationService.Get("archivePreview.noConflict")
        : string.Format(_localizationService.Get("archivePreview.replaceSummary"), Inspection.ExistingById.Name);

    public IReadOnlyList<string> Warnings { get; }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        SelectedDecision = ArchiveImportDecision.Cancel;
        DialogResult = false;
    }

    private void OnImportCopyClick(object sender, RoutedEventArgs e)
    {
        SelectedDecision = ArchiveImportDecision.ImportCopy;
        DialogResult = true;
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        SelectedDecision = ArchiveImportDecision.ReplaceExisting;
        DialogResult = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum ArchiveImportDecision
{
    Cancel,
    ImportCopy,
    ReplaceExisting
}
