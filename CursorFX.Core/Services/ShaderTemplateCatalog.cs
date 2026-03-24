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
        SeedTemplate("prism-bloom.cursorfx-plugin.json", BuildPrismBloomSuite(), overwrite: true);
        SeedTemplate("arc-sparkle.cursorfx-plugin.json", BuildArcSparkleSuite(), overwrite: true);
        SeedTemplate("comet-ribbon.cursorfx-plugin.json", BuildCometRibbonSuite(), overwrite: true);
        SeedTemplate("nebula-drift.cursorfx-plugin.json", BuildNebulaDriftSuite(), overwrite: true);
        SeedTemplate("frost-halo.cursorfx-plugin.json", BuildFrostHaloSuite(), overwrite: true);
        SeedTemplate("solar-flare.cursorfx-plugin.json", BuildSolarFlareSuite(), overwrite: true);
        SeedTemplate("mystic-runes.cursorfx-plugin.json", BuildMysticRunesSuite(), overwrite: true);
        SeedTemplate("ribbon-wave.cursorfx-plugin.json", BuildRibbonWaveSuite(), overwrite: true);
        SeedTemplate("torn-current.cursorfx-plugin.json", BuildTornCurrentSuite(), overwrite: true);
        SeedTemplate("matrix-cascade.cursorfx-plugin.json", BuildMatrixCascadeSuite(), overwrite: true);
        SeedTemplate("cosmic-rift.cursorfx-plugin.json", BuildCosmicRiftSuite(), overwrite: true);
        SeedTemplate("glitch-fracture.cursorfx-plugin.json", BuildGlitchFractureSuite(), overwrite: true);
        SeedTemplate("velvet-flame.cursorfx-plugin.json", BuildVelvetFlameSuite(), overwrite: true);
        SeedTemplate("spark-shower.cursorfx-plugin.json", BuildSparkShowerSuite(), overwrite: true);
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
            return CloneTemplate(template, string.Empty, string.Empty);
        }

        var sourceDirectory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var iconSourcePath = Path.IsPathRooted(requestedIconPath)
            ? requestedIconPath
            : Path.Combine(sourceDirectory, requestedIconPath);

        if (!File.Exists(iconSourcePath))
        {
            TryDeleteOldIcon(previousIconPath);
            return CloneTemplate(template, string.Empty, string.Empty);
        }

        var targetFileName = $"{template.Id}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(iconSourcePath)}";
        var targetPath = Path.Combine(CatalogDirectory, targetFileName);
        File.Copy(iconSourcePath, targetPath, overwrite: true);
        TryDeleteOldIcon(previousIconPath, targetPath);

        return CloneTemplate(template, targetFileName, targetPath);
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

    private static ShaderTemplateDefinition CloneTemplate(ShaderTemplateDefinition template, string iconPath, string resolvedIconPath)
    {
        return new ShaderTemplateDefinition
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IconGlyph = template.IconGlyph,
            IconPath = iconPath,
            ResolvedIconPath = resolvedIconPath,
            AccentColor = template.AccentColor,
            RuntimeKind = template.RuntimeKind,
            AssemblyFileName = template.AssemblyFileName,
            EntryTypeName = template.EntryTypeName,
            Kind = template.Kind,
            Trigger = template.Trigger,
            Parameters = template.Parameters
        };
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

    private static ShaderTemplateDefinition BuildNeonSuite() =>
        BuildSuite("neon-suite", "Neon Suite", "Balanced neon cursor profile with trail, glow, ripple and shader aura.", "N", "#54D0C8", TemplateEffectKind.CursorAura,
            "#22D3EE", "#67E8F9", "#A5F3FC", "#22D3EE", "#F97316", 32, 12, 0.55, 32, 0.42, 86, 0.7, 0.75, 3, 54, 0.42, 1.4, 3,
            [Toggle("showRing", "Show Orbit Ring", PluginParameterSection.Shader, "Shader", true), Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 18), Number("clickLifetime", "Click Accent Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.8), Number("particles", "Accent Particles", PluginParameterSection.Shader, "Shader", 4, 24, 1, 8)]);

    private static ShaderTemplateDefinition BuildMinimalSuite() =>
        BuildSuite("minimal-suite", "Minimal Suite", "Soft lightweight profile with subtle built-in effects.", "M", "#AAB8C8", TemplateEffectKind.CursorAura,
            "#CBD5E1", "#E2E8F0", "#CBD5E1", "#E2E8F0", "#94A3B8", 18, 6, 0.32, 18, 0.18, 54, 0.45, 0.35, 2, 26, 0.18, 0.9, 2,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 20), Number("clickLifetime", "Click Accent Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.5), Number("particles", "Accent Particles", PluginParameterSection.Shader, "Shader", 4, 24, 1, 6)]);

    private static ShaderTemplateDefinition BuildGamingSuite() =>
        BuildSuite("gaming-suite", "Gaming Suite", "Aggressive preset with stronger trail and click feedback.", "G", "#F59E0B", TemplateEffectKind.ClickBurst,
            "#38BDF8", "#818CF8", "#C4B5FD", "#C4B5FD", "#F43F5E", 48, 18, 0.75, 42, 0.55, 112, 0.9, 0.9, 4, 120, 0.9, 0.9, 4,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 17), Number("clickLifetime", "Click Accent Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.9), Number("particles", "Burst Rays", PluginParameterSection.Shader, "Shader", 6, 28, 1, 12)]);

    private static ShaderTemplateDefinition BuildPrismBloomSuite() =>
        BuildSuite("prism-bloom", "Prism Bloom", "Layered iridescent bloom with petal rings and soft click echoes.", "P", "#7C3AED", TemplateEffectKind.PrismBloom,
            "#A78BFA", "#C4B5FD", "#DDD6FE", "#67E8F9", "#F472B6", 28, 10, 0.42, 30, 0.26, 74, 0.66, 0.62, 2.6, 64, 0.48, 1.25, 6,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 16), Number("clickLifetime", "Click Echo Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.76), Number("particles", "Bloom Layers", PluginParameterSection.Shader, "Shader", 4, 16, 1, 6)]);

    private static ShaderTemplateDefinition BuildArcSparkleSuite() =>
        BuildSuite("arc-sparkle", "Arc Sparkle", "Magic spark halo with orbiting points and bright click bursts.", "A", "#A78BFA", TemplateEffectKind.ArcSparkle,
            "#F59E0B", "#FDE68A", "#FDE68A", "#A78BFA", "#FDE68A", 26, 8, 0.34, 26, 0.22, 68, 0.58, 0.68, 2.4, 46, 0.55, 2.1, 9,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 19), Number("clickLifetime", "Spark Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.64), Number("particles", "Spark Count", PluginParameterSection.Shader, "Shader", 8, 30, 1, 14)]);

    private static ShaderTemplateDefinition BuildCometRibbonSuite() =>
        BuildSuite("comet-ribbon", "Comet Ribbon", "Inertial comet head with a directional ribbon and clean impact ring.", "C", "#38BDF8", TemplateEffectKind.CometRibbon,
            "#38BDF8", "#7DD3FC", "#E0F2FE", "#38BDF8", "#E0F2FE", 44, 14, 0.78, 30, 0.3, 96, 0.72, 0.74, 3.2, 64, 0.6, 1.3, 6,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 12), Number("clickLifetime", "Impact Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.8), Number("particles", "Tail Accents", PluginParameterSection.Shader, "Shader", 4, 20, 1, 8)]);

    private static ShaderTemplateDefinition BuildNebulaDriftSuite() =>
        BuildSuite("nebula-drift", "Nebula Drift", "Soft cosmic dust cloud with drifting particles and plush click blooms.", "D", "#C084FC", TemplateEffectKind.NebulaDust,
            "#7DD3FC", "#C084FC", "#E9D5FF", "#7DD3FC", "#C084FC", 30, 11, 0.58, 38, 0.32, 90, 0.8, 0.65, 2.6, 72, 0.44, 1.15, 10,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 15), Number("clickLifetime", "Bloom Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.88), Number("particles", "Dust Count", PluginParameterSection.Shader, "Shader", 8, 30, 1, 16)]);

    private static ShaderTemplateDefinition BuildFrostHaloSuite() =>
        BuildSuite("frost-halo", "Frost Halo", "Crystalline halo with orbiting snowflakes and icy tap accents.", "F", "#BFDBFE", TemplateEffectKind.FrostHalo,
            "#BFDBFE", "#E0F2FE", "#DBEAFE", "#BFDBFE", "#E0F2FE", 24, 8, 0.36, 28, 0.26, 72, 0.72, 0.54, 2.2, 58, 0.4, 1.55, 7,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 18), Number("clickLifetime", "Snow Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.82), Number("particles", "Snowflake Count", PluginParameterSection.Shader, "Shader", 4, 18, 1, 8)]);

    private static ShaderTemplateDefinition BuildSolarFlareSuite() =>
        BuildSuite("solar-flare", "Solar Flare", "Hot solar corona with flare spokes and bright expanding taps.", "S", "#F59E0B", TemplateEffectKind.SolarFlare,
            "#F59E0B", "#FDE68A", "#FED7AA", "#F59E0B", "#FDE68A", 34, 13, 0.62, 36, 0.38, 104, 0.8, 0.78, 3.4, 60, 0.58, 1.6, 8,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 14), Number("clickLifetime", "Flare Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.86), Number("particles", "Flare Rays", PluginParameterSection.Shader, "Shader", 6, 24, 1, 10)]);

    private static ShaderTemplateDefinition BuildMysticRunesSuite() =>
        BuildSuite("mystic-runes", "Mystic Runes", "Rotating rune circles with ceremonial taps and enchanted glow.", "R", "#34D399", TemplateEffectKind.MysticRunes,
            "#34D399", "#A7F3D0", "#D1FAE5", "#34D399", "#A7F3D0", 28, 10, 0.48, 30, 0.28, 78, 0.72, 0.62, 2.8, 64, 0.48, 0.95, 8,
            [Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 6, 32, 1, 17), Number("clickLifetime", "Rune Echo Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.78), Number("particles", "Rune Marks", PluginParameterSection.Shader, "Shader", 6, 24, 1, 12)]);

    private static ShaderTemplateDefinition BuildRibbonWaveSuite() =>
        BuildSuite("ribbon-wave", "Ribbon Wave", "Smooth ribbon trail with shader-cut waves gliding across the full cursor path.", "W", "#4FD1C5", TemplateEffectKind.CometRibbon,
            "#4FD1C5", "#8BDAE8", "#D8FBFF", "#67E8F9", "#A5F3FC", 56, 18, 0.92, 28, 0.22, 84, 0.6, 0.55, 2.2, 52, 0.34, 1.3, 6,
            trailMode: TrailRenderMode.WaveRibbon,
            waveAmplitude: 9.5,
            waveFrequency: 2.2,
            noiseAmount: 0.0,
            ribbonSoftness: 0.68,
            sourceLag: 9,
            idleRadius: 1.8,
            idleSpeed: 1.1,
            randomness: 0.8,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 12),
                Number("clickLifetime", "Impact Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.72),
                Number("particles", "Tail Accents", PluginParameterSection.Shader, "Shader", 4, 20, 1, 7)
            ]);

    private static ShaderTemplateDefinition BuildTornCurrentSuite() =>
        BuildSuite("torn-current", "Torn Current", "Ragged heat-like trail where the ribbon is sliced by waves and soft noise.", "T", "#FB923C", TemplateEffectKind.SolarFlare,
            "#FB923C", "#FDBA74", "#FED7AA", "#F97316", "#FDE68A", 54, 16, 0.86, 26, 0.18, 92, 0.7, 0.62, 2.4, 56, 0.38, 1.55, 7,
            trailMode: TrailRenderMode.TornRibbon,
            waveAmplitude: 8.0,
            waveFrequency: 2.8,
            noiseAmount: 4.5,
            ribbonSoftness: 0.6,
            sourceLag: 11,
            idleRadius: 2.0,
            idleSpeed: 1.5,
            gravityY: 1.0,
            randomness: 1.4,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 13),
                Number("clickLifetime", "Flare Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.8),
                Number("particles", "Flare Rays", PluginParameterSection.Shader, "Shader", 6, 24, 1, 9)
            ]);

    private static ShaderTemplateDefinition BuildMatrixCascadeSuite() =>
        BuildSuite("matrix-cascade", "Matrix Cascade", "Filled matrix-style glyph trail that flows behind the cursor as a soft continuous cascade.", "X", "#22C55E", TemplateEffectKind.MatrixCascade,
            "#166534", "#14532D", "#BBF7D0", "#22C55E", "#BBF7D0", 20, 4, 0.3, 18, 0.12, 76, 0.66, 0.5, 1.8, 58, 0.72, 1.8, 10,
            trailEnabled: false,
            trailMode: TrailRenderMode.SmoothLine,
            waveAmplitude: 0,
            waveFrequency: 1.2,
            noiseAmount: 0,
            ribbonSoftness: 0.45,
            sourceLag: 11,
            idleRadius: 0.8,
            idleSpeed: 0.85,
            gravityY: 0,
            randomness: 0.6,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 14),
                Number("clickLifetime", "Glyph Burst Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.72),
                Number("particles", "Glyph Density", PluginParameterSection.Shader, "Shader", 6, 30, 1, 18),
                Number("symbolSpacing", "Symbol Spacing", PluginParameterSection.Shader, "Shader", 4, 30, 1, 14),
                Number("matrixSpeed", "Particle Speed", PluginParameterSection.Shader, "Shader", 12, 180, 1, 68),
                Number("matrixSpread", "Trail Spread", PluginParameterSection.Shader, "Shader", 6, 80, 1, 26),
                Number("spawnRadius", "Spawn Radius", PluginParameterSection.Shader, "Shader", 0, 24, 1, 4),
                Number("matrixGlyphSize", "Glyph Size", PluginParameterSection.Shader, "Shader", 8, 28, 1, 12),
                Number("matrixLifetime", "Particle Lifetime", PluginParameterSection.Shader, "Shader", 0.2, 2.4, 0.05, 1.05),
                Number("matrixDamping", "Particle Damping", PluginParameterSection.Shader, "Shader", 0.2, 8, 0.1, 1.6),
                Number("spawnRate", "Spawn Rate", PluginParameterSection.Shader, "Shader", 8, 80, 1, 34),
                Number("driftStrength", "Drift Strength", PluginParameterSection.Shader, "Shader", 0, 12, 0.25, 1.2),
                Number("idleScatterThreshold", "Idle Scatter Threshold", PluginParameterSection.Shader, "Shader", 4, 120, 1, 32),
                Number("idleScatterRadius", "Idle Scatter Radius", PluginParameterSection.Shader, "Shader", 4, 80, 1, 16),
                Number("idleScatterSpeed", "Idle Scatter Speed", PluginParameterSection.Shader, "Shader", 4, 120, 1, 28)
            ]);

    private static ShaderTemplateDefinition BuildCosmicRiftSuite() =>
        BuildSuite("cosmic-rift", "Cosmic Rift", "Soft space fracture with dark velvet cracks and drifting star dust around the cursor path.", "C", "#7C3AED", TemplateEffectKind.CosmicRift,
            "#111827", "#8B5CF6", "#C4B5FD", "#0F172A", "#A78BFA", 34, 10, 0.62, 28, 0.18, 84, 0.82, 0.48, 2.2, 78, 0.52, 1.1, 8,
            trailEnabled: false,
            sourceLag: 8,
            idleRadius: 1.4,
            idleSpeed: 0.9,
            randomness: 1.2,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 14),
                Number("clickLifetime", "Rift Echo Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.95),
                Number("particles", "Star Density", PluginParameterSection.Shader, "Shader", 6, 28, 1, 14),
                Number("trailLifetime", "Healing Time", PluginParameterSection.Shader, "Shader", 0.3, 3.0, 0.05, 1.35),
                Number("trailFreedom", "Trail Freedom", PluginParameterSection.Shader, "Shader", 0.2, 2.4, 0.05, 1.15),
                Number("trailSpawnSpacing", "Trail Spacing", PluginParameterSection.Shader, "Shader", 4, 42, 1, 14),
                Number("backdropSize", "Backdrop Size", PluginParameterSection.Shader, "Shader", 64, 280, 4, 164),
                Number("sampleOpacity", "Backdrop Blend", PluginParameterSection.Shader, "Shader", 0.05, 0.95, 0.01, 0.48)
            ]);

    private static ShaderTemplateDefinition BuildGlitchFractureSuite() =>
        BuildSuite("glitch-fracture", "Glitch Fracture", "Broken digital slashes, RGB offsets and soft glitch bands trailing behind the cursor.", "G", "#60A5FA", TemplateEffectKind.GlitchFracture,
            "#0EA5E9", "#F472B6", "#F9A8D4", "#60A5FA", "#F472B6", 30, 8, 0.48, 22, 0.12, 76, 0.58, 0.56, 2.6, 70, 0.5, 1.8, 8,
            trailEnabled: false,
            sourceLag: 10,
            idleRadius: 0.6,
            idleSpeed: 0.7,
            randomness: 2.2,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 12),
                Number("clickLifetime", "Glitch Burst Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.68),
                Number("particles", "Fracture Count", PluginParameterSection.Shader, "Shader", 4, 20, 1, 10),
                Number("trailLifetime", "Persistence", PluginParameterSection.Shader, "Shader", 0.3, 3.0, 0.05, 1.0),
                Number("trailFreedom", "Fragment Freedom", PluginParameterSection.Shader, "Shader", 0.2, 2.6, 0.05, 1.25),
                Number("trailSpawnSpacing", "Fragment Spacing", PluginParameterSection.Shader, "Shader", 4, 42, 1, 12),
                Number("backdropSize", "Backdrop Size", PluginParameterSection.Shader, "Shader", 64, 280, 4, 152),
                Number("sampleOpacity", "Backdrop Blend", PluginParameterSection.Shader, "Shader", 0.05, 0.95, 0.01, 0.52),
                Number("distortion", "Band Distortion", PluginParameterSection.Shader, "Shader", 1, 28, 1, 10)
            ]);

    private static ShaderTemplateDefinition BuildVelvetFlameSuite() =>
        BuildSuite("velvet-flame", "Velvet Flame", "Soft real-flame veil with smooth volume and no hard-cut ribbon edges.", "V", "#F97316", TemplateEffectKind.VelvetFlame,
            "#F97316", "#FDBA74", "#FDE68A", "#F97316", "#FDE68A", 42, 14, 0.76, 32, 0.2, 88, 0.76, 0.62, 2.4, 74, 0.58, 1.45, 8,
            trailEnabled: false,
            sourceLag: 9,
            idleRadius: 1.0,
            idleSpeed: 1.2,
            gravityY: -0.6,
            randomness: 0.8,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 13),
                Number("clickLifetime", "Flame Bloom Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.84),
                Number("particles", "Flame Softness", PluginParameterSection.Shader, "Shader", 4, 18, 1, 9),
                Number("trailLifetime", "Flame Lifetime", PluginParameterSection.Shader, "Shader", 0.3, 3.0, 0.05, 1.15),
                Number("trailFreedom", "Flame Freedom", PluginParameterSection.Shader, "Shader", 0.2, 2.4, 0.05, 0.95),
                Number("trailSpawnSpacing", "Flame Spacing", PluginParameterSection.Shader, "Shader", 4, 42, 1, 12)
            ]);

    private static ShaderTemplateDefinition BuildSparkShowerSuite() =>
        BuildSuite("spark-shower", "Spark Shower", "Cursor emits clean warm sparks and soft bright dust instead of a heavy ribbon trail.", "S", "#F59E0B", TemplateEffectKind.SparkShower,
            "#F59E0B", "#FDE68A", "#FCD34D", "#F59E0B", "#FDE68A", 20, 6, 0.32, 18, 0.14, 72, 0.56, 0.58, 2.4, 62, 0.62, 1.8, 12,
            trailEnabled: false,
            sourceLag: 11,
            idleRadius: 0.6,
            idleSpeed: 1.1,
            gravityY: 0.8,
            randomness: 1.7,
            extraParameters:
            [
                Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Shader", 4, 32, 1, 15),
                Number("clickLifetime", "Spark Burst Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, 0.62),
                Number("particles", "Spark Count", PluginParameterSection.Shader, "Shader", 6, 28, 1, 16),
                Number("trailLifetime", "Spark Lifetime", PluginParameterSection.Shader, "Shader", 0.3, 3.0, 0.05, 0.82),
                Number("trailFreedom", "Spark Freedom", PluginParameterSection.Shader, "Shader", 0.2, 2.6, 0.05, 1.35),
                Number("trailSpawnSpacing", "Spark Spacing", PluginParameterSection.Shader, "Shader", 4, 42, 1, 10)
            ]);

    private static ShaderTemplateDefinition BuildSuite(
        string id,
        string name,
        string description,
        string iconGlyph,
        string accentColor,
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
        double shaderDetail,
        IReadOnlyList<TemplateParameterDefinition>? extraParameters = null,
        bool trailEnabled = true,
        TrailRenderMode trailMode = TrailRenderMode.SmoothLine,
        double waveAmplitude = 0,
        double waveFrequency = 1.2,
        double noiseAmount = 0,
        double ribbonSoftness = 0.45,
        double sourceLag = 10,
        double idleRadius = 0.8,
        double idleSpeed = 1.1,
        double gravityX = 0,
        double gravityY = 0,
        double randomness = 0.3)
    {
        var parameters = new List<TemplateParameterDefinition>
        {
            Toggle("trailEnabled", "Enable Trail", PluginParameterSection.Trail, "Trail", trailEnabled),
            Number("trailLength", "Trail Length", PluginParameterSection.Trail, "Trail", 8, 96, 1, trailLength),
            Number("trailThickness", "Trail Thickness", PluginParameterSection.Trail, "Trail", 2, 28, 0.5, trailThickness),
            Number("trailFade", "Trail Fade", PluginParameterSection.Trail, "Trail", 0.15, 2.4, 0.05, trailFade),
            Color("trailColor", "Trail Color", PluginParameterSection.Trail, "Trail", trailColor),
            Number("trailMode", "Trail Style", PluginParameterSection.Trail, "Trail", 0, 2, 1, (double)trailMode),
            Number("waveAmplitude", "Wave Amplitude", PluginParameterSection.Trail, "Trail", 0, 18, 0.5, waveAmplitude),
            Number("waveFrequency", "Wave Frequency", PluginParameterSection.Trail, "Trail", 0.5, 6, 0.1, waveFrequency),
            Number("noiseAmount", "Noise Amount", PluginParameterSection.Trail, "Trail", 0, 12, 0.25, noiseAmount),
            Number("ribbonSoftness", "Ribbon Width Bias", PluginParameterSection.Trail, "Trail", 0.1, 1, 0.05, ribbonSoftness),

            Toggle("glowEnabled", "Enable Glow", PluginParameterSection.Glow, "Glow", true),
            Number("glowSize", "Glow Size", PluginParameterSection.Glow, "Glow", 12, 120, 1, glowSize),
            Number("glowOpacity", "Glow Opacity", PluginParameterSection.Glow, "Glow", 0.05, 1, 0.01, glowOpacity),
            Color("glowColor", "Glow Color", PluginParameterSection.Glow, "Glow", glowColor),

            Toggle("rippleEnabled", "Enable Ripple", PluginParameterSection.Ripple, "Ripple", true),
            Number("rippleRadius", "Ripple Radius", PluginParameterSection.Ripple, "Ripple", 24, 220, 1, rippleRadius),
            Number("rippleLifetime", "Ripple Lifetime", PluginParameterSection.Ripple, "Ripple", 0.2, 2.5, 0.05, rippleLifetime),
            Number("rippleOpacity", "Ripple Opacity", PluginParameterSection.Ripple, "Ripple", 0.05, 1, 0.01, rippleOpacity),
            Number("rippleThickness", "Ripple Thickness", PluginParameterSection.Ripple, "Ripple", 1, 12, 0.5, rippleThickness),
            Color("rippleColor", "Ripple Color", PluginParameterSection.Ripple, "Ripple", rippleColor),

            Toggle("shaderEnabled", "Enable Shader Layer", PluginParameterSection.Shader, "Shader", true),
            Color("primaryColor", "Primary Color", PluginParameterSection.Shader, "Shader", shaderPrimary),
            Color("accentColor", "Accent Color", PluginParameterSection.Shader, "Shader", shaderAccent),
            Number("size", "Shader Size", PluginParameterSection.Shader, "Shader", 12, 220, 1, shaderSize),
            Number("opacity", "Shader Opacity", PluginParameterSection.Shader, "Shader", 0.05, 1, 0.01, shaderOpacity),
            Number("motion", kind == TemplateEffectKind.OrbitTrail ? "Orbit Speed" : kind == TemplateEffectKind.ClickBurst ? "Burst Lifetime" : "Motion", PluginParameterSection.Shader, "Shader", 0.1, 8, 0.1, shaderMotion),
            Number("detail", ResolveDetailLabel(kind), PluginParameterSection.Shader, "Shader", 1, 16, 0.5, shaderDetail),
            Number("sourceLag", "Emitter Follow", PluginParameterSection.Shader, "Shader", 2, 32, 1, sourceLag),
            Number("idleRadius", "Idle Radius", PluginParameterSection.Shader, "Shader", 0, 24, 0.5, idleRadius),
            Number("idleSpeed", "Idle Speed", PluginParameterSection.Shader, "Shader", 0.1, 6, 0.1, idleSpeed),
            Number("gravityX", "Gravity X", PluginParameterSection.Shader, "Shader", -20, 20, 0.5, gravityX),
            Number("gravityY", "Gravity Y", PluginParameterSection.Shader, "Shader", -20, 20, 0.5, gravityY),
            Number("randomness", "Randomness", PluginParameterSection.Shader, "Shader", 0, 16, 0.25, randomness)
        };

        if (extraParameters is not null)
        {
            parameters.AddRange(extraParameters);
        }

        return new ShaderTemplateDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            IconGlyph = iconGlyph,
            AccentColor = accentColor,
            RuntimeKind = TemplateRuntimeKind.BuiltInTemplate,
            Kind = kind,
            Trigger = TemplateTrigger.FollowCursor,
            Parameters = parameters
        };
    }

    private static string ResolveDetailLabel(TemplateEffectKind kind) => kind switch
    {
        TemplateEffectKind.OrbitTrail => "Dot Size",
        TemplateEffectKind.PrismBloom => "Bloom Layers",
        TemplateEffectKind.ArcSparkle => "Spark Density",
        TemplateEffectKind.CometRibbon => "Tail Width",
        TemplateEffectKind.NebulaDust => "Dust Density",
        TemplateEffectKind.FrostHalo => "Shard Count",
        TemplateEffectKind.SolarFlare => "Flare Count",
        TemplateEffectKind.MysticRunes => "Rune Density",
        TemplateEffectKind.MatrixCascade => "Glyph Streams",
        TemplateEffectKind.CosmicRift => "Fracture Depth",
        TemplateEffectKind.GlitchFracture => "Glitch Count",
        TemplateEffectKind.VelvetFlame => "Flame Layers",
        TemplateEffectKind.SparkShower => "Spark Density",
        TemplateEffectKind.ClickBurst => "Burst Thickness",
        _ => "Ring Thickness"
    };

    private static TemplateParameterDefinition Number(string key, string displayName, PluginParameterSection section, string sectionName, double min, double max, double step, double defaultNumber) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Number,
            Min = min,
            Max = max,
            Step = step,
            DefaultNumber = defaultNumber
        };

    private static TemplateParameterDefinition Color(string key, string displayName, PluginParameterSection section, string sectionName, string defaultColor) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Color,
            DefaultColor = defaultColor
        };

    private static TemplateParameterDefinition Toggle(string key, string displayName, PluginParameterSection section, string sectionName, bool defaultBoolean) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Toggle,
            DefaultBoolean = defaultBoolean
        };
}
