using System.Windows;

namespace CursorFX.Core.Models;

public sealed class PluginRenderContext
{
    public required Point CursorPosition { get; init; }

    public required Point RawCursorPosition { get; init; }

    public required bool IsCursorVisible { get; init; }

    public required TimeSpan DeltaTime { get; init; }

    public CursorVisualSnapshot? CursorSnapshot { get; init; }

    public ScreenSampleFrame? BackdropSample { get; init; }
}
