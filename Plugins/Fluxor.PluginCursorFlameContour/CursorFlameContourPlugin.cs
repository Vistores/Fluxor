using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace Fluxor.PluginCursorFlameContour;

public sealed class CursorFlameContourPlugin : ICursorEffectPlugin
{
    private const uint CursorShowing = 0x00000001;

    private readonly Dictionary<string, TemplateParameterValue> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private Point _cursorPosition;
    private double _timeSeconds;
    private double _masterOpacity = 1.0;
    private CursorSnapshot? _snapshot;
    private double _cursorRefreshCooldown;

    public string DisplayName => "Cursor Flame Contour";
    public string PluginId => "cursor-flame-contour";
    public string Description => "Uses the live cursor alpha as an opacity mask to render a molten flame contour that sticks directly to the visible cursor.";
    public string IconGlyph => "F";
    public string AccentColor => "#FB923C";
    public TemplateEffectKind Kind => TemplateEffectKind.VelvetFlame;
    public TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    public IReadOnlyList<TemplateParameterDefinition> GetParameters() =>
    [
        Toggle("enabled", "Enable Contour", PluginParameterSection.Shader, "Flame", true),
        ColorParameter("baseColor", "Base Color", PluginParameterSection.Shader, "Flame", "#FF7A18"),
        ColorParameter("coreColor", "Core Color", PluginParameterSection.Shader, "Flame", "#FFE7A1"),
        ColorParameter("edgeColor", "Edge Color", PluginParameterSection.Shader, "Flame", "#C2410C"),
        Number("opacity", "Opacity", PluginParameterSection.Shader, "Flame", 0.05, 1.0, 0.01, 0.88),
        Number("contourScale", "Contour Scale", PluginParameterSection.Shader, "Flame", 1.0, 2.4, 0.05, 1.24),
        Number("waveAmount", "Wave Amount", PluginParameterSection.Shader, "Flame", 0, 24, 0.5, 7),
        Number("waveSpeed", "Wave Speed", PluginParameterSection.Shader, "Flame", 0.2, 8, 0.1, 3.2),
        Number("outlineThickness", "Outline Thickness", PluginParameterSection.Shader, "Flame", 0.5, 18, 0.1, 3.2),
        Number("outlinePasses", "Outline Passes", PluginParameterSection.Shader, "Flame", 4, 24, 1, 12),
        Number("haloSize", "Halo Size", PluginParameterSection.Glow, "Glow", 6, 72, 1, 24),
        Number("haloOpacity", "Halo Opacity", PluginParameterSection.Glow, "Glow", 0.05, 1.0, 0.01, 0.24),
        Number("clickBloom", "Click Bloom", PluginParameterSection.Ripple, "Clicks", 0, 24, 0.5, 8)
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
        _cursorRefreshCooldown -= dt;

        if (_cursorRefreshCooldown <= 0)
        {
            _snapshot?.Dispose();
            _snapshot = CursorSnapshot.Capture();
            _cursorRefreshCooldown = 0.09;
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        if (!GetToggle("enabled", true) || _snapshot is null)
        {
            return;
        }

        var baseColor = GetColor("baseColor", "#FF7A18");
        var coreColor = GetColor("coreColor", "#FFE7A1");
        var edgeColor = GetColor("edgeColor", "#C2410C");
        var opacity = GetNumber("opacity", 0.88) * _masterOpacity;
        var haloSize = GetNumber("haloSize", 24);
        var haloOpacity = GetNumber("haloOpacity", 0.24) * opacity;
        var contourScale = GetNumber("contourScale", 1.24);
        var waveAmount = GetNumber("waveAmount", 7);
        var waveSpeed = GetNumber("waveSpeed", 3.2);
        var outlineThickness = GetNumber("outlineThickness", 3.2);
        var outlinePasses = Math.Clamp((int)Math.Round(GetNumber("outlinePasses", 12)), 4, 24);

        drawingContext.DrawEllipse(CreateRadialBrush(baseColor, haloOpacity, Colors.Transparent), null, _cursorPosition, haloSize, haloSize);

        var phase = _timeSeconds * waveSpeed;
        var outerScale = contourScale + (Math.Sin(phase) * 0.04);
        var midScale = Math.Max(1.0, contourScale * 1.08 + (Math.Cos(phase * 0.87) * 0.025));
        var innerScale = Math.Max(1.0, contourScale * 0.98);

        DrawMaskedStamp(drawingContext, _snapshot, _cursorPosition, outerScale, CreateVerticalGradient(edgeColor, baseColor, opacity * 0.34), Math.Sin(phase * 1.3) * waveAmount * 0.22, -Math.Abs(Math.Cos(phase * 0.9)) * waveAmount * 0.35);
        DrawMaskedStamp(drawingContext, _snapshot, _cursorPosition, midScale, CreateVerticalGradient(baseColor, coreColor, opacity * 0.72), Math.Cos(phase * 0.8) * waveAmount * 0.1, -Math.Abs(Math.Sin(phase * 1.1)) * waveAmount * 0.18);
        DrawMaskedStamp(drawingContext, _snapshot, _cursorPosition, innerScale, CreateVerticalGradient(coreColor, baseColor, opacity * 0.3), 0, 0);

        DrawContourAura(drawingContext, _snapshot, _cursorPosition, contourScale, edgeColor, outlineThickness, outlinePasses, opacity * 0.42, phase, waveAmount);
        DrawContourAura(drawingContext, _snapshot, _cursorPosition, contourScale * 0.98, coreColor, Math.Max(0.6, outlineThickness * 0.55), Math.Max(6, outlinePasses / 2), opacity * 0.36, phase * 0.85, waveAmount * 0.45);
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
    }

    public void OnMouseClick(Point position)
    {
        _timeSeconds += GetNumber("clickBloom", 8) * 0.02;
    }

    public void Dispose()
    {
        _snapshot?.Dispose();
    }

    private static void DrawMaskedStamp(DrawingContext drawingContext, CursorSnapshot snapshot, Point cursorPoint, double scale, Brush brush, double offsetX, double offsetY)
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

    private static void DrawContourAura(DrawingContext drawingContext, CursorSnapshot snapshot, Point cursorPoint, double scale, Color color, double thickness, int passCount, double opacity, double phase, double waveAmount)
    {
        for (var index = 0; index < passCount; index++)
        {
            var t = index / (double)passCount;
            var angle = (Math.PI * 2.0 * t) + phase * 0.35;
            var radius = thickness * (0.8 + Math.Sin(phase + index * 0.7) * 0.18);
            var offsetX = Math.Cos(angle) * radius;
            var offsetY = Math.Sin(angle) * radius - Math.Abs(Math.Cos(angle * 1.4 + phase)) * waveAmount * 0.08;
            DrawMaskedStamp(
                drawingContext,
                snapshot,
                cursorPoint,
                scale,
                CreateSolidBrush(color, opacity / passCount * 2.2),
                offsetX,
                offsetY);
        }
    }

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

    private static Brush CreateVerticalGradient(Color top, Color bottom, double opacity)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        var alpha = (byte)(Math.Clamp(opacity, 0, 1) * 255);
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, top.R, top.G, top.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * 0.92, 0, 255), bottom.R, bottom.G, bottom.B), 0.45));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * 0.2, 0, 255), bottom.R, bottom.G, bottom.B), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateSolidBrush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private sealed class CursorSnapshot : IDisposable
    {
        public required BitmapSource Image { get; init; }
        public required Point Hotspot { get; init; }
        public required Size Size { get; init; }
        public IntPtr IconHandle { get; init; }

        public static CursorSnapshot? Capture()
        {
            var cursorInfo = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (!GetCursorInfo(ref cursorInfo) || cursorInfo.flags != CursorShowing || cursorInfo.hCursor == IntPtr.Zero)
            {
                return null;
            }

            var copiedIcon = CopyIcon(cursorInfo.hCursor);
            if (copiedIcon == IntPtr.Zero)
            {
                return null;
            }

            if (!GetIconInfo(copiedIcon, out var iconInfo))
            {
                DestroyIcon(copiedIcon);
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(copiedIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();

                return new CursorSnapshot
                {
                    Image = source,
                    Hotspot = new Point(iconInfo.xHotspot, iconInfo.yHotspot),
                    Size = new Size(source.PixelWidth, source.PixelHeight),
                    IconHandle = copiedIcon
                };
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmColor);
                }

                if (iconInfo.hbmMask != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmMask);
                }
            }
        }

        public void Dispose()
        {
            if (IconHandle != IntPtr.Zero)
            {
                DestroyIcon(IconHandle);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
