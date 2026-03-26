using System.Text.Json.Serialization;

namespace CursorFX.Core.Models;

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();

    public TrailSettings Trail { get; set; } = new();

    public GlowSettings Glow { get; set; } = new();

    public RippleSettings Ripple { get; set; } = new();

    public TemplateEffectSettings TemplateEffect { get; set; } = new();

    public LocalizationSettings Localization { get; set; } = new();

    public string SelectedPreset { get; set; } = "Neon";

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            General = new GeneralSettings
            {
                MasterOpacity = 0.85,
                TargetFps = 60,
                CursorAttachStrength = 2.0,
                LaunchOnStartup = false,
                RunInBackground = true,
                PauseWhenCursorHidden = true
            },
            Trail = new TrailSettings
            {
                IsEnabled = true,
                MaxPoints = 32,
                Thickness = 12,
                FadeSeconds = 0.55,
                Color = "#22D3EE"
            },
            Glow = new GlowSettings
            {
                IsEnabled = true,
                Size = 32,
                Opacity = 0.42,
                Color = "#67E8F9"
            },
            Ripple = new RippleSettings
            {
                IsEnabled = true,
                MaxRadius = 86,
                LifetimeSeconds = 0.7,
                Opacity = 0.75,
                Thickness = 3,
                Color = "#A5F3FC"
            },
            TemplateEffect = new TemplateEffectSettings
            {
                IsEnabled = false,
                SelectedTemplateId = "neon-suite"
            },
            Localization = new LocalizationSettings
            {
                UseSystemLanguage = true,
                LanguageCode = "en"
            },
            SelectedPreset = "Neon"
        };
    }
}

public sealed class LocalizationSettings
{
    public bool UseSystemLanguage { get; set; } = true;

    public string LanguageCode { get; set; } = "en";
}

public sealed class GeneralSettings
{
    public double MasterOpacity { get; set; } = 0.85;

    public int TargetFps { get; set; } = 60;

    public double CursorAttachStrength { get; set; } = 2.0;

    public bool LaunchOnStartup { get; set; }

    public bool RunInBackground { get; set; }

    public bool PauseWhenCursorHidden { get; set; } = true;
}

public sealed class TrailSettings
{
    public bool IsEnabled { get; set; } = true;

    public int MaxPoints { get; set; } = 32;

    public double Thickness { get; set; } = 12;

    public double FadeSeconds { get; set; } = 0.55;

    public string Color { get; set; } = "#22D3EE";

    public TrailRenderMode RenderMode { get; set; } = TrailRenderMode.SmoothLine;

    public double WaveAmplitude { get; set; } = 0;

    public double WaveFrequency { get; set; } = 1.2;

    public double NoiseAmount { get; set; } = 0;

    public double RibbonSoftness { get; set; } = 0.45;
}

public enum TrailRenderMode
{
    SmoothLine,
    WaveRibbon,
    TornRibbon
}

public sealed class GlowSettings
{
    public bool IsEnabled { get; set; } = true;

    public double Size { get; set; } = 32;

    public double Opacity { get; set; } = 0.42;

    public string Color { get; set; } = "#67E8F9";
}

public sealed class RippleSettings
{
    public bool IsEnabled { get; set; } = true;

    public double MaxRadius { get; set; } = 86;

    public double LifetimeSeconds { get; set; } = 0.7;

    public double Opacity { get; set; } = 0.75;

    public double Thickness { get; set; } = 3;

    public string Color { get; set; } = "#A5F3FC";
}

public sealed class TemplateEffectSettings
{
    public bool IsEnabled { get; set; }

    public string SelectedTemplateId { get; set; } = "neon-suite";

    public Dictionary<string, Dictionary<string, TemplateParameterValue>> PluginParameterValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("ParameterValues")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TemplateParameterValue>? LegacyParameterValues
    {
        get => null;
        set
        {
            if (value is null || value.Count == 0)
            {
                return;
            }

            var selectedTemplateId = string.IsNullOrWhiteSpace(SelectedTemplateId)
                ? "neon-suite"
                : SelectedTemplateId;

            PluginParameterValues[selectedTemplateId] = new Dictionary<string, TemplateParameterValue>(value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
