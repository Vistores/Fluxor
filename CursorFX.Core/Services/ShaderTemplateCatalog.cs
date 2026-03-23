using System.IO;
using System.Text.Json;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Core.Services;

public sealed class ShaderTemplateCatalog : IShaderTemplateCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public ShaderTemplateCatalog()
    {
        CatalogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CursorFX",
            "Plugins");
    }

    public string CatalogDirectory { get; }

    public void EnsureCatalog()
    {
        Directory.CreateDirectory(CatalogDirectory);
        SeedTemplate("neon-suite.cursorfx-plugin.json", BuildNeonSuite(), overwrite: true);
        SeedTemplate("minimal-suite.cursorfx-plugin.json", BuildMinimalSuite(), overwrite: true);
        SeedTemplate("gaming-suite.cursorfx-plugin.json", BuildGamingSuite(), overwrite: true);
    }

    public IReadOnlyList<ShaderTemplateDefinition> LoadTemplates()
    {
        EnsureCatalog();

        var templates = new List<ShaderTemplateDefinition>();
        foreach (var filePath in Directory.EnumerateFiles(CatalogDirectory, "*.cursorfx-plugin.json"))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var template = JsonSerializer.Deserialize<ShaderTemplateDefinition>(json, SerializerOptions);
                if (template is not null)
                {
                    ValidateTemplate(template);
                    template.ResolvedIconPath = ResolveIconPath(template.IconPath);
                    templates.Add(template);
                }
            }
            catch
            {
            }
        }

        return templates
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ShaderTemplateDefinition ImportTemplate(string sourceFilePath, string? iconOverridePath = null)
    {
        EnsureCatalog();

        var json = File.ReadAllText(sourceFilePath);
        var template = JsonSerializer.Deserialize<ShaderTemplateDefinition>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Unable to parse plugin file.");
        ValidateTemplate(template);

        template = PrepareImportedAssets(template, sourceFilePath, iconOverridePath);

        var destinationPath = Path.Combine(CatalogDirectory, $"{template.Id}.cursorfx-plugin.json");
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(template, SerializerOptions));
        return template;
    }

    public ShaderTemplateDefinition SaveTemplate(ShaderTemplateDefinition template, string? iconOverridePath = null)
    {
        EnsureCatalog();
        ValidateTemplate(template);

        template = PrepareImportedAssets(template, Path.Combine(CatalogDirectory, $"{template.Id}.cursorfx-plugin.json"), iconOverridePath);

        var destinationPath = Path.Combine(CatalogDirectory, $"{template.Id}.cursorfx-plugin.json");
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(template, SerializerOptions));
        return template;
    }

    public void ExportTemplate(ShaderTemplateDefinition template, string destinationFilePath)
    {
        ValidateTemplate(template);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
        File.WriteAllText(destinationFilePath, JsonSerializer.Serialize(template, SerializerOptions));
    }

    private void SeedTemplate(string fileName, ShaderTemplateDefinition template, bool overwrite = false)
    {
        var path = Path.Combine(CatalogDirectory, fileName);
        if (File.Exists(path) && !overwrite)
        {
            return;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(template, SerializerOptions));
    }

    private ShaderTemplateDefinition PrepareImportedAssets(ShaderTemplateDefinition template, string sourceFilePath, string? iconOverridePath)
    {
        var previousIconPath = ResolveIconPath(template.IconPath);
        var requestedIconPath = string.IsNullOrWhiteSpace(iconOverridePath)
            ? template.IconPath
            : iconOverridePath;
        if (string.IsNullOrWhiteSpace(requestedIconPath))
        {
            TryDeleteOldIcon(previousIconPath);
            return new ShaderTemplateDefinition
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                IconGlyph = template.IconGlyph,
                IconPath = string.Empty,
                ResolvedIconPath = string.Empty,
                AccentColor = template.AccentColor,
                RuntimeKind = template.RuntimeKind,
                AssemblyFileName = template.AssemblyFileName,
                EntryTypeName = template.EntryTypeName,
                Kind = template.Kind,
                Trigger = template.Trigger,
                Parameters = template.Parameters
            };
        }

        var sourceDirectory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var iconSourcePath = Path.IsPathRooted(requestedIconPath)
            ? requestedIconPath
            : Path.Combine(sourceDirectory, requestedIconPath);

        if (!File.Exists(iconSourcePath))
        {
            TryDeleteOldIcon(previousIconPath);
            return new ShaderTemplateDefinition
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                IconGlyph = template.IconGlyph,
                IconPath = string.Empty,
                ResolvedIconPath = string.Empty,
                AccentColor = template.AccentColor,
                RuntimeKind = template.RuntimeKind,
                AssemblyFileName = template.AssemblyFileName,
                EntryTypeName = template.EntryTypeName,
                Kind = template.Kind,
                Trigger = template.Trigger,
                Parameters = template.Parameters
            };
        }

        var targetFileName = $"{template.Id}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(iconSourcePath)}";
        var targetPath = Path.Combine(CatalogDirectory, targetFileName);
        File.Copy(iconSourcePath, targetPath, overwrite: true);
        TryDeleteOldIcon(previousIconPath, targetPath);

        return new ShaderTemplateDefinition
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IconGlyph = template.IconGlyph,
            IconPath = targetFileName,
            ResolvedIconPath = targetPath,
            AccentColor = template.AccentColor,
            RuntimeKind = template.RuntimeKind,
            AssemblyFileName = template.AssemblyFileName,
            EntryTypeName = template.EntryTypeName,
            Kind = template.Kind,
            Trigger = template.Trigger,
            Parameters = template.Parameters
        };
    }

    private static void TryDeleteOldIcon(string previousIconPath, string? replacementPath = null)
    {
        if (string.IsNullOrWhiteSpace(previousIconPath) || !File.Exists(previousIconPath))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(replacementPath) &&
            string.Equals(previousIconPath, replacementPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            File.Delete(previousIconPath);
        }
        catch
        {
        }
    }

    private string ResolveIconPath(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(iconPath))
        {
            return File.Exists(iconPath) ? iconPath : string.Empty;
        }

        var combined = Path.Combine(CatalogDirectory, iconPath);
        return File.Exists(combined) ? combined : string.Empty;
    }

    private static void ValidateTemplate(ShaderTemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            throw new InvalidOperationException("Plugin validation failed: Id is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new InvalidOperationException("Plugin validation failed: Name is required.");
        }

        if (template.Parameters.Count == 0)
        {
            throw new InvalidOperationException("Plugin validation failed: at least one parameter is required.");
        }

        if (template.RuntimeKind == TemplateRuntimeKind.ExternalAssembly)
        {
            if (string.IsNullOrWhiteSpace(template.AssemblyFileName))
            {
                throw new InvalidOperationException("Plugin validation failed: AssemblyFileName is required for external assembly plugins.");
            }

            if (string.IsNullOrWhiteSpace(template.EntryTypeName))
            {
                throw new InvalidOperationException("Plugin validation failed: EntryTypeName is required for external assembly plugins.");
            }
        }

        var duplicateKey = template.Parameters
            .GroupBy(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(duplicateKey.Key)
                    ? "Plugin validation failed: every parameter must have a key."
                    : $"Plugin validation failed: duplicate parameter key '{duplicateKey.Key}'.");
        }

        foreach (var parameter in template.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.DisplayName))
            {
                throw new InvalidOperationException($"Plugin validation failed: parameter '{parameter.Key}' is missing DisplayName.");
            }

            if (parameter.Type == TemplateParameterType.Number && parameter.Min >= parameter.Max)
            {
                throw new InvalidOperationException($"Plugin validation failed: numeric parameter '{parameter.Key}' has invalid Min/Max.");
            }
        }
    }

    private static ShaderTemplateDefinition BuildNeonSuite()
    {
        return BuildSuite(
            "neon-suite",
            "Neon Suite",
            "Balanced neon cursor profile with trail, glow, ripple and shader aura.",
            TemplateEffectKind.CursorAura,
            trailColor: "#22D3EE",
            glowColor: "#67E8F9",
            rippleColor: "#A5F3FC",
            shaderPrimary: "#22D3EE",
            shaderAccent: "#F97316",
            trailLength: 32,
            trailThickness: 12,
            trailFade: 0.55,
            glowSize: 32,
            glowOpacity: 0.42,
            rippleRadius: 86,
            rippleLifetime: 0.7,
            rippleOpacity: 0.75,
            rippleThickness: 3,
            shaderSize: 54,
            shaderOpacity: 0.42,
            shaderMotion: 1.4,
            shaderDetail: 3);
    }

    private static ShaderTemplateDefinition BuildMinimalSuite()
    {
        return BuildSuite(
            "minimal-suite",
            "Minimal Suite",
            "Soft lightweight profile with subtle built-in effects.",
            TemplateEffectKind.CursorAura,
            trailColor: "#CBD5E1",
            glowColor: "#E2E8F0",
            rippleColor: "#CBD5E1",
            shaderPrimary: "#E2E8F0",
            shaderAccent: "#94A3B8",
            trailLength: 18,
            trailThickness: 6,
            trailFade: 0.32,
            glowSize: 18,
            glowOpacity: 0.18,
            rippleRadius: 54,
            rippleLifetime: 0.45,
            rippleOpacity: 0.35,
            rippleThickness: 2,
            shaderSize: 26,
            shaderOpacity: 0.18,
            shaderMotion: 0.9,
            shaderDetail: 2);
    }

    private static ShaderTemplateDefinition BuildGamingSuite()
    {
        return BuildSuite(
            "gaming-suite",
            "Gaming Suite",
            "Aggressive preset with stronger trail and click feedback.",
            TemplateEffectKind.ClickBurst,
            trailColor: "#38BDF8",
            glowColor: "#818CF8",
            rippleColor: "#C4B5FD",
            shaderPrimary: "#C4B5FD",
            shaderAccent: "#F43F5E",
            trailLength: 48,
            trailThickness: 18,
            trailFade: 0.75,
            glowSize: 42,
            glowOpacity: 0.55,
            rippleRadius: 112,
            rippleLifetime: 0.9,
            rippleOpacity: 0.9,
            rippleThickness: 4,
            shaderSize: 120,
            shaderOpacity: 0.9,
            shaderMotion: 0.9,
            shaderDetail: 4);
    }

    private static ShaderTemplateDefinition BuildSuite(
        string id,
        string name,
        string description,
        TemplateEffectKind kind,
        string trailColor,
        string glowColor,
        string rippleColor,
        string shaderPrimary,
        string shaderAccent,
        double trailLength,
        double trailThickness,
        double trailFade,
        double glowSize,
        double glowOpacity,
        double rippleRadius,
        double rippleLifetime,
        double rippleOpacity,
        double rippleThickness,
        double shaderSize,
        double shaderOpacity,
        double shaderMotion,
        double shaderDetail)
    {
        return new ShaderTemplateDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            IconGlyph = id switch
            {
                "neon-suite" => "✦",
                "minimal-suite" => "◌",
                "gaming-suite" => "▣",
                _ => "✦"
            },
            AccentColor = id switch
            {
                "neon-suite" => "#54D0C8",
                "minimal-suite" => "#AAB8C8",
                "gaming-suite" => "#F59E0B",
                _ => "#54D0C8"
            },
            RuntimeKind = TemplateRuntimeKind.BuiltInTemplate,
            Kind = kind,
            Trigger = kind == TemplateEffectKind.ClickBurst ? TemplateTrigger.MouseClick : TemplateTrigger.FollowCursor,
            Parameters =
            [
                new TemplateParameterDefinition { Key = "trailEnabled", DisplayName = "Enable Trail", Section = PluginParameterSection.Trail, SectionName = "Trail", Type = TemplateParameterType.Toggle, DefaultBoolean = true },
                new TemplateParameterDefinition { Key = "trailLength", DisplayName = "Trail Length", Section = PluginParameterSection.Trail, SectionName = "Trail", Type = TemplateParameterType.Number, Min = 8, Max = 96, Step = 1, DefaultNumber = trailLength },
                new TemplateParameterDefinition { Key = "trailThickness", DisplayName = "Trail Thickness", Section = PluginParameterSection.Trail, SectionName = "Trail", Type = TemplateParameterType.Number, Min = 2, Max = 28, Step = 0.5, DefaultNumber = trailThickness },
                new TemplateParameterDefinition { Key = "trailFade", DisplayName = "Trail Fade", Section = PluginParameterSection.Trail, SectionName = "Trail", Type = TemplateParameterType.Number, Min = 0.15, Max = 2.4, Step = 0.05, DefaultNumber = trailFade },
                new TemplateParameterDefinition { Key = "trailColor", DisplayName = "Trail Color", Section = PluginParameterSection.Trail, SectionName = "Trail", Type = TemplateParameterType.Color, DefaultColor = trailColor },

                new TemplateParameterDefinition { Key = "glowEnabled", DisplayName = "Enable Glow", Section = PluginParameterSection.Glow, SectionName = "Glow", Type = TemplateParameterType.Toggle, DefaultBoolean = true },
                new TemplateParameterDefinition { Key = "glowSize", DisplayName = "Glow Size", Section = PluginParameterSection.Glow, SectionName = "Glow", Type = TemplateParameterType.Number, Min = 12, Max = 120, Step = 1, DefaultNumber = glowSize },
                new TemplateParameterDefinition { Key = "glowOpacity", DisplayName = "Glow Opacity", Section = PluginParameterSection.Glow, SectionName = "Glow", Type = TemplateParameterType.Number, Min = 0.05, Max = 1, Step = 0.01, DefaultNumber = glowOpacity },
                new TemplateParameterDefinition { Key = "glowColor", DisplayName = "Glow Color", Section = PluginParameterSection.Glow, SectionName = "Glow", Type = TemplateParameterType.Color, DefaultColor = glowColor },

                new TemplateParameterDefinition { Key = "rippleEnabled", DisplayName = "Enable Ripple", Section = PluginParameterSection.Ripple, SectionName = "Ripple", Type = TemplateParameterType.Toggle, DefaultBoolean = true },
                new TemplateParameterDefinition { Key = "rippleRadius", DisplayName = "Ripple Radius", Section = PluginParameterSection.Ripple, SectionName = "Ripple", Type = TemplateParameterType.Number, Min = 24, Max = 220, Step = 1, DefaultNumber = rippleRadius },
                new TemplateParameterDefinition { Key = "rippleLifetime", DisplayName = "Ripple Lifetime", Section = PluginParameterSection.Ripple, SectionName = "Ripple", Type = TemplateParameterType.Number, Min = 0.2, Max = 2.5, Step = 0.05, DefaultNumber = rippleLifetime },
                new TemplateParameterDefinition { Key = "rippleOpacity", DisplayName = "Ripple Opacity", Section = PluginParameterSection.Ripple, SectionName = "Ripple", Type = TemplateParameterType.Number, Min = 0.05, Max = 1, Step = 0.01, DefaultNumber = rippleOpacity },
                new TemplateParameterDefinition { Key = "rippleThickness", DisplayName = "Ripple Thickness", Section = PluginParameterSection.Ripple, SectionName = "Ripple", Type = TemplateParameterType.Number, Min = 1, Max = 12, Step = 0.5, DefaultNumber = rippleThickness },
                new TemplateParameterDefinition { Key = "rippleColor", DisplayName = "Ripple Color", Section = PluginParameterSection.Ripple, SectionName = "Ripple", Type = TemplateParameterType.Color, DefaultColor = rippleColor },

                new TemplateParameterDefinition { Key = "shaderEnabled", DisplayName = "Enable Shader Layer", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Toggle, DefaultBoolean = true },
                new TemplateParameterDefinition { Key = "primaryColor", DisplayName = "Primary Color", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Color, DefaultColor = shaderPrimary },
                new TemplateParameterDefinition { Key = "accentColor", DisplayName = "Accent Color", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Color, DefaultColor = shaderAccent },
                new TemplateParameterDefinition { Key = "size", DisplayName = "Shader Size", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Number, Min = 12, Max = 220, Step = 1, DefaultNumber = shaderSize },
                new TemplateParameterDefinition { Key = "opacity", DisplayName = "Shader Opacity", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Number, Min = 0.05, Max = 1, Step = 0.01, DefaultNumber = shaderOpacity },
                new TemplateParameterDefinition { Key = "motion", DisplayName = kind == TemplateEffectKind.OrbitTrail ? "Orbit Speed" : kind == TemplateEffectKind.ClickBurst ? "Burst Lifetime" : "Pulse Speed", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Number, Min = 0.1, Max = 8, Step = 0.1, DefaultNumber = shaderMotion },
                new TemplateParameterDefinition { Key = "detail", DisplayName = kind == TemplateEffectKind.OrbitTrail ? "Dot Size" : "Ring Thickness", Section = PluginParameterSection.Shader, SectionName = "Shader", Type = TemplateParameterType.Number, Min = 1, Max = 16, Step = 0.5, DefaultNumber = shaderDetail }
            ]
        };
    }
}
