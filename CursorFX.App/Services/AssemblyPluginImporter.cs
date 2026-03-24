using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.App.Services;

public sealed class AssemblyPluginImporter
{
    public ShaderTemplateDefinition Import(
        string assemblyFilePath,
        string? entryTypeName,
        string catalogDirectory,
        IShaderTemplateCatalog templateCatalog,
        string? iconOverridePath = null)
    {
        if (!File.Exists(assemblyFilePath))
        {
            throw new FileNotFoundException("Plugin DLL was not found.", assemblyFilePath);
        }

        Directory.CreateDirectory(catalogDirectory);

        var manifest = BuildDefinitionFromAssembly(assemblyFilePath, entryTypeName);

        var assemblyBaseName = Path.GetFileNameWithoutExtension(assemblyFilePath);
        var uniqueAssemblyName = $"{assemblyBaseName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        foreach (var filePath in EnumerateAssemblyFiles(assemblyFilePath))
        {
            var destinationPath = Path.Combine(catalogDirectory, uniqueAssemblyName + Path.GetExtension(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }

        var updatedManifest = new ShaderTemplateDefinition
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            IconGlyph = manifest.IconGlyph,
            IconPath = manifest.IconPath,
            ResolvedIconPath = manifest.ResolvedIconPath,
            AccentColor = manifest.AccentColor,
            RuntimeKind = TemplateRuntimeKind.ExternalAssembly,
            AssemblyFileName = uniqueAssemblyName + ".dll",
            EntryTypeName = manifest.EntryTypeName,
            Kind = manifest.Kind,
            Trigger = manifest.Trigger,
            Parameters = manifest.Parameters
        };

        return templateCatalog.SaveTemplate(updatedManifest, iconOverridePath);
    }

    public IReadOnlyList<PluginAssemblyCandidate> DiscoverPlugins(string assemblyFilePath)
    {
        if (!File.Exists(assemblyFilePath))
        {
            throw new FileNotFoundException("Plugin DLL was not found.", assemblyFilePath);
        }

        using var inspectionContext = new PluginInspectionLoadContext(assemblyFilePath);
        var assembly = inspectionContext.LoadPluginAssembly();
        var pluginTypes = GetPluginTypes(assembly)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        var candidates = new List<PluginAssemblyCandidate>(pluginTypes.Count);
        foreach (var pluginType in pluginTypes)
        {
            if (Activator.CreateInstance(pluginType) is not ICursorEffectPlugin plugin)
            {
                continue;
            }

            try
            {
                candidates.Add(new PluginAssemblyCandidate(
                    pluginType.FullName ?? pluginType.Name,
                    string.IsNullOrWhiteSpace(plugin.DisplayName) ? pluginType.Name : plugin.DisplayName,
                    plugin.PluginId,
                    plugin.Description));
            }
            finally
            {
                plugin.Dispose();
            }
        }

        return candidates;
    }

    private ShaderTemplateDefinition BuildDefinitionFromAssembly(string assemblyFilePath, string? entryTypeName)
    {
        using var inspectionContext = new PluginInspectionLoadContext(assemblyFilePath);
        var assembly = inspectionContext.LoadPluginAssembly();
        var pluginType = ResolvePluginType(assembly, entryTypeName);

        if (pluginType is null)
        {
            throw new InvalidOperationException("No public ICursorEffectPlugin implementation was found in the selected DLL.");
        }

        if (Activator.CreateInstance(pluginType) is not ICursorEffectPlugin plugin)
        {
            throw new InvalidOperationException("Plugin type could not be instantiated. A public parameterless constructor is required.");
        }

        try
        {
            var parameters = plugin.GetParameters()?.ToList() ?? [];
            if (parameters.Count == 0)
            {
                throw new InvalidOperationException("DLL-only import requires the plugin to provide parameter metadata via GetParameters().");
            }

            return new ShaderTemplateDefinition
            {
                Id = string.IsNullOrWhiteSpace(plugin.PluginId) ? ToKebabCase(plugin.DisplayName) : plugin.PluginId,
                Name = string.IsNullOrWhiteSpace(plugin.DisplayName) ? pluginType.Name : plugin.DisplayName,
                Description = plugin.Description,
                IconGlyph = string.IsNullOrWhiteSpace(plugin.IconGlyph) ? "*" : plugin.IconGlyph,
                AccentColor = string.IsNullOrWhiteSpace(plugin.AccentColor) ? "#4FD1C5" : plugin.AccentColor,
                RuntimeKind = TemplateRuntimeKind.ExternalAssembly,
                AssemblyFileName = Path.GetFileName(assemblyFilePath),
                EntryTypeName = pluginType.FullName ?? pluginType.Name,
                Kind = plugin.Kind,
                Trigger = plugin.Trigger,
                Parameters = parameters
            };
        }
        finally
        {
            plugin.Dispose();
        }
    }

    private static Type? ResolvePluginType(Assembly assembly, string? entryTypeName)
    {
        var pluginTypes = GetPluginTypes(assembly).ToList();

        if (pluginTypes.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(entryTypeName))
        {
            var matched = pluginTypes.FirstOrDefault(type => string.Equals(type.FullName, entryTypeName, StringComparison.Ordinal));
            if (matched is null)
            {
                throw new InvalidOperationException($"Plugin type '{entryTypeName}' was not found in the selected DLL.");
            }

            return matched;
        }

        if (pluginTypes.Count > 1)
        {
            throw new InvalidOperationException("The selected DLL contains multiple plugins. Choose one plugin type before importing.");
        }

        return pluginTypes[0];
    }

    private static IEnumerable<Type> GetPluginTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(ICursorEffectPlugin).IsAssignableFrom(type));
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

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "plugin";
        }

        var buffer = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                buffer.Append('-');
            }

            buffer.Append(char.ToLowerInvariant(c));
        }

        return buffer.ToString();
    }
}

public sealed record PluginAssemblyCandidate(
    string EntryTypeName,
    string DisplayName,
    string PluginId,
    string Description);

internal sealed class PluginInspectionLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly string _pluginAssemblyPath;
    private readonly string _pluginDirectory;

    public PluginInspectionLoadContext(string pluginAssemblyPath)
        : base($"FluxorPluginInspection:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}:{Guid.NewGuid():N}", isCollectible: true)
    {
        _pluginAssemblyPath = pluginAssemblyPath;
        _pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath) ?? string.Empty;
    }

    public Assembly LoadPluginAssembly()
    {
        return LoadFromAssemblyPath(_pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var defaultAssembly = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
        if (defaultAssembly is not null)
        {
            return defaultAssembly;
        }

        if (string.IsNullOrWhiteSpace(_pluginDirectory))
        {
            return null;
        }

        var candidatePath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(candidatePath))
        {
            return LoadFromAssemblyPath(candidatePath);
        }

        return null;
    }

    public void Dispose()
    {
        Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
