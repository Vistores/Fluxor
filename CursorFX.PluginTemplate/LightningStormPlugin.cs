using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.PluginTemplate;

public sealed class LightningStormPlugin : ICursorEffectPlugin
{
    private const int MaxImpactPulses = 14;
    private const int MaxArcBursts = 24;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ImpactPulse> _impactPulses = [];
    private readonly List<ArcBurst> _arcBursts = [];
    private Point _cursorPosition;
    private Point _smoothedCursorPosition;
    private Point _emitterPosition;
    private Vector _smoothedVelocity;
    private Vector _trailDirection = new(0, 1);
    private double _timeSeconds;
    private double _masterOpacity = 1.0;
    private double _spawnAccumulator;

    public string DisplayName => "Lightning Storm Plugin";

    public void Dispose()
    {
        _impactPulses.Clear();
        _arcBursts.Clear();
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
        var dt = Math.Clamp(deltaTime.TotalSeconds, 0.0, 0.05);
        _timeSeconds += dt;

        var inertia = Math.Max(2.0, GetNumber("inertia", 16));
        var previous = _smoothedCursorPosition;
        var blend = Math.Clamp(dt * inertia, 0d, 1d);
        _smoothedCursorPosition = new Point(
            _smoothedCursorPosition.X + ((_cursorPosition.X - _smoothedCursorPosition.X) * blend),
            _smoothedCursorPosition.Y + ((_cursorPosition.Y - _smoothedCursorPosition.Y) * blend));

        if (dt > 0.0001)
        {
            var instantVelocity = (_smoothedCursorPosition - previous) / dt;
            var velocityBlend = Math.Clamp(dt * 9.0, 0d, 1d);
            _smoothedVelocity = new Vector(
                _smoothedVelocity.X + ((instantVelocity.X - _smoothedVelocity.X) * velocityBlend),
                _smoothedVelocity.Y + ((instantVelocity.Y - _smoothedVelocity.Y) * velocityBlend));

            if (_smoothedVelocity.LengthSquared > 9)
            {
                var targetDirection = -_smoothedVelocity;
                targetDirection.Normalize();
                var directionBlend = Math.Clamp(dt * 7.0, 0d, 1d);
                _trailDirection = new Vector(
                    _trailDirection.X + ((targetDirection.X - _trailDirection.X) * directionBlend),
                    _trailDirection.Y + ((targetDirection.Y - _trailDirection.Y) * directionBlend));
                if (_trailDirection.LengthSquared > 0.0001)
                {
                    _trailDirection.Normalize();
                }
            }
        }

        UpdateEmitter(dt);
        UpdateImpactPulses(dt);
        UpdateArcBursts(dt);
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("customShaderEnabled", true))
        {
            return;
        }

        var emitter = GetEmitterPosition();
        var primaryColor = GetColor("customPrimaryColor", "#7DD3FC");
        var accentColor = GetColor("customAccentColor", "#FFFFFF");
        var coreRadius = GetNumber("coreRadius", 24);
        var coreOpacity = GetNumber("customOpacity", 0.82) * _masterOpacity;
        var arcCount = Math.Max(1, (int)Math.Round(GetNumber("arcCount", 5)));
        var arcLength = GetNumber("arcLength", 36);
        var jitter = GetNumber("jitter", 9);
        var thickness = GetNumber("arcThickness", 2.2);
        var animationSpeed = GetNumber("animationSpeed", 2.8);
        var branchChance = GetNumber("branchChance", 0.45);
        var branchLength = GetNumber("branchLength", 18);
        var coronaRadius = GetNumber("coronaRadius", coreRadius * 1.6);
        var idleRadius = GetNumber("idleArcRadius", coronaRadius * 0.5);

        DrawCoreGlow(drawingContext, emitter, primaryColor, accentColor, coreRadius, coronaRadius, coreOpacity);

