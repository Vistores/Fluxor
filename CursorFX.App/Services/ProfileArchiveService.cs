using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.App.Services;

public sealed class ProfileArchiveService
{
    public const string ArchiveExtension = ".fluxorprofile";

    private const string ArchiveMetadataFileName = "fluxor-profile.archive.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public void ExportArchive(ShaderTemplateDefinition template, string catalogDirectory, string destinationArchivePath)
    {
        destinationArchivePath = NormalizeArchivePath(destinationArchivePath);
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
            File.WriteAllText(
                Path.Combine(tempDirectory, ArchiveMetadataFileName),
                JsonSerializer.Serialize(
                    new FluxorProfileArchiveMetadata
                    {
                        Format = "fluxor-profile-archive",
                        Version = 1,
                        ExportedAtUtc = DateTime.UtcNow,
                        ProfileId = archiveTemplate.Id,
                        ProfileName = archiveTemplate.Name
                    },
                    SerializerOptions));

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

    public ProfileArchiveInspectionResult InspectArchive(string archivePath, IShaderTemplateCatalog templateCatalog)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Profile archive was not found.", archivePath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"FluxorProfileInspect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            ZipFile.ExtractToDirectory(archivePath, tempDirectory);
            var metadata = ReadArchiveMetadata(tempDirectory);

            var manifestPath = Directory.EnumerateFiles(tempDirectory, "*.cursorfx-plugin.json", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? throw new InvalidOperationException("The selected archive does not contain a Fluxor profile manifest.");

            var json = File.ReadAllText(manifestPath);
            var template = JsonSerializer.Deserialize<ShaderTemplateDefinition>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The selected archive manifest could not be read.");

            var existingTemplates = templateCatalog.LoadTemplates();
            var existingById = existingTemplates.FirstOrDefault(item => string.Equals(item.Id, template.Id, StringComparison.OrdinalIgnoreCase));
            var existingByName = existingTemplates.FirstOrDefault(item => string.Equals(item.Name, template.Name, StringComparison.OrdinalIgnoreCase));

            var warnings = new List<string>();
            if (existingById is not null)
            {
                warnings.Add($"A profile with ID '{template.Id}' already exists. Fluxor will import this archive as a copy unless you replace the existing profile.");
            }

            if (existingByName is not null && !string.Equals(existingByName.Id, template.Id, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"A profile named '{template.Name}' already exists. Fluxor will adjust the imported name to avoid a collision.");
            }

            var hasManifestIcon = !string.IsNullOrWhiteSpace(template.IconPath);
            var iconFound = hasManifestIcon && File.Exists(Path.Combine(tempDirectory, template.IconPath));
            if (hasManifestIcon && !iconFound)
            {
                warnings.Add("The archive references a profile icon, but the icon file is missing.");
            }

            var hasAssembly = template.RuntimeKind == TemplateRuntimeKind.ExternalAssembly;
            var assemblyFound = false;
            if (hasAssembly)
            {
                if (string.IsNullOrWhiteSpace(template.AssemblyFileName))
                {
                    warnings.Add("The archive references an external plugin profile, but the assembly file name is missing.");
                }
                else
                {
                    assemblyFound = File.Exists(Path.Combine(tempDirectory, template.AssemblyFileName));
                    if (!assemblyFound)
                    {
                        warnings.Add("The archive references an external plugin DLL, but the DLL file is missing.");
                    }
                }
            }

            return new ProfileArchiveInspectionResult
            {
                ArchivePath = archivePath,
                FileName = Path.GetFileName(archivePath),
                Template = template,
                Metadata = metadata,
                ExistingById = existingById,
                ExistingByName = existingByName,
                HasIcon = iconFound,
                HasAssembly = assemblyFound,
                Warnings = warnings
            };
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    public ShaderTemplateDefinition ImportArchive(string archivePath, IShaderTemplateCatalog templateCatalog)
    {
        return ImportArchive(archivePath, templateCatalog, replaceExisting: false);
    }

    public ShaderTemplateDefinition ImportArchive(string archivePath, IShaderTemplateCatalog templateCatalog, bool replaceExisting)
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
            ValidateArchiveMetadata(tempDirectory);

            var manifestPath = Directory.EnumerateFiles(tempDirectory, "*.cursorfx-plugin.json", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? throw new InvalidOperationException("The selected archive does not contain a Fluxor profile manifest.");

            var json = File.ReadAllText(manifestPath);
            var template = JsonSerializer.Deserialize<ShaderTemplateDefinition>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The selected archive manifest could not be read.");

            var existing = templateCatalog.LoadTemplates();
            var replaceTarget = replaceExisting
                ? existing.FirstOrDefault(item => string.Equals(item.Id, template.Id, StringComparison.OrdinalIgnoreCase))
                : null;
            var uniqueName = replaceTarget is null
                ? EnsureUniqueName(template.Name, existing.Select(item => item.Name))
                : replaceTarget.Name;
            var uniqueId = replaceTarget is null
                ? EnsureUniqueId(template.Id, existing.Select(item => item.Id))
                : replaceTarget.Id;

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
                Description = replaceTarget?.Description ?? template.Description,
                IconGlyph = replaceTarget?.IconGlyph ?? template.IconGlyph,
                IconPath = replaceTarget?.IconPath ?? template.IconPath,
                AccentColor = replaceTarget?.AccentColor ?? template.AccentColor,
                RuntimeKind = template.RuntimeKind,
                AssemblyFileName = assemblyFileName,
                EntryTypeName = template.EntryTypeName,
                Kind = template.Kind,
                Trigger = template.Trigger,
                Parameters = template.Parameters
            };

            return templateCatalog.SaveTemplate(
                importedTemplate,
                iconSourcePath
                ?? (!string.IsNullOrWhiteSpace(replaceTarget?.ResolvedIconPath) ? replaceTarget.ResolvedIconPath : null));
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

    private static string NormalizeArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Path.GetExtension(path).Length == 0
            ? path + ArchiveExtension
            : path;
    }

    private static FluxorProfileArchiveMetadata? ReadArchiveMetadata(string extractedDirectory)
    {
        var metadataPath = Path.Combine(extractedDirectory, ArchiveMetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = File.ReadAllText(metadataPath);
        return JsonSerializer.Deserialize<FluxorProfileArchiveMetadata>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The selected profile archive metadata is invalid.");
    }

    private static void ValidateArchiveMetadata(string extractedDirectory)
    {
        var metadata = ReadArchiveMetadata(extractedDirectory);
        if (metadata is null)
        {
            return;
        }

        if (!string.Equals(metadata.Format, "fluxor-profile-archive", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The selected archive is not a valid Fluxor profile archive.");
        }
    }
}

public sealed class FluxorProfileArchiveMetadata
{
    public string Format { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTime ExportedAtUtc { get; set; }

    public string ProfileId { get; set; } = string.Empty;

    public string ProfileName { get; set; } = string.Empty;
}

public sealed class ProfileArchiveInspectionResult
{
    public string ArchivePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public required ShaderTemplateDefinition Template { get; init; }

    public FluxorProfileArchiveMetadata? Metadata { get; init; }

    public ShaderTemplateDefinition? ExistingById { get; init; }

    public ShaderTemplateDefinition? ExistingByName { get; init; }

    public bool HasIcon { get; init; }

    public bool HasAssembly { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
