using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.App.Services;

public sealed class ProfileArchiveService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public void ExportArchive(ShaderTemplateDefinition template, string catalogDirectory, string destinationArchivePath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"FluxorProfileExport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var archiveTemplate = new ShaderTemplateDefinition
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                IconGlyph = template.IconGlyph,
                IconPath = string.Empty,
                AccentColor = template.AccentColor,
                RuntimeKind = template.RuntimeKind,
                AssemblyFileName = template.AssemblyFileName,
                EntryTypeName = template.EntryTypeName,
                Kind = template.Kind,
                Trigger = template.Trigger,
                Parameters = template.Parameters
            };

            if (!string.IsNullOrWhiteSpace(template.ResolvedIconPath) && File.Exists(template.ResolvedIconPath))
            {
                var iconFileName = Path.GetFileName(template.ResolvedIconPath);
                File.Copy(template.ResolvedIconPath, Path.Combine(tempDirectory, iconFileName), overwrite: true);
                archiveTemplate = CloneWithIcon(archiveTemplate, iconFileName);
            }

            if (template.RuntimeKind == TemplateRuntimeKind.ExternalAssembly && !string.IsNullOrWhiteSpace(template.AssemblyFileName))
            {
                var assemblyPath = Path.Combine(catalogDirectory, template.AssemblyFileName);
                foreach (var filePath in EnumerateAssemblyFiles(assemblyPath))
                {
                    if (File.Exists(filePath))
                    {
                        File.Copy(filePath, Path.Combine(tempDirectory, Path.GetFileName(filePath)), overwrite: true);
                    }
                }
            }

            var manifestPath = Path.Combine(tempDirectory, $"{archiveTemplate.Id}.cursorfx-plugin.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(archiveTemplate, SerializerOptions));

            Directory.CreateDirectory(Path.GetDirectoryName(destinationArchivePath)!);
            if (File.Exists(destinationArchivePath))
            {
                File.Delete(destinationArchivePath);
            }

            ZipFile.CreateFromDirectory(tempDirectory, destinationArchivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    public ShaderTemplateDefinition ImportArchive(string archivePath, IShaderTemplateCatalog templateCatalog)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Profile archive was not found.", archivePath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"FluxorProfileImport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            ZipFile.ExtractToDirectory(archivePath, tempDirectory);

            var manifestPath = Directory.EnumerateFiles(tempDirectory, "*.cursorfx-plugin.json", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? throw new InvalidOperationException("The selected archive does not contain a Fluxor profile manifest.");

            var json = File.ReadAllText(manifestPath);
            var template = JsonSerializer.Deserialize<ShaderTemplateDefinition>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The selected archive manifest could not be read.");

            var existing = templateCatalog.LoadTemplates();
            var uniqueName = EnsureUniqueName(template.Name, existing.Select(item => item.Name));
            var uniqueId = EnsureUniqueId(template.Id, existing.Select(item => item.Id));

            var iconSourcePath = string.IsNullOrWhiteSpace(template.IconPath)
                ? null
                : Path.Combine(tempDirectory, template.IconPath);

            var assemblyFileName = template.AssemblyFileName;
            if (template.RuntimeKind == TemplateRuntimeKind.ExternalAssembly)
            {
                if (string.IsNullOrWhiteSpace(template.AssemblyFileName))
                {
                    throw new InvalidOperationException("The archive references an external plugin, but the assembly file name is missing.");
                }

                var sourceAssemblyPath = Path.Combine(tempDirectory, template.AssemblyFileName);
                if (!File.Exists(sourceAssemblyPath))
                {
                    throw new InvalidOperationException("The archive references an external plugin assembly, but the DLL is missing.");
                }

                var uniqueAssemblyBase = $"{Path.GetFileNameWithoutExtension(template.AssemblyFileName)}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                foreach (var filePath in EnumerateAssemblyFiles(sourceAssemblyPath))
                {
                    var destinationPath = Path.Combine(templateCatalog.CatalogDirectory, uniqueAssemblyBase + Path.GetExtension(filePath));
                    File.Copy(filePath, destinationPath, overwrite: true);
                }

                assemblyFileName = uniqueAssemblyBase + ".dll";
            }

            var importedTemplate = new ShaderTemplateDefinition
            {
                Id = uniqueId,
                Name = uniqueName,
                Description = template.Description,
                IconGlyph = template.IconGlyph,
                IconPath = template.IconPath,
                AccentColor = template.AccentColor,
                RuntimeKind = template.RuntimeKind,
                AssemblyFileName = assemblyFileName,
                EntryTypeName = template.EntryTypeName,
                Kind = template.Kind,
                Trigger = template.Trigger,
                Parameters = template.Parameters
            };

            return templateCatalog.SaveTemplate(importedTemplate, iconSourcePath);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static IEnumerable<string> EnumerateAssemblyFiles(string assemblyPath)
    {
        yield return assemblyPath;

        var directory = Path.GetDirectoryName(assemblyPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
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

    private static string EnsureUniqueId(string baseId, IEnumerable<string> existingIds)
    {
        var usedIds = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var candidate = string.IsNullOrWhiteSpace(baseId) ? "profile" : baseId;
        if (!usedIds.Contains(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (usedIds.Contains($"{candidate}-{suffix}"))
        {
            suffix++;
        }

        return $"{candidate}-{suffix}";
    }

    private static string EnsureUniqueName(string baseName, IEnumerable<string> existingNames)
    {
        var usedNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var candidate = string.IsNullOrWhiteSpace(baseName) ? "Imported Profile" : baseName;
        if (!usedNames.Contains(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (usedNames.Contains($"{candidate} {suffix}"))
        {
            suffix++;
        }

        return $"{candidate} {suffix}";
    }

    private static ShaderTemplateDefinition CloneWithIcon(ShaderTemplateDefinition template, string iconFileName)
    {
        return new ShaderTemplateDefinition
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IconGlyph = template.IconGlyph,
            IconPath = iconFileName,
            AccentColor = template.AccentColor,
            RuntimeKind = template.RuntimeKind,
            AssemblyFileName = template.AssemblyFileName,
            EntryTypeName = template.EntryTypeName,
            Kind = template.Kind,
            Trigger = template.Trigger,
            Parameters = template.Parameters
        };
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}
