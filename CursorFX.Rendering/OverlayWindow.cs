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

            layouts.Add(new MonitorLayout(
                $"monitor-{physical.Left}-{physical.Top}-{physical.Width}-{physical.Height}",
                physical,
                Rect.Empty,
                dpiX,
                dpiY,
                (monitorInfo.dwFlags & NativeMethods.MonitorinfofPrimary) == NativeMethods.MonitorinfofPrimary));
            return true;
        }, IntPtr.Zero);

        if (layouts.Count == 0)
        {
            return layouts;
        }

        var primarySource = layouts.FirstOrDefault(layout => layout.IsPrimary) ?? layouts[0];
        var resolved = new List<MonitorLayout>
        {
            primarySource with
            {
                LogicalBounds = new Rect(
                    0,
                    0,
                    primarySource.PhysicalBounds.Width * 96.0 / primarySource.DpiX,
                    primarySource.PhysicalBounds.Height * 96.0 / primarySource.DpiY)
            }
        };

        var pending = layouts.Where(layout => layout.Id != primarySource.Id).ToList();
        while (pending.Count > 0)
        {
            var progress = false;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var placement = TryResolve(pending[i], resolved);
                if (placement is null)
                {
                    continue;
                }

                resolved.Add(placement);
                pending.RemoveAt(i);
                progress = true;
            }

            if (progress)
            {
                continue;
            }

            foreach (var candidate in pending)
            {
                resolved.Add(candidate with
                {
                    LogicalBounds = new Rect(
                        (candidate.PhysicalBounds.Left - primarySource.PhysicalBounds.Left) * 96.0 / candidate.DpiX,
                        (candidate.PhysicalBounds.Top - primarySource.PhysicalBounds.Top) * 96.0 / candidate.DpiY,
                        candidate.PhysicalBounds.Width * 96.0 / candidate.DpiX,
                        candidate.PhysicalBounds.Height * 96.0 / candidate.DpiY)
                });
            }

            break;
        }

        var minLeft = resolved.Min(layout => layout.LogicalBounds.Left);
        var minTop = resolved.Min(layout => layout.LogicalBounds.Top);
        return resolved.Select(layout => layout with
        {
            LogicalBounds = new Rect(
                layout.LogicalBounds.Left - minLeft,
                layout.LogicalBounds.Top - minTop,
                layout.LogicalBounds.Width,
                layout.LogicalBounds.Height)
        }).ToList();
    }

    private static MonitorLayout? TryResolve(MonitorLayout candidate, IReadOnlyList<MonitorLayout> resolved)
    {
        foreach (var anchor in resolved)
        {
            var logicalWidth = candidate.PhysicalBounds.Width * 96.0 / candidate.DpiX;
            var logicalHeight = candidate.PhysicalBounds.Height * 96.0 / candidate.DpiY;

            if (candidate.PhysicalBounds.Left == anchor.PhysicalBounds.Right && OverlapsVertically(candidate.PhysicalBounds, anchor.PhysicalBounds))
            {
                return candidate with
                {
                    LogicalBounds = new Rect(
                        anchor.LogicalBounds.Right,
                        anchor.LogicalBounds.Top + ((candidate.PhysicalBounds.Top - anchor.PhysicalBounds.Top) * 96.0 / candidate.DpiY),
                        logicalWidth,
                        logicalHeight)
                };
            }

            if (candidate.PhysicalBounds.Right == anchor.PhysicalBounds.Left && OverlapsVertically(candidate.PhysicalBounds, anchor.PhysicalBounds))
            {
                return candidate with
                {
                    LogicalBounds = new Rect(
                        anchor.LogicalBounds.Left - logicalWidth,
                        anchor.LogicalBounds.Top + ((candidate.PhysicalBounds.Top - anchor.PhysicalBounds.Top) * 96.0 / candidate.DpiY),
                        logicalWidth,
                        logicalHeight)
                };
            }

            if (candidate.PhysicalBounds.Top == anchor.PhysicalBounds.Bottom && OverlapsHorizontally(candidate.PhysicalBounds, anchor.PhysicalBounds))
            {
                return candidate with
                {
                    LogicalBounds = new Rect(
                        anchor.LogicalBounds.Left + ((candidate.PhysicalBounds.Left - anchor.PhysicalBounds.Left) * 96.0 / candidate.DpiX),
                        anchor.LogicalBounds.Bottom,
                        logicalWidth,
                        logicalHeight)
                };
            }

            if (candidate.PhysicalBounds.Bottom == anchor.PhysicalBounds.Top && OverlapsHorizontally(candidate.PhysicalBounds, anchor.PhysicalBounds))
            {
                return candidate with
                {
                    LogicalBounds = new Rect(
                        anchor.LogicalBounds.Left + ((candidate.PhysicalBounds.Left - anchor.PhysicalBounds.Left) * 96.0 / candidate.DpiX),
                        anchor.LogicalBounds.Top - logicalHeight,
                        logicalWidth,
                        logicalHeight)
                };
            }
        }

        return null;
    }

    private static bool OverlapsVertically(Rect a, Rect b) => a.Top < b.Bottom && a.Bottom > b.Top;

    private static bool OverlapsHorizontally(Rect a, Rect b) => a.Left < b.Right && a.Right > b.Left;

    private sealed record MonitorLayout(string Id, Rect PhysicalBounds, Rect LogicalBounds, double DpiX, double DpiY, bool IsPrimary);

    private sealed class OverlayViewportWindow : Window
    {
        private readonly RenderSurface _renderSurface;

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
            Left = layout.PhysicalBounds.Left * 96.0 / layout.DpiX;
            Top = layout.PhysicalBounds.Top * 96.0 / layout.DpiY;
            Width = layout.PhysicalBounds.Width * 96.0 / layout.DpiX;
            Height = layout.PhysicalBounds.Height * 96.0 / layout.DpiY;
            Visibility = visibility;
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

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is not HwndSource source)
            {
                return;
            }

            var hwnd = source.Handle;
            var currentStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
            var newStyle = currentStyle.ToInt64() |
                           NativeMethods.WsExTransparent |
                           NativeMethods.WsExToolWindow |
                           NativeMethods.WsExNoActivate;

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(newStyle));
            EnsureTopmost();
        }
    }
}
