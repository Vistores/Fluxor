using System.Windows;
using System.Windows.Threading;
using CursorFX.Core.Interfaces;
using CursorFX.Platform.Interop;

namespace CursorFX.Platform.Services;

public sealed class MouseTracker : IMouseTracker
{
    private readonly DispatcherTimer _timer;
    private System.Windows.Point _currentPosition;

    public MouseTracker()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(8)
        };
        _timer.Tick += OnTick;
        _currentPosition = GetCursorPosition();
    }

    public event EventHandler<System.Windows.Point>? MouseMoved;

    public System.Windows.Point CurrentPosition => _currentPosition;

    public void Start()
    {
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
        var position = GetCursorPosition();
        if (position == _currentPosition)
        {
            return;
        }

        _currentPosition = position;
        MouseMoved?.Invoke(this, position);
    }

    private static System.Windows.Point GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return new System.Windows.Point(point.X, point.Y);
    }
}
