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
    private readonly RenderLoop _renderLoop;
    private bool _pauseWhenCursorHidden = true;
    private bool _effectsSuspended;
    private Point _lastOverlayCursorPosition;

    public CursorFxEngine(
        OverlayWindow overlayWindow,
        EffectManager effectManager,
        IMouseTracker mouseTracker,
        IClickMonitor clickMonitor,
        IWindowStateMonitor windowStateMonitor,
        int targetFps)
    {
        _overlayWindow = overlayWindow;
        _effectManager = effectManager;
        _mouseTracker = mouseTracker;
        _clickMonitor = clickMonitor;
        _windowStateMonitor = windowStateMonitor;
        _renderLoop = new RenderLoop(targetFps);

        _mouseTracker.MouseMoved += OnMouseMoved;
        _clickMonitor.MouseClicked += OnMouseClicked;
        _windowStateMonitor.FullscreenStateChanged += OnFullscreenChanged;
        _windowStateMonitor.EffectsSuspendedChanged += OnEffectsSuspendedChanged;
        _renderLoop.FrameRendering += OnFrameRendering;
    }

    public void Start()
    {
        _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(_mouseTracker.CurrentPosition);
        _effectManager.OnMouseMove(_lastOverlayCursorPosition);
        _mouseTracker.Start();
        _clickMonitor.Start();
        _windowStateMonitor.Start();
        if (!_effectsSuspended || !_pauseWhenCursorHidden)
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
        UpdateOverlayVisibility();
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
        _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(position);
        _effectManager.OnMouseMove(_lastOverlayCursorPosition);
    }

    private void OnMouseClicked(object? sender, Point position)
    {
        if (_lastOverlayCursorPosition == default)
        {
            _lastOverlayCursorPosition = _overlayWindow.ScreenToOverlay(position);
        }

        _effectManager.OnMouseClick(_lastOverlayCursorPosition);
    }

    private void OnFullscreenChanged(object? sender, bool isFullscreen)
    {
        UpdateOverlayVisibility();
    }

    private void OnEffectsSuspendedChanged(object? sender, bool areEffectsSuspended)
    {
        _effectsSuspended = areEffectsSuspended;
        UpdateRenderLoopState();
        UpdateOverlayVisibility();
    }

    private void OnFrameRendering(object? sender, TimeSpan deltaTime)
    {
        if (_effectsSuspended && _pauseWhenCursorHidden)
        {
            return;
        }

        _effectManager.Update(deltaTime);
        _overlayWindow.InvalidateSurface();
    }

    private void UpdateOverlayVisibility()
    {
        var shouldHide = _windowStateMonitor.IsFullscreen || (_pauseWhenCursorHidden && _effectsSuspended);
        _overlayWindow.Visibility = shouldHide
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    private void UpdateRenderLoopState()
    {
        var shouldRun = !_pauseWhenCursorHidden || !_effectsSuspended;
        if (shouldRun)
        {
            if (!_renderLoop.IsRunning)
            {
                _renderLoop.Start();
            }

            return;
        }

        if (_renderLoop.IsRunning)
        {
            _renderLoop.Stop();
        }
    }
}
