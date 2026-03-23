using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;

namespace CursorFX.Core.Services;

public sealed class EffectManager
{
    private readonly List<IEffect> _effects = [];

    public IReadOnlyList<IEffect> Effects => _effects;

    public void Register(IEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (!_effects.Contains(effect))
        {
            _effects.Add(effect);
        }
    }

    public void Update(TimeSpan deltaTime)
    {
        foreach (var effect in _effects)
        {
            if (effect.IsEnabled)
            {
                effect.Update(deltaTime);
            }
        }
    }

    public void Render(DrawingContext drawingContext)
    {
        foreach (var effect in _effects)
        {
            if (effect.IsEnabled)
            {
                effect.Render(drawingContext);
            }
        }
    }

    public void OnMouseMove(Point position)
    {
        foreach (var effect in _effects)
        {
            if (effect.IsEnabled)
            {
                effect.OnMouseMove(position);
            }
        }
    }

    public void OnMouseClick(Point position)
    {
        foreach (var effect in _effects)
        {
            if (effect.IsEnabled)
            {
                effect.OnMouseClick(position);
            }
        }
    }
}
