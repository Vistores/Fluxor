using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginHeatContour;

public sealed class HeatContourPlugin : ICursorEffectPlugin
{
    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private Point _cursorPosition;
    private Point _rawCursorPosition;
    private bool _isCursorVisible = true;
    private double _timeSeconds;
    private double _masterOpacity = 1.0;
    private CursorVisualSnapshot? _cursorSnapshot;
    private ScreenSampleFrame? _backdropSample;

    public string DisplayName => "Heat Contour";
    public string PluginId => "heat-contour";
    public string Description => "A cursor-locked alpha contour with backdrop heat distortion that bends the visible image around the cursor.";
    public string IconGlyph => "H";
    public string AccentColor => "#F59E0B";
    public TemplateEffectKind Kind => TemplateEffectKind.VelvetFlame;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Heat Contour", PluginParameterSection.Shader, "Contour", true),
        ColorParameter("edgeColor", "Edge Color", PluginParameterSection.Shader, "Contour", "#FF8A36"),
        ColorParameter("coreColor", "Core Glow", PluginParameterSection.Shader, "Contour", "#FFE5B4"),
        ColorParameter("heatColor", "Heat Tint", PluginParameterSection.Shader, "Heat", "#FFC57A"),
        Number("opacity", "Opacity", PluginParameterSection.Shader, "Contour", 0.05, 1.0, 0.01, 0.9),
        Number("outlineThickness", "Outline Thickness", PluginParameterSection.Shader, "Contour", 0.5, 18, 0.1, 2.2),
        Number("outlinePasses", "Outline Passes", PluginParameterSection.Shader, "Contour", 4, 20, 1, 9, isAdvanced: true),
        Number("haloSize", "Halo Size", PluginParameterSection.Glow, "Glow", 4, 72, 1, 18),
        Number("haloOpacity", "Halo Opacity", PluginParameterSection.Glow, "Glow", 0.05, 1.0, 0.01, 0.22),
        Number("distortionRadius", "Distortion Radius", PluginParameterSection.Shader, "Heat", 12, 140, 1, 34),
        Number("distortionStrength", "Distortion Strength", PluginParameterSection.Shader, "Heat", 0.2, 20, 0.1, 3.8),
        Number("distortionLayers", "Distortion Layers", PluginParameterSection.Shader, "Heat", 1, 8, 1, 4, isAdvanced: true),
        Number("distortionOpacity", "Distortion Opacity", PluginParameterSection.Shader, "Heat", 0.03, 1.0, 0.01, 0.16, isAdvanced: true),
        Number("heatSpeed", "Heat Speed", PluginParameterSection.Shader, "Heat", 0.2, 8, 0.1, 2.4, isAdvanced: true)
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
        _rawCursorPosition = context.RawCursorPosition;
        _isCursorVisible = context.IsCursorVisible;
        _cursorSnapshot = context.CursorSnapshot;
        _backdropSample = context.BackdropSample;
        _timeSeconds += Math.Clamp(context.DeltaTime.TotalSeconds, 0.0, 0.05);
    }

    public void Render(PluginRenderContext context, DrawingContext drawingContext)
    {
        if (!_isCursorVisible || !GetToggle("enabled", true) || _cursorSnapshot is null)
        {
            return;
        }

        var edgeColor = GetColor("edgeColor", "#FF8A36");
        var coreColor = GetColor("coreColor", "#FFE5B4");
        var heatColor = GetColor("heatColor", "#FFC57A");
        var opacity = GetNumber("opacity", 0.9) * _masterOpacity;
        var haloSize = GetNumber("haloSize", 18);
        var haloOpacity = GetNumber("haloOpacity", 0.22) * opacity;
        var outlineThickness = GetNumber("outlineThickness", 2.2);
        var outlinePasses = Math.Clamp((int)Math.Round(GetNumber("outlinePasses", 9)), 4, 20);
        var distortionRadius = GetNumber("distortionRadius", 34);
        var distortionStrength = GetNumber("distortionStrength", 3.8);
        var distortionLayers = Math.Clamp((int)Math.Round(GetNumber("distortionLayers", 4)), 1, 8);
        var distortionOpacity = GetNumber("distortionOpacity", 0.16) * opacity;
        var heatSpeed = GetNumber("heatSpeed", 2.4);

        if (_backdropSample is not null)
        {
            DrawBackdropDistortion(
                drawingContext,
                _backdropSample,
                _cursorPosition,
                _rawCursorPosition,
                distortionRadius,
                distortionStrength,
                distortionLayers,
                distortionOpacity,
                _timeSeconds * heatSpeed);
        }

        drawingContext.DrawEllipse(CreateRadialBrush(edgeColor, haloOpacity, Colors.Transparent), null, _cursorPosition, haloSize, haloSize);
        DrawMaskedStamp(drawingContext, _cursorSnapshot, _cursorPosition, 1.0, CreateSolidBrush(coreColor, opacity * 0.16), 0, 0);
        DrawContourAura(drawingContext, _cursorSnapshot, _cursorPosition, edgeColor, outlineThickness, outlinePasses, opacity * 0.55, _timeSeconds * heatSpeed, distortionStrength);
        DrawContourAura(drawingContext, _cursorSnapshot, _cursorPosition, heatColor, Math.Max(0.6, outlineThickness * 0.6), Math.Max(4, outlinePasses / 2), opacity * 0.24, _timeSeconds * heatSpeed * 0.75, distortionStrength * 0.65);
    }

    public void OnMouseMove(PluginRenderContext context, Point position)
    {
        _cursorPosition = context.CursorPosition;
        _rawCursorPosition = context.RawCursorPosition;
    }

    public void OnMouseClick(PluginRenderContext context, Point position)
    {
        _timeSeconds += 0.18;
    }

    public void Render(DrawingContext drawingContext)
    {
    }

    public void OnMouseMove(Point position)
    {
    }

    public void OnMouseClick(Point position)
    {
    }

    public void Dispose()
    {
    }

    private static void DrawBackdropDistortion(
        DrawingContext drawingContext,
        ScreenSampleFrame sample,
        Point cursorPoint,
        Point rawCursorPosition,
        double radius,
        double strength,
        int layers,
        double opacity,
        double phase)
    {
        var left = cursorPoint.X + (sample.ScreenBounds.Left - rawCursorPosition.X);
        var top = cursorPoint.Y + (sample.ScreenBounds.Top - rawCursorPosition.Y);
        var destinationRect = new Rect(left, top, sample.ScreenBounds.Width, sample.ScreenBounds.Height);
        var center = new Point(
            (cursorPoint.X - destinationRect.X) / Math.Max(1, destinationRect.Width),
            (cursorPoint.Y - destinationRect.Y) / Math.Max(1, destinationRect.Height));

        for (var layer = 0; layer < layers; layer++)
        {
            var layerPhase = phase + layer * 0.7;
            var layerStrength = strength * (0.65 + layer * 0.12);
            var offsetX = Math.Sin(layerPhase * 1.35 + layer) * layerStrength;
            var offsetY = Math.Cos(layerPhase * 0.9 + layer * 1.4) * layerStrength * 0.7;
            var scale = 1.0 + (Math.Sin(layerPhase * 0.7 + layer * 0.5) * 0.012);

            drawingContext.PushOpacity(Math.Max(0.02, opacity * (1.0 - layer * 0.16)));
            drawingContext.PushOpacityMask(CreateHeatMaskBrush(radius, layer, center));
            drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
            drawingContext.PushTransform(new ScaleTransform(scale, scale, cursorPoint.X, cursorPoint.Y));
            drawingContext.DrawRectangle(CreateBackdropBrush(sample), null, destinationRect);
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }

    private static void DrawMaskedStamp(DrawingContext drawingContext, CursorVisualSnapshot snapshot, Point cursorPoint, double scale, Brush brush, double offsetX, double offsetY)
    {
        var width = snapshot.Size.Width * scale;
        var height = snapshot.Size.Height * scale;
        var originX = cursorPoint.X - snapshot.Hotspot.X * scale + offsetX;
        var originY = cursorPoint.Y - snapshot.Hotspot.Y * scale + offsetY;
        var rect = new Rect(originX, originY, width, height);

        drawingContext.PushTransform(new TranslateTransform(rect.X, rect.Y));
        drawingContext.PushOpacityMask(new ImageBrush(snapshot.Image) { Stretch = Stretch.Fill });
        drawingContext.DrawRectangle(brush, null, new Rect(0, 0, rect.Width, rect.Height));
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static void DrawContourAura(DrawingContext drawingContext, CursorVisualSnapshot snapshot, Point cursorPoint, Color color, double thickness, int passCount, double opacity, double phase, double shimmerStrength)
    {
        for (var index = 0; index < passCount; index++)
        {
            var t = index / (double)passCount;
            var angle = (Math.PI * 2.0 * t) + phase * 0.3;
            var radius = thickness * (0.88 + Math.Sin(phase + index * 0.6) * 0.12);
            var offsetX = Math.Cos(angle) * radius + Math.Sin(phase * 1.2 + index) * shimmerStrength * 0.06;
            var offsetY = Math.Sin(angle) * radius + Math.Cos(phase * 0.85 + index * 0.7) * shimmerStrength * 0.05;
            DrawMaskedStamp(
                drawingContext,
                snapshot,
                cursorPoint,
                1.0,
                CreateSolidBrush(color, opacity / passCount * 2.25),
                offsetX,
                offsetY);
        }
    }

    private static ImageBrush CreateBackdropBrush(ScreenSampleFrame sample)
    {
        var brush = new ImageBrush(sample.Image)
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
        brush.Freeze();
        return brush;
    }

    private static RadialGradientBrush CreateHeatMaskBrush(double radius, int layer, Point center)
    {
        var normalizedRadius = Math.Clamp(radius / 96.0, 0.18, 0.85);
        var brush = new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = normalizedRadius + layer * 0.03,
            RadiusY = normalizedRadius + layer * 0.02
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(188, 255, 255, 255), 0.35));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        brush.Freeze();
        return brush;
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

    private static Brush CreateRadialBrush(Color center, double opacity, Color edge)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), center.R, center.G, center.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, edge.R, edge.G, edge.B), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateSolidBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
