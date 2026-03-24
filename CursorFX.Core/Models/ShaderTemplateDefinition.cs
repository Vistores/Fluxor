using System.Text.Json.Serialization;

namespace CursorFX.Core.Models;

public sealed class ShaderTemplateDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string IconGlyph { get; init; } = string.Empty;

    public string IconPath { get; init; } = string.Empty;

    public string AccentColor { get; init; } = "#4FD1C5";

    public string ResolvedIconPath { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTime DateAddedUtc { get; set; }

    public TemplateRuntimeKind RuntimeKind { get; init; } = TemplateRuntimeKind.BuiltInTemplate;

    public string AssemblyFileName { get; init; } = string.Empty;

    public string EntryTypeName { get; init; } = string.Empty;

    public TemplateEffectKind Kind { get; init; }

    public TemplateTrigger Trigger { get; init; }

    public List<TemplateParameterDefinition> Parameters { get; init; } = [];
}

public sealed class TemplateParameterDefinition
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public required PluginParameterSection Section { get; init; }

    public string SectionName { get; init; } = string.Empty;

    public TemplateParameterType Type { get; init; }

    public double Min { get; init; }

    public double Max { get; init; }

    public double Step { get; init; } = 1;

    public double DefaultNumber { get; init; }

    public string DefaultColor { get; init; } = "#FFFFFF";

    public bool DefaultBoolean { get; init; }
}

public sealed class TemplateParameterValue
{
    public double? NumberValue { get; set; }

    public string? ColorValue { get; set; }

    public bool? BooleanValue { get; set; }
}

public enum PluginParameterSection
{
    Trail,
    Glow,
    Ripple,
    Shader
}

public enum TemplateEffectKind
{
    CursorAura,
    ClickBurst,
    OrbitTrail,
    PrismBloom,
    ArcSparkle,
    CometRibbon,
    NebulaDust,
    FrostHalo,
    SolarFlare,
    MysticRunes,
    MatrixCascade,
    CosmicRift,
    GlitchFracture,
    VelvetFlame,
    SparkShower,
    IrregularCrossTap,
    CriticalSpikes
}

public enum TemplateRuntimeKind
{
    BuiltInTemplate,
    ExternalAssembly
}

public enum TemplateTrigger
{
    FollowCursor,
    MouseClick
}

public enum TemplateParameterType
{
    Number,
    Color,
    Toggle
}
