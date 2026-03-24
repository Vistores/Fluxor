using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CursorFX.Core.Models;

public sealed class ScreenSampleFrame
{
    public required BitmapSource Image { get; init; }

    public required Rect ScreenBounds { get; init; }

    public required Point CursorScreenPosition { get; init; }

    public required Color AverageColor { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }
}
