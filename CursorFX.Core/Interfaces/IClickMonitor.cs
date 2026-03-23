using System.Windows;

namespace CursorFX.Core.Interfaces;

public interface IClickMonitor : IDisposable
{
    event EventHandler<Point>? MouseClicked;

    void Start();

    void Stop();
}
