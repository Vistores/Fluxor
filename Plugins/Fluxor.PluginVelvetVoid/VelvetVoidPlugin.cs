using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginVelvetVoid;

public sealed class VelvetVoidPlugin : ICursorEffectPlugin
{
    private const int MaxNodes = 80;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<VoidNode> _nodes = [];
    private Point _cursorPosition;
    private Point _smoothedCursor;
    private Point _emitter;
    private double _timeSeconds;
    private double _masterOpacity = 1.0;

    public string DisplayName => "Velvet Void";
    public string PluginId => "velvet-void";
    public string Description => "A heavy black void tear with layered violet depth that heals slowly behind the cursor.";
    public string IconGlyph => "V";
    public string AccentColor => "#7C3AED";
    public TemplateEffectKind Kind => TemplateEffectKind.CosmicRift;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Void", PluginParameterSection.Shader, "Void", true),
        ColorParameter("voidColor", "Void Color", PluginParameterSection.Shader, "Void", "#05050A"),
        ColorParameter("coreColor", "Core Glow", PluginParameterSection.Shader, "Void", "#4C1D95"),
        ColorParameter("starColor", "Star Dust", PluginParameterSection.Shader, "Void", "#DDD6FE"),
        Number("opacity", "Opacity", PluginParameterSection.Shader, "Void", 0.05, 1.0, 0.01, 0.84),
        Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Motion", 2, 28, 1, 10),
        Number("follow", "Emitter Follow", PluginParameterSection.Shader, "Motion", 2, 28, 1, 8),
        Number("spacing", "Trail Spacing", PluginParameterSection.Shader, "Trail", 4, 40, 1, 14),
        Number("lifetime", "Healing Time", PluginParameterSection.Shader, "Trail", 0.4, 3.5, 0.05, 1.6),
        Number("width", "Void Width", PluginParameterSection.Shader, "Void", 12, 150, 1, 44),
        Number("wave", "Wave Amount", PluginParameterSection.Shader, "Void", 0, 20, 0.5, 5.5),
        Number("layers", "Core Layers", PluginParameterSection.Shader, "Void", 1, 6, 1, 3),
        Number("stars", "Dust Density", PluginParameterSection.Shader, "Void", 0, 16, 1, 5)
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

        var inertia = Math.Max(2.0, GetNumber("inertia", 10));
        _smoothedCursor = Lerp(_smoothedCursor == default ? _cursorPosition : _smoothedCursor, _cursorPosition, Math.Clamp(dt * inertia, 0d, 1d));

        var follow = Math.Max(2.0, GetNumber("follow", 8));
        _emitter = Lerp(_emitter == default ? _cursorPosition : _emitter, _smoothedCursor, Math.Clamp(dt * follow, 0d, 1d));

