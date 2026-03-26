using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;
using CursorFX.Platform.Interop;

namespace CursorFX.Platform.Services;

public sealed class Win32CursorSnapshotProvider : ICursorSnapshotProvider
{
    private readonly object _syncRoot = new();
    private System.Windows.Point _cursorScreenPosition;
    private bool _isSuspended;
    private CursorVisualSnapshot? _cachedSnapshot;

    public void UpdateCursorPosition(System.Windows.Point screenPixels)
    {
        lock (_syncRoot)
        {
            _cursorScreenPosition = screenPixels;
        }
    }

    public void SetSuspended(bool isSuspended)
    {
        lock (_syncRoot)
        {
            _isSuspended = isSuspended;
            if (_isSuspended)
            {
                _cachedSnapshot = null;
            }
        }
    }

    public CursorVisualSnapshot? GetSnapshot(TimeSpan maxAge)
    {
        lock (_syncRoot)
        {
            if (_isSuspended)
            {
                return null;
            }

            if (_cachedSnapshot is not null && (DateTimeOffset.UtcNow - _cachedSnapshot.CapturedAtUtc) <= maxAge)
            {
                return _cachedSnapshot;
            }

            _cachedSnapshot = CaptureSnapshot();
            return _cachedSnapshot;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _cachedSnapshot = null;
        }
    }

    private static CursorVisualSnapshot? CaptureSnapshot()
    {
        var cursorInfo = new NativeMethods.CURSORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.CURSORINFO>()
        };

        if (!NativeMethods.GetCursorInfo(ref cursorInfo) ||
            (cursorInfo.flags & NativeMethods.CursorShowing) != NativeMethods.CursorShowing ||
            cursorInfo.hCursor == IntPtr.Zero)
        {
            return null;
        }

        var copiedIcon = NativeMethods.CopyIcon(cursorInfo.hCursor);
        if (copiedIcon == IntPtr.Zero)
        {
            return null;
        }

        if (!NativeMethods.GetIconInfo(copiedIcon, out var iconInfo))
        {
            NativeMethods.DestroyIcon(copiedIcon);
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(copiedIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            return new CursorVisualSnapshot
            {
                Image = source,
                Hotspot = new System.Windows.Point(iconInfo.xHotspot, iconInfo.yHotspot),
                Size = new System.Windows.Size(source.PixelWidth, source.PixelHeight),
                CapturedAtUtc = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(iconInfo.hbmColor);
            }

            if (iconInfo.hbmMask != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(iconInfo.hbmMask);
            }

            NativeMethods.DestroyIcon(copiedIcon);
        }
    }
}
