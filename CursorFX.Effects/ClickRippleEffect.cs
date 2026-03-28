using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class ClickRippleEffect : IEffect
{
    private readonly List<RippleInstance> _ripples = [];
    private RippleSettings _settings;
    private double _masterOpacity = 1.0;
    private Color _baseColor;
    private EffectQualityPreset _qualityPreset = EffectQualityPreset.Balanced;

    public ClickRippleEffect(RippleSettings settings)
    {
        _settings = Clone(settings);
        IsEnabled = settings.IsEnabled;
    }

    public string Name => "Click Ripple";

    public bool IsEnabled { get; set; }

    public void Update(TimeSpan deltaTime)
    {
        var elapsed = deltaTime.TotalSeconds;
        for (var index = _ripples.Count - 1; index >= 0; index--)
        {
            var ripple = _ripples[index];
            ripple.AgeSeconds += elapsed;
            if (ripple.AgeSeconds >= _settings.LifetimeSeconds)
            {
                _ripples.RemoveAt(index);
                continue;
            }

            _ripples[index] = ripple;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!IsEnabled || _ripples.Count == 0)
        {
            return;
        }

        foreach (var ripple in _ripples)
        {
            var progress = ripple.AgeSeconds / _settings.LifetimeSeconds;
            var opacity = Math.Clamp((1d - progress) * _settings.Opacity * _masterOpacity, 0, 1);
            var radius = Math.Max(4, _settings.MaxRadius * EaseOut(progress));
            var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), _baseColor.R, _baseColor.G, _baseColor.B));
            brush.Freeze();
            var pen = new Pen(brush, Math.Max(1, _settings.Thickness * (1d - (progress * 0.5))));
            pen.Freeze();

            drawingContext.DrawEllipse(null, pen, ripple.Position, radius, radius);
        }
    }

    public void OnMouseMove(Point position)
    {
    }

    public void OnMouseClick(Point position)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (_ripples.Count >= GetRippleCapacity())
        {
            _ripples.RemoveAt(0);
        }

        _ripples.Add(new RippleInstance(position));
    }

    public void UpdateSettings(RippleSettings settings, double masterOpacity, EffectQualityPreset qualityPreset = EffectQualityPreset.Balanced)
    {
        _settings = Clone(settings);
        _masterOpacity = masterOpacity;
        _qualityPreset = qualityPreset;
        IsEnabled = settings.IsEnabled;
        _baseColor = ParseColor(_settings.Color);
    }

    private int GetRippleCapacity()
    {
        return _qualityPreset switch
        {
            EffectQualityPreset.Low => 6,
            EffectQualityPreset.Balanced => 12,
            EffectQualityPreset.High => 18,
            _ => 12
        };
    }

    private static double EaseOut(double progress)
    {
        var inverse = 1d - progress;
        return 1d - (inverse * inverse);
    }

    private static RippleSettings Clone(RippleSettings settings)
    {
        return new RippleSettings
        {
            IsEnabled = settings.IsEnabled,
            MaxRadius = settings.MaxRadius,
            LifetimeSeconds = settings.LifetimeSeconds,
            Opacity = settings.Opacity,
            Thickness = settings.Thickness,
            Color = settings.Color
        };
    }

    private static Color ParseColor(string value)
    {
        return (Color)ColorConverter.ConvertFromString(value);
    }

    private struct RippleInstance(Point position)
    {
        public Point Position { get; } = position;

        public double AgeSeconds { get; set; }
    }
}
