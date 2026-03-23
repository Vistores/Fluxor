using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CursorFX.Core.Services;
using CursorFX.Platform.Interop;
using Microsoft.Win32;

namespace CursorFX.Rendering;

public sealed class OverlayWindow : IDisposable
{
    private readonly EffectManager _effectManager;
    private readonly Dictionary<string, OverlayViewportWindow> _viewports = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MonitorLayout> _monitorLayouts = Array.Empty<MonitorLayout>();
    private bool _isShown;
    private Visibility _visibility = Visibility.Visible;

    public OverlayWindow(EffectManager effectManager)
    {
        _effectManager = effectManager;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        RefreshBounds();
    }

    public Visibility Visibility
    {
        get => _visibility;
        set
        {
            _visibility = value;
            foreach (var viewport in _viewports.Values)
            {
                viewport.Visibility = value;
            }
        }
    }

    public void Show()
    {
        _isShown = true;
        RefreshBounds();
        foreach (var viewport in _viewports.Values)
        {
            if (!viewport.IsVisible)
            {
                viewport.Show();
            }

            viewport.Visibility = _visibility;
            viewport.EnsureTopmost();
        }
    }

    public void Close()
    {
        foreach (var viewport in _viewports.Values.ToList())
        {
            viewport.Close();
        }

        _viewports.Clear();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Close();
    }

    public void InvalidateSurface()
    {
        foreach (var viewport in _viewports.Values)
        {
            viewport.InvalidateSurface();
        }
    }

    public void RefreshBounds()
    {
        _monitorLayouts = BuildMonitorLayouts();
        var activeIds = new HashSet<string>(_monitorLayouts.Select(layout => layout.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var stale in _viewports.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            _viewports[stale].Close();
            _viewports.Remove(stale);
        }

        foreach (var layout in _monitorLayouts)
        {
            if (!_viewports.TryGetValue(layout.Id, out var viewport))
            {
                viewport = new OverlayViewportWindow(_effectManager);
                _viewports.Add(layout.Id, viewport);
                if (_isShown)
                {
                    viewport.Show();
                }
            }

            viewport.UpdateLayout(layout, _visibility);
        }
    }

    public Point ScreenToOverlay(Point screenPixels)
    {
        var layout = FindLayout(screenPixels);
        if (layout is null)
        {
            return screenPixels;
        }

        if (_viewports.TryGetValue(layout.Id, out var viewport))
        {
            var local = viewport.PointFromScreenSafe(screenPixels);
            if (local.HasValue)
            {
                return new Point(
                    layout.LogicalBounds.Left + local.Value.X,
                    layout.LogicalBounds.Top + local.Value.Y);
            }
        }

        return new Point(
            layout.LogicalBounds.Left + ((screenPixels.X - layout.PhysicalBounds.Left) * 96.0 / layout.DpiX),
            layout.LogicalBounds.Top + ((screenPixels.Y - layout.PhysicalBounds.Top) * 96.0 / layout.DpiY));
    }

    private MonitorLayout? FindLayout(Point screenPixels)
    {
        foreach (var layout in _monitorLayouts)
        {
            if (layout.PhysicalBounds.Contains(screenPixels))
            {
                return layout;
            }
        }

        return _monitorLayouts
            .OrderBy(layout => DistanceSquared(layout.PhysicalBounds, screenPixels))
            .FirstOrDefault();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(RefreshBounds);
    }

    private static double DistanceSquared(Rect bounds, Point point)
    {
        var dx = point.X < bounds.Left ? bounds.Left - point.X : point.X > bounds.Right ? point.X - bounds.Right : 0.0;
        var dy = point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0.0;
        return (dx * dx) + (dy * dy);
    }

    private static IReadOnlyList<MonitorLayout> BuildMonitorLayouts()
    {
        var layouts = new List<MonitorLayout>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var monitorInfo = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
            {
                return true;
            }

            var dpiX = 96.0;
            var dpiY = 96.0;
            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var rawDpiX, out var rawDpiY) == 0)
            {
                dpiX = rawDpiX;
                dpiY = rawDpiY;
            }

            var physical = new Rect(
                monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Top,
                monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top);

            var logical = new Rect(
                physical.Left * 96.0 / dpiX,
                physical.Top * 96.0 / dpiY,
                physical.Width * 96.0 / dpiX,
                physical.Height * 96.0 / dpiY);

            layouts.Add(new MonitorLayout(
                $"monitor-{physical.Left}-{physical.Top}-{physical.Width}-{physical.Height}",
                physical,
                logical,
                dpiX,
                dpiY,
                (monitorInfo.dwFlags & NativeMethods.MonitorinfofPrimary) == NativeMethods.MonitorinfofPrimary));
            return true;
        }, IntPtr.Zero);

        return layouts;
    }

    private sealed record MonitorLayout(string Id, Rect PhysicalBounds, Rect LogicalBounds, double DpiX, double DpiY, bool IsPrimary);

    private sealed class OverlayViewportWindow : Window
    {
        private readonly RenderSurface _renderSurface;
        private Rect _pendingPhysicalBounds;
        private bool _sourceReady;

        public OverlayViewportWindow(EffectManager effectManager)
        {
            _renderSurface = new RenderSurface
            {
                EffectManager = effectManager,
                IsHitTestVisible = false
            };

            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            Content = _renderSurface;
            SourceInitialized += OnSourceInitialized;
        }

        public void UpdateLayout(MonitorLayout layout, Visibility visibility)
        {
            _renderSurface.RenderOffset = new Vector(layout.LogicalBounds.Left, layout.LogicalBounds.Top);
            Width = layout.PhysicalBounds.Width * 96.0 / layout.DpiX;
            Height = layout.PhysicalBounds.Height * 96.0 / layout.DpiY;
            Left = layout.LogicalBounds.Left;
            Top = layout.LogicalBounds.Top;
            _pendingPhysicalBounds = layout.PhysicalBounds;
            Visibility = visibility;
            ApplyPhysicalPlacement();
            EnsureTopmost();
            _renderSurface.InvalidateVisual();
        }

        public void InvalidateSurface()
        {
            _renderSurface.InvalidateVisual();
        }

        public void EnsureTopmost()
        {
            Topmost = false;
            Topmost = true;
        }

        public Point? PointFromScreenSafe(Point screenPixels)
        {
            if (!_sourceReady)
            {
                return null;
            }

            return PointFromScreen(screenPixels);
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is not HwndSource source)
            {
                return;
            }

            _sourceReady = true;
            var hwnd = source.Handle;
            var currentStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
            var newStyle = currentStyle.ToInt64() |
                           NativeMethods.WsExTransparent |
                           NativeMethods.WsExToolWindow |
                           NativeMethods.WsExNoActivate;

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(newStyle));
            ApplyPhysicalPlacement();
            EnsureTopmost();
        }

        private void ApplyPhysicalPlacement()
        {
            if (!_sourceReady || PresentationSource.FromVisual(this) is not HwndSource source)
            {
                return;
            }

            NativeMethods.SetWindowPos(
                source.Handle,
                NativeMethods.HwndTopmost,
                (int)Math.Round(_pendingPhysicalBounds.Left),
                (int)Math.Round(_pendingPhysicalBounds.Top),
                (int)Math.Round(_pendingPhysicalBounds.Width),
                (int)Math.Round(_pendingPhysicalBounds.Height),
                NativeMethods.SwpNoActivate);
        }
    }
}
