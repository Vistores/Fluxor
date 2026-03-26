using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface ICursorEffectPlugin : IDisposable
{
    string DisplayName => GetType().Name;

    string PluginId => ToKebabCase(GetType().Name);

    string Description => string.Empty;

    string IconGlyph => "*";

    string AccentColor => "#4FD1C5";

    TemplateEffectKind Kind => TemplateEffectKind.CursorAura;

    TemplateTrigger Trigger => TemplateTrigger.FollowCursor;

    IReadOnlyList<TemplateParameterDefinition> GetParameters() => [];

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

    void Update(PluginRenderContext context)
    {
        Update(context.DeltaTime);
    }

    void Update(TimeSpan deltaTime)
    {
        Update(deltaTime.TotalSeconds);
    }

    void Render(PluginRenderContext context, DrawingContext drawingContext)
    {
        Render(drawingContext);
    }

    void Render(DrawingContext drawingContext);

    void OnMouseMove(PluginRenderContext context, Point position)
    {
        OnMouseMove(position);
    }

    void OnMouseMove(Point position);

    void OnMouseClick(PluginRenderContext context, Point position)
    {
        OnMouseClick(position);
    }

    void OnMouseClick(Point position);

    void ApplyParameters(IDictionary<string, object> parameters)
    {
    }

    void Update(double deltaTime)
    {
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "plugin";
        }

        var buffer = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                buffer.Append('-');
            }

            buffer.Append(char.ToLowerInvariant(c));
        }

        return buffer.ToString();
    }
}
