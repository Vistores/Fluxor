using System.Windows;

namespace CursorFX.Core.Interfaces;

public interface IMouseTracker : IDisposable
{
    event EventHandler<Point>? MouseMoved;

    Point CurrentPosition { get; }

    void Start();

    void Stop();
}
