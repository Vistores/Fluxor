namespace CursorFX.Core.Interfaces;

public interface IWindowStateMonitor : IDisposable
{
    event EventHandler<bool>? FullscreenStateChanged;

    event EventHandler<bool>? EffectsSuspendedChanged;

    bool IsFullscreen { get; }

    bool AreEffectsSuspended { get; }

    void Start();

    void Stop();
}
