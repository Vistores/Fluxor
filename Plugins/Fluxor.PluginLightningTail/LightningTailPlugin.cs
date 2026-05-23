using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginLightningTail;

public sealed class LightningTailPlugin : ICursorEffectPlugin
{
    private const int MaxSamples = 220;
    private const int MaxImpacts = 12;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TailSample> _samples = [];
    private readonly List<Impact> _impacts = [];
    private readonly Random _random = new(47017);

    private Point _cursorPosition;
    private Point _lastSamplePosition;
    private bool _hasPosition;
    private double _timeSeconds;
    private double _masterOpacity = 1.0;

    public string DisplayName => "Lightning Tail";
    public string PluginId => "lightning-tail";
    public string Description => "A cursor-locked lightning tail with sharp forks and a thunder-strike tap impact.";
    public string IconGlyph => "L";
    public string AccentColor => "#67E8F9";
    public TemplateEffectKind Kind => TemplateEffectKind.ArcSparkle;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Lightning Tail", PluginParameterSection.Trail, "Lightning Tail", true),
        ColorParameter("coreColor", "Core Color", PluginParameterSection.Trail, "Lightning Tail", "#F8FAFC"),
        ColorParameter("boltColor", "Bolt Color", PluginParameterSection.Trail, "Lightning Tail", "#67E8F9"),
        ColorParameter("edgeColor", "Edge Glow", PluginParameterSection.Glow, "Glow", "#2563EB"),
        ColorParameter("impactColor", "Impact Color", PluginParameterSection.Ripple, "Tap Impact", "#FACC15"),
        Number("opacity", "Opacity", PluginParameterSection.Trail, "Lightning Tail", 0.05, 1.0, 0.01, 0.92),
        Number("tailLifetime", "Tail Lifetime", PluginParameterSection.Trail, "Lightning Tail", 0.06, 0.9, 0.01, 0.22),
        Number("sampleSpacing", "Point Spacing", PluginParameterSection.Trail, "Lightning Tail", 2, 24, 1, 5),
        Number("thickness", "Bolt Thickness", PluginParameterSection.Trail, "Lightning Tail", 0.6, 8, 0.1, 2.2),
        Number("glowSize", "Glow Size", PluginParameterSection.Glow, "Glow", 1, 22, 0.5, 7),
        Number("jitter", "Sharpness Jitter", PluginParameterSection.Shader, "Lightning Shape", 0, 28, 0.5, 9),
        Number("forks", "Fork Amount", PluginParameterSection.Shader, "Lightning Shape", 0, 8, 1, 3),
        Number("forkLength", "Fork Length", PluginParameterSection.Shader, "Lightning Shape", 4, 56, 1, 18, isAdvanced: true),
        Number("flicker", "Flicker", PluginParameterSection.Shader, "Lightning Shape", 0, 14, 0.25, 5.5, isAdvanced: true),
        Number("impactRadius", "Impact Radius", PluginParameterSection.Ripple, "Tap Impact", 12, 180, 1, 58),
        Number("impactLifetime", "Impact Lifetime", PluginParameterSection.Ripple, "Tap Impact", 0.08, 1.2, 0.01, 0.34),
        Number("impactBolts", "Impact Bolts", PluginParameterSection.Ripple, "Tap Impact", 3, 18, 1, 9, isAdvanced: true)
    ];

    public void ApplyParameters(IReadOnlyDictionary<string, TemplateParameterValue> parameters, double masterOpacity)
    {
        _parameters.Clear();
        foreach (var entry in parameters)
        {
            _parameters[entry.Key] = entry.Value;
        }

        _masterOpacity = masterOpacity;
    }

    public void Update(PluginRenderContext context)
    {
        _cursorPosition = context.CursorPosition;
        AddPosition(context.CursorPosition);
        Update(context.DeltaTime);
    }

    public void Update(TimeSpan deltaTime)
    {
        var dt = Math.Clamp(deltaTime.TotalSeconds, 0.0, 0.05);
        _timeSeconds += dt;
        var tailLifetime = Math.Max(0.06, GetNumber("tailLifetime", 0.22));

        for (var index = _samples.Count - 1; index >= 0; index--)
        {
            var sample = _samples[index];
            sample.Age += dt;
            if (sample.Age > tailLifetime || index >= MaxSamples)
            {
                _samples.RemoveAt(index);
                continue;
            }

            _samples[index] = sample;
        }

        for (var index = _impacts.Count - 1; index >= 0; index--)
        {
            var impact = _impacts[index];
            impact.Age += dt;
            if (impact.Age > impact.Lifetime)
            {
                _impacts.RemoveAt(index);
                continue;
            }

            _impacts[index] = impact;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("enabled", true))
        {
            return;
        }

        var opacity = GetNumber("opacity", 0.92) * _masterOpacity;
        DrawTail(drawingContext, opacity);
        DrawImpacts(drawingContext, opacity);
    }

    public void OnMouseMove(PluginRenderContext context, Point position)
    {
        _cursorPosition = context.CursorPosition;
        AddPosition(context.CursorPosition);
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
        AddPosition(position);
    }

    public void OnMouseClick(PluginRenderContext context, Point position)
    {
        AddImpact(context.CursorPosition);
    }

    public void OnMouseClick(Point position)
    {
        AddImpact(position);
    }

    public void Dispose()
    {
        _samples.Clear();
        _impacts.Clear();
    }

    private void AddPosition(Point position)
    {
        if (!_hasPosition)
        {
            _hasPosition = true;
            _cursorPosition = position;
            _lastSamplePosition = position;
            AddSample(position);
            return;
        }

        var delta = position - _lastSamplePosition;
        var distance = delta.Length;
        var spacing = Math.Max(2.0, GetNumber("sampleSpacing", 5));
        if (distance < spacing)
        {
            _cursorPosition = position;
            return;
        }

        delta.Normalize();
        var steps = Math.Min(48, (int)Math.Floor(distance / spacing));
        for (var i = 1; i <= steps; i++)
        {
            AddSample(_lastSamplePosition + delta * (spacing * i));
        }

        _lastSamplePosition += delta * (spacing * steps);
        _cursorPosition = position;
    }

    private void AddSample(Point position)
    {
        _samples.Insert(0, new TailSample
        {
            Position = position,
            Age = 0,
            Seed = _random.NextDouble() * Math.PI * 2.0
        });

        while (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(_samples.Count - 1);
        }
    }

    private void AddImpact(Point position)
    {
        if (_impacts.Count >= MaxImpacts)
        {
            _impacts.RemoveAt(0);
        }

        _impacts.Add(new Impact
        {
            Position = position,
            Age = 0,
            Lifetime = Math.Max(0.08, GetNumber("impactLifetime", 0.34)),
            Seed = _random.NextDouble() * Math.PI * 2.0
        });

        for (var i = 0; i < 4; i++)
        {
            AddSample(position + RandomVector(2 + i * 1.6));
        }
    }

    private void DrawTail(DrawingContext drawingContext, double opacity)
    {
        if (_samples.Count < 2)
        {
            return;
        }

        var coreColor = GetColor("coreColor", "#F8FAFC");
        var boltColor = GetColor("boltColor", "#67E8F9");
        var edgeColor = GetColor("edgeColor", "#2563EB");
        var thickness = GetNumber("thickness", 2.2);
        var glowSize = GetNumber("glowSize", 7);
        var jitter = GetNumber("jitter", 9);
        var forks = (int)Math.Round(GetNumber("forks", 3));
        var forkLength = GetNumber("forkLength", 18);
        var flicker = GetNumber("flicker", 5.5);
        var tailLifetime = Math.Max(0.06, GetNumber("tailLifetime", 0.22));

        var points = BuildLightningPoints(jitter, flicker);
        for (var i = 0; i < points.Count - 1; i++)
        {
            var age = Math.Min(_samples[Math.Min(i, _samples.Count - 1)].Age, tailLifetime);
            var fade = 1.0 - Math.Clamp(age / tailLifetime, 0.0, 1.0);
            var localAlpha = opacity * fade * (0.72 + Math.Sin(_timeSeconds * 18 + i) * 0.08);
            var localThickness = Math.Max(0.45, thickness * (0.32 + fade * 0.92));

            drawingContext.DrawLine(CreatePen(edgeColor, localThickness + glowSize, localAlpha * 0.16), points[i], points[i + 1]);
            drawingContext.DrawLine(CreatePen(boltColor, localThickness + 1.1, localAlpha * 0.68), points[i], points[i + 1]);
            drawingContext.DrawLine(CreatePen(coreColor, Math.Max(0.45, localThickness * 0.46), localAlpha), points[i], points[i + 1]);

            if (forks > 0 && i % 3 == 1 && fade > 0.18)
            {
                DrawForks(drawingContext, points[i], points[i + 1], boltColor, coreColor, forkLength, forks, localAlpha, localThickness, i);
            }
        }

        drawingContext.DrawEllipse(CreateRadialBrush(boltColor, opacity * 0.28), null, _cursorPosition, glowSize * 1.25, glowSize * 1.25);
        drawingContext.DrawEllipse(CreateBrush(coreColor, opacity * 0.95), null, _cursorPosition, Math.Max(1.5, thickness), Math.Max(1.5, thickness));
    }

    private List<Point> BuildLightningPoints(double jitter, double flicker)
    {
        var points = new List<Point> { _cursorPosition };
        var count = Math.Min(_samples.Count, MaxSamples);

        for (var i = 0; i < count; i++)
        {
            var basePoint = _samples[i].Position;
            var previous = points[^1];
            var direction = basePoint - previous;
            var normal = new Vector(-direction.Y, direction.X);
            if (normal.LengthSquared > 0.001)
            {
                normal.Normalize();
            }

            var flickerOffset = Math.Sin((_timeSeconds * flicker * 3.4) + _samples[i].Seed + i * 0.83) * jitter;
            var sharpOffset = ((i % 2 == 0) ? 1 : -1) * jitter * 0.35;
            points.Add(basePoint + normal * (flickerOffset + sharpOffset));
        }

        return points;
    }

    private void DrawForks(
        DrawingContext drawingContext,
        Point from,
        Point to,
        Color boltColor,
        Color coreColor,
        double forkLength,
        int forkAmount,
        double opacity,
        double thickness,
        int seedOffset)
    {
        var direction = to - from;
        if (direction.LengthSquared < 0.001)
        {
            return;
        }

        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var amount = Math.Min(forkAmount, 3);

        for (var i = 0; i < amount; i++)
        {
            var sign = ((seedOffset + i) % 2 == 0) ? 1 : -1;
            var start = Lerp(from, to, 0.24 + i * 0.18);
            var branchDirection = (direction * (0.22 + i * 0.06)) + normal * sign * (0.95 - i * 0.08);
            if (branchDirection.LengthSquared < 0.001)
            {
                continue;
            }

            branchDirection.Normalize();
            var mid = start + branchDirection * forkLength * (0.45 + i * 0.12);
            var end = mid + (branchDirection + normal * sign * 0.22) * forkLength * (0.28 + i * 0.06);

            drawingContext.DrawLine(CreatePen(boltColor, Math.Max(0.5, thickness * 0.55), opacity * 0.42), start, mid);
            drawingContext.DrawLine(CreatePen(coreColor, Math.Max(0.35, thickness * 0.24), opacity * 0.7), mid, end);
        }
    }

    private void DrawImpacts(DrawingContext drawingContext, double opacity)
    {
        var impactColor = GetColor("impactColor", "#FACC15");
        var boltColor = GetColor("boltColor", "#67E8F9");
        var coreColor = GetColor("coreColor", "#F8FAFC");
        var radius = GetNumber("impactRadius", 58);
        var impactBolts = (int)Math.Round(GetNumber("impactBolts", 9));
        var thickness = GetNumber("thickness", 2.2);

        foreach (var impact in _impacts)
        {
            var progress = Math.Clamp(impact.Age / impact.Lifetime, 0.0, 1.0);
            var fade = 1.0 - progress;
            var currentRadius = radius * (0.18 + progress * 0.92);
            var alpha = opacity * fade;

            drawingContext.DrawEllipse(null, CreatePen(impactColor, thickness + 2.5, alpha * 0.3), impact.Position, currentRadius, currentRadius);
            drawingContext.DrawEllipse(CreateRadialBrush(impactColor, alpha * 0.22), null, impact.Position, currentRadius * 0.72, currentRadius * 0.72);

            for (var i = 0; i < impactBolts; i++)
            {
                var angle = impact.Seed + (Math.PI * 2.0 * i / Math.Max(1, impactBolts)) + Math.Sin(_timeSeconds * 12 + i) * 0.08;
                var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
                var start = impact.Position + dir * currentRadius * 0.1;
                var mid = impact.Position + dir * currentRadius * (0.38 + (i % 3) * 0.07) + new Vector(-dir.Y, dir.X) * Math.Sin(i + impact.Seed) * 9;
                var end = impact.Position + dir * currentRadius * (0.78 + (i % 2) * 0.12);

                drawingContext.DrawLine(CreatePen(boltColor, thickness + 1.2, alpha * 0.5), start, mid);
                drawingContext.DrawLine(CreatePen(coreColor, Math.Max(0.45, thickness * 0.38), alpha), mid, end);
            }
        }
    }

    private double GetNumber(string key, double fallback)
        => _parameters.TryGetValue(key, out var value) && value.NumberValue.HasValue ? value.NumberValue.Value : fallback;

    private bool GetToggle(string key, bool fallback)
        => _parameters.TryGetValue(key, out var value) && value.BooleanValue.HasValue ? value.BooleanValue.Value : fallback;

    private Color GetColor(string key, string fallback)
        => ParseColor(_parameters.TryGetValue(key, out var value) ? value.ColorValue ?? fallback : fallback, fallback);

    private static Point Lerp(Point from, Point to, double amount) =>
        new(from.X + ((to.X - from.X) * amount), from.Y + ((to.Y - from.Y) * amount));

    private Vector RandomVector(double length)
    {
        var angle = _random.NextDouble() * Math.PI * 2.0;
        return new Vector(Math.Cos(angle), Math.Sin(angle)) * length;
    }

    private static TemplateParameterDefinition Number(string key, string name, PluginParameterSection section, string sectionName, double min, double max, double step, double value, bool isAdvanced = false) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Number,
            Min = min,
            Max = max,
            Step = step,
            DefaultNumber = value,
            DefaultColor = "#FFFFFF",
            IsAdvanced = isAdvanced
        };

    private static TemplateParameterDefinition ColorParameter(string key, string name, PluginParameterSection section, string sectionName, string color, bool isAdvanced = false) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Color,
            DefaultColor = color,
            IsAdvanced = isAdvanced
        };

    private static TemplateParameterDefinition Toggle(string key, string name, PluginParameterSection section, string sectionName, bool value, bool isAdvanced = false) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Toggle,
            DefaultBoolean = value,
            DefaultColor = "#FFFFFF",
            IsAdvanced = isAdvanced
        };

    private static Pen CreatePen(Color color, double thickness, double opacity)
    {
        var pen = new Pen(CreateBrush(color, opacity), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush CreateBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private static RadialGradientBrush CreateRadialBrush(Color color, double opacity)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string value, string fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }

    private struct TailSample
    {
        public Point Position { get; set; }
        public double Age { get; set; }
        public double Seed { get; set; }
    }

    private struct Impact
    {
        public Point Position { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public double Seed { get; set; }
    }
}
