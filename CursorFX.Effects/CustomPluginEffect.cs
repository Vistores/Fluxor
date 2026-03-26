using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Models;

namespace CursorFX.Effects;

public sealed class CustomPluginEffect : IEffect, IPluginRuntimeContextSink, IDisposable
{
    private readonly PluginRuntimeLoader _runtimeLoader;
    private ICursorEffectPlugin? _runtime;
    private ShaderTemplateDefinition? _currentDefinition;
    private string _runtimeSignature = string.Empty;
    private string? _lastError;
    private Point _cursorPosition;
    private Point _rawCursorPosition;
    private bool _isCursorVisible = true;
    private ScreenSampleFrame? _backdropSample;
    private CursorVisualSnapshot? _cursorSnapshot;
    private TimeSpan _lastDeltaTime;
    private DateTimeOffset? _loadedAtUtc;
    private DateTimeOffset? _lastErrorAtUtc;

    public CustomPluginEffect(PluginRuntimeLoader runtimeLoader)
    {
        _runtimeLoader = runtimeLoader;
    }

    public string Name => _runtime?.DisplayName ?? "Custom Plugin";

    public bool IsEnabled { get; set; }

    public string Status => _runtime is not null
        ? "Loaded"
        : string.IsNullOrWhiteSpace(_lastError)
            ? "Idle"
            : "Error";

    public string StatusDetails => _runtime is not null
        ? "Plugin runtime loaded and active."
        : !string.IsNullOrWhiteSpace(_lastError)
            ? _lastError
            : "No external plugin is active.";

    public string RuntimeAssemblyFileName => _currentDefinition?.AssemblyFileName ?? string.Empty;

    public string RuntimeAssemblyPath => _currentDefinition is null ? string.Empty : _runtimeLoader.ResolveAssemblyPath(_currentDefinition);

    public string RuntimeEntryTypeName => _currentDefinition?.EntryTypeName ?? string.Empty;

    public string LoadedAtLabel => _loadedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Not loaded";

    public string LastErrorAtLabel => _lastErrorAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "No runtime errors";

    public string ContextSummary => $"Cursor snapshot: {(_cursorSnapshot is null ? "no" : "yes")} | Backdrop sample: {(_backdropSample is null ? "no" : "yes")} | Cursor visible: {(_isCursorVisible ? "yes" : "no")}";

    public void Dispose()
    {
        UnloadRuntime();
    }

    public void UpdateRuntimeContext(
        Point cursorPosition,
        Point rawCursorPosition,
        bool isCursorVisible,
        ScreenSampleFrame? backdropSample,
        CursorVisualSnapshot? cursorSnapshot,
        TimeSpan deltaTime)
    {
        _cursorPosition = cursorPosition;
        _rawCursorPosition = rawCursorPosition;
        _isCursorVisible = isCursorVisible;
        _backdropSample = backdropSample;
        _cursorSnapshot = cursorSnapshot;
        _lastDeltaTime = deltaTime;
    }

    public void Update(TimeSpan deltaTime)
    {
        _lastDeltaTime = deltaTime;
        ExecuteSafely((runtime, context) => runtime.Update(context));
    }

    public void Render(DrawingContext drawingContext)
    {
        ExecuteSafely((runtime, context) => runtime.Render(context, drawingContext));
    }

    public void OnMouseMove(Point position)
    {
        _cursorPosition = position;
        ExecuteSafely((runtime, context) => runtime.OnMouseMove(context, position));
    }

    public void OnMouseClick(Point position)
    {
        ExecuteSafely((runtime, context) => runtime.OnMouseClick(context, position));
    }

    public void UpdatePlugin(
        ShaderTemplateDefinition? definition,
        IReadOnlyDictionary<string, TemplateParameterValue> parameterValues,
        double masterOpacity)
    {
        if (definition is null || definition.RuntimeKind != TemplateRuntimeKind.ExternalAssembly)
        {
            IsEnabled = false;
            _currentDefinition = null;
            _lastError = null;
            _loadedAtUtc = null;
            _lastErrorAtUtc = null;
            UnloadRuntime();
            return;
        }

        var signature = $"{definition.AssemblyFileName}|{definition.EntryTypeName}";
        if (!string.Equals(_runtimeSignature, signature, StringComparison.Ordinal))
        {
            UnloadRuntime();
            _currentDefinition = definition;
            _runtime = _runtimeLoader.Load(definition);
            _runtimeSignature = signature;
            _lastError = null;
            _lastErrorAtUtc = null;
            _loadedAtUtc = DateTimeOffset.UtcNow;
        }

        try
        {
            if (_runtime is null)
            {
                throw new InvalidOperationException("Plugin runtime is not available.");
            }

            _runtime.ApplyParameters(parameterValues, masterOpacity);
            IsEnabled = true;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _lastErrorAtUtc = DateTimeOffset.UtcNow;
            IsEnabled = false;
            UnloadRuntime();
        }
    }

    private void UnloadRuntime()
    {
        _runtime?.Dispose();
        _runtime = null;
        _runtimeSignature = string.Empty;
    }

    private void ExecuteSafely(Action<ICursorEffectPlugin, PluginRenderContext> action)
    {
        if (_runtime is null || !IsEnabled)
        {
            return;
        }

        try
        {
            action(_runtime, CreateContext());
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _lastErrorAtUtc = DateTimeOffset.UtcNow;
            IsEnabled = false;
            UnloadRuntime();
        }
    }

    private PluginRenderContext CreateContext()
    {
        return new PluginRenderContext
        {
            CursorPosition = _cursorPosition,
            RawCursorPosition = _rawCursorPosition,
            IsCursorVisible = _isCursorVisible,
            DeltaTime = _lastDeltaTime,
            CursorSnapshot = _cursorSnapshot,
            BackdropSample = _backdropSample
        };
    }
}
