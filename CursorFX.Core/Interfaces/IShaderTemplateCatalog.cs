using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface IShaderTemplateCatalog
{
    string CatalogDirectory { get; }

    IReadOnlyList<ShaderTemplateDefinition> LoadTemplates();

    ShaderTemplateDefinition ImportTemplate(string sourceFilePath, string? iconOverridePath = null);

    ShaderTemplateDefinition SaveTemplate(ShaderTemplateDefinition template, string? iconOverridePath = null);

    void ExportTemplate(ShaderTemplateDefinition template, string destinationFilePath);

    void EnsureCatalog();
}
