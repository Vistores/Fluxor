using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface ICursorEffectPlugin : IDisposable
{
    string DisplayName => GetType().Name;

    void ApplyParameters(IReadOnlyDictionary<string, TemplateParameterValue> parameters, double masterOpacity)
    {
        var legacyValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in parameters)
        {
            if (entry.Value.BooleanValue.HasValue)
            {
                legacyValues[entry.Key] = entry.Value.BooleanValue.Value;
                continue;
            }

            if (entry.Value.NumberValue.HasValue)
            {
                legacyValues[entry.Key] = entry.Value.NumberValue.Value;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Value.ColorValue))
            {
                legacyValues[entry.Key] = entry.Value.ColorValue!;
            }
        }

        legacyValues["masterOpacity"] = masterOpacity;
        ApplyParameters(legacyValues);
    }

    void Update(TimeSpan deltaTime)
    {
        Update(deltaTime.TotalSeconds);
    }

    void Render(DrawingContext drawingContext);

    void OnMouseMove(Point position);

    void OnMouseClick(Point position);

    void ApplyParameters(IDictionary<string, object> parameters)
    {
    }

    void Update(double deltaTime)
    {
    }
}
