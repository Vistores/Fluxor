using System.Windows;
using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface IPluginRuntimeContextSink
{
    void UpdateRuntimeContext(
        Point cursorPosition,
        Point rawCursorPosition,
        bool isCursorVisible,
        ScreenSampleFrame? backdropSample,
        CursorVisualSnapshot? cursorSnapshot,
        TimeSpan deltaTime);
}
