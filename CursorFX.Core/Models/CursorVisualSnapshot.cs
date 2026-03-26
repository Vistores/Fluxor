using System.Windows;
using System.Windows.Media.Imaging;

namespace CursorFX.Core.Models;

public sealed class CursorVisualSnapshot
{
    public required BitmapSource Image { get; init; }

    public required Point Hotspot { get; init; }

    public required Size Size { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }
}
