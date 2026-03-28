using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorFX.Effects;

public sealed partial class TemplateEffect
{
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

    private static void DrawCrossArm(DrawingContext drawingContext, Point center, double angle, double radius, double detail, Color primaryColor, Color accentColor, double opacity)
    {
        var direction = new Vector(Math.Cos(angle), Math.Sin(angle));
        var lengthA = radius * 0.78;
        var lengthB = radius * 1.08;
        var start = center - (direction * lengthA);
        var end = center + (direction * lengthB);
        drawingContext.DrawLine(CreatePen(primaryColor, 1.2 + (detail * 0.08), opacity * 0.82), start, end);
        drawingContext.DrawLine(CreatePen(accentColor, 0.7 + (detail * 0.04), opacity), center - (direction * (lengthA * 0.54)), center + (direction * (lengthB * 0.76)));
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

    private static StreamGeometry? BuildResidualRibbonGeometry(
        IReadOnlyList<ResidualNode> nodes,
        Func<ResidualNode, double, double> widthSelector,
        Func<ResidualNode, double, double> waveSelector)
    {
        if (nodes.Count < 2)
        {
            return null;
        }

        var leftPoints = new List<Point>(nodes.Count);
        var rightPoints = new List<Point>(nodes.Count);

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var t = nodes.Count == 1 ? 0.0 : index / (double)(nodes.Count - 1);

            Vector tangent;
            if (index == 0)
            {
                tangent = nodes[index + 1].Position - node.Position;
            }
            else if (index == nodes.Count - 1)
            {
                tangent = node.Position - nodes[index - 1].Position;
            }
            else
            {
                tangent = nodes[index + 1].Position - nodes[index - 1].Position;
            }

            if (tangent.LengthSquared <= 0.0001)
            {
                tangent = new Vector(0, -1);
            }

            tangent.Normalize();
            var normal = new Vector(-tangent.Y, tangent.X);
            var width = Math.Max(1.0, widthSelector(node, t));
            var wave = waveSelector(node, t);
            var center = node.Position + (normal * wave);
            leftPoints.Add(center + (normal * width));
            rightPoints.Add(center - (normal * width));
        }

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(leftPoints[0], true, true);
        for (var index = 1; index < leftPoints.Count; index++)
        {
            context.LineTo(leftPoints[index], true, false);
        }

        for (var index = rightPoints.Count - 1; index >= 0; index--)
        {
            context.LineTo(rightPoints[index], true, false);
        }

        geometry.Freeze();
        return geometry;
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

        public BitmapSource? BackdropImage { get; set; }

        public Rect BackdropBounds { get; set; }
    }
}