        for (var index = 0; index < arcCount; index++)
        {
            var angle = ((_timeSeconds * animationSpeed) + (index * (Math.PI * 2 / arcCount))) % (Math.PI * 2);
            var tangent = new Vector(Math.Cos(angle), Math.Sin(angle));
            var start = emitter + (tangent * (coreRadius * 0.25));
            var end = emitter + (tangent * (coreRadius + arcLength));
            var geometry = BuildBoltGeometry(start, end, jitter, index);

            drawingContext.DrawGeometry(null, CreatePen(primaryColor, thickness + 2.4, coreOpacity * 0.22), geometry);
            drawingContext.DrawGeometry(null, CreatePen(primaryColor, thickness, coreOpacity), geometry);
            drawingContext.DrawGeometry(null, CreatePen(accentColor, Math.Max(1.0, thickness * 0.42), coreOpacity * 0.95), geometry);

            if (HashToUnit(index + 7, _timeSeconds * 1.3) < branchChance)
            {
                DrawBranchBolt(drawingContext, end, tangent, branchLength, jitter * 0.5, primaryColor, accentColor, thickness * 0.7, coreOpacity * 0.78, index);
            }
        }

        if (_smoothedVelocity.LengthSquared < 100)
        {
            DrawIdleCorona(drawingContext, emitter, idleRadius, jitter, primaryColor, accentColor, thickness * 0.85, coreOpacity * 0.72);
        }

        foreach (var burst in _arcBursts)
        {
            DrawArcBurst(drawingContext, burst, primaryColor, accentColor, thickness, coreOpacity);
        }

