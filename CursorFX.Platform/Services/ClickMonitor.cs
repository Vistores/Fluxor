using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CursorFX.Core.Interfaces;
using CursorFX.Platform.Interop;

namespace CursorFX.Platform.Services;

public sealed class ClickMonitor : IClickMonitor
{
    private readonly NativeMethods.LowLevelMouseProc _hookProc;
    private readonly DispatcherTimer _pollTimer;
    private IntPtr _hookHandle;
    private bool _isStarted;
    private bool _leftButtonDown;
    private bool _rightButtonDown;
    private long _lastEmissionTick;

    public ClickMonitor()
    {
        _hookProc = HookCallback;
        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _pollTimer.Tick += OnPollTick;
    }

    public event EventHandler<System.Windows.Point>? MouseClicked;

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(module?.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _hookProc, moduleHandle, 0);
        _isStarted = _hookHandle != IntPtr.Zero;
        _leftButtonDown = IsButtonDown(NativeMethods.VkLbutton);
        _rightButtonDown = IsButtonDown(NativeMethods.VkRbutton);
        _pollTimer.Start();
    }

    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _isStarted = false;
        _pollTimer.Stop();
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Tick -= OnPollTick;
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam.ToInt32() == NativeMethods.WmLButtonDown || wParam.ToInt32() == NativeMethods.WmRButtonDown))
        {
            var mouseData = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            EmitClick(new System.Windows.Point(mouseData.pt.X, mouseData.pt.Y));
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        var currentLeft = IsButtonDown(NativeMethods.VkLbutton);
        var currentRight = IsButtonDown(NativeMethods.VkRbutton);

        if (currentLeft && !_leftButtonDown)
        {
            EmitClick(GetCurrentCursorPosition());
        }

        if (currentRight && !_rightButtonDown)
        {
            EmitClick(GetCurrentCursorPosition());
        }

        _leftButtonDown = currentLeft;
        _rightButtonDown = currentRight;
    }

    private void EmitClick(System.Windows.Point position)
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastEmissionTick != 0 &&
            Stopwatch.GetElapsedTime(_lastEmissionTick, now) < TimeSpan.FromMilliseconds(50))
        {
            return;
        }

        _lastEmissionTick = now;
        MouseClicked?.Invoke(this, position);
    }

    private static bool IsButtonDown(int virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private static System.Windows.Point GetCurrentCursorPosition()
    {
        return NativeMethods.GetCursorPos(out var point)
            ? new System.Windows.Point(point.X, point.Y)
            : default;
    }
}
