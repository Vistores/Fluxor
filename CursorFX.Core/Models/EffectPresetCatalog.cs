namespace CursorFX.Core.Models;

public static class EffectPresetCatalog
{
    public static IReadOnlyList<EffectPreset> GetPresets()
    {
        return
        [
            new EffectPreset
            {
                Name = "Minimal",
                Settings = new AppSettings
                {
                    General = new GeneralSettings
                    {
                        MasterOpacity = 0.55,
                        TargetFps = 60
                    },
                    Trail = new TrailSettings
                    {
                        IsEnabled = true,
                        MaxPoints = 18,
                        Thickness = 6,
                        FadeSeconds = 0.32,
                        Color = "#E2E8F0"
                    },
                    Glow = new GlowSettings
                    {
                        IsEnabled = true,
                        Size = 18,
                        Opacity = 0.18,
                        Color = "#F8FAFC"
                    },
                    Ripple = new RippleSettings
                    {
                        IsEnabled = true,
                        MaxRadius = 54,
                        LifetimeSeconds = 0.45,
                        Opacity = 0.35,
                        Thickness = 2,
                        Color = "#CBD5E1"
                    },
                    SelectedPreset = "Minimal"
                }
            },
            new EffectPreset
            {
                Name = "Neon",
                Settings = AppSettings.CreateDefault()
            },
            new EffectPreset
            {
                Name = "Gaming",
                Settings = new AppSettings
                {
                    General = new GeneralSettings
                    {
                        MasterOpacity = 0.95,
                        TargetFps = 120
                    },
                    Trail = new TrailSettings
                    {
                        IsEnabled = true,
                        MaxPoints = 48,
                        Thickness = 18,
                        FadeSeconds = 0.75,
                        Color = "#38BDF8"
                    },
                    Glow = new GlowSettings
                    {
                        IsEnabled = true,
                        Size = 42,
                        Opacity = 0.55,
                        Color = "#818CF8"
                    },
                    Ripple = new RippleSettings
                    {
                        IsEnabled = true,
                        MaxRadius = 112,
                        LifetimeSeconds = 0.9,
                        Opacity = 0.9,
                        Thickness = 4,
                        Color = "#C4B5FD"
                    },
                    SelectedPreset = "Gaming"
                }
            }
        ];
    }
}
