using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CursorFX.Core.Models;

namespace CursorFX.App.Services;

public sealed class SourcePluginCompiler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private const string PluginInterfaceNamespace = "CursorFX.Core.Interfaces";
    private static readonly Regex NamespaceRegex = new(@"namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"class\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*[^\\r\\n{]*ICursorEffectPlugin", RegexOptions.Compiled);
    private static readonly Regex ParameterKeyRegex = new(@"Get(?<type>Bool|Double|Color)\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s*,\s*""(?<key>[^""]+)""", RegexOptions.Compiled);

    public SourcePluginCompilationResult CompileAndInstall(string sourceFilePath, string catalogDirectory, string? manifestFilePath = null, string? iconOverridePath = null)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Source plugin file was not found.", sourceFilePath);
        }

        Directory.CreateDirectory(catalogDirectory);

        var sourceCode = File.ReadAllText(sourceFilePath);
        ValidateSourceCode(sourceCode);
        var metadata = ExtractMetadata(sourceCode, sourceFilePath);
        var buildRoot = Path.Combine(catalogDirectory, ".source-build", metadata.PluginId);
        Directory.CreateDirectory(buildRoot);

        var sourceTargetPath = Path.Combine(buildRoot, $"{metadata.ClassName}.cs");
        File.WriteAllText(sourceTargetPath, sourceCode, Encoding.UTF8);

        var projectPath = Path.Combine(buildRoot, $"{metadata.AssemblyName}.csproj");
        File.WriteAllText(projectPath, BuildProjectFile(metadata.AssemblyName), Encoding.UTF8);

        var buildOutputDirectory = Path.Combine(buildRoot, "artifacts");
        Directory.CreateDirectory(buildOutputDirectory);
        RunDotnetBuild(projectPath, buildOutputDirectory);

        var uniqueAssemblyName = $"{metadata.AssemblyName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var builtAssemblyPath = Path.Combine(buildOutputDirectory, $"{metadata.AssemblyName}.dll");
        if (!File.Exists(builtAssemblyPath))
        {
            throw new InvalidOperationException("Source plugin build completed, but the output assembly was not found.");
        }

        foreach (var builtFile in EnumerateBuiltFiles(buildOutputDirectory, metadata.AssemblyName))
        {
            var extension = Path.GetExtension(builtFile);
            var destinationPath = Path.Combine(catalogDirectory, uniqueAssemblyName + extension);
            File.Copy(builtFile, destinationPath, overwrite: true);
        }

        var manifest = LoadOrCreateManifest(sourceFilePath, metadata, manifestFilePath);
        manifest = PrepareIconAsset(manifest, catalogDirectory, manifestFilePath ?? sourceFilePath, iconOverridePath);
        manifest = new ShaderTemplateDefinition
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            IconGlyph = manifest.IconGlyph,
            IconPath = manifest.IconPath,
            ResolvedIconPath = manifest.ResolvedIconPath,
            AccentColor = manifest.AccentColor,
            RuntimeKind = TemplateRuntimeKind.ExternalAssembly,
            AssemblyFileName = $"{uniqueAssemblyName}.dll",
            EntryTypeName = metadata.EntryTypeName,
            Kind = manifest.Kind,
            Trigger = manifest.Trigger,
            Parameters = manifest.Parameters
        };

        var manifestPath = Path.Combine(catalogDirectory, $"{manifest.Id}.cursorfx-plugin.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, SerializerOptions), Encoding.UTF8);

        return new SourcePluginCompilationResult(manifest, manifestPath, Path.Combine(catalogDirectory, manifest.AssemblyFileName));
    }

    private static PluginSourceMetadata ExtractMetadata(string sourceCode, string sourceFilePath)
    {
        var className = ClassRegex.Match(sourceCode).Groups[1].Value;
        if (string.IsNullOrWhiteSpace(className))
        {
            className = Path.GetFileNameWithoutExtension(sourceFilePath);
        }

        var namespaceName = NamespaceRegex.Match(sourceCode).Groups[1].Value;
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            namespaceName = "CursorFX.UserPlugins";
        }

        var pluginId = ToKebabCase(className);
        var assemblyName = $"CursorFX.{className}";
        var entryTypeName = $"{namespaceName}.{className}";
        var parameters = ExtractParameters(sourceCode);

        return new PluginSourceMetadata(pluginId, className, assemblyName, entryTypeName, parameters);
    }

    private static void ValidateSourceCode(string sourceCode)
    {
        var referencesInterface = sourceCode.Contains("ICursorEffectPlugin", StringComparison.Ordinal);
        var hasUsing = sourceCode.Contains($"using {PluginInterfaceNamespace};", StringComparison.Ordinal);
        var usesQualifiedName = sourceCode.Contains($"{PluginInterfaceNamespace}.ICursorEffectPlugin", StringComparison.Ordinal);

        if (referencesInterface && !hasUsing && !usesQualifiedName)
        {
            throw new InvalidOperationException(
                "Source plugin validation failed: add 'using CursorFX.Core.Interfaces;' at the top of the .cs file, or use the fully qualified interface name 'CursorFX.Core.Interfaces.ICursorEffectPlugin'.");
        }
    }

    private static List<TemplateParameterDefinition> ExtractParameters(string sourceCode)
    {
        var definitions = new List<TemplateParameterDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ParameterKeyRegex.Matches(sourceCode))
        {
            var key = match.Groups["key"].Value;
            if (!seen.Add(key))
            {
                continue;
            }

            var type = match.Groups["type"].Value switch
            {
                "Bool" => TemplateParameterType.Toggle,
                "Color" => TemplateParameterType.Color,
                _ => TemplateParameterType.Number
            };

            var section = InferSection(key);
            definitions.Add(new TemplateParameterDefinition
            {
                Key = key,
                DisplayName = ToDisplayName(key),
                Section = section,
                SectionName = InferSectionName(key, section),
                Type = type,
                Min = InferMin(key, type),
                Max = InferMax(key, type),
                Step = InferStep(key, type),
                DefaultNumber = InferDefaultNumber(key),
                DefaultColor = InferDefaultColor(key),
                DefaultBoolean = key.EndsWith("Enabled", StringComparison.OrdinalIgnoreCase)
            });
        }

        return definitions;
    }

    private static ShaderTemplateDefinition LoadOrCreateManifest(string sourceFilePath, PluginSourceMetadata metadata, string? manifestFilePath)
    {
        var resolvedManifestPath = manifestFilePath;
        if (string.IsNullOrWhiteSpace(resolvedManifestPath))
        {
            resolvedManifestPath = Path.Combine(
                Path.GetDirectoryName(sourceFilePath)!,
                $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.cursorfx-plugin.json");
        }

        if (!string.IsNullOrWhiteSpace(resolvedManifestPath) && File.Exists(resolvedManifestPath))
        {
            var manifest = JsonSerializer.Deserialize<ShaderTemplateDefinition>(File.ReadAllText(resolvedManifestPath), SerializerOptions);
            if (manifest is not null)
            {
                return manifest;
            }
        }

        return new ShaderTemplateDefinition
        {
            Id = metadata.PluginId,
            Name = ToDisplayName(metadata.ClassName),
            Description = $"Source-imported plugin generated from {Path.GetFileName(sourceFilePath)}.",
            IconGlyph = "*",
            AccentColor = "#4FD1C5",
            RuntimeKind = TemplateRuntimeKind.ExternalAssembly,
            AssemblyFileName = $"{metadata.AssemblyName}.dll",
            EntryTypeName = metadata.EntryTypeName,
            Kind = TemplateEffectKind.CursorAura,
            Trigger = TemplateTrigger.FollowCursor,
            Parameters = metadata.Parameters
        };
    }

    private static ShaderTemplateDefinition PrepareIconAsset(ShaderTemplateDefinition manifest, string catalogDirectory, string contextPath, string? iconOverridePath)
    {
        var requestedIconPath = string.IsNullOrWhiteSpace(iconOverridePath)
            ? manifest.IconPath
            : iconOverridePath;
        if (string.IsNullOrWhiteSpace(requestedIconPath))
        {
            return manifest;
        }

        var baseDirectory = Directory.Exists(contextPath)
            ? contextPath
            : Path.GetDirectoryName(contextPath) ?? string.Empty;
        var sourceIconPath = Path.IsPathRooted(requestedIconPath)
            ? requestedIconPath
            : Path.Combine(baseDirectory, requestedIconPath);

        if (!File.Exists(sourceIconPath))
        {
            return manifest;
        }

        var targetFileName = $"{manifest.Id}{Path.GetExtension(sourceIconPath)}";
        var targetPath = Path.Combine(catalogDirectory, targetFileName);
        File.Copy(sourceIconPath, targetPath, overwrite: true);

        return new ShaderTemplateDefinition
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            IconGlyph = manifest.IconGlyph,
            IconPath = targetFileName,
            ResolvedIconPath = targetPath,
            AccentColor = manifest.AccentColor,
            RuntimeKind = manifest.RuntimeKind,
            AssemblyFileName = manifest.AssemblyFileName,
            EntryTypeName = manifest.EntryTypeName,
            Kind = manifest.Kind,
            Trigger = manifest.Trigger,
            Parameters = manifest.Parameters
        };
    }

    private static void RunDotnetBuild(string projectPath, string outputDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectPath}\" -c Release -o \"{outputDirectory}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start dotnet build.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}{Environment.NewLine}{stderr}";
            throw new InvalidOperationException($"Source plugin build failed.{Environment.NewLine}{output}".Trim());
        }
    }

    private static IEnumerable<string> EnumerateBuiltFiles(string outputDirectory, string assemblyName)
    {
        foreach (var extension in new[] { ".dll", ".pdb", ".deps.json", ".runtimeconfig.json" })
        {
            var filePath = Path.Combine(outputDirectory, assemblyName + extension);
            if (File.Exists(filePath))
            {
                yield return filePath;
            }
        }
    }

    private static string BuildProjectFile(string assemblyName)
    {
        var coreDllPath = Path.Combine(AppContext.BaseDirectory, "CursorFX.Core.dll");
        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <AssemblyName>{{assemblyName}}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="CursorFX.Core">
      <HintPath>{{coreDllPath}}</HintPath>
    </Reference>
  </ItemGroup>
</Project>
""";
    }

    private static PluginParameterSection InferSection(string key)
    {
        if (key.StartsWith("trail", StringComparison.OrdinalIgnoreCase))
        {
            return PluginParameterSection.Trail;
        }

        if (key.StartsWith("glow", StringComparison.OrdinalIgnoreCase))
        {
            return PluginParameterSection.Glow;
        }

        if (key.StartsWith("ripple", StringComparison.OrdinalIgnoreCase) || key.StartsWith("impact", StringComparison.OrdinalIgnoreCase))
        {
            return PluginParameterSection.Ripple;
        }

        return PluginParameterSection.Shader;
    }

    private static string InferSectionName(string key, PluginParameterSection section)
    {
        return section switch
        {
            PluginParameterSection.Trail => "Trail",
            PluginParameterSection.Glow => "Glow",
            PluginParameterSection.Ripple => key.StartsWith("impact", StringComparison.OrdinalIgnoreCase) ? "Impact" : "Ripple",
            _ => key.StartsWith("custom", StringComparison.OrdinalIgnoreCase) ? "Custom Shader" : "Shader"
        };
    }

    private static double InferMin(string key, TemplateParameterType type)
    {
        if (type != TemplateParameterType.Number)
        {
            return 0;
        }

        if (key.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("fade", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("lifetime", StringComparison.OrdinalIgnoreCase))
        {
            return 0.05;
        }

        if (key.Contains("thickness", StringComparison.OrdinalIgnoreCase) || key.Contains("detail", StringComparison.OrdinalIgnoreCase))
        {
            return 0.5;
        }

        return 0;
    }

    private static double InferMax(string key, TemplateParameterType type)
    {
        if (type != TemplateParameterType.Number)
        {
            return type == TemplateParameterType.Toggle ? 1 : 0;
        }

        if (key.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("fade", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (key.Contains("lifetime", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (key.Contains("motion", StringComparison.OrdinalIgnoreCase) || key.Contains("speed", StringComparison.OrdinalIgnoreCase))
        {
            return 8;
        }

        if (key.Contains("thickness", StringComparison.OrdinalIgnoreCase) || key.Contains("detail", StringComparison.OrdinalIgnoreCase))
        {
            return 16;
        }

        if (key.Contains("size", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("radius", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("length", StringComparison.OrdinalIgnoreCase))
        {
            return 220;
        }

        return 100;
    }

    private static double InferStep(string key, TemplateParameterType type)
    {
        if (type == TemplateParameterType.Toggle || type == TemplateParameterType.Color)
        {
            return 1;
        }

        if (key.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("fade", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("lifetime", StringComparison.OrdinalIgnoreCase))
        {
            return 0.01;
        }

        if (key.Contains("motion", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("speed", StringComparison.OrdinalIgnoreCase))
        {
            return 0.05;
        }

        if (key.Contains("thickness", StringComparison.OrdinalIgnoreCase) || key.Contains("detail", StringComparison.OrdinalIgnoreCase))
        {
            return 0.5;
        }

        return 1;
    }

    private static double InferDefaultNumber(string key)
    {
        return key.ToLowerInvariant() switch
        {
            var name when name.Contains("opacity") => 0.75,
            var name when name.Contains("fade") => 0.25,
            var name when name.Contains("lifetime") => 0.35,
            var name when name.Contains("motion") => 1.5,
            var name when name.Contains("speed") => 1.5,
            var name when name.Contains("thickness") => 2.5,
            var name when name.Contains("detail") => 1.5,
            var name when name.Contains("size") => 24,
            var name when name.Contains("radius") => 72,
            var name when name.Contains("length") => 20,
            _ => 0
        };
    }

    private static string InferDefaultColor(string key)
    {
        return key.ToLowerInvariant() switch
        {
            var name when name.Contains("accent") => "#E0F2FE",
            var name when name.Contains("glow") => "#E0F2FE",
            var name when name.Contains("trail") => "#60A5FA",
            var name when name.Contains("ripple") || name.Contains("impact") => "#22D3EE",
            var name when name.Contains("primary") => "#38BDF8",
            _ => "#FFFFFF"
        };
    }

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string ToDisplayName(string key)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < key.Length; index++)
        {
            var character = key[index];
            if (index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(key[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(index == 0 ? char.ToUpper(character, CultureInfo.InvariantCulture) : character);
        }

        return builder.ToString();
    }

    private sealed record PluginSourceMetadata(
        string PluginId,
        string ClassName,
        string AssemblyName,
        string EntryTypeName,
        List<TemplateParameterDefinition> Parameters);
}

public sealed record SourcePluginCompilationResult(
    ShaderTemplateDefinition Definition,
    string ManifestPath,
    string AssemblyPath);
