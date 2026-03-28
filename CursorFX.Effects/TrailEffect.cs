using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class TrailEffect : IEffect
{
    private readonly List<TrailNode> _nodes = [];
    private readonly List<Point> _leftEdge = [];
    private readonly List<Point> _rightEdge = [];
    private TrailSettings _settings;
    private double _masterOpacity = 1.0;
    private EffectQualityPreset _qualityPreset = EffectQualityPreset.Balanced;
    private Point _lastInputPosition;
    private Point _latestRawPosition;
    private bool _hasInputPosition;
    private bool _hasRawPosition;
    private Color _baseColor;
    private double _timeSeconds;

    public TrailEffect(TrailSettings settings)
    {
        _settings = Clone(settings);
        _baseColor = ParseColor(_settings.Color);
        IsEnabled = settings.IsEnabled;
    }

    public string Name => "Trail";

    public bool IsEnabled { get; set; }

    public void Update(TimeSpan deltaTime)
    {
        var elapsed = deltaTime.TotalSeconds;
        _timeSeconds += elapsed;

        for (var index = _nodes.Count - 1; index >= 0; index--)
        {
            var node = _nodes[index];
            node.AgeSeconds += elapsed;
            if (node.AgeSeconds >= _settings.FadeSeconds)
            {
                _nodes.RemoveAt(index);
                continue;
            }

            _nodes[index] = node;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!IsEnabled || _nodes.Count < 2)
        {
            return;
        }

        switch (_settings.RenderMode)
        {
            case TrailRenderMode.WaveRibbon:
                RenderRibbon(drawingContext, addNoise: false);
                break;
            case TrailRenderMode.TornRibbon:
                RenderRibbon(drawingContext, addNoise: true);
                break;
            default:
                RenderSmoothLine(drawingContext);
                break;
        }
    }

    public void OnMouseMove(Point position)
    {
        var rawPosition = position;
        _latestRawPosition = rawPosition;
        _hasRawPosition = true;

        if (_hasInputPosition)
        {
            var rawDistance = (position - _lastInputPosition).Length;
            var lerp = rawDistance switch
            {
                >= 220 => 1.0,
                >= 140 => 0.9,
                >= 80 => 0.76,
                >= 36 => 0.58,
                _ => 0.42
            };
            position = new Point(
                _lastInputPosition.X + ((position.X - _lastInputPosition.X) * lerp),
                _lastInputPosition.Y + ((position.Y - _lastInputPosition.Y) * lerp));
        }

        if (_nodes.Count == 0)
        {
            _nodes.Add(new TrailNode(position));
            _lastInputPosition = position;
            _hasInputPosition = true;
            return;
        }

        var previous = _nodes[^1].Position;
        var distance = (position - previous).Length;
        if (distance < 1.2)
        {
            _lastInputPosition = position;
            _hasInputPosition = true;
            return;
        }

        var segmentSpacing = distance >= 180 ? 6 : distance >= 100 ? 5 : 4;
        var steps = Math.Max(1, (int)(distance / segmentSpacing));
        for (var step = 1; step <= steps; step++)
        {
            var t = step / (double)steps;
            var interpolated = new Point(
                previous.X + ((position.X - previous.X) * t),
                previous.Y + ((position.Y - previous.Y) * t));

            _nodes.Add(new TrailNode(interpolated));
        }

        while (_nodes.Count > GetMaxTrailPoints())
        {
            _nodes.RemoveAt(0);
        }

        if (_nodes.Count >= 2)
        {
            _nodes[^1] = new TrailNode(position);
        }

        _lastInputPosition = position;
        _hasInputPosition = true;
    }

    public void OnMouseClick(Point position)
    {
    }

    public void UpdateSettings(TrailSettings settings, double masterOpacity, EffectQualityPreset qualityPreset = EffectQualityPreset.Balanced)
    {
        _settings = Clone(settings);
        _masterOpacity = masterOpacity;
        _qualityPreset = qualityPreset;
        IsEnabled = settings.IsEnabled;
        _baseColor = ParseColor(_settings.Color);

        while (_nodes.Count > GetMaxTrailPoints())
        {
            _nodes.RemoveAt(0);
        }
    }

    private int GetMaxTrailPoints()
    {
        var cap = _qualityPreset switch
        {
            EffectQualityPreset.Low => 18,
            EffectQualityPreset.Balanced => 32,
            EffectQualityPreset.High => 48,
            _ => 32
        };

        return Math.Min(_settings.MaxPoints, cap);
    }

    private void RenderSmoothLine(DrawingContext drawingContext)
    {
        for (var index = 1; index < _nodes.Count; index++)
        {
            var previous = _nodes[index - 1];
            var current = _nodes[index];
            var lifeRatio = 1d - ((previous.AgeSeconds + current.AgeSeconds) * 0.5 / _settings.FadeSeconds);
            if (lifeRatio <= 0)
            {
                continue;
            }

            var thickness = Math.Max(0.85, _settings.Thickness * lifeRatio);
            var alpha = Math.Clamp(lifeRatio * _masterOpacity, 0, 1);
            var glowThickness = Math.Max(1.1, thickness * 1.28);
            var pen = CreatePen(_baseColor, thickness, alpha);
            var glowPen = CreatePen(_baseColor, glowThickness, alpha * 0.22);

            var start = previous.Position;
            var end = current.Position;
            var control = new Point((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5);
            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            context.BeginFigure(start, false, false);
            context.QuadraticBezierTo(control, end, true, true);
            geometry.Freeze();
            drawingContext.DrawGeometry(null, glowPen, geometry);
            drawingContext.DrawGeometry(null, pen, geometry);
        }

        RenderHeadConnector(drawingContext);
    }

    private void RenderRibbon(DrawingContext drawingContext, bool addNoise)
    {
        if (_nodes.Count < 3)
        {
            RenderSmoothLine(drawingContext);
            return;
        }

        _leftEdge.Clear();
        _rightEdge.Clear();

        var pointCount = _nodes.Count;
        for (var index = 0; index < pointCount; index++)
        {
            var node = _nodes[index];
            var ageRatio = Math.Clamp(node.AgeSeconds / Math.Max(0.001, _settings.FadeSeconds), 0, 1);
            var strength = 1.0 - ageRatio;
            if (strength <= 0.01)
            {
                continue;
            }

            var progress = pointCount <= 1 ? 0.0 : index / (double)(pointCount - 1);
            var tangent = GetTangent(index);
            if (tangent.LengthSquared < 0.0001)
            {
                continue;
            }

            tangent.Normalize();
            var normal = new Vector(-tangent.Y, tangent.X);
            var baseWidth = Math.Max(1.6, _settings.Thickness * strength);
            var widthScale = 0.78 + (_settings.RibbonSoftness * 0.65);
            var width = baseWidth * widthScale;

            var phase = (_timeSeconds * (2.0 + _settings.WaveFrequency)) + (progress * Math.PI * 2.0 * Math.Max(0.35, _settings.WaveFrequency));
            var envelope = Math.Sin(progress * Math.PI);
            var wave = Math.Sin(phase) * _settings.WaveAmplitude * envelope * strength;

            var noise = 0.0;
            if (addNoise && _settings.NoiseAmount > 0.01)
            {
                var jitterPhase = (progress * 17.0) + (_timeSeconds * 3.3);
                noise = (Math.Sin(jitterPhase) + (Math.Cos(jitterPhase * 1.73) * 0.65)) * _settings.NoiseAmount * strength;
            }

            var center = node.Position + (normal * (wave * 0.32));
            var leftOffset = width + wave + noise;
            var rightOffset = width - wave - (noise * 0.75);

            _leftEdge.Add(center + (normal * leftOffset));
            _rightEdge.Add(center - (normal * rightOffset));
        }

        if (_leftEdge.Count < 2 || _rightEdge.Count < 2)
        {
            RenderSmoothLine(drawingContext);
            return;
        }

        var ribbonGeometry = new StreamGeometry();
        using (var context = ribbonGeometry.Open())
        {
            context.BeginFigure(_leftEdge[0], true, true);
            for (var index = 1; index < _leftEdge.Count; index++)
            {
                context.LineTo(_leftEdge[index], true, false);
            }

            for (var index = _rightEdge.Count - 1; index >= 0; index--)
            {
                context.LineTo(_rightEdge[index], true, false);
            }
        }
        ribbonGeometry.Freeze();

        var edgeOpacity = Math.Clamp(_masterOpacity * 0.85, 0, 1);
        var fillOpacity = Math.Clamp(_masterOpacity * 0.26, 0, 1);
        drawingContext.DrawGeometry(CreateFillBrush(_baseColor, fillOpacity), null, ribbonGeometry);
        drawingContext.DrawGeometry(null, CreatePen(_baseColor, Math.Max(1.1, _settings.Thickness * 0.16), edgeOpacity), ribbonGeometry);
        RenderHeadConnector(drawingContext);
    }

    private void RenderHeadConnector(DrawingContext drawingContext)
    {
        if (!_hasRawPosition || _nodes.Count == 0)
        {
            return;
        }

        var lastNode = _nodes[^1];
        var headDistance = (_latestRawPosition - lastNode.Position).Length;
        if (headDistance < 1.5)
        {
            return;
        }

        var connectorAlpha = Math.Clamp((_masterOpacity * 0.82) * Math.Min(1.0, headDistance / 28.0), 0, 1);
        var connectorThickness = Math.Max(0.9, _settings.Thickness * 0.74);
        var connectorGlowThickness = Math.Max(1.2, connectorThickness * 1.32);
        var start = lastNode.Position;
        var end = _latestRawPosition;
        var control = new Point(
            start.X + ((end.X - start.X) * 0.72),
            start.Y + ((end.Y - start.Y) * 0.72));

        var connector = new StreamGeometry();
        using (var context = connector.Open())
        {
            context.BeginFigure(start, false, false);
            context.QuadraticBezierTo(control, end, true, true);
        }
        connector.Freeze();
        drawingContext.DrawGeometry(null, CreatePen(_baseColor, connectorGlowThickness, connectorAlpha * 0.22), connector);
        drawingContext.DrawGeometry(null, CreatePen(_baseColor, connectorThickness, connectorAlpha), connector);
    }

    private Vector GetTangent(int index)
    {
        var previousIndex = Math.Max(0, index - 1);
        var nextIndex = Math.Min(_nodes.Count - 1, index + 1);
        return _nodes[nextIndex].Position - _nodes[previousIndex].Position;
    }

    private static TrailSettings Clone(TrailSettings settings)
    {
        return new TrailSettings
        {
            IsEnabled = settings.IsEnabled,
            MaxPoints = settings.MaxPoints,
            Thickness = settings.Thickness,
            FadeSeconds = settings.FadeSeconds,
            Color = settings.Color,
            RenderMode = settings.RenderMode,
            WaveAmplitude = settings.WaveAmplitude,
            WaveFrequency = settings.WaveFrequency,
            NoiseAmount = settings.NoiseAmount,
            RibbonSoftness = settings.RibbonSoftness
        };
    }

    private static Pen CreatePen(Color color, double thickness, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static Brush CreateFillBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string value)
    {
        return (Color)ColorConverter.ConvertFromString(value);
    }

    private struct TrailNode(Point position)
    {
        public Point Position { get; } = position;

        public double AgeSeconds { get; set; }
    }
}