        UpdateNodes(dt);
        SpawnNode();
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("enabled", true) || _nodes.Count < 3)
        {
            return;
        }

        var opacity = GetNumber("opacity", 0.84) * _masterOpacity;
        var width = GetNumber("width", 44);
        var wave = GetNumber("wave", 5.5);
        var baseColor = GetColor("voidColor", "#05050A");
        var coreColor = GetColor("coreColor", "#4C1D95");
        var starColor = GetColor("starColor", "#DDD6FE");
        var points = _nodes.Select(node => node.Position).ToList();

        var outer = BuildRibbon(points, (index, t) =>
        {
            var life = 1.0 - (_nodes[index].Age / _nodes[index].Lifetime);
            return width * (0.55 + life * 0.12) * (0.88 + Math.Sin(t * Math.PI) * 0.12);
        },
        (index, t) => Math.Sin((_nodes[index].Seed * 0.7) + (_timeSeconds * 0.55) + (t * 3.6)) * wave);

        if (outer is null)
        {
            return;
        }

        drawingContext.DrawGeometry(CreateBrush(baseColor, opacity * 0.96), null, outer);

        var layerCount = Math.Clamp((int)Math.Round(GetNumber("layers", 3)), 1, 6);
        for (var layer = 0; layer < layerCount; layer++)
        {
            var layerScale = 0.42 - layer * 0.08;
            if (layerScale <= 0.08)
            {
                break;
            }

            var inner = BuildRibbon(points, (index, _) =>
            {
                var life = 1.0 - (_nodes[index].Age / _nodes[index].Lifetime);
                return width * (layerScale + life * 0.04);
            },
            (index, t) => Math.Sin((_nodes[index].Seed * (0.58 + layer * 0.1)) + (_timeSeconds * (0.38 + layer * 0.07)) + (t * (2.6 + layer * 0.5))) * wave * (0.22 + layerScale));

            if (inner is null)
            {
                continue;
            }

            drawingContext.DrawGeometry(CreateBrush(coreColor, opacity * (0.15 - layer * 0.02)), null, inner);
        }

        var dustCount = Math.Clamp((int)Math.Round(GetNumber("stars", 5)), 0, 16);
        for (var index = 0; index < dustCount && _nodes.Count > 0; index++)
        {
            var nodeIndex = (index * (_nodes.Count - 1)) / Math.Max(1, dustCount - 1);
            var node = _nodes[nodeIndex];
            var drift = new Vector(HashSigned(node.Seed, index + 3) * width * 0.18, HashSigned(node.Seed, index + 17) * width * 0.12);
            var point = node.Position + drift;
            drawingContext.DrawEllipse(CreateBrush(starColor, opacity * 0.18), null, point, 1.2, 1.2);
        }
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
        if (_smoothedCursor == default)
        {
            _smoothedCursor = position;
            _emitter = position;
        }
    }

    public void OnMouseClick(Point position)
    {
        _nodes.Add(new VoidNode
        {
            Position = position,
            Age = 0,
            Lifetime = Math.Max(0.4, GetNumber("lifetime", 1.6)) * 0.7,
            Seed = _timeSeconds + 13.0
        });
        TrimNodes();
    }

    public void Dispose() => _nodes.Clear();

    private void SpawnNode()
    {
        if (_emitter == default)
        {
            return;
        }

        var spacing = Math.Max(4.0, GetNumber("spacing", 14));
        if (_nodes.Count > 0 && (_emitter - _nodes[^1].Position).Length < spacing)
        {
            return;
        }

        _nodes.Add(new VoidNode
        {
            Position = _emitter,
            Age = 0,
            Lifetime = Math.Max(0.4, GetNumber("lifetime", 1.6)),
            Seed = _timeSeconds + _nodes.Count * 0.61
        });
        TrimNodes();
    }

    private void UpdateNodes(double dt)
    {
        for (var index = _nodes.Count - 1; index >= 0; index--)
        {
            var node = _nodes[index];
            node.Age += dt;
            if (node.Age >= node.Lifetime)
            {
                _nodes.RemoveAt(index);
                continue;
            }

            _nodes[index] = node;
        }
    }

    private void TrimNodes()
    {
        while (_nodes.Count > MaxNodes)
        {
            _nodes.RemoveAt(0);
        }
    }

    private static StreamGeometry? BuildRibbon(IReadOnlyList<Point> points, Func<int, double, double> widthSelector, Func<int, double, double> offsetSelector)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var left = new List<Point>(points.Count);
        var right = new List<Point>(points.Count);

        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index];
            var previous = points[Math.Max(0, index - 1)];
            var next = points[Math.Min(points.Count - 1, index + 1)];
            var tangent = next - previous;
            if (tangent.LengthSquared < 0.001)
            {
                tangent = new Vector(0, -1);
            }

            tangent.Normalize();
            var normal = new Vector(-tangent.Y, tangent.X);
            var t = points.Count == 1 ? 0.0 : (double)index / (points.Count - 1);
            var width = Math.Max(1.0, widthSelector(index, t));
            var offset = offsetSelector(index, t);
            var center = current + normal * offset;
            left.Add(center + normal * width);
            right.Add(center - normal * width);
        }

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(left[0], true, true);
        for (var i = 1; i < left.Count; i++)
        {
            context.LineTo(left[i], true, false);
        }

        for (var i = right.Count - 1; i >= 0; i--)
        {
            context.LineTo(right[i], true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point Lerp(Point from, Point to, double amount) =>
        new(from.X + ((to.X - from.X) * amount), from.Y + ((to.Y - from.Y) * amount));

    private static double HashSigned(double seed, int offset)
    {
        var value = Math.Sin(seed * 12.9898 + offset * 78.233) * 43758.5453;
        return (value - Math.Floor(value)) * 2.0 - 1.0;
    }

    private double GetNumber(string key, double fallback)
        => _parameters.TryGetValue(key, out var value) && value.NumberValue.HasValue ? value.NumberValue.Value : fallback;

    private bool GetToggle(string key, bool fallback)
        => _parameters.TryGetValue(key, out var value) && value.BooleanValue.HasValue ? value.BooleanValue.Value : fallback;

    private Color GetColor(string key, string fallback)
        => ParseColor(_parameters.TryGetValue(key, out var value) ? value.ColorValue ?? fallback : fallback, fallback);

    private static SolidColorBrush CreateBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private static TemplateParameterDefinition Number(string key, string name, PluginParameterSection section, string sectionName, double min, double max, double step, double value) =>
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
            DefaultColor = "#FFFFFF"
        };

    private static TemplateParameterDefinition ColorParameter(string key, string name, PluginParameterSection section, string sectionName, string color) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Color,
            DefaultColor = color
        };

    private static TemplateParameterDefinition Toggle(string key, string name, PluginParameterSection section, string sectionName, bool value) =>
        new()
        {
            Key = key,
            DisplayName = name,
            Section = section,
            SectionName = sectionName,
            Type = TemplateParameterType.Toggle,
            DefaultBoolean = value,
            DefaultColor = "#FFFFFF"
        };

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

    private struct VoidNode
    {
        public Point Position { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public double Seed { get; set; }
    }
}
