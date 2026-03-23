using System.Windows.Threading;

namespace CursorFX.Rendering;

public sealed class RenderLoop : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly FrameClock _frameClock = new();

    public RenderLoop(int targetFps)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += OnTick;
        SetTargetFps(targetFps);
    }

    public event EventHandler<TimeSpan>? FrameRendering;

    public int TargetFps { get; private set; }

    public bool IsRunning => _timer.IsEnabled;

    public void SetTargetFps(int targetFps)
    {
        TargetFps = Math.Clamp(targetFps, 30, 144);
        _timer.Interval = TimeSpan.FromSeconds(1d / TargetFps);
    }

    public void Start()
    {
        _frameClock.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        FrameRendering?.Invoke(this, _frameClock.Restart());
    }
}
