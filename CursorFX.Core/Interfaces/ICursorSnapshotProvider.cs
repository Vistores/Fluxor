using System.Windows;
using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface ICursorSnapshotProvider : IDisposable
{
    void UpdateCursorPosition(Point screenPixels);

    void SetSuspended(bool isSuspended);

    CursorVisualSnapshot? GetSnapshot(TimeSpan maxAge);
}
