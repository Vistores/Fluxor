using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class CustomPluginEffect : IEffect, IDisposable
{
    private readonly PluginRuntimeLoader _runtimeLoader;
    private ICursorEffectPlugin? _runtime;
    private string _runtimeSignature = string.Empty;
    private string? _lastError;

    public CustomPluginEffect(PluginRuntimeLoader runtimeLoader)
    {
        _runtimeLoader = runtimeLoader;
    }

    public string Name => _runtime?.DisplayName ?? "Custom Plugin";

    public bool IsEnabled { get; set; }

    public void Dispose()
    {
        UnloadRuntime();
    }

    public void Update(TimeSpan deltaTime)
    {
        ExecuteSafely(runtime => runtime.Update(deltaTime));
    }

    public void Render(DrawingContext drawingContext)
    {
        ExecuteSafely(runtime => runtime.Render(drawingContext));
    }

    public void OnMouseMove(Point position)
    {
        ExecuteSafely(runtime => runtime.OnMouseMove(position));
    }

    public void OnMouseClick(Point position)
    {
        ExecuteSafely(runtime => runtime.OnMouseClick(position));
    }

    public void UpdatePlugin(
        ShaderTemplateDefinition? definition,
        IReadOnlyDictionary<string, TemplateParameterValue> parameterValues,
        double masterOpacity)
    {
        if (definition is null || definition.RuntimeKind != TemplateRuntimeKind.ExternalAssembly)
        {
            IsEnabled = false;
            UnloadRuntime();
            return;
        }

        var signature = $"{definition.AssemblyFileName}|{definition.EntryTypeName}";
        if (!string.Equals(_runtimeSignature, signature, StringComparison.Ordinal))
        {
            UnloadRuntime();
            _runtime = _runtimeLoader.Load(definition);
            _runtimeSignature = signature;
            _lastError = null;
        }

        ExecuteSafely(runtime => runtime.ApplyParameters(parameterValues, masterOpacity));
        IsEnabled = true;
    }

    private void UnloadRuntime()
    {
        _runtime?.Dispose();
        _runtime = null;
        _runtimeSignature = string.Empty;
    }

    private void ExecuteSafely(Action<ICursorEffectPlugin> action)
    {
        if (_runtime is null || !IsEnabled)
        {
            return;
        }

        try
        {
            action(_runtime);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            IsEnabled = false;
            UnloadRuntime();
        }
    }
}
