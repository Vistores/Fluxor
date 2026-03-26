using System.IO;
using System.Reflection;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class PluginRuntimeLoader
{
    private readonly string _pluginDirectory;

    public PluginRuntimeLoader(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
    }

    public string ResolveAssemblyPath(ShaderTemplateDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.AssemblyFileName))
        {
            return string.Empty;
        }

        return Path.Combine(_pluginDirectory, definition.AssemblyFileName);
    }

    public ICursorEffectPlugin Load(ShaderTemplateDefinition definition)
    {
        if (definition.RuntimeKind != TemplateRuntimeKind.ExternalAssembly)
        {
            throw new InvalidOperationException("The selected plugin does not use an external assembly runtime.");
        }

        if (string.IsNullOrWhiteSpace(definition.AssemblyFileName))
        {
            throw new InvalidOperationException($"Plugin '{definition.Name}' is missing AssemblyFileName.");
        }

        if (string.IsNullOrWhiteSpace(definition.EntryTypeName))
        {
            throw new InvalidOperationException($"Plugin '{definition.Name}' is missing EntryTypeName.");
        }

        var assemblyPath = ResolveAssemblyPath(definition);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly was not found: {assemblyPath}", assemblyPath);
        }

        var assembly = Assembly.LoadFrom(assemblyPath);
        var entryType = assembly.GetType(definition.EntryTypeName, throwOnError: false, ignoreCase: false);
        if (entryType is null)
        {
            throw new InvalidOperationException(
                $"Plugin type '{definition.EntryTypeName}' was not found in '{definition.AssemblyFileName}'.");
        }

        if (!typeof(ICursorEffectPlugin).IsAssignableFrom(entryType))
        {
            throw new InvalidOperationException(
                $"Plugin type '{definition.EntryTypeName}' must implement {nameof(ICursorEffectPlugin)}.");
        }

        if (Activator.CreateInstance(entryType) is not ICursorEffectPlugin instance)
        {
            throw new InvalidOperationException(
                $"Plugin type '{definition.EntryTypeName}' could not be instantiated. A public parameterless constructor is required.");
        }

        return instance;
    }
}
