using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using CursorFX.Core.Interfaces;
using CursorFX.Platform.Interop;

namespace CursorFX.Platform.Services;

public sealed class WindowStateMonitor : IWindowStateMonitor
{
    private readonly DispatcherTimer _timer;
    private bool _isFullscreen;
    private bool _areEffectsSuspended;

    public WindowStateMonitor()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += OnTick;
    }

    public event EventHandler<bool>? FullscreenStateChanged;

    public event EventHandler<bool>? EffectsSuspendedChanged;

    public bool IsFullscreen => _isFullscreen;

    public bool AreEffectsSuspended => _areEffectsSuspended;

    public void Start()
    {
        _timer.Start();
        UpdateState();
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
        UpdateState();
    }

    private void UpdateState()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var isDesktopShell = IsDesktopShellWindow(foregroundWindow);
        var isFullscreen = foregroundWindow != IntPtr.Zero && !isDesktopShell && IsWindowFullscreen(foregroundWindow);
        if (isFullscreen != _isFullscreen)
        {
            _isFullscreen = isFullscreen;
            FullscreenStateChanged?.Invoke(this, _isFullscreen);
        }

        var isCursorVisible = IsCursorVisible();
        var shouldSuspendEffects = isFullscreen && !isCursorVisible;
        if (shouldSuspendEffects == _areEffectsSuspended)
        {
            return;
        }

        _areEffectsSuspended = shouldSuspendEffects;
        EffectsSuspendedChanged?.Invoke(this, _areEffectsSuspended);
    }

    private static bool IsWindowFullscreen(IntPtr windowHandle)
    {
        if (!NativeMethods.GetWindowRect(windowHandle, out var windowRect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(windowHandle, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        return windowRect.Left <= monitorInfo.rcMonitor.Left &&
               windowRect.Top <= monitorInfo.rcMonitor.Top &&
               windowRect.Right >= monitorInfo.rcMonitor.Right &&
               windowRect.Bottom >= monitorInfo.rcMonitor.Bottom;
    }

    private static bool IsCursorVisible()
    {
        var cursorInfo = new NativeMethods.CURSORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>()
        };

        return NativeMethods.GetCursorInfo(ref cursorInfo) &&
               (cursorInfo.flags & NativeMethods.CursorShowing) == NativeMethods.CursorShowing;
    }

    private static bool IsDesktopShellWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (windowHandle == NativeMethods.GetShellWindow() || windowHandle == NativeMethods.GetDesktopWindow())
        {
            return true;
        }

        var className = new StringBuilder(256);
        if (NativeMethods.GetClassName(windowHandle, className, className.Capacity) == 0)
        {
            return false;
        }

        return className.ToString() is "Progman" or "WorkerW" or "SHELLDLL_DefView";
    }
}
