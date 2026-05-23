using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginSakuraInk;

public sealed class SakuraInkPlugin : ICursorEffectPlugin
{
    private const int MaxPetals = 180;
    private const int MaxInkBlots = 80;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Petal> _petals = [];
    private readonly List<InkBlot> _inkBlots = [];
    private readonly Random _random = new(88421);

    private Point _cursorPosition;
    private Point _lastSpawnPosition;
    private bool _hasCursor;
    private double _timeSeconds;
    private double _petalAccumulator;
    private double _inkAccumulator;
    private double _masterOpacity = 1.0;

    public string DisplayName => "Sakura Ink";
    public string PluginId => "sakura-ink";
    public string Description => "Soft sakura petals drift away from the cursor while ink blots remain as an expressive trail.";
    public string IconGlyph => "S";
    public string AccentColor => "#F9A8D4";
    public TemplateEffectKind Kind => TemplateEffectKind.NebulaDust;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Sakura Ink", PluginParameterSection.Shader, "Sakura Ink", true),
        ColorParameter("petalColor", "Petal Color", PluginParameterSection.Shader, "Petals", "#FDA4AF"),
        ColorParameter("petalHighlight", "Petal Highlight", PluginParameterSection.Shader, "Petals", "#FFE4E6"),
        ColorParameter("inkColor", "Ink Color", PluginParameterSection.Trail, "Ink Trail", "#00FFA4FF"),
        ColorParameter("inkEdgeColor", "Ink Edge Color", PluginParameterSection.Trail, "Ink Trail", "#005B2647"),
        Number("opacity", "Opacity", PluginParameterSection.Shader, "Sakura Ink", 0.05, 1.0, 0.01, 1.0),
        Number("passiveRate", "Passive Petal Rate", PluginParameterSection.Shader, "Petals", 0, 80, 1, 40.879056047197636),
        Number("petalSize", "Petal Size", PluginParameterSection.Shader, "Petals", 3, 28, 0.5, 8.613312812620245),
        Number("petalLifetime", "Petal Lifetime", PluginParameterSection.Shader, "Petals", 0.25, 4.0, 0.05, 1.1538091573682185),
        Number("petalSpeed", "Petal Speed", PluginParameterSection.Shader, "Petals", 10, 260, 1, 104.7048864948057),
        Number("petalSpread", "Petal Spread", PluginParameterSection.Shader, "Petals", 4, 120, 1, 34),
        Number("inkRate", "Ink Trail Rate", PluginParameterSection.Trail, "Ink Trail", 0, 80, 1, 0),
        Number("inkSize", "Ink Blot Size", PluginParameterSection.Trail, "Ink Trail", 3, 48, 0.5, 3),
        Number("inkLifetime", "Ink Lifetime", PluginParameterSection.Trail, "Ink Trail", 0.15, 3.0, 0.05, 0.24936514043863006),
        Number("burstPetals", "Tap Petals", PluginParameterSection.Ripple, "Tap Burst", 4, 80, 1, 9.819161215852226),
        Number("burstInk", "Tap Ink Blots", PluginParameterSection.Ripple, "Tap Burst", 2, 36, 1, 2),
        Number("burstPower", "Tap Burst Power", PluginParameterSection.Ripple, "Tap Burst", 40, 520, 5, 201.108118507118),
        Number("swirl", "Petal Swirl", PluginParameterSection.Shader, "Petals", 0, 18, 0.25, 5.5, isAdvanced: true),
        Number("gravity", "Falling Drift", PluginParameterSection.Shader, "Petals", -80, 180, 5, 26, isAdvanced: true),
        Number("inkBleed", "Ink Bleed", PluginParameterSection.Trail, "Ink Trail", 0, 1.0, 0.01, 0.42, isAdvanced: true)
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
        SpawnPassive(context.CursorPosition, Math.Clamp(context.DeltaTime.TotalSeconds, 0.0, 0.05));
        Update(context.DeltaTime);
    }

    public void Update(TimeSpan deltaTime)
    {
        var dt = Math.Clamp(deltaTime.TotalSeconds, 0.0, 0.05);
        _timeSeconds += dt;
        UpdatePetals(dt);
        UpdateInk(dt);
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("enabled", true))
        {
            return;
        }

        var opacity = GetNumber("opacity", 1.0) * _masterOpacity;
        DrawInk(drawingContext, opacity);
        DrawPetals(drawingContext, opacity);
    }

    public void OnMouseMove(PluginRenderContext context, Point position)
    {
        _cursorPosition = context.CursorPosition;
        SpawnPassive(context.CursorPosition, 1.0 / 144.0);
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
        SpawnPassive(position, 1.0 / 144.0);
    }

    public void OnMouseClick(PluginRenderContext context, Point position)
    {
        SpawnBurst(context.CursorPosition);
    }

    public void OnMouseClick(Point position)
    {
        SpawnBurst(position);
    }

    public void Dispose()
    {
        _petals.Clear();
        _inkBlots.Clear();
    }

    private void SpawnPassive(Point cursor, double dt)
    {
        if (!_hasCursor)
        {
            _hasCursor = true;
            _lastSpawnPosition = cursor;
        }

        var movement = cursor - _lastSpawnPosition;
        var movementDistance = movement.Length;
        var direction = movementDistance > 0.001 ? movement : RandomVector(1);
        if (direction.LengthSquared > 0.001)
        {
            direction.Normalize();
        }

        var movementBoost = Math.Clamp(movementDistance / 22.0, 0.0, 3.0);
        _petalAccumulator += dt * GetNumber("passiveRate", 40.879056047197636) * (0.35 + movementBoost);
        _inkAccumulator += dt * GetNumber("inkRate", 0) * (0.25 + movementBoost * 0.75);

        while (_petalAccumulator >= 1.0)
        {
            _petalAccumulator -= 1.0;
            SpawnPetal(cursor, direction, false);
        }

        while (_inkAccumulator >= 1.0)
        {
            _inkAccumulator -= 1.0;
            SpawnInk(cursor + RandomVector(GetNumber("petalSpread", 34) * 0.18), 0.85 + _random.NextDouble() * 0.3);
        }

        _lastSpawnPosition = cursor;
    }

    private void SpawnBurst(Point cursor)
    {
        var petalCount = Math.Clamp((int)Math.Round(GetNumber("burstPetals", 9.819161215852226)), 4, 80);
        var inkCount = Math.Clamp((int)Math.Round(GetNumber("burstInk", 2)), 2, 36);

        for (var i = 0; i < petalCount; i++)
        {
            var angle = (Math.PI * 2.0 * i / petalCount) + (_random.NextDouble() - 0.5) * 0.42;
            var direction = new Vector(Math.Cos(angle), Math.Sin(angle));
            SpawnPetal(cursor + direction * (_random.NextDouble() * 10), direction, true);
        }

        for (var i = 0; i < inkCount; i++)
        {
            SpawnInk(cursor + RandomVector(8 + _random.NextDouble() * 18), 1.8 + _random.NextDouble() * 1.2);
        }
    }

    private void SpawnPetal(Point origin, Vector direction, bool burst)
    {
        if (_petals.Count >= MaxPetals)
        {
            _petals.RemoveAt(0);
        }

        if (direction.LengthSquared < 0.001)
        {
            direction = RandomVector(1);
        }

        direction.Normalize();
        var tangent = new Vector(-direction.Y, direction.X);
        var spread = GetNumber("petalSpread", 34);
        var speed = GetNumber("petalSpeed", 104.7048864948057);
        var burstPower = GetNumber("burstPower", 201.108118507118);
        var velocity = burst
            ? direction * (burstPower * (0.45 + _random.NextDouble() * 0.85)) + tangent * ((_random.NextDouble() - 0.5) * burstPower * 0.32)
            : direction * (speed * (0.15 + _random.NextDouble() * 0.35)) + tangent * ((_random.NextDouble() - 0.5) * spread) + RandomVector(speed * 0.22);

        _petals.Add(new Petal
        {
            Position = origin + RandomVector(spread * (burst ? 0.16 : 0.32)),
            Velocity = velocity,
            Age = 0,
            Lifetime = Math.Max(0.25, GetNumber("petalLifetime", 1.1538091573682185)) * (0.75 + _random.NextDouble() * 0.65) * (burst ? 1.15 : 1.0),
            Size = GetNumber("petalSize", 8.613312812620245) * (0.72 + _random.NextDouble() * 0.55),
            Rotation = _random.NextDouble() * 360,
            Spin = (_random.NextDouble() - 0.5) * (burst ? 720 : 260),
            Seed = _random.NextDouble() * Math.PI * 2.0
        });
    }

    private void SpawnInk(Point position, double scale)
    {
        if (_inkBlots.Count >= MaxInkBlots)
        {
            _inkBlots.RemoveAt(0);
        }

        _inkBlots.Add(new InkBlot
        {
            Position = position,
            Age = 0,
            Lifetime = Math.Max(0.15, GetNumber("inkLifetime", 0.24936514043863006)) * (0.78 + _random.NextDouble() * 0.5),
            Size = GetNumber("inkSize", 3) * scale * (0.72 + _random.NextDouble() * 0.62),
            Seed = _random.NextDouble() * Math.PI * 2.0,
            Stretch = 0.65 + _random.NextDouble() * 0.8,
            Rotation = _random.NextDouble() * 360
        });
    }

    private void UpdatePetals(double dt)
    {
        var gravity = GetNumber("gravity", 26);
        var swirl = GetNumber("swirl", 5.5);

        for (var i = _petals.Count - 1; i >= 0; i--)
        {
            var petal = _petals[i];
            petal.Age += dt;
            if (petal.Age >= petal.Lifetime)
            {
                _petals.RemoveAt(i);
                continue;
            }

            var wind = new Vector(
                Math.Sin(_timeSeconds * 2.1 + petal.Seed) * swirl,
                Math.Cos(_timeSeconds * 1.7 + petal.Seed * 0.6) * swirl * 0.35);
            petal.Velocity += (wind + new Vector(0, gravity)) * dt;
            petal.Velocity *= 1.0 - Math.Min(0.22, dt * 0.85);
            petal.Position += petal.Velocity * dt;
            petal.Rotation += petal.Spin * dt;
            _petals[i] = petal;
        }
    }

    private void UpdateInk(double dt)
    {
        for (var i = _inkBlots.Count - 1; i >= 0; i--)
        {
            var blot = _inkBlots[i];
            blot.Age += dt;
            if (blot.Age >= blot.Lifetime)
            {
                _inkBlots.RemoveAt(i);
                continue;
            }

            _inkBlots[i] = blot;
        }
    }

    private void DrawInk(DrawingContext drawingContext, double opacity)
    {
        var inkColor = GetColor("inkColor", "#00FFA4FF");
        var edgeColor = GetColor("inkEdgeColor", "#005B2647");
        var bleed = GetNumber("inkBleed", 0.42);

        foreach (var blot in _inkBlots)
        {
            var progress = Math.Clamp(blot.Age / blot.Lifetime, 0, 1);
            var fade = 1.0 - progress;
            var size = blot.Size * (1.0 + progress * bleed);
            var alpha = opacity * fade;

            drawingContext.PushTransform(new RotateTransform(blot.Rotation, blot.Position.X, blot.Position.Y));
            drawingContext.DrawEllipse(CreateBrush(edgeColor, alpha * 0.22), null, blot.Position, size * 0.72 * blot.Stretch, size * 0.52);
            drawingContext.DrawEllipse(CreateBrush(inkColor, alpha * 0.5), null, blot.Position, size * 0.48 * blot.Stretch, size * 0.36);

            for (var i = 0; i < 4; i++)
            {
                var offset = new Vector(Math.Cos(blot.Seed + i * 1.7), Math.Sin(blot.Seed + i * 1.7)) * size * (0.16 + i * 0.045);
                drawingContext.DrawEllipse(CreateBrush(inkColor, alpha * (0.18 - i * 0.025)), null, blot.Position + offset, size * (0.18 + i * 0.035), size * (0.12 + i * 0.03));
            }

            drawingContext.Pop();
        }
    }

    private void DrawPetals(DrawingContext drawingContext, double opacity)
    {
        var petalColor = GetColor("petalColor", "#FDA4AF");
        var highlight = GetColor("petalHighlight", "#FFE4E6");

        foreach (var petal in _petals)
        {
            var progress = Math.Clamp(petal.Age / petal.Lifetime, 0, 1);
            var fade = 1.0 - progress;
            var flutter = 0.72 + Math.Sin(_timeSeconds * 8.0 + petal.Seed) * 0.22;
            var alpha = opacity * fade * flutter;

            drawingContext.PushTransform(new RotateTransform(petal.Rotation, petal.Position.X, petal.Position.Y));
            DrawPetalShape(drawingContext, petal.Position, petal.Size, petalColor, highlight, alpha);
            drawingContext.Pop();
        }
    }

    private static void DrawPetalShape(DrawingContext drawingContext, Point center, double size, Color petalColor, Color highlight, double opacity)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var top = new Point(center.X, center.Y - size * 0.74);
            var right = new Point(center.X + size * 0.44, center.Y + size * 0.22);
            var bottom = new Point(center.X, center.Y + size * 0.62);
            var left = new Point(center.X - size * 0.44, center.Y + size * 0.22);

            ctx.BeginFigure(top, true, true);
            ctx.BezierTo(
                new Point(center.X + size * 0.42, center.Y - size * 0.52),
                new Point(center.X + size * 0.56, center.Y - size * 0.04),
                right,
                true,
                true);
            ctx.BezierTo(
                new Point(center.X + size * 0.22, center.Y + size * 0.46),
                new Point(center.X + size * 0.08, center.Y + size * 0.6),
                bottom,
                true,
                true);
            ctx.BezierTo(
                new Point(center.X - size * 0.08, center.Y + size * 0.6),
                new Point(center.X - size * 0.22, center.Y + size * 0.46),
                left,
                true,
                true);
            ctx.BezierTo(
                new Point(center.X - size * 0.56, center.Y - size * 0.04),
                new Point(center.X - size * 0.42, center.Y - size * 0.52),
                top,
                true,
                true);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(CreateBrush(petalColor, opacity * 0.88), null, geometry);
        drawingContext.DrawLine(CreatePen(highlight, Math.Max(0.45, size * 0.08), opacity * 0.42), new Point(center.X, center.Y - size * 0.38), new Point(center.X, center.Y + size * 0.36));
    }

    private double GetNumber(string key, double fallback)
        => _parameters.TryGetValue(key, out var value) && value.NumberValue.HasValue ? value.NumberValue.Value : fallback;

    private bool GetToggle(string key, bool fallback)
        => _parameters.TryGetValue(key, out var value) && value.BooleanValue.HasValue ? value.BooleanValue.Value : fallback;

    private Color GetColor(string key, string fallback)
        => ParseColor(_parameters.TryGetValue(key, out var value) ? value.ColorValue ?? fallback : fallback, fallback);

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

    private struct Petal
    {
        public Point Position { get; set; }
        public Vector Velocity { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public double Size { get; set; }
        public double Rotation { get; set; }
        public double Spin { get; set; }
        public double Seed { get; set; }
    }

    private struct InkBlot
    {
        public Point Position { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public double Size { get; set; }
        public double Seed { get; set; }
        public double Stretch { get; set; }
        public double Rotation { get; set; }
    }
}
