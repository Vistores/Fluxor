using System.Collections.ObjectModel;

namespace CursorFX.App.ViewModels;

public sealed class PluginCategoryViewModel
{
    public required string Name { get; init; }

    public ObservableCollection<ShaderTemplateParameterViewModel> Parameters { get; } = [];
}
