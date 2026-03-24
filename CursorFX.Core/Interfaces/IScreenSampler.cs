using System.Windows;
using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface IScreenSampler : IDisposable
{
    void UpdateCursorPosition(Point screenPixels);

    void SetSuspended(bool isSuspended);

    ScreenSampleFrame? GetSample(int sizePixels, TimeSpan maxAge);
}
