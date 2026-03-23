using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class TemplateEffect : IEffect
{
    private readonly List<ClickPulse> _clickPulses = [];
    private ShaderTemplateDefinition? _template;
    private Dictionary<string, TemplateParameterValue> _parameterValues = new(StringComparer.OrdinalIgnoreCase);
    private double _masterOpacity = 1.0;
    private Point _cursorPosition;
    private Point _smoothedCursorPosition;
    private double _timeSeconds;

    public string Name => "Template Shader";

    public bool IsEnabled { get; set; }

    public void Update(TimeSpan deltaTime)
    {
        _timeSeconds += deltaTime.TotalSeconds;

        var followBlend = Math.Clamp(deltaTime.TotalSeconds * 18d, 0d, 1d);
        _smoothedCursorPosition = new Point(
            _smoothedCursorPosition.X + ((_cursorPosition.X - _smoothedCursorPosition.X) * followBlend),
            _smoothedCursorPosition.Y + ((_cursorPosition.Y - _smoothedCursorPosition.Y) * followBlend));

        for (var index = _clickPulses.Count - 1; index >= 0; index--)
        {
            var pulse = _clickPulses[index];
            pulse.Age += deltaTime.TotalSeconds;
            if (pulse.Age >= GetBurstLifetime())
            {
                _clickPulses.RemoveAt(index);
                continue;
            }

            _clickPulses[index] = pulse;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!IsEnabled || _template is null)
        {
            return;
        }

        switch (_template.Kind)
        {
            case TemplateEffectKind.CursorAura:
                RenderCursorAura(drawingContext);
                break;
            case TemplateEffectKind.ClickBurst:
                RenderClickBurst(drawingContext);
                break;
            case TemplateEffectKind.OrbitTrail:
                RenderOrbitTrail(drawingContext);
                break;
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
        if (!IsEnabled || _template?.Trigger != TemplateTrigger.MouseClick)
        {
            return;
        }

        _clickPulses.Add(new ClickPulse(position));
    }

    public void UpdateTemplate(
        ShaderTemplateDefinition? template,
        IReadOnlyDictionary<string, TemplateParameterValue> parameterValues,
        bool isEnabled,
        double masterOpacity)
    {
        _template = template;
        _parameterValues = parameterValues.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        IsEnabled = isEnabled && template is not null;
        _masterOpacity = masterOpacity;
        _clickPulses.Clear();
    }

    private void RenderCursorAura(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 54);
        var opacity = GetNumber("opacity", 0.42) * _masterOpacity;
        var pulse = 1 + (Math.Sin(_timeSeconds * GetNumber("motion", 1.4)) * 0.08);
        var primaryColor = GetColor("primaryColor", "#22D3EE");
        var accentColor = GetColor("accentColor", "#A5F3FC");
        var radius = size * pulse;

        var fillBrush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        fillBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, opacity), 0));
        fillBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, 0), 1));
        fillBrush.Freeze();

        var ringPen = CreatePen(accentColor, GetNumber("detail", 3), opacity * 0.9);
        drawingContext.DrawEllipse(fillBrush, null, _smoothedCursorPosition, radius, radius);
        drawingContext.DrawEllipse(null, ringPen, _smoothedCursorPosition, radius * 0.75, radius * 0.75);
    }

    private void RenderClickBurst(DrawingContext drawingContext)
    {
        var lifetime = GetBurstLifetime();
        var maxRadius = GetNumber("size", 120);
        var opacity = GetNumber("opacity", 0.9) * _masterOpacity;
        var thickness = GetNumber("detail", 4);
        var primaryColor = GetColor("primaryColor", "#C4B5FD");
        var accentColor = GetColor("accentColor", "#FCA5A5");

        foreach (var pulse in _clickPulses)
        {
            var progress = Math.Clamp(pulse.Age / lifetime, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 2);
            var radius = Math.Max(4, maxRadius * eased);
            var alpha = (1 - progress) * opacity;

            drawingContext.DrawEllipse(
                null,
                CreatePen(primaryColor, thickness, alpha),
                pulse.Position,
                radius,
                radius);

            drawingContext.DrawEllipse(
                null,
                CreatePen(accentColor, Math.Max(1, thickness * 0.45), alpha * 0.8),
                pulse.Position,
                radius * 0.55,
                radius * 0.55);
        }
    }

    private void RenderOrbitTrail(DrawingContext drawingContext)
    {
        var radius = GetNumber("size", 22);
        var speed = GetNumber("motion", 2.2);
        var dotSize = GetNumber("detail", 8);
        var opacity = GetNumber("opacity", 0.75) * _masterOpacity;
        var showRing = GetToggle("showRing", true);
        var primaryColor = GetColor("primaryColor", "#F97316");
        var accentColor = GetColor("accentColor", "#FDBA74");

        var angleA = _timeSeconds * speed;
        var angleB = angleA + Math.PI;
        var pointA = new Point(
            _smoothedCursorPosition.X + (Math.Cos(angleA) * radius),
            _smoothedCursorPosition.Y + (Math.Sin(angleA) * radius));
        var pointB = new Point(
            _smoothedCursorPosition.X + (Math.Cos(angleB) * radius),
            _smoothedCursorPosition.Y + (Math.Sin(angleB) * radius));

        if (showRing)
        {
            drawingContext.DrawEllipse(
                null,
                CreatePen(primaryColor, 1.5, opacity * 0.4),
                _smoothedCursorPosition,
                radius,
                radius);
        }

        drawingContext.DrawEllipse(CreateSolidBrush(primaryColor, opacity), null, pointA, dotSize, dotSize);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity), null, pointB, dotSize * 0.8, dotSize * 0.8);
    }

    private double GetNumber(string key, double defaultValue)
    {
        if (_parameterValues.TryGetValue(key, out var value) && value.NumberValue.HasValue)
        {
            return value.NumberValue.Value;
        }

        return _template?.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase))?.DefaultNumber
            ?? defaultValue;
    }

    private double GetBurstLifetime()
    {
        return GetNumber("motion", 0.85);
    }

    private bool GetToggle(string key, bool defaultValue)
    {
        if (_parameterValues.TryGetValue(key, out var value) && value.BooleanValue.HasValue)
        {
            return value.BooleanValue.Value;
        }

        return _template?.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase))?.DefaultBoolean
            ?? defaultValue;
    }

    private Color GetColor(string key, string defaultValue)
    {
        var colorValue = _parameterValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.ColorValue)
            ? value.ColorValue!
            : _template?.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase))?.DefaultColor
                ?? defaultValue;

        return (Color)ColorConverter.ConvertFromString(colorValue);
    }

    private static Pen CreatePen(Color color, double thickness, double opacity)
    {
        var pen = new Pen(CreateSolidBrush(color, opacity), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush CreateSolidBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(WithAlpha(color, opacity));
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color color, double opacity)
    {
        return Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B);
    }

    private struct ClickPulse(Point position)
    {
        public Point Position { get; } = position;

        public double Age { get; set; }
    }
}
