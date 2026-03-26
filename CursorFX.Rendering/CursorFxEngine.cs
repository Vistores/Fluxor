using System.Windows;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Services;

namespace CursorFX.Rendering;

public sealed class CursorFxEngine : IDisposable
{
    private readonly OverlayWindow _overlayWindow;
    private readonly EffectManager _effectManager;
    private readonly IMouseTracker _mouseTracker;
    private readonly IClickMonitor _clickMonitor;
    private readonly IWindowStateMonitor _windowStateMonitor;
    private readonly IScreenSampler? _screenSampler;
    private readonly ICursorSnapshotProvider? _cursorSnapshotProvider;
    private readonly IPluginRuntimeContextSink? _pluginRuntimeContextSink;
    private readonly RenderLoop _renderLoop;
    private bool _pauseWhenCursorHidden = true;
    private bool _effectsSuspended;
    private Point _lastOverlayCursorPosition;
    private Point _lastRawCursorPosition;

    public CursorFxEngine(
        OverlayWindow overlayWindow,
        EffectManager effectManager,
        IMouseTracker mouseTracker,
        IClickMonitor clickMonitor,
        IWindowStateMonitor windowStateMonitor,
        IScreenSampler? screenSampler,
        ICursorSnapshotProvider? cursorSnapshotProvider,
        IPluginRuntimeContextSink? pluginRuntimeContextSink,
        int targetFps)
    {
        _overlayWindow = overlayWindow;
        _effectManager = effectManager;
        _mouseTracker = mouseTracker;
        _clickMonitor = clickMonitor;
        _windowStateMonitor = windowStateMonitor;
        _screenSampler = screenSampler;
        _cursorSnapshotProvider = cursorSnapshotProvider;
        _pluginRuntimeContextSink = pluginRuntimeContextSink;
        _renderLoop = new RenderLoop(targetFps);

        _mouseTracker.MouseMoved += OnMouseMoved;
        _clickMonitor.MouseClicked += OnMouseClicked;
        _windowStateMonitor.FullscreenStateChanged += OnFullscreenChanged;
        _windowStateMonitor.EffectsSuspendedChanged += OnEffectsSuspendedChanged;
        _renderLoop.FrameRendering += OnFrameRendering;
    }

    public void Start()
    {
        _screenSampler?.UpdateCursorPosition(_mouseTracker.CurrentPosition);
        _cursorSnapshotProvider?.UpdateCursorPosition(_mouseTracker.CurrentPosition);
        _lastRawCursorPosition = _mouseTracker.CurrentPosition;
        _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(_mouseTracker.CurrentPosition);
        RefreshPluginRuntimeContext(TimeSpan.Zero);
        _effectManager.OnMouseMove(_lastOverlayCursorPosition);
        _mouseTracker.Start();
        _clickMonitor.Start();
        _windowStateMonitor.Start();
        if (!_renderLoop.IsRunning)
        {
            _renderLoop.Start();
        }
    }

    public void SetTargetFps(int targetFps)
    {
        _renderLoop.SetTargetFps(targetFps);
    }

    public void SetPauseWhenCursorHidden(bool pauseWhenCursorHidden)
    {
        _pauseWhenCursorHidden = pauseWhenCursorHidden;
        UpdateRenderLoopState();
        UpdateOverlayDormantState();
    }

    public void Dispose()
    {
        _renderLoop.FrameRendering -= OnFrameRendering;
        _windowStateMonitor.EffectsSuspendedChanged -= OnEffectsSuspendedChanged;
        _windowStateMonitor.FullscreenStateChanged -= OnFullscreenChanged;
        _clickMonitor.MouseClicked -= OnMouseClicked;
        _mouseTracker.MouseMoved -= OnMouseMoved;
        _renderLoop.Dispose();
    }

    private void OnMouseMoved(object? sender, Point position)
    {
        _screenSampler?.UpdateCursorPosition(position);
        _cursorSnapshotProvider?.UpdateCursorPosition(position);
        _lastRawCursorPosition = position;
        _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(position);
        RefreshPluginRuntimeContext(TimeSpan.Zero);
        _effectManager.OnMouseMove(_lastOverlayCursorPosition);
    }

    private void OnMouseClicked(object? sender, Point position)
    {
        if (_effectsSuspended && _pauseWhenCursorHidden)
        {
            return;
        }

        if (_lastOverlayCursorPosition == default)
        {
            _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(position);
        }

        _effectManager.OnMouseClick(_lastOverlayCursorPosition);
    }

    private void OnFullscreenChanged(object? sender, bool isFullscreen)
    {
        UpdateOverlayDormantState();
    }

    private void OnEffectsSuspendedChanged(object? sender, bool areEffectsSuspended)
    {
        var wasSuspended = _effectsSuspended;
        _effectsSuspended = areEffectsSuspended;
        _screenSampler?.SetSuspended(areEffectsSuspended && _pauseWhenCursorHidden);
        _cursorSnapshotProvider?.SetSuspended(areEffectsSuspended && _pauseWhenCursorHidden);
        UpdateRenderLoopState();
        if (wasSuspended && !_effectsSuspended)
        {
            ResumeEffectsAtCurrentCursor();
        }

        UpdateOverlayDormantState();
    }

    private void OnFrameRendering(object? sender, TimeSpan deltaTime)
    {
        if (_effectsSuspended && _pauseWhenCursorHidden)
        {
            return;
        }

        RefreshPluginRuntimeContext(deltaTime);
        _effectManager.Update(deltaTime);
        _overlayWindow.InvalidateSurface();
    }

    private void UpdateOverlayDormantState()
    {
        _overlayWindow.IsDormant = _pauseWhenCursorHidden && _effectsSuspended;
    }

    private void UpdateRenderLoopState()
    {
        if (!_renderLoop.IsRunning)
        {
            _renderLoop.Start();
        }
    }

    private void ResumeEffectsAtCurrentCursor()
    {
        _renderLoop.ResetClock();
        _screenSampler?.UpdateCursorPosition(_mouseTracker.CurrentPosition);
        _screenSampler?.SetSuspended(false);
        _cursorSnapshotProvider?.UpdateCursorPosition(_mouseTracker.CurrentPosition);
        _cursorSnapshotProvider?.SetSuspended(false);
        _lastRawCursorPosition = _mouseTracker.CurrentPosition;
        _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(_mouseTracker.CurrentPosition);
        RefreshPluginRuntimeContext(TimeSpan.Zero);
        _effectManager.OnMouseMove(_lastOverlayCursorPosition);
        _overlayWindow.InvalidateSurface();
    }

    private void RefreshPluginRuntimeContext(TimeSpan deltaTime)
    {
        if (_pluginRuntimeContextSink is null)
        {
            return;
        }

        var backdropSample = _screenSampler?.GetSample(192, TimeSpan.FromMilliseconds(80));
        var cursorSnapshot = _cursorSnapshotProvider?.GetSnapshot(TimeSpan.FromMilliseconds(80));
        _pluginRuntimeContextSink.UpdateRuntimeContext(
            _lastOverlayCursorPosition,
            _lastRawCursorPosition,
            _windowStateMonitor.IsCursorVisible,
            backdropSample,
            cursorSnapshot,
            deltaTime);
    }
}
