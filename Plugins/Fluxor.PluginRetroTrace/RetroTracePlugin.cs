using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginRetroTrace;

public sealed class RetroTracePlugin : ICursorEffectPlugin
{
    private const int MaxImprints = 56;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Imprint> _imprints = [];
    private readonly Random _random = new(91337);

    private Point _cursorPosition;
    private Point _lastSpawnPoint;
    private bool _hasSpawnPoint;
    private double _timeSeconds;
    private double _masterOpacity = 1.0;

    public string DisplayName => "Retro Trace";
    public string PluginId => "retro-trace";
    public string Description => "Leaves behind noisy retro monitor afterimages instead of a stretched cursor trail.";
    public string IconGlyph => "R";
    public string AccentColor => "#8CF7FF";
    public TemplateEffectKind Kind => TemplateEffectKind.MatrixCascade;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Retro Trace", PluginParameterSection.Trail, "Retro Trace", true),
        ColorParameter("traceColor", "Trace Color", PluginParameterSection.Trail, "Retro Trace", "#8CF7FF"),
        ColorParameter("ghostColor", "Ghost Color", PluginParameterSection.Trail, "Retro Trace", "#D8F6FF"),
        ColorParameter("noiseColor", "Noise Color", PluginParameterSection.Shader, "CRT Noise", "#5EF2C3"),
        Number("opacity", "Opacity", PluginParameterSection.Trail, "Retro Trace", 0.05, 1.0, 0.01, 0.82),
        Number("spacing", "Imprint Spacing", PluginParameterSection.Trail, "Retro Trace", 4, 42, 1, 13),
        Number("lifetime", "Afterimage Lifetime", PluginParameterSection.Trail, "Retro Trace", 0.08, 1.8, 0.01, 0.42),
        Number("size", "Imprint Size", PluginParameterSection.Trail, "Retro Trace", 4, 40, 0.5, 12),
        Number("bloom", "Bloom Size", PluginParameterSection.Glow, "CRT Noise", 2, 36, 0.5, 7),
        Number("noiseDensity", "Noise Density", PluginParameterSection.Shader, "CRT Noise", 4, 36, 1, 13),
        Number("scanlines", "Scanline Count", PluginParameterSection.Shader, "CRT Noise", 2, 12, 1, 5, isAdvanced: true),
        Number("jitter", "Noise Jitter", PluginParameterSection.Shader, "CRT Noise", 0, 12, 0.25, 2.2, isAdvanced: true),
        Number("drift", "Ghost Drift", PluginParameterSection.Trail, "Motion", 0, 30, 0.5, 5.5, isAdvanced: true),
        Number("flicker", "Flicker", PluginParameterSection.Shader, "CRT Noise", 0, 8, 0.1, 2.8, isAdvanced: true)
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

    public void Update(TimeSpan deltaTime)
    {
        var dt = Math.Clamp(deltaTime.TotalSeconds, 0.0, 0.05);
        _timeSeconds += dt;

        var drift = GetNumber("drift", 5.5);
        for (var index = _imprints.Count - 1; index >= 0; index--)
        {
            var imprint = _imprints[index];
            imprint.Age += dt;
            if (imprint.Age >= imprint.Lifetime)
            {
                _imprints.RemoveAt(index);
                continue;
            }

            imprint.Position += imprint.Drift * drift * dt;
            _imprints[index] = imprint;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("enabled", true))
        {
            return;
        }

        var traceColor = GetColor("traceColor", "#8CF7FF");
        var ghostColor = GetColor("ghostColor", "#D8F6FF");
        var noiseColor = GetColor("noiseColor", "#5EF2C3");
        var opacity = GetNumber("opacity", 0.82) * _masterOpacity;
        var bloom = GetNumber("bloom", 7);
        var noiseDensity = (int)Math.Round(GetNumber("noiseDensity", 13));
        var scanlineCount = (int)Math.Round(GetNumber("scanlines", 5));
        var jitter = GetNumber("jitter", 2.2);
        var flicker = GetNumber("flicker", 2.8);

        foreach (var imprint in _imprints)
        {
            var life = Math.Clamp(imprint.Age / imprint.Lifetime, 0.0, 1.0);
            var fade = 1.0 - life;
            var pulse = 0.78 + Math.Sin((_timeSeconds * flicker * 3.1) + imprint.Seed) * 0.12;
            var alpha = opacity * fade * pulse;
            var stretch = 1.0 + life * 0.35;
            var width = imprint.Size * stretch;
            var height = imprint.Size * (0.66 + life * 0.18);

            drawingContext.DrawEllipse(CreateBrush(traceColor, alpha * 0.22), null, imprint.Position, width + bloom, height + bloom * 0.8);
            DrawScanlineBody(drawingContext, imprint, ghostColor, noiseColor, alpha, width, height, scanlineCount, noiseDensity, jitter);
        }
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;

        if (!_hasSpawnPoint)
        {
            _lastSpawnPoint = position;
            _hasSpawnPoint = true;
            SpawnImprint(position, new Vector(0, -1));
            return;
        }

        var segment = position - _lastSpawnPoint;
        var distance = segment.Length;
        var spacing = Math.Max(4.0, GetNumber("spacing", 13));
        if (distance < spacing)
        {
            return;
        }

        var direction = segment;
        direction.Normalize();
        var steps = (int)Math.Floor(distance / spacing);
        for (var i = 1; i <= steps; i++)
        {
            var sample = _lastSpawnPoint + direction * (spacing * i);
            SpawnImprint(sample, direction);
        }

        _lastSpawnPoint += direction * (spacing * steps);
    }

