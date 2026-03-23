using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class TrailEffect : IEffect
{
    private readonly List<TrailNode> _nodes = [];
    private readonly List<Point> _renderPoints = [];
    private TrailSettings _settings;
    private double _masterOpacity = 1.0;
    private Point _lastInputPosition;
    private bool _hasInputPosition;
    private Color _baseColor;

    public TrailEffect(TrailSettings settings)
    {
        _settings = Clone(settings);
        IsEnabled = settings.IsEnabled;
    }

    public string Name => "Trail";

    public bool IsEnabled { get; set; }

    public void Update(TimeSpan deltaTime)
    {
        var elapsed = deltaTime.TotalSeconds;
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

        _renderPoints.Clear();
        foreach (var node in _nodes)
        {
            _renderPoints.Add(node.Position);
        }

        for (var index = 1; index < _renderPoints.Count; index++)
        {
            var previous = _nodes[index - 1];
            var current = _nodes[index];
            var lifeRatio = 1d - ((previous.AgeSeconds + current.AgeSeconds) * 0.5 / _settings.FadeSeconds);
            if (lifeRatio <= 0)
            {
                continue;
            }

            var thickness = Math.Max(1, _settings.Thickness * lifeRatio);
            var alpha = (byte)(Math.Clamp(lifeRatio * _masterOpacity, 0, 1) * 255);
            var penBrush = new SolidColorBrush(Color.FromArgb(alpha, _baseColor.R, _baseColor.G, _baseColor.B));
            penBrush.Freeze();
            var pen = new Pen(penBrush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();

            var start = previous.Position;
            var end = current.Position;
            var control = new Point((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.QuadraticBezierTo(control, end, true, true);
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }
    }

    public void OnMouseMove(Point position)
    {
        if (_hasInputPosition)
        {
            var lerp = 0.42;
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
        if (distance < 1.5)
        {
            return;
        }

        var steps = Math.Max(1, (int)(distance / 8));
        for (var step = 1; step <= steps; step++)
        {
            var t = step / (double)steps;
            var interpolated = new Point(
                previous.X + ((position.X - previous.X) * t),
                previous.Y + ((position.Y - previous.Y) * t));

            _nodes.Add(new TrailNode(interpolated));
        }

        while (_nodes.Count > _settings.MaxPoints)
        {
            _nodes.RemoveAt(0);
        }

        _lastInputPosition = position;
        _hasInputPosition = true;
    }

    public void OnMouseClick(Point position)
    {
    }

    public void UpdateSettings(TrailSettings settings, double masterOpacity)
    {
        _settings = Clone(settings);
        _masterOpacity = masterOpacity;
        IsEnabled = settings.IsEnabled;
        _baseColor = ParseColor(_settings.Color);

        while (_nodes.Count > _settings.MaxPoints)
        {
            _nodes.RemoveAt(0);
        }
    }

    private static TrailSettings Clone(TrailSettings settings)
    {
        return new TrailSettings
        {
            IsEnabled = settings.IsEnabled,
            MaxPoints = settings.MaxPoints,
            Thickness = settings.Thickness,
            FadeSeconds = settings.FadeSeconds,
            Color = settings.Color
        };
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
