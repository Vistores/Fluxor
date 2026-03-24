using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginFireflySwarm;

public sealed class FireflySwarmPlugin : ICursorEffectPlugin
{
    private const int MaxFireflies = 20;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Firefly> _fireflies = [];
    private readonly Random _random = new(19477);
    private Point _cursorPosition;
    private Point _smoothedCursor;
    private Point _emitter;
    private double _masterOpacity = 1.0;
    private double _timeSeconds;
    private double _spawnAccumulator;

    public string DisplayName => "Firefly Swarm";
    public string PluginId => "firefly-swarm";
    public string Description => "Sparse glowing fireflies that leave tiny curved micro-trails and drift away from the cursor.";
    public string IconGlyph => "I";
    public string AccentColor => "#FDE68A";
    public TemplateEffectKind Kind => TemplateEffectKind.SparkShower;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Fireflies", PluginParameterSection.Shader, "Swarm", true),
        ColorParameter("sparkColor", "Spark Color", PluginParameterSection.Shader, "Swarm", "#FDE68A"),
        ColorParameter("trailColor", "Trail Color", PluginParameterSection.Shader, "Swarm", "#FB923C"),
        Number("opacity", "Opacity", PluginParameterSection.Shader, "Swarm", 0.05, 1.0, 0.01, 0.85),
        Number("inertia", "Cursor Inertia", PluginParameterSection.Shader, "Motion", 2, 28, 1, 10),
        Number("follow", "Emitter Follow", PluginParameterSection.Shader, "Motion", 2, 28, 1, 8),
        Number("spawnRate", "Spawn Rate", PluginParameterSection.Shader, "Swarm", 2, 28, 1, 8),
        Number("spawnRadius", "Spawn Radius", PluginParameterSection.Shader, "Swarm", 0, 36, 1, 6),
        Number("speed", "Drift Speed", PluginParameterSection.Shader, "Swarm", 10, 180, 1, 58),
        Number("curvature", "Curve Amount", PluginParameterSection.Shader, "Swarm", 0, 10, 0.25, 2.6),
        Number("life", "Spark Lifetime", PluginParameterSection.Shader, "Swarm", 0.2, 2.2, 0.05, 0.82),
        Number("size", "Spark Size", PluginParameterSection.Shader, "Swarm", 1, 14, 0.5, 4.5),
        Number("tail", "Micro Trail Length", PluginParameterSection.Shader, "Swarm", 4, 36, 1, 14)
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

