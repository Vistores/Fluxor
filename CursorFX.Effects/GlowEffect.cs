using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class GlowEffect : IEffect
{
    private Point _position;
    private RadialGradientBrush _brush = CreateBrush(Colors.Transparent, 0.3);
    private GlowSettings _settings;
    private double _masterOpacity = 1.0;
    private Point _smoothedPosition;
    private double _cursorAttachStrength = 2.0;

    public GlowEffect(GlowSettings settings)
    {
        _settings = Clone(settings);
        IsEnabled = settings.IsEnabled;
        RefreshBrush();
    }

    public string Name => "Glow";

    public bool IsEnabled { get; set; }

    public void Update(TimeSpan deltaTime)
    {
        var lag = (_position - _smoothedPosition).Length;
        var snapDistance = Math.Max(12.0, _settings.Size * (1.1 - Math.Min(0.45, (_cursorAttachStrength - 1.0) * 0.12)));

        if (lag >= snapDistance)
        {
            _smoothedPosition = _position;
            return;
        }

        if (_cursorAttachStrength >= 3.95)
        {
            _smoothedPosition = _position;
            return;
        }

        var followRate = _cursorAttachStrength switch
        {
            >= 3.5 => 28d,
            >= 2.5 => 24d,
            >= 1.5 => 20d,
            _ => 16d
        };
        var blend = Math.Clamp(deltaTime.TotalSeconds * followRate * Math.Max(1.0, _cursorAttachStrength * 0.85), 0d, 1d);
        _smoothedPosition = new Point(
            _smoothedPosition.X + ((_position.X - _smoothedPosition.X) * blend),
            _smoothedPosition.Y + ((_position.Y - _smoothedPosition.Y) * blend));
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!IsEnabled)
        {
            return;
        }

        drawingContext.DrawEllipse(
            _brush,
            null,
            _smoothedPosition,
            _settings.Size * 1.2,
            _settings.Size * 1.2);
    }

    public void OnMouseMove(Point position)
    {
        _position = position;
        if (_smoothedPosition == default)
        {
            _smoothedPosition = position;
        }
    }

    public void OnMouseClick(Point position)
    {
    }

    public void UpdateSettings(GlowSettings settings, double masterOpacity, double cursorAttachStrength = 2.0)
    {
        _settings = Clone(settings);
        _masterOpacity = masterOpacity;
        _cursorAttachStrength = cursorAttachStrength;
        IsEnabled = settings.IsEnabled;
        RefreshBrush();
    }

    private void RefreshBrush()
    {
        var color = (Color)ColorConverter.ConvertFromString(_settings.Color);
        _brush = CreateBrush(color, _settings.Opacity * _masterOpacity);
    }

    private static GlowSettings Clone(GlowSettings settings)
    {
        return new GlowSettings
        {
            IsEnabled = settings.IsEnabled,
            Size = settings.Size,
            Opacity = settings.Opacity,
            Color = settings.Color
        };
    }

    private static RadialGradientBrush CreateBrush(Color color, double opacity)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };

        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        brush.Freeze();
        return brush;
    }
}
