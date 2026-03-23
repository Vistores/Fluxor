using System.Windows;
using System.Windows.Media;

namespace CursorFX.Core.Interfaces;

public interface IEffect
{
    string Name { get; }

    bool IsEnabled { get; set; }

    void Update(TimeSpan deltaTime);

    void Render(DrawingContext drawingContext);

    void OnMouseMove(Point position);

    void OnMouseClick(Point position);
}