        UpdateFireflies(dt);
        SpawnFireflies(dt);
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("enabled", true))
        {
            return;
        }

        var glowColor = GetColor("sparkColor", "#FDE68A");
        var trailColor = GetColor("trailColor", "#FB923C");
        var opacity = GetNumber("opacity", 0.85) * _masterOpacity;

        foreach (var firefly in _fireflies)
        {
            var progress = Math.Clamp(firefly.Age / firefly.Lifetime, 0.0, 1.0);
            var alpha = opacity * (1.0 - progress);
            var size = firefly.Size * (1.0 - progress * 0.15);

            drawingContext.DrawLine(CreatePen(trailColor, Math.Max(1.0, size * 0.36), alpha * 0.28), firefly.TrailA, firefly.Position);
            drawingContext.DrawLine(CreatePen(trailColor, Math.Max(0.8, size * 0.22), alpha * 0.18), firefly.TrailB, firefly.TrailA);
            drawingContext.DrawEllipse(CreateBrush(glowColor, alpha * 0.3), null, firefly.Position, size * 2.1, size * 2.1);
            drawingContext.DrawEllipse(CreateBrush(glowColor, alpha * 0.95), null, firefly.Position, size, size);
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
        for (var i = 0; i < 3; i++)
        {
            SpawnOne(position, GetNumber("speed", 58) * (1.1 + i * 0.12));
        }
    }

    public void Dispose() => _fireflies.Clear();

    private void SpawnFireflies(double dt)
    {
        _spawnAccumulator += dt * Math.Max(2.0, GetNumber("spawnRate", 8));
        while (_spawnAccumulator >= 1.0)
        {
            _spawnAccumulator -= 1.0;
            SpawnOne(_emitter, GetNumber("speed", 58));
        }
    }

    private void SpawnOne(Point origin, double speed)
    {
        var radius = GetNumber("spawnRadius", 6);
        var angle = _random.NextDouble() * Math.PI * 2.0;
        var distance = _random.NextDouble() * radius;
        var spawnPoint = origin + new Vector(Math.Cos(angle), Math.Sin(angle)) * distance;
        var velocityAngle = angle + ((_random.NextDouble() - 0.5) * 0.8);
        var velocity = new Vector(Math.Cos(velocityAngle), Math.Sin(velocityAngle)) * speed * (0.55 + _random.NextDouble() * 0.7);

        if (_fireflies.Count >= MaxFireflies)
        {
            _fireflies.RemoveAt(0);
        }

        _fireflies.Add(new Firefly
        {
            Position = spawnPoint,
            TrailA = spawnPoint,
            TrailB = spawnPoint,
            Velocity = velocity,
            Age = 0,
            Lifetime = Math.Max(0.2, GetNumber("life", 0.82)),
            Size = GetNumber("size", 4.5) * (0.7 + _random.NextDouble() * 0.5),
            Seed = _random.NextDouble() * Math.PI * 2.0
        });
    }

    private void UpdateFireflies(double dt)
    {
        var curvature = GetNumber("curvature", 2.6);
        var tailLength = GetNumber("tail", 14);
        for (var index = _fireflies.Count - 1; index >= 0; index--)
        {
            var firefly = _fireflies[index];
            firefly.Age += dt;
            if (firefly.Age >= firefly.Lifetime)
            {
                _fireflies.RemoveAt(index);
                continue;
            }

            var tangent = new Vector(-firefly.Velocity.Y, firefly.Velocity.X);
            if (tangent.LengthSquared > 0.001)
            {
                tangent.Normalize();
            }

            firefly.Velocity += tangent * Math.Sin((_timeSeconds * 3.2) + firefly.Seed) * curvature * dt * 12;
            firefly.Velocity *= 1.0 - Math.Min(0.4, dt * 1.2);
            firefly.TrailB = Lerp(firefly.TrailB, firefly.TrailA, Math.Clamp(dt * 12.0, 0d, 1d));
            firefly.TrailA = Lerp(firefly.TrailA, firefly.Position - NormalizeSafe(firefly.Velocity) * tailLength, Math.Clamp(dt * 14.0, 0d, 1d));
            firefly.Position += firefly.Velocity * dt;
            _fireflies[index] = firefly;
        }
    }

    private static Vector NormalizeSafe(Vector value)
    {
        if (value.LengthSquared < 0.001)
        {
            return new Vector(0, -1);
        }

        value.Normalize();
        return value;
    }

    private static Point Lerp(Point from, Point to, double amount) =>
        new(from.X + ((to.X - from.X) * amount), from.Y + ((to.Y - from.Y) * amount));

    private double GetNumber(string key, double fallback)
        => _parameters.TryGetValue(key, out var value) && value.NumberValue.HasValue ? value.NumberValue.Value : fallback;

    private bool GetToggle(string key, bool fallback)
        => _parameters.TryGetValue(key, out var value) && value.BooleanValue.HasValue ? value.BooleanValue.Value : fallback;

    private Color GetColor(string key, string fallback)
        => ParseColor(_parameters.TryGetValue(key, out var value) ? value.ColorValue ?? fallback : fallback, fallback);

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

    private static SolidColorBrush CreateBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

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

    private struct Firefly
    {
        public Point Position { get; set; }
        public Point TrailA { get; set; }
        public Point TrailB { get; set; }
        public Vector Velocity { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public double Size { get; set; }
        public double Seed { get; set; }
    }
}