    public void OnMouseClick(Point position)
    {
        for (var i = 0; i < 3; i++)
        {
            SpawnImprint(position + new Vector((_random.NextDouble() - 0.5) * 6, (_random.NextDouble() - 0.5) * 6), new Vector(0, -1));
        }
    }

    public void Dispose()
    {
        _imprints.Clear();
    }

    private void SpawnImprint(Point position, Vector direction)
    {
        if (_imprints.Count >= MaxImprints)
        {
            _imprints.RemoveAt(0);
        }

        if (direction.LengthSquared < 0.001)
        {
            direction = new Vector(0, -1);
        }

        direction.Normalize();
        var tangent = new Vector(-direction.Y, direction.X);
        var drift = tangent * ((_random.NextDouble() - 0.5) * 0.65) + direction * (_random.NextDouble() * 0.16);

        _imprints.Add(new Imprint
        {
            Position = position,
            Age = 0,
            Lifetime = Math.Max(0.08, GetNumber("lifetime", 0.42)) * (0.82 + _random.NextDouble() * 0.42),
            Size = GetNumber("size", 12) * (0.88 + _random.NextDouble() * 0.28),
            Drift = drift,
            Seed = _random.NextDouble() * Math.PI * 2.0
        });
    }

    private void DrawScanlineBody(
        DrawingContext drawingContext,
        Imprint imprint,
        Color ghostColor,
        Color noiseColor,
        double opacity,
        double width,
        double height,
        int scanlineCount,
        int noiseDensity,
        double jitter)
    {
        var halfWidth = width * 0.5;
        var halfHeight = height * 0.5;
        var lineSpacing = height / Math.Max(1, scanlineCount);

        for (var line = 0; line < scanlineCount; line++)
        {
            var normalized = scanlineCount == 1 ? 0.5 : line / (double)(scanlineCount - 1);
            var y = imprint.Position.Y - halfHeight + normalized * height;
            var lineInset = Math.Abs(normalized - 0.5) * width * 0.24;
            var x1 = imprint.Position.X - halfWidth + lineInset;
            var x2 = imprint.Position.X + halfWidth - lineInset;
            var wobble = Math.Sin((imprint.Seed * 0.7) + line * 1.37 + _timeSeconds * 4.2) * jitter;

            drawingContext.DrawLine(
                CreatePen(ghostColor, Math.Max(1.0, lineSpacing * 0.46), opacity * (0.46 - Math.Abs(normalized - 0.5) * 0.12)),
                new Point(x1 + wobble, y),
                new Point(x2 + wobble, y));
        }

        for (var i = 0; i < noiseDensity; i++)
        {
            var nx = imprint.Position.X + (_random.NextDouble() - 0.5) * width * 1.1;
            var ny = imprint.Position.Y + (_random.NextDouble() - 0.5) * height * 1.2;
            var nw = 0.8 + _random.NextDouble() * 2.2;
            var nh = 0.6 + _random.NextDouble() * 1.6;
            var pulse = 0.45 + Math.Sin(_timeSeconds * 9 + imprint.Seed + i * 0.33) * 0.25;
            drawingContext.DrawRectangle(
                CreateBrush(noiseColor, opacity * Math.Max(0.08, pulse) * 0.38),
                null,
                new Rect(nx, ny, nw, nh));
        }
    }

    private double GetNumber(string key, double fallback)
        => _parameters.TryGetValue(key, out var value) && value.NumberValue.HasValue ? value.NumberValue.Value : fallback;

    private bool GetToggle(string key, bool fallback)
        => _parameters.TryGetValue(key, out var value) && value.BooleanValue.HasValue ? value.BooleanValue.Value : fallback;

    private Color GetColor(string key, string fallback)
        => ParseColor(_parameters.TryGetValue(key, out var value) ? value.ColorValue ?? fallback : fallback, fallback);

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

    private struct Imprint
    {
        public Point Position { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public double Size { get; set; }
        public Vector Drift { get; set; }
        public double Seed { get; set; }
    }
}
