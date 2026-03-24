using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class TemplateEffect : IEffect
{
    private const int MaxClickPulses = 14;
    private const int MaxMatrixParticles = 96;
    private const int MaxResidualNodes = 160;

    private readonly List<ClickPulse> _clickPulses = [];
    private readonly List<MatrixParticle> _matrixParticles = [];
    private readonly List<ResidualNode> _residualNodes = [];
    private readonly IScreenSampler? _screenSampler;
    private ShaderTemplateDefinition? _template;
    private ScreenSampleFrame? _lastBackdropSample;
    private Dictionary<string, TemplateParameterValue> _parameterValues = new(StringComparer.OrdinalIgnoreCase);
    private double _masterOpacity = 1.0;
    private Point _cursorPosition;
    private Point _smoothedCursorPosition;
    private Point _emitterPosition;
    private Vector _cursorVelocity;
    private Vector _trailDirection = new(0, 1);
    private double _timeSeconds;
    private double _matrixSpawnAccumulator;

    public string Name => "Template Shader";

    public bool IsEnabled { get; set; }

    public TemplateEffect(IScreenSampler? screenSampler = null)
    {
        _screenSampler = screenSampler;
    }

    public void Update(TimeSpan deltaTime)
    {
        if (!IsEnabled)
        {
            return;
        }

        var dt = Math.Clamp(deltaTime.TotalSeconds, 0.0, 0.05);
        _timeSeconds += dt;

        var inertia = Math.Max(2.0, GetNumber("inertia", 18));
        var previous = _smoothedCursorPosition;
        var followBlend = Math.Clamp(dt * inertia, 0d, 1d);
        _smoothedCursorPosition = new Point(
            _smoothedCursorPosition.X + ((_cursorPosition.X - _smoothedCursorPosition.X) * followBlend),
            _smoothedCursorPosition.Y + ((_cursorPosition.Y - _smoothedCursorPosition.Y) * followBlend));

        if (dt > 0.0001)
        {
            var instantVelocity = (_smoothedCursorPosition - previous) / dt;
            var velocityBlend = Math.Clamp(dt * 10.0, 0d, 1d);
            _cursorVelocity = new Vector(
                _cursorVelocity.X + ((instantVelocity.X - _cursorVelocity.X) * velocityBlend),
                _cursorVelocity.Y + ((instantVelocity.Y - _cursorVelocity.Y) * velocityBlend));

            if (_cursorVelocity.LengthSquared > 4)
            {
                var targetDirection = -_cursorVelocity;
                targetDirection.Normalize();
                var directionBlend = Math.Clamp(dt * 8.0, 0d, 1d);
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
        UpdateResidualNodes(dt);
        UpdateMatrixParticles(dt);
        UpdateBackdropSample();

        var clickLifetime = GetClickLifetime();
        for (var index = _clickPulses.Count - 1; index >= 0; index--)
        {
            var pulse = _clickPulses[index];
            pulse.Age += dt;
            if (pulse.Age >= clickLifetime)
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
            case TemplateEffectKind.PrismBloom:
                RenderPrismBloom(drawingContext);
                break;
            case TemplateEffectKind.ArcSparkle:
                RenderArcSparkle(drawingContext);
                break;
            case TemplateEffectKind.CometRibbon:
                RenderCometRibbon(drawingContext);
                break;
            case TemplateEffectKind.NebulaDust:
                RenderNebulaDust(drawingContext);
                break;
            case TemplateEffectKind.FrostHalo:
                RenderFrostHalo(drawingContext);
                break;
            case TemplateEffectKind.SolarFlare:
                RenderSolarFlare(drawingContext);
                break;
            case TemplateEffectKind.MysticRunes:
                RenderMysticRunes(drawingContext);
                break;
            case TemplateEffectKind.MatrixCascade:
                RenderMatrixCascade(drawingContext);
                break;
            case TemplateEffectKind.CosmicRift:
                RenderCosmicRift(drawingContext);
                break;
            case TemplateEffectKind.GlitchFracture:
                RenderGlitchFracture(drawingContext);
                break;
            case TemplateEffectKind.VelvetFlame:
                RenderVelvetFlame(drawingContext);
                break;
            case TemplateEffectKind.SparkShower:
                RenderSparkShower(drawingContext);
                break;
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
        if (!IsEnabled)
        {
            return;
        }

        if (_clickPulses.Count >= MaxClickPulses)
        {
            _clickPulses.RemoveAt(0);
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
        _matrixParticles.Clear();
        _residualNodes.Clear();
        _matrixSpawnAccumulator = 0;
        _lastBackdropSample = null;
    }

    private void UpdateEmitter(double dt)
    {
        var previousEmitter = _emitterPosition;
        if (_emitterPosition == default)
        {
            _emitterPosition = _smoothedCursorPosition;
            previousEmitter = _emitterPosition;
        }

        var emitterFollow = Math.Max(2.0, GetNumber("sourceLag", Math.Max(3.0, GetNumber("inertia", 18) * 0.6)));
        var emitterBlend = Math.Clamp(dt * emitterFollow, 0d, 1d);
        _emitterPosition = new Point(
            _emitterPosition.X + ((_smoothedCursorPosition.X - _emitterPosition.X) * emitterBlend),
            _emitterPosition.Y + ((_smoothedCursorPosition.Y - _emitterPosition.Y) * emitterBlend));

        SpawnResidualTrail(previousEmitter, _emitterPosition, dt);
    }

    private void UpdateResidualNodes(double dt)
    {
        if (!UsesResidualTrail())
        {
            _residualNodes.Clear();
            return;
        }

        var damping = Math.Clamp(GetNumber("trailDamping", 2.0), 0.1, 12.0);
        for (var index = _residualNodes.Count - 1; index >= 0; index--)
        {
            var node = _residualNodes[index];
            node.Age += dt;
            if (node.Age >= node.Lifetime)
            {
                _residualNodes.RemoveAt(index);
                continue;
            }

            node.Velocity *= Math.Clamp(1.0 - (damping * dt * 0.12), 0.75, 0.995);
            node.Position += node.Velocity * dt;
            _residualNodes[index] = node;
        }
    }

    private void SpawnResidualTrail(Point previousEmitter, Point currentEmitter, double dt)
    {
        if (!UsesResidualTrail())
        {
            return;
        }

        var delta = currentEmitter - previousEmitter;
        var distance = delta.Length;
        if (distance < 0.5)
        {
            return;
        }

        var direction = distance > 0.001 ? delta / distance : new Vector(0, -1);
        var normal = new Vector(-direction.Y, direction.X);
        var spacing = Math.Max(8.0, GetNumber("trailSpawnSpacing", GetNumber("size", 60) * 0.18));
        var steps = Math.Min(18, Math.Max(1, (int)Math.Ceiling(distance / spacing)));
        var lifetime = Math.Max(0.35, GetNumber("trailLifetime", 1.1));
        var freedom = GetNumber("trailFreedom", 1.0);
        var spread = GetNumber("trailSpread", Math.Max(10, GetNumber("size", 60) * 0.18));
        var baseSpeed = GetNumber("trailDriftSpeed", Math.Max(8, GetNumber("motion", 1.0) * 10));
        var scale = Math.Max(0.3, GetNumber("trailScale", 1.0));
        var baseAgeSeed = _timeSeconds * 1.37;

        for (var step = 0; step <= steps && _residualNodes.Count < MaxResidualNodes; step++)
        {
            var t = step / (double)steps;
            var point = previousEmitter + (delta * t);
            var lateral = HashToSigned(baseAgeSeed, step + _residualNodes.Count) * spread * freedom;
            var seededNormal = normal * lateral;
            var randomDir = new Vector(
                HashToSigned(baseAgeSeed * 0.7, step + 5),
                HashToSigned(baseAgeSeed * 1.3, step + 17));
            if (randomDir.LengthSquared > 0.001)
            {
                randomDir.Normalize();
            }

            var velocity = (-direction * baseSpeed * (0.65 + (HashToUnit(baseAgeSeed, step + 29) * 0.6)))
                + (randomDir * baseSpeed * 0.35 * freedom);

            _residualNodes.Add(new ResidualNode
            {
                Position = point + seededNormal,
                Velocity = velocity,
                Age = 0,
                Lifetime = lifetime * (0.8 + HashToUnit(baseAgeSeed, step + 43) * 0.5),
                Seed = baseAgeSeed + step,
                Scale = scale * (0.8 + HashToUnit(baseAgeSeed, step + 61) * 0.5)
            });
        }

        if (_residualNodes.Count > MaxResidualNodes)
        {
            _residualNodes.RemoveRange(0, _residualNodes.Count - MaxResidualNodes);
        }
    }

    private void UpdateMatrixParticles(double dt)
    {
        if (_template?.Kind != TemplateEffectKind.MatrixCascade || !IsEnabled)
        {
            _matrixParticles.Clear();
            return;
        }

        var gravity = GetGravityVector();
        var damping = Math.Clamp(GetNumber("matrixDamping", 1.8), 0.1, 12.0);
        var lifetime = Math.Max(0.2, GetNumber("matrixLifetime", 0.9));

        for (var index = _matrixParticles.Count - 1; index >= 0; index--)
        {
            var particle = _matrixParticles[index];
            particle.Age += dt;
            if (particle.Age >= particle.Lifetime)
            {
                _matrixParticles.RemoveAt(index);
                continue;
            }

            particle.Velocity += gravity * dt;
            particle.Velocity *= Math.Clamp(1.0 - (damping * dt * 0.08), 0.78, 0.995);
            particle.Position += particle.Velocity * dt;
            _matrixParticles[index] = particle;
        }

        var density = Math.Max(6, (int)Math.Round(GetNumber("particles", 18)));
        var symbolSpacing = Math.Max(4.0, GetNumber("symbolSpacing", 14));
        var speed = Math.Max(12.0, GetNumber("matrixSpeed", 64));
        var computedSpawnRate = Math.Max(8.0, (density * speed) / Math.Max(10.0, symbolSpacing * 1.8));
        var spawnRate = Math.Max(6.0, GetNumber("spawnRate", computedSpawnRate));
        _matrixSpawnAccumulator += dt * spawnRate;
        var spawnCount = Math.Min(8, (int)_matrixSpawnAccumulator);
        _matrixSpawnAccumulator -= spawnCount;

        for (var index = 0; index < spawnCount && _matrixParticles.Count < MaxMatrixParticles; index++)
        {
            _matrixParticles.Add(CreateMatrixParticle(lifetime));
        }
    }

    private void RenderCursorAura(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 54);
        var opacity = GetNumber("opacity", 0.42) * _masterOpacity;
        var pulse = 1 + (Math.Sin(_timeSeconds * GetNumber("motion", 1.4)) * 0.08);
        var primaryColor = GetColor("primaryColor", "#22D3EE");
        var accentColor = GetColor("accentColor", "#A5F3FC");
        var radius = size * pulse;
        var emitter = GetEmitterPosition();

        var fillBrush = CreateRadialBrush(primaryColor, opacity, 0.0, 1.0);
        var ringPen = CreatePen(accentColor, GetNumber("detail", 3), opacity * 0.9);
        drawingContext.DrawEllipse(fillBrush, null, emitter, radius, radius);
        drawingContext.DrawEllipse(null, ringPen, emitter, radius * 0.75, radius * 0.75);
        RenderClickShockwaves(drawingContext, accentColor, size * 1.45, opacity * 0.8, 2.2);
    }

    private void RenderClickBurst(DrawingContext drawingContext)
    {
        var lifetime = GetClickLifetime();
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

            drawingContext.DrawEllipse(null, CreatePen(primaryColor, thickness, alpha), pulse.Position, radius, radius);
            drawingContext.DrawEllipse(null, CreatePen(accentColor, Math.Max(1, thickness * 0.45), alpha * 0.8), pulse.Position, radius * 0.55, radius * 0.55);

            var rayCount = Math.Max(6, (int)Math.Round(GetNumber("particles", 10)));
            for (var index = 0; index < rayCount; index++)
            {
                var angle = ((Math.PI * 2.0) / rayCount) * index + (_timeSeconds * 0.7);
                var start = pulse.Position + new Vector(Math.Cos(angle), Math.Sin(angle)) * (radius * 0.35);
                var end = pulse.Position + new Vector(Math.Cos(angle), Math.Sin(angle)) * (radius * 0.9);
                drawingContext.DrawLine(CreatePen(accentColor, Math.Max(1.1, thickness * 0.28), alpha * 0.65), start, end);
            }
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
        var emitter = GetEmitterPosition();

        var angleA = _timeSeconds * speed;
        var angleB = angleA + Math.PI;
        var pointA = new Point(
            emitter.X + (Math.Cos(angleA) * radius),
            emitter.Y + (Math.Sin(angleA) * radius));
        var pointB = new Point(
            emitter.X + (Math.Cos(angleB) * radius),
            emitter.Y + (Math.Sin(angleB) * radius));

        if (showRing)
        {
            drawingContext.DrawEllipse(null, CreatePen(primaryColor, 1.5, opacity * 0.4), emitter, radius, radius);
        }

        drawingContext.DrawEllipse(CreateSolidBrush(primaryColor, opacity), null, pointA, dotSize, dotSize);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity), null, pointB, dotSize * 0.8, dotSize * 0.8);
        RenderClickSparkDots(drawingContext, accentColor, dotSize * 0.85, opacity * 0.8);
    }

    private void RenderPrismBloom(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 62);
        var opacity = GetNumber("opacity", 0.46) * _masterOpacity;
        var detail = Math.Max(4, (int)Math.Round(GetNumber("detail", 6)));
        var motion = GetNumber("motion", 1.25);
        var primaryColor = GetColor("primaryColor", "#67E8F9");
        var accentColor = GetColor("accentColor", "#C084FC");
        var emitter = GetEmitterPosition();

        for (var layer = 0; layer < detail; layer++)
        {
            var phase = (_timeSeconds * motion) + (layer * 0.35);
            var width = size * (0.65 + (layer * 0.08));
            var height = width * (0.45 + (Math.Sin(phase) * 0.06));
            var rotation = 360.0 * ((phase / (Math.PI * 2.0)) % 1.0) + (layer * (180.0 / detail));
            drawingContext.PushOpacity(opacity * (0.28 + (layer * 0.08)));
            drawingContext.PushTransform(new RotateTransform(rotation, emitter.X, emitter.Y));
            drawingContext.DrawEllipse(null, CreatePen(layer % 2 == 0 ? primaryColor : accentColor, 1.6 + (layer * 0.35), opacity), emitter, width, height);
            drawingContext.Pop();
            drawingContext.Pop();
        }

        drawingContext.DrawEllipse(CreateRadialBrush(accentColor, opacity * 0.22, 0.0, 1.0), null, emitter, size * 0.72, size * 0.72);
        drawingContext.DrawEllipse(CreateSolidBrush(primaryColor, opacity * 0.25), null, emitter, size * 0.48, size * 0.48);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.14), null, emitter, size * 0.24, size * 0.24);
        RenderClickShockwaves(drawingContext, accentColor, size * 1.2, opacity * 0.85, 1.8);
    }

    private void RenderArcSparkle(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 46);
        var opacity = GetNumber("opacity", 0.55) * _masterOpacity;
        var detail = Math.Max(6, (int)Math.Round(GetNumber("detail", 9)));
        var motion = GetNumber("motion", 2.1);
        var primaryColor = GetColor("primaryColor", "#A78BFA");
        var accentColor = GetColor("accentColor", "#FDE68A");
        var emitter = GetEmitterPosition();

        var swirlCount = Math.Max(10, detail + 2);
        drawingContext.DrawEllipse(CreateRadialBrush(primaryColor, opacity * 0.18, 0.0, 1.0), null, emitter, size * 0.55, size * 0.55);
        for (var index = 0; index < swirlCount; index++)
        {
            var progress = index / (double)swirlCount;
            var angle = (_timeSeconds * motion * 2.4) + (progress * Math.PI * 2.0);
            var radius = size * (0.35 + progress * 0.85);
            var point = emitter + new Vector(Math.Cos(angle), Math.Sin(angle)) * radius;
            var sparkSize = 1.8 + ((1.0 - progress) * detail * 0.22);
            var brush = CreateSolidBrush(progress < 0.45 ? accentColor : primaryColor, opacity * (0.35 + ((1.0 - progress) * 0.45)));
            drawingContext.DrawEllipse(brush, null, point, sparkSize, sparkSize);
        }

        RenderClickSparkDots(drawingContext, accentColor, 4 + (detail * 0.28), opacity);
    }

    private void RenderCometRibbon(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 64);
        var opacity = GetNumber("opacity", 0.6) * _masterOpacity;
        var detail = GetNumber("detail", 6);
        var motion = GetNumber("motion", 1.3);
        var primaryColor = GetColor("primaryColor", "#38BDF8");
        var accentColor = GetColor("accentColor", "#E0F2FE");
        var emitter = GetEmitterPosition();

        var velocity = _cursorVelocity;
        if (velocity.LengthSquared < 1)
        {
            velocity = new Vector(0, -1);
        }
        else
        {
            velocity.Normalize();
        }

        var normal = new Vector(-velocity.Y, velocity.X);
        var tailLength = size * (1.5 + (motion * 0.32));
        var tailEnd = emitter - (velocity * tailLength);
        var spread = 8 + (detail * 1.4);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var nose = emitter + (velocity * (size * 0.28));
            var leftMid = emitter - (velocity * (tailLength * 0.28)) + (normal * spread);
            var rightMid = emitter - (velocity * (tailLength * 0.18)) - (normal * spread * 0.78);
            context.BeginFigure(nose, true, true);
            context.QuadraticBezierTo(leftMid, tailEnd + (normal * (spread * 0.22)), true, false);
            context.QuadraticBezierTo(rightMid, nose, true, false);
        }
        geometry.Freeze();

        drawingContext.DrawGeometry(CreateSolidBrush(primaryColor, opacity * 0.24), null, geometry);
        drawingContext.DrawGeometry(null, CreatePen(primaryColor, 2.0 + (detail * 0.25), opacity * 0.75), geometry);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.9), null, emitter, size * 0.18, size * 0.18);
        RenderClickShockwaves(drawingContext, accentColor, size, opacity * 0.7, 1.35);
    }

    private void RenderNebulaDust(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 72);
        var opacity = GetNumber("opacity", 0.44) * _masterOpacity;
        var detail = Math.Max(8, (int)Math.Round(GetNumber("detail", 10)));
        var motion = GetNumber("motion", 1.15);
        var primaryColor = GetColor("primaryColor", "#7DD3FC");
        var accentColor = GetColor("accentColor", "#C084FC");
        var emitter = GetEmitterPosition();

        drawingContext.DrawEllipse(CreateRadialBrush(primaryColor, opacity * 0.33, 0.0, 1.0), null, emitter, size, size * 0.75);
        drawingContext.DrawEllipse(CreateRadialBrush(accentColor, opacity * 0.16, 0.0, 1.0), null, emitter, size * 0.62, size * 0.52);

        for (var index = 0; index < detail; index++)
        {
            var phase = (_timeSeconds * motion) + (index * 0.73);
            var radius = size * (0.25 + ((index % 5) * 0.13));
            var offset = new Vector(Math.Cos(phase * 0.9), Math.Sin(phase * 1.1)) * radius;
            var point = emitter + offset;
            var alpha = opacity * (0.24 + (0.12 * Math.Abs(Math.Sin(phase * 1.8))));
            var dotSize = 2.0 + ((index % 4) * 1.1);
            drawingContext.DrawEllipse(CreateSolidBrush(index % 2 == 0 ? primaryColor : accentColor, alpha), null, point, dotSize, dotSize);
        }

        RenderClickSparkDots(drawingContext, accentColor, 5, opacity * 0.9);
    }

    private void RenderFrostHalo(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 58);
        var opacity = GetNumber("opacity", 0.4) * _masterOpacity;
        var detail = Math.Max(5, (int)Math.Round(GetNumber("detail", 7)));
        var motion = GetNumber("motion", 1.55);
        var primaryColor = GetColor("primaryColor", "#BFDBFE");
        var accentColor = GetColor("accentColor", "#E0F2FE");
        var emitter = GetEmitterPosition();

        drawingContext.DrawEllipse(null, CreatePen(primaryColor, 1.6, opacity * 0.7), emitter, size, size);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.12), null, emitter, size * 0.55, size * 0.55);

        for (var index = 0; index < detail; index++)
        {
            var angle = (_timeSeconds * motion) + (index * ((Math.PI * 2.0) / detail));
            var point = emitter + new Vector(Math.Cos(angle), Math.Sin(angle)) * size;
            DrawSnowflake(drawingContext, point, 4 + (detail * 0.18), accentColor, opacity * 0.85);
        }

        RenderClickSnowBursts(drawingContext, primaryColor, accentColor, opacity);
    }

    private void RenderSolarFlare(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 60);
        var opacity = GetNumber("opacity", 0.58) * _masterOpacity;
        var detail = Math.Max(6, (int)Math.Round(GetNumber("detail", 8)));
        var motion = GetNumber("motion", 1.6);
        var primaryColor = GetColor("primaryColor", "#F59E0B");
        var accentColor = GetColor("accentColor", "#FDE68A");
        var emitter = GetEmitterPosition();

        drawingContext.DrawEllipse(CreateRadialBrush(primaryColor, opacity * 0.48, 0.0, 1.0), null, emitter, size * 0.8, size * 0.8);
        drawingContext.DrawEllipse(null, CreatePen(accentColor, 2.2, opacity * 0.82), emitter, size, size);
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.16), null, emitter, size * 0.28, size * 0.28);

        for (var index = 0; index < detail; index++)
        {
            var angle = (_timeSeconds * motion * 0.7) + (index * ((Math.PI * 2.0) / detail));
            var inner = emitter + new Vector(Math.Cos(angle), Math.Sin(angle)) * (size * 0.82);
            var outer = emitter + new Vector(Math.Cos(angle), Math.Sin(angle)) * (size * 1.32);
            drawingContext.DrawLine(CreatePen(primaryColor, 1.5 + ((index % 3) * 0.55), opacity * 0.75), inner, outer);
        }

        RenderClickShockwaves(drawingContext, accentColor, size * 1.55, opacity, 2.4);
    }

    private void RenderMysticRunes(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 64);
        var opacity = GetNumber("opacity", 0.48) * _masterOpacity;
        var detail = Math.Max(6, (int)Math.Round(GetNumber("detail", 8)));
        var motion = GetNumber("motion", 0.95);
        var primaryColor = GetColor("primaryColor", "#34D399");
        var accentColor = GetColor("accentColor", "#A7F3D0");
        var emitter = GetEmitterPosition();

        var rings = 2 + (detail / 4);
        for (var ring = 0; ring < rings; ring++)
        {
            var ringRadius = size * (0.55 + (ring * 0.32));
            var count = 5 + ring + (detail / 3);
            var rotation = (_timeSeconds * motion * (ring % 2 == 0 ? 1 : -1));

            for (var index = 0; index < count; index++)
            {
                var angle = rotation + (index * ((Math.PI * 2.0) / count));
                var point = emitter + new Vector(Math.Cos(angle), Math.Sin(angle)) * ringRadius;
                DrawRuneMark(drawingContext, point, angle, 5 + ring, index % 2 == 0 ? primaryColor : accentColor, opacity * (0.5 + (ring * 0.08)));
            }
        }

        drawingContext.DrawEllipse(null, CreatePen(primaryColor, 1.4, opacity * 0.55), emitter, size * 0.7, size * 0.7);
        RenderClickShockwaves(drawingContext, accentColor, size * 1.2, opacity * 0.85, 1.5);
    }

    private void RenderMatrixCascade(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 54);
        var opacity = GetNumber("opacity", 0.7) * _masterOpacity;
        var primaryColor = GetColor("primaryColor", "#22C55E");
        var accentColor = GetColor("accentColor", "#BBF7D0");
        var emitter = GetEmitterPosition();
        var glyphSize = Math.Max(10, GetNumber("matrixGlyphSize", Math.Max(11, size * 0.22)));
        var headRadius = size * 0.22;

        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.28), null, emitter, headRadius, headRadius);
        drawingContext.DrawEllipse(null, CreatePen(primaryColor, 1.1, opacity * 0.7), emitter, headRadius * 1.4, headRadius * 1.4);

        foreach (var particle in _matrixParticles)
        {
            var progress = Math.Clamp(particle.Age / particle.Lifetime, 0, 1);
            var alpha = opacity * (1.0 - progress) * (0.72 + (0.18 * Math.Sin((_timeSeconds * 4.0) + particle.Seed)));
            var color = particle.Highlight ? accentColor : primaryColor;
            DrawGlyph(
                drawingContext,
                particle.Glyph,
                particle.Position,
                glyphSize * (1.0 - (progress * 0.2)),
                color,
                alpha);
        }

        RenderClickSparkDots(drawingContext, accentColor, 4.5, opacity * 0.85);
    }

    private void RenderCosmicRift(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 78);
        var opacity = GetNumber("opacity", 0.52) * _masterOpacity;
        var detail = Math.Max(4, (int)Math.Round(GetNumber("detail", 8)));
        var motion = GetNumber("motion", 1.1);
        var primaryColor = GetColor("primaryColor", "#0B1020");
        var accentColor = GetColor("accentColor", "#A78BFA");
        var sampleOpacity = GetNumber("sampleOpacity", 0.48);
        var nodeCount = Math.Max(1, _residualNodes.Count);
        foreach (var node in _residualNodes)
        {
            var progress = Math.Clamp(node.Age / node.Lifetime, 0, 1);
            var alpha = opacity * (1.0 - progress);
            var nodeSize = size * (0.28 + ((1.0 - progress) * 0.22)) * node.Scale;
            var direction = node.Velocity.LengthSquared > 0.1 ? node.Velocity : _trailDirection;
            if (direction.LengthSquared <= 0.0001)
            {
                direction = new Vector(0, 1);
            }
            direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);

            drawingContext.DrawEllipse(CreateRadialBrush(accentColor, alpha * 0.12, 0.0, 1.0), null, node.Position, nodeSize * 0.95, nodeSize * 0.74);

            var rift = new StreamGeometry();
            using (var context = rift.Open())
            {
                var start = node.Position + (direction * (nodeSize * 0.1));
                context.BeginFigure(start, true, true);
                var crackLength = nodeSize * 1.25;
                for (var index = 1; index <= detail; index++)
                {
                    var t = index / (double)detail;
                    var along = start - (direction * crackLength * t);
                    var offset = Math.Sin((node.Seed * 1.3) + (_timeSeconds * motion * 1.8) + (index * 0.7)) * (nodeSize * 0.08);
                    context.LineTo(along + (normal * (offset + (nodeSize * 0.04 * (1.0 - t)))), true, false);
                }

                for (var index = detail; index >= 0; index--)
                {
                    var t = index / (double)detail;
                    var along = start - (direction * crackLength * t);
                    var offset = Math.Sin((node.Seed * 1.3) + (_timeSeconds * motion * 1.8) + (index * 0.7)) * (nodeSize * 0.08);
                    context.LineTo(along - (normal * (offset + (nodeSize * 0.04 * (1.0 - t)))), true, false);
                }
            }
            rift.Freeze();

            if (_lastBackdropSample is not null)
            {
                var rect = BuildSampleRect(node.Position, nodeSize * 1.32, nodeSize * 1.08);
                drawingContext.PushClip(rift);
                drawingContext.DrawRectangle(CreateImageBrush(_lastBackdropSample.Image, alpha * sampleOpacity), null, rect);
                drawingContext.Pop();
            }

            drawingContext.DrawGeometry(CreateSolidBrush(primaryColor, alpha * 0.7), CreatePen(accentColor, 1.0, alpha * 0.35), rift);

            var starsPerNode = Math.Max(2, GetNumber("particles", 14) / Math.Max(1, nodeCount / 3));
            for (var index = 0; index < starsPerNode; index++)
            {
                var phase = node.Seed + (_timeSeconds * motion) + (index * 0.47);
                var radial = nodeSize * (0.28 + ((index % 4) * 0.18));
                var point = node.Position + new Vector(Math.Cos(phase * 0.8), Math.Sin(phase * 1.1)) * radial;
                var starAlpha = alpha * (0.18 + (0.2 * Math.Abs(Math.Sin(phase * 2.1))));
                drawingContext.DrawEllipse(CreateSolidBrush(index % 4 == 0 ? accentColor : Colors.White, starAlpha), null, point, 1.2, 1.2);
            }
        }

        RenderClickShockwaves(drawingContext, accentColor, size * 1.15, opacity * 0.7, 1.6);
    }

    private void RenderGlitchFracture(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 70);
        var opacity = GetNumber("opacity", 0.5) * _masterOpacity;
        var detail = Math.Max(5, (int)Math.Round(GetNumber("detail", 8)));
        var motion = GetNumber("motion", 1.8);
        var primaryColor = GetColor("primaryColor", "#60A5FA");
        var accentColor = GetColor("accentColor", "#F472B6");
        var sampleOpacity = GetNumber("sampleOpacity", 0.52);
        var distortion = GetNumber("distortion", 10.0);
        foreach (var node in _residualNodes)
        {
            var progress = Math.Clamp(node.Age / node.Lifetime, 0, 1);
            var alpha = opacity * (1.0 - progress);
            var sizeScale = size * (0.4 + ((1.0 - progress) * 0.3)) * node.Scale;
            var direction = node.Velocity.LengthSquared > 0.1 ? node.Velocity : _trailDirection;
            if (direction.LengthSquared <= 0.0001)
            {
                direction = new Vector(0, 1);
            }
            direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);
            var sampleRect = BuildSampleRect(node.Position, sizeScale * 1.38, sizeScale * 0.92);

            if (_lastBackdropSample is not null)
            {
                var bandCount = Math.Max(3, detail / 2);
                for (var band = 0; band < bandCount; band++)
                {
                    var bandHeight = sampleRect.Height / bandCount;
                    var bandRect = new Rect(sampleRect.X, sampleRect.Y + (band * bandHeight), sampleRect.Width, bandHeight + 1);
                    var shift = HashToSigned(node.Seed + (_timeSeconds * motion), band + 41) * distortion;
                    var yShift = HashToSigned(node.Seed * 0.7, band + 73) * 2.4;
                    drawingContext.PushClip(new RectangleGeometry(bandRect, 1.5, 1.5));
                    drawingContext.DrawRectangle(CreateImageBrush(_lastBackdropSample.Image, alpha * sampleOpacity, shift, yShift), null, sampleRect);
                    drawingContext.Pop();
                }
            }

            for (var layer = 0; layer < 3; layer++)
            {
                var offsetShift = (layer - 1) * (1.2 + (Math.Sin((_timeSeconds * motion * 4.0) + node.Seed) * 1.2));
                var color = layer == 0 ? Colors.White : layer == 1 ? primaryColor : accentColor;
                var pen = CreatePen(color, 1.0 + (layer * 0.15), alpha * (layer == 0 ? 0.32 : 0.54));

                for (var branch = 0; branch < detail; branch++)
                {
                    var t = branch / (double)Math.Max(1, detail - 1);
                    var anchor = node.Position - (direction * sizeScale * (0.15 + t * 0.9));
                    var jag = HashToSigned((node.Seed * 1.7) + (_timeSeconds * 3.2), branch + (layer * 19)) * (sizeScale * 0.12);
                    var branchDir = normal * (jag + offsetShift);
                    var start = anchor + branchDir;
                    var end = start + (direction * (-sizeScale * 0.16)) + (normal * HashToSigned(node.Seed * 0.9, branch + 7) * sizeScale * 0.08);
                    drawingContext.DrawLine(pen, start, end);
                }
            }

            drawingContext.DrawRectangle(CreateSolidBrush(primaryColor, alpha * 0.08), null, new Rect(node.Position.X - (sizeScale * 0.7), node.Position.Y - 3, sizeScale, 6));
            drawingContext.DrawRectangle(CreateSolidBrush(accentColor, alpha * 0.06), null, new Rect(node.Position.X - (sizeScale * 0.3), node.Position.Y - 10, sizeScale * 0.8, 4));
        }

        RenderClickSparkDots(drawingContext, accentColor, 4.0, opacity * 0.9);
    }

    private void RenderVelvetFlame(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 74);
        var opacity = GetNumber("opacity", 0.58) * _masterOpacity;
        var detail = Math.Max(5, (int)Math.Round(GetNumber("detail", 8)));
        var motion = GetNumber("motion", 1.45);
        var primaryColor = GetColor("primaryColor", "#F97316");
        var accentColor = GetColor("accentColor", "#FDE68A");
        foreach (var node in _residualNodes)
        {
            var progress = Math.Clamp(node.Age / node.Lifetime, 0, 1);
            var alpha = opacity * (1.0 - progress);
            var nodeSize = size * (0.42 + ((1.0 - progress) * 0.32)) * node.Scale;
            var direction = node.Velocity.LengthSquared > 0.1 ? node.Velocity : _trailDirection;
            if (direction.LengthSquared <= 0.0001)
            {
                direction = new Vector(0, 1);
            }
            direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);

            var plume = new StreamGeometry();
            using (var context = plume.Open())
            {
                var head = node.Position + (direction * (nodeSize * 0.08));
                context.BeginFigure(head, true, true);
                for (var index = 0; index <= detail; index++)
                {
                    var t = index / (double)detail;
                    var along = head - (direction * (nodeSize * (0.12 + (t * 1.05))));
                    var width = nodeSize * (0.12 + (Math.Sin(t * Math.PI) * 0.2));
                    var wave = Math.Sin((node.Seed * 0.8) + (_timeSeconds * motion * 2.0) + (t * 5.0)) * (nodeSize * 0.04);
                    context.LineTo(along + (normal * (width + wave)), true, false);
                }

                for (var index = detail; index >= 0; index--)
                {
                    var t = index / (double)detail;
                    var along = head - (direction * (nodeSize * (0.12 + (t * 1.05))));
                    var width = nodeSize * (0.12 + (Math.Sin(t * Math.PI) * 0.2));
                    var wave = Math.Sin((node.Seed * 0.8) + (_timeSeconds * motion * 2.0) + (t * 5.0)) * (nodeSize * 0.04);
                    context.LineTo(along - (normal * (width - wave * 0.6)), true, false);
                }
            }
            plume.Freeze();

            drawingContext.DrawGeometry(CreateSolidBrush(primaryColor, alpha * 0.38), null, plume);
            drawingContext.DrawGeometry(CreateRadialBrush(accentColor, alpha * 0.2, 0.0, 1.0), null, plume);
        }

        var emitter = GetEmitterPosition();
        drawingContext.DrawEllipse(CreateSolidBrush(accentColor, opacity * 0.3), null, emitter, size * 0.12, size * 0.12);
        RenderClickShockwaves(drawingContext, accentColor, size * 0.9, opacity * 0.7, 1.4);
    }

    private void RenderSparkShower(DrawingContext drawingContext)
    {
        var size = GetNumber("size", 62);
        var opacity = GetNumber("opacity", 0.62) * _masterOpacity;
        var detail = Math.Max(8, (int)Math.Round(GetNumber("detail", 12)));
        var motion = GetNumber("motion", 1.8);
        var primaryColor = GetColor("primaryColor", "#F59E0B");
        var accentColor = GetColor("accentColor", "#FDE68A");
        foreach (var node in _residualNodes)
        {
            var progress = Math.Clamp(node.Age / node.Lifetime, 0, 1);
            var alpha = opacity * (1.0 - progress);
            var nodeSize = size * (0.45 + ((1.0 - progress) * 0.35)) * node.Scale;
            var direction = node.Velocity.LengthSquared > 0.1 ? node.Velocity : _trailDirection;
            if (direction.LengthSquared <= 0.0001)
            {
                direction = new Vector(0, 1);
            }
            direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);

            for (var index = 0; index < detail; index++)
            {
                var phase = node.Seed + (_timeSeconds * motion * 2.2) + (index * 0.63);
                var distance = nodeSize * (0.18 + HashToUnit(phase, index) * 0.95);
                var spread = HashToSigned(phase * 0.7, index + 11) * nodeSize * 0.34;
                var point = node.Position - (direction * distance) + (normal * spread);
                var tail = point + (direction * (6 + (HashToUnit(phase, index + 5) * 12)));
                drawingContext.DrawLine(CreatePen(index % 3 == 0 ? accentColor : primaryColor, 1.0, alpha * 0.72), point, tail);
                drawingContext.DrawEllipse(CreateSolidBrush(accentColor, alpha * 0.62), null, point, 1.5, 1.5);
            }
        }

        RenderClickSparkDots(drawingContext, primaryColor, 4.8, opacity);
    }

    private MatrixParticle CreateMatrixParticle(double lifetime)
    {
        var emitter = GetEmitterPosition();
        var speedMagnitude = _cursorVelocity.Length;
        var isIdle = speedMagnitude < GetNumber("idleScatterThreshold", 36);
        var direction = _trailDirection.LengthSquared > 0.0001 ? _trailDirection : new Vector(0, 1);
        var normal = new Vector(-direction.Y, direction.X);
        var spread = GetNumber("matrixSpread", GetNumber("size", 54) * 0.34);
        var speed = GetNumber("matrixSpeed", 64);
        var driftStrength = GetNumber("driftStrength", 2.2);
        var randomness = GetNumber("randomness", 0.6);
        var phase = _timeSeconds + (_matrixParticles.Count * 0.37);
        var spawnRadius = GetNumber("spawnRadius", Math.Max(2, spread * 0.16));
        Point position;
        Vector velocity;

        if (isIdle)
        {
            var idleRadius = GetNumber("idleScatterRadius", Math.Max(8, spread * 0.55));
            var idleSpeed = GetNumber("idleScatterSpeed", Math.Max(18, speed * 0.42));
            var angle = HashToUnit(phase * 1.713, _matrixParticles.Count) * Math.PI * 2.0;
            var radialFactor = Math.Sqrt(HashToUnit(phase * 0.913, _matrixParticles.Count + 13));
            var radial = new Vector(Math.Cos(angle), Math.Sin(angle));
            var orbit = new Vector(-radial.Y, radial.X);
            position = emitter + (radial * (idleRadius * radialFactor * 0.35));
            velocity = (radial * idleSpeed) + (orbit * (driftStrength * (0.8 + radialFactor)));
        }
        else
        {
            var lateral = HashToSigned(phase * 0.77, _matrixParticles.Count) * spread;
            var wobble = Math.Sin(phase * 2.1) * driftStrength;
            position = emitter + (normal * (lateral + wobble)) + (direction * (HashToUnit(phase * 1.11, _matrixParticles.Count + 5) * spawnRadius));
            velocity = (direction * speed) + (normal * (Math.Cos(phase * 1.7) * driftStrength * 8.0));
        }

        if (randomness > 0.01)
        {
            velocity += new Vector(Math.Sin(phase * 3.2), Math.Cos(phase * 2.4)) * (randomness * 6.0);
        }

        return new MatrixParticle
        {
            Position = position,
            Velocity = velocity,
            Age = 0,
            Lifetime = lifetime,
            Glyph = GetMatrixGlyph(_matrixParticles.Count, (int)Math.Round(phase * 10)),
            Highlight = (_matrixParticles.Count % 6) == 0,
            Seed = phase
        };
    }

    private void RenderClickShockwaves(DrawingContext drawingContext, Color color, double size, double opacity, double thickness)
    {
        var lifetime = GetClickLifetime();
        foreach (var pulse in _clickPulses)
        {
            var progress = Math.Clamp(pulse.Age / lifetime, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 2);
            var radius = Math.Max(6, size * eased);
            var alpha = (1 - progress) * opacity;
            drawingContext.DrawEllipse(null, CreatePen(color, thickness, alpha), pulse.Position, radius, radius);
        }
    }

    private void RenderClickSparkDots(DrawingContext drawingContext, Color color, double size, double opacity)
    {
        var lifetime = GetClickLifetime();
        var count = Math.Max(8, (int)Math.Round(GetNumber("particles", 10)));
        var gravity = GetGravityVector();
        var randomness = GetNumber("randomness", 0.0);
        foreach (var pulse in _clickPulses)
        {
            var progress = Math.Clamp(pulse.Age / lifetime, 0, 1);
            var radius = GetNumber("size", 52) * (0.35 + progress * 0.95);
            var alpha = (1 - progress) * opacity;
            for (var index = 0; index < count; index++)
            {
                var angle = (index * ((Math.PI * 2.0) / count)) + (pulse.Seed * 0.9);
                var randomWave = new Vector(
                    Math.Sin((pulse.Seed + index) * 4.1 + (_timeSeconds * 5.7)),
                    Math.Cos((pulse.Seed + index) * 3.6 + (_timeSeconds * 4.9))) * (randomness * (1.0 - progress));
                var point = pulse.Position + new Vector(Math.Cos(angle), Math.Sin(angle)) * radius + (gravity * progress) + randomWave;
                drawingContext.DrawEllipse(CreateSolidBrush(color, alpha * (0.45 + ((index % 3) * 0.1))), null, point, size * 0.42, size * 0.42);
            }
        }
    }

    private void RenderClickSnowBursts(DrawingContext drawingContext, Color primaryColor, Color accentColor, double opacity)
    {
        var lifetime = GetClickLifetime();
        var gravity = GetGravityVector();
        foreach (var pulse in _clickPulses)
        {
            var progress = Math.Clamp(pulse.Age / lifetime, 0, 1);
            var radius = GetNumber("size", 58) * (0.25 + progress * 0.9);
            var alpha = (1 - progress) * opacity;
            for (var index = 0; index < 6; index++)
            {
                var angle = (Math.PI / 3.0) * index;
                var point = pulse.Position + new Vector(Math.Cos(angle), Math.Sin(angle)) * radius + (gravity * progress);
                DrawSnowflake(drawingContext, point, 4.5, index % 2 == 0 ? primaryColor : accentColor, alpha);
            }
        }
    }

    private static void DrawSnowflake(DrawingContext drawingContext, Point center, double size, Color color, double opacity)
    {
        var pen = CreatePen(color, 1.1, opacity);
        for (var index = 0; index < 3; index++)
        {
            var angle = index * (Math.PI / 3.0);
            var direction = new Vector(Math.Cos(angle), Math.Sin(angle));
            drawingContext.DrawLine(pen, center - (direction * size), center + (direction * size));
        }
    }

    private static void DrawRuneMark(DrawingContext drawingContext, Point center, double angle, double size, Color color, double opacity)
    {
        var pen = CreatePen(color, 1.0, opacity);
        drawingContext.PushTransform(new RotateTransform(angle * 180.0 / Math.PI, center.X, center.Y));
        drawingContext.DrawLine(pen, new Point(center.X, center.Y - size), new Point(center.X, center.Y + size));
        drawingContext.DrawLine(pen, new Point(center.X - (size * 0.6), center.Y), new Point(center.X + (size * 0.6), center.Y));
        drawingContext.Pop();
    }

    private static void DrawGlyph(DrawingContext drawingContext, string glyph, Point point, double fontSize, Color color, double opacity)
    {
        var formattedText = new FormattedText(
            glyph,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            fontSize,
            CreateSolidBrush(color, opacity),
            1.0);
        drawingContext.DrawText(formattedText, new Point(point.X - (formattedText.Width * 0.5), point.Y - (formattedText.Height * 0.5)));
    }

    private static string GetMatrixGlyph(int stream, int glyphIndex)
    {
        const string glyphs = "01ZXCVBNMASDFGHJKLQWERTYUIOP";
        var index = Math.Abs((stream * 7) + (glyphIndex * 11)) % glyphs.Length;
        return glyphs[index].ToString();
    }

    private Point GetEmitterPosition()
    {
        var idleRadius = GetNumber("idleRadius", 0);
        if (idleRadius <= 0.01)
        {
            return _emitterPosition;
        }

        var idleSpeed = GetNumber("idleSpeed", 1.0);
        var randomness = GetNumber("randomness", 0.0);
        var gravity = GetGravityVector();
        var idleOffset = new Vector(
            Math.Sin(_timeSeconds * idleSpeed) * idleRadius,
            Math.Cos((_timeSeconds * idleSpeed * 1.37) + (randomness * 0.15)) * idleRadius * 0.65);

        return _emitterPosition + idleOffset + (gravity * 0.18);
    }

    private bool UsesResidualTrail()
    {
        return _template?.Kind is
            TemplateEffectKind.CosmicRift or
            TemplateEffectKind.GlitchFracture or
            TemplateEffectKind.VelvetFlame or
            TemplateEffectKind.SparkShower;
    }

    private bool UsesBackdropSampling()
    {
        return _template?.Kind is TemplateEffectKind.CosmicRift or TemplateEffectKind.GlitchFracture;
    }

    private void UpdateBackdropSample()
    {
        if (!UsesBackdropSampling() || _screenSampler is null)
        {
            _lastBackdropSample = null;
            return;
        }

        var sampleSize = Math.Clamp((int)Math.Round(GetNumber("backdropSize", Math.Max(96, GetNumber("size", 70) * 1.8))), 64, 320);
        _lastBackdropSample = _screenSampler.GetSample(sampleSize, TimeSpan.FromMilliseconds(42));
    }

    private Vector GetGravityVector()
    {
        return new Vector(
            GetNumber("gravityX", 0),
            GetNumber("gravityY", 0));
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

    private double GetClickLifetime()
    {
        return Math.Max(0.2, GetNumber("clickLifetime", GetNumber("motion", 0.85)));
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

    private static RadialGradientBrush CreateRadialBrush(Color color, double opacity, double innerOffset, double outerOffset)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        brush.GradientStops.Add(new GradientStop(WithAlpha(color, opacity), innerOffset));
        brush.GradientStops.Add(new GradientStop(WithAlpha(color, 0), outerOffset));
        brush.Freeze();
        return brush;
    }

    private static ImageBrush CreateImageBrush(ImageSource imageSource, double opacity, double offsetX = 0, double offsetY = 0)
    {
        var brush = new ImageBrush(imageSource)
        {
            Stretch = Stretch.Fill,
            Opacity = Math.Clamp(opacity, 0, 1),
            Transform = Math.Abs(offsetX) > 0.001 || Math.Abs(offsetY) > 0.001
                ? new TranslateTransform(offsetX, offsetY)
                : Transform.Identity
        };
        brush.Freeze();
        return brush;
    }

    private static Rect BuildSampleRect(Point center, double width, double height)
    {
        return new Rect(center.X - (width * 0.5), center.Y - (height * 0.5), width, height);
    }

    private static Color WithAlpha(Color color, double opacity)
    {
        return Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B);
    }

    private static double HashToUnit(double seed, int salt)
    {
        var value = Math.Sin((seed * 12.9898) + (salt * 78.233)) * 43758.5453;
        return value - Math.Floor(value);
    }

    private static double HashToSigned(double seed, int salt)
    {
        return (HashToUnit(seed, salt) * 2.0) - 1.0;
    }

    private struct ClickPulse(Point position)
    {
        public Point Position { get; } = position;

        public double Seed { get; } = (position.X * 0.013) + (position.Y * 0.009);

        public double Age { get; set; }
    }

    private struct MatrixParticle
    {
        public Point Position { get; set; }

        public Vector Velocity { get; set; }

        public double Age { get; set; }

        public double Lifetime { get; set; }

        public string Glyph { get; set; }

        public bool Highlight { get; set; }

        public double Seed { get; set; }
    }

    private struct ResidualNode
    {
        public Point Position { get; set; }

        public Vector Velocity { get; set; }

        public double Age { get; set; }

        public double Lifetime { get; set; }

        public double Seed { get; set; }

        public double Scale { get; set; }
    }
}
