using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.PluginTemplate;

public sealed class LightningStormPlugin : ICursorEffectPlugin
{
    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ImpactPulse> _impactPulses = [];
    private Point _cursorPosition;
    private Point _smoothedCursorPosition;
    private double _timeSeconds;
    private double _masterOpacity = 1.0;

    public string DisplayName => "Lightning Storm Plugin";

    public void Dispose()
    {
        _impactPulses.Clear();
    }

    public void ApplyParameters(IReadOnlyDictionary<string, TemplateParameterValue> parameters, double masterOpacity)
    {
        _parameters.Clear();
        foreach (var entry in parameters)
        {
            _parameters[entry.Key] = entry.Value;
        }

        _masterOpacity = masterOpacity;
    }

    public void Update(TimeSpan deltaTime)
    {
        _timeSeconds += deltaTime.TotalSeconds;

        var blend = Math.Clamp(deltaTime.TotalSeconds * 18d, 0d, 1d);
        _smoothedCursorPosition = new Point(
            _smoothedCursorPosition.X + ((_cursorPosition.X - _smoothedCursorPosition.X) * blend),
            _smoothedCursorPosition.Y + ((_cursorPosition.Y - _smoothedCursorPosition.Y) * blend));

        var lifetime = GetNumber("boltLifetime", 0.35);
        for (var index = _impactPulses.Count - 1; index >= 0; index--)
        {
            var pulse = _impactPulses[index];
            pulse.Age += deltaTime.TotalSeconds;
            if (pulse.Age >= lifetime)
            {
                _impactPulses.RemoveAt(index);
                continue;
            }

            _impactPulses[index] = pulse;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("customShaderEnabled", true))
        {
            return;
        }

        var primaryColor = GetColor("customPrimaryColor", "#7DD3FC");
        var accentColor = GetColor("customAccentColor", "#FFFFFF");
        var arcCount = Math.Max(1, (int)Math.Round(GetNumber("arcCount", 4)));
        var orbitRadius = GetNumber("coreRadius", 28);
        var arcLength = GetNumber("arcLength", 34);
        var jitter = GetNumber("jitter", 9);
        var thickness = GetNumber("arcThickness", 2.2);
        var speed = GetNumber("animationSpeed", 2.8);
        var baseOpacity = GetNumber("customOpacity", 0.8) * _masterOpacity;

        var haloBrush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        haloBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, baseOpacity * 0.45), 0));
        haloBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, 0), 1));
        haloBrush.Freeze();

        drawingContext.DrawEllipse(haloBrush, null, _smoothedCursorPosition, orbitRadius, orbitRadius);

        for (var index = 0; index < arcCount; index++)
        {
            var angle = ((_timeSeconds * speed) + (index * (Math.PI * 2 / arcCount))) % (Math.PI * 2);
            var start = new Point(
                _smoothedCursorPosition.X + (Math.Cos(angle) * orbitRadius * 0.25),
                _smoothedCursorPosition.Y + (Math.Sin(angle) * orbitRadius * 0.25));
            var end = new Point(
                _smoothedCursorPosition.X + (Math.Cos(angle) * (orbitRadius + arcLength)),
                _smoothedCursorPosition.Y + (Math.Sin(angle) * (orbitRadius + arcLength)));

            var geometry = BuildArcGeometry(start, end, jitter, index);
            drawingContext.DrawGeometry(null, CreatePen(primaryColor, thickness, baseOpacity), geometry);
            drawingContext.DrawGeometry(null, CreatePen(accentColor, Math.Max(1, thickness * 0.45), baseOpacity * 0.9), geometry);
        }

        foreach (var pulse in _impactPulses)
        {
            var progress = Math.Clamp(pulse.Age / GetNumber("boltLifetime", 0.35), 0, 1);
            var radius = 8 + (GetNumber("impactRadius", 80) * progress);
            var opacity = (1 - progress) * baseOpacity;

            drawingContext.DrawEllipse(null, CreatePen(primaryColor, thickness + 1, opacity), pulse.Position, radius, radius);
            drawingContext.DrawEllipse(null, CreatePen(accentColor, thickness * 0.6, opacity), pulse.Position, radius * 0.55, radius * 0.55);
        }
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
        if (_smoothedCursorPosition == default)
        {
            _smoothedCursorPosition = position;
        }
    }

    public void OnMouseClick(Point position)
    {
        _impactPulses.Add(new ImpactPulse(position));
    }

    private StreamGeometry BuildArcGeometry(Point start, Point end, double jitter, int seed)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(start, isFilled: false, isClosed: false);

        var segments = 6;
        for (var index = 1; index <= segments; index++)
        {
            var progress = index / (double)segments;
            var point = new Point(
                start.X + ((end.X - start.X) * progress),
                start.Y + ((end.Y - start.Y) * progress));

            if (index < segments)
            {
                var phase = (_timeSeconds * GetNumber("animationSpeed", 2.8) * 3) + (seed * 1.7) + (index * 0.85);
                point = new Point(
                    point.X + (Math.Sin(phase) * jitter),
                    point.Y + (Math.Cos(phase * 0.8) * jitter));
            }

            context.LineTo(point, isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        return geometry;
    }

    private double GetNumber(string key, double fallback)
    {
        return _parameters.TryGetValue(key, out var value) && value.NumberValue.HasValue
            ? value.NumberValue.Value
            : fallback;
    }

    private bool GetToggle(string key, bool fallback)
    {
        return _parameters.TryGetValue(key, out var value) && value.BooleanValue.HasValue
            ? value.BooleanValue.Value
            : fallback;
    }

    private Color GetColor(string key, string fallback)
    {
        var value = _parameters.TryGetValue(key, out var parameter) && !string.IsNullOrWhiteSpace(parameter.ColorValue)
            ? parameter.ColorValue!
            : fallback;
        return (Color)ColorConverter.ConvertFromString(value);
    }

    private static Pen CreatePen(Color color, double thickness, double opacity)
    {
        var pen = new Pen(new SolidColorBrush(WithAlpha(color, opacity)), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Brush.Freeze();
        pen.Freeze();
        return pen;
    }

    private static Color WithAlpha(Color color, double opacity)
    {
        return Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B);
    }

    private struct ImpactPulse(Point position)
    {
        public Point Position { get; } = position;

        public double Age { get; set; }
    }
}
