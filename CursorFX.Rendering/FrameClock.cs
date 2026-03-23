using System.Diagnostics;

namespace CursorFX.Rendering;

public sealed class FrameClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTicks;

    public TimeSpan Restart()
    {
        var currentTicks = _stopwatch.ElapsedTicks;
        var deltaTicks = currentTicks - _lastTicks;
        _lastTicks = currentTicks;
        return TimeSpan.FromSeconds(deltaTicks / (double)Stopwatch.Frequency);
    }
}