        foreach (var pulse in _impactPulses)
        {
            var progress = Math.Clamp(pulse.Age / pulse.Lifetime, 0, 1);
            var radius = 8 + (GetNumber("impactRadius", 80) * progress);
            var opacity = (1 - progress) * coreOpacity;

            drawingContext.DrawEllipse(null, CreatePen(primaryColor, thickness + 1.4, opacity * 0.32), pulse.Position, radius, radius);
            drawingContext.DrawEllipse(null, CreatePen(primaryColor, thickness + 0.8, opacity), pulse.Position, radius, radius);
            drawingContext.DrawEllipse(null, CreatePen(accentColor, thickness * 0.6, opacity), pulse.Position, radius * 0.55, radius * 0.55);
        }
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
        if (_smoothedCursorPosition == default)
        {
            _smoothedCursorPosition = position;
            _emitterPosition = position;
        }
    }

    public void OnMouseClick(Point position)
    {
        if (_impactPulses.Count >= MaxImpactPulses)
        {
            _impactPulses.RemoveAt(0);
        }

        _impactPulses.Add(new ImpactPulse(position, Math.Max(0.08, GetNumber("boltLifetime", 0.35))));

        for (var index = 0; index < 4 && _arcBursts.Count < MaxArcBursts; index++)
        {
            _arcBursts.Add(CreateBurst(position, index));
        }
    }

    private void UpdateEmitter(double dt)
    {
        if (_emitterPosition == default)
        {
            _emitterPosition = _smoothedCursorPosition;
        }

        var sourceLag = Math.Max(2.0, GetNumber("sourceLag", 10));
        var blend = Math.Clamp(dt * sourceLag, 0d, 1d);
        _emitterPosition = new Point(
            _emitterPosition.X + ((_smoothedCursorPosition.X - _emitterPosition.X) * blend),
            _emitterPosition.Y + ((_smoothedCursorPosition.Y - _emitterPosition.Y) * blend));
    }

    private void UpdateImpactPulses(double dt)
    {
        for (var index = _impactPulses.Count - 1; index >= 0; index--)
        {
            var pulse = _impactPulses[index];
            pulse.Age += dt;
            if (pulse.Age >= pulse.Lifetime)
            {
                _impactPulses.RemoveAt(index);
                continue;
            }

            _impactPulses[index] = pulse;
        }
    }

    private void UpdateArcBursts(double dt)
    {
        var gravity = GetGravityVector();
        var burstLifetime = Math.Max(0.1, GetNumber("burstLifetime", 0.42));
        var spawnRate = Math.Max(0.0, GetNumber("ambientBurstRate", 8));

        for (var index = _arcBursts.Count - 1; index >= 0; index--)
        {
            var burst = _arcBursts[index];
            burst.Age += dt;
            if (burst.Age >= burst.Lifetime)
            {
                _arcBursts.RemoveAt(index);
                continue;
            }

            burst.Velocity += gravity * dt;
            burst.Velocity *= Math.Clamp(1.0 - (GetNumber("burstDamping", 1.6) * dt * 0.1), 0.82, 0.995);
            burst.Position += burst.Velocity * dt;
            _arcBursts[index] = burst;
        }

        _spawnAccumulator += dt * spawnRate;
        var spawnCount = Math.Min(4, (int)_spawnAccumulator);
        _spawnAccumulator -= spawnCount;

        for (var index = 0; index < spawnCount && _arcBursts.Count < MaxArcBursts; index++)
        {
            _arcBursts.Add(CreateBurst(GetEmitterPosition(), index + _arcBursts.Count, burstLifetime));
        }
    }

    private void DrawCoreGlow(DrawingContext drawingContext, Point emitter, Color primaryColor, Color accentColor, double coreRadius, double coronaRadius, double opacity)
    {
        var haloBrush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        haloBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, opacity * 0.5), 0));
        haloBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, opacity * 0.16), 0.42));
        haloBrush.GradientStops.Add(new GradientStop(WithAlpha(primaryColor, 0), 1));
        haloBrush.Freeze();

        drawingContext.DrawEllipse(haloBrush, null, emitter, coronaRadius, coronaRadius);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.14), null, emitter, coreRadius * 0.34, coreRadius * 0.34);
        drawingContext.DrawEllipse(null, CreatePen(primaryColor, 1.4, opacity * 0.68), emitter, coreRadius * 0.9, coreRadius * 0.9);
    }

    private void DrawIdleCorona(DrawingContext drawingContext, Point emitter, double radius, double jitter, Color primaryColor, Color accentColor, double thickness, double opacity)
    {
        const int arcCount = 5;
        for (var index = 0; index < arcCount; index++)
        {
            var angle = (_timeSeconds * 1.8) + (index * (Math.PI * 2.0 / arcCount));
            var start = emitter + new Vector(Math.Cos(angle), Math.Sin(angle)) * (radius * 0.3);
            var end = emitter + new Vector(Math.Cos(angle + 0.35), Math.Sin(angle + 0.35)) * radius;
            var geometry = BuildBoltGeometry(start, end, jitter * 0.65, index + 19);
            drawingContext.DrawGeometry(null, CreatePen(primaryColor, thickness + 1.8, opacity * 0.18), geometry);
            drawingContext.DrawGeometry(null, CreatePen(accentColor, Math.Max(0.9, thickness * 0.42), opacity * 0.74), geometry);
        }
    }

    private void DrawArcBurst(DrawingContext drawingContext, ArcBurst burst, Color primaryColor, Color accentColor, double thickness, double opacity)
    {
        var progress = Math.Clamp(burst.Age / burst.Lifetime, 0, 1);
        var alpha = (1.0 - progress) * opacity;
        var geometry = BuildBoltGeometry(burst.Position, burst.Position + burst.Velocity * 0.06, burst.Jitter, burst.Seed);
        drawingContext.DrawGeometry(null, CreatePen(primaryColor, thickness + 1.6, alpha * 0.22), geometry);
        drawingContext.DrawGeometry(null, CreatePen(primaryColor, Math.Max(1.0, thickness * 0.88), alpha), geometry);
        drawingContext.DrawGeometry(null, CreatePen(accentColor, Math.Max(0.8, thickness * 0.36), alpha * 0.94), geometry);
    }

    private void DrawBranchBolt(DrawingContext drawingContext, Point origin, Vector tangent, double length, double jitter, Color primaryColor, Color accentColor, double thickness, double opacity, int seed)
    {
        var normal = new Vector(-tangent.Y, tangent.X);
        var direction = tangent + (normal * ((HashToSigned(seed, _timeSeconds) * 0.75)));
        if (direction.LengthSquared < 0.0001)
        {
            return;
        }

        direction.Normalize();
        var end = origin + (direction * length);
        var geometry = BuildBoltGeometry(origin, end, jitter, seed + 41);
        drawingContext.DrawGeometry(null, CreatePen(primaryColor, thickness + 1.0, opacity * 0.22), geometry);
        drawingContext.DrawGeometry(null, CreatePen(accentColor, Math.Max(0.8, thickness * 0.55), opacity), geometry);
    }

    private StreamGeometry BuildBoltGeometry(Point start, Point end, double jitter, int seed)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(start, false, false);

        var segments = 7;
        var direction = end - start;
        var normal = direction.LengthSquared > 0.0001 ? new Vector(-direction.Y, direction.X) : new Vector(0, 1);
        if (normal.LengthSquared > 0.0001)
        {
            normal.Normalize();
        }

        for (var index = 1; index <= segments; index++)
        {
            var progress = index / (double)segments;
            var point = new Point(
                start.X + (direction.X * progress),
                start.Y + (direction.Y * progress));

            if (index < segments)
            {
                var phase = (_timeSeconds * GetNumber("animationSpeed", 2.8) * 3.2) + (seed * 1.7) + (index * 0.85);
                var offset = (Math.Sin(phase) + (Math.Cos(phase * 0.73) * 0.65)) * jitter;
                point += normal * offset;
            }

            context.LineTo(point, true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private ArcBurst CreateBurst(Point origin, int index, double? explicitLifetime = null)
    {
        var baseDirection = _trailDirection.LengthSquared > 0.0001 ? _trailDirection : new Vector(0, 1);
        var normal = new Vector(-baseDirection.Y, baseDirection.X);
        var randomness = GetNumber("randomness", 0.8);
        var speed = GetNumber("burstSpeed", 90);
        var spread = GetNumber("burstSpread", 0.85);
        var phase = _timeSeconds + (index * 0.63);
        var angleOffset = HashToSigned(index + 3, phase) * spread;
        var direction = baseDirection + (normal * angleOffset);
        if (direction.LengthSquared < 0.0001)
        {
            direction = new Vector(0, 1);
        }
        direction.Normalize();

        var position = origin + (direction * GetNumber("spawnRadius", 4));
        var velocity = direction * speed;
        if (randomness > 0.01)
        {
            velocity += new Vector(Math.Sin(phase * 2.4), Math.Cos(phase * 1.9)) * (randomness * 8.0);
        }

        return new ArcBurst
        {
            Position = position,
            Velocity = velocity,
            Age = 0,
            Lifetime = explicitLifetime ?? Math.Max(0.12, GetNumber("burstLifetime", 0.42)),
            Jitter = GetNumber("burstJitter", Math.Max(2.0, GetNumber("jitter", 9) * 0.5)),
            Seed = index + (int)Math.Round(_timeSeconds * 10)
        };
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

    private Vector GetGravityVector()
    {
        return new Vector(
            GetNumber("gravityX", 0),
            GetNumber("gravityY", 0));
    }

    private Point GetEmitterPosition()
    {
        var idleRadius = GetNumber("idleRadius", 0);
        if (idleRadius <= 0.01)
        {
            return _emitterPosition;
        }

        var idleSpeed = GetNumber("idleSpeed", 1.1);
        var offset = new Vector(
            Math.Sin(_timeSeconds * idleSpeed) * idleRadius,
            Math.Cos((_timeSeconds * idleSpeed * 1.21) + 0.7) * idleRadius * 0.7);
        return _emitterPosition + offset;
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

    private static double HashToUnit(int salt, double seed)
    {
        var value = Math.Sin((salt * 12.9898) + (seed * 78.233)) * 43758.5453;
        return value - Math.Floor(value);
    }

    private static double HashToSigned(int salt, double seed)
    {
        return (HashToUnit(salt, seed) * 2.0) - 1.0;
    }

    private static Color WithAlpha(Color color, double opacity)
    {
        return Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B);
    }

    private struct ImpactPulse(Point position, double lifetime)
    {
        public Point Position { get; } = position;

        public double Lifetime { get; } = lifetime;

        public double Age { get; set; }
    }

    private struct ArcBurst
    {
        public Point Position { get; set; }

        public Vector Velocity { get; set; }

        public double Age { get; set; }

        public double Lifetime { get; set; }

        public double Jitter { get; set; }

        public int Seed { get; set; }
    }
}
