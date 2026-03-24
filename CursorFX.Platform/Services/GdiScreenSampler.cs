using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;
using CursorFX.Platform.Interop;

namespace CursorFX.Platform.Services;

public sealed class GdiScreenSampler : IScreenSampler
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<int, CacheEntry> _cache = [];
    private System.Windows.Point _cursorScreenPosition;
    private bool _isSuspended;

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
                _cache.Clear();
            }
        }
    }

    public ScreenSampleFrame? GetSample(int sizePixels, TimeSpan maxAge)
    {
        sizePixels = Math.Clamp(sizePixels, 48, 320);

        lock (_syncRoot)
        {
            if (_isSuspended)
            {
                return null;
            }

            if (_cache.TryGetValue(sizePixels, out var cacheEntry))
            {
                var age = DateTimeOffset.UtcNow - cacheEntry.Frame.CapturedAtUtc;
                var cursorDelta = cacheEntry.CursorScreenPosition - _cursorScreenPosition;
                if (age <= maxAge && cursorDelta.LengthSquared <= Math.Pow(sizePixels * 0.12, 2))
                {
                    return cacheEntry.Frame;
                }
            }

            var frame = Capture(sizePixels, _cursorScreenPosition);
            if (frame is null)
            {
                return cacheEntry?.Frame;
            }

            _cache[sizePixels] = new CacheEntry(frame, _cursorScreenPosition);
            return frame;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _cache.Clear();
        }
    }

    private static ScreenSampleFrame? Capture(int sizePixels, System.Windows.Point cursorScreenPosition)
    {
        var left = (int)Math.Round(cursorScreenPosition.X - (sizePixels * 0.5));
        var top = (int)Math.Round(cursorScreenPosition.Y - (sizePixels * 0.5));

        try
        {
            using var bitmap = new System.Drawing.Bitmap(sizePixels, sizePixels, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(sizePixels, sizePixels), CopyPixelOperation.SourceCopy);
            }

            var averageColor = EstimateAverageColor(bitmap);
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                var image = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(sizePixels, sizePixels));
                image.Freeze();

                return new ScreenSampleFrame
                {
                    Image = image,
                    ScreenBounds = new Rect(left, top, sizePixels, sizePixels),
                    CursorScreenPosition = cursorScreenPosition,
                    AverageColor = averageColor,
                    CapturedAtUtc = DateTimeOffset.UtcNow
                };
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
    }

    private static System.Windows.Media.Color EstimateAverageColor(System.Drawing.Bitmap bitmap)
    {
        var samples = new List<System.Drawing.Color>(9);
        var stepX = Math.Max(1, bitmap.Width / 4);
        var stepY = Math.Max(1, bitmap.Height / 4);
        for (var y = stepY / 2; y < bitmap.Height; y += stepY)
        {
            for (var x = stepX / 2; x < bitmap.Width; x += stepX)
            {
                samples.Add(bitmap.GetPixel(Math.Min(bitmap.Width - 1, x), Math.Min(bitmap.Height - 1, y)));
            }
        }

        if (samples.Count == 0)
        {
            return System.Windows.Media.Colors.Black;
        }

        var avgR = (byte)Math.Clamp(samples.Average(c => c.R), 0, 255);
        var avgG = (byte)Math.Clamp(samples.Average(c => c.G), 0, 255);
        var avgB = (byte)Math.Clamp(samples.Average(c => c.B), 0, 255);
        return System.Windows.Media.Color.FromRgb(avgR, avgG, avgB);
    }

    private sealed record CacheEntry(ScreenSampleFrame Frame, System.Windows.Point CursorScreenPosition);
}
