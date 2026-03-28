using System.Windows;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using CursorFX.App.Services;
using CursorFX.App.ViewModels;
using CursorFX.Core.Interfaces;
using CursorFX.Core.Services;
using CursorFX.Effects;
using CursorFX.Platform.Services;
using CursorFX.Rendering;

namespace CursorFX.App;

public partial class App : System.Windows.Application
{
    private static readonly string AppIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "FluxorIco.ico");
    private static readonly string SingleInstanceMutexName = "Fluxor.SingleInstance";
    private Mutex? _singleInstanceMutex;
    private JsonSettingsStore? _settingsStore;
    private CursorFxEngine? _engine;
    private MainViewModel? _mainViewModel;
    private OverlayWindow? _overlayWindow;
    private MouseTracker? _mouseTracker;
    private ClickMonitor? _clickMonitor;
    private WindowStateMonitor? _windowStateMonitor;
    private IShaderTemplateCatalog? _templateCatalog;
    private CustomPluginEffect? _customPluginEffect;
    private TrayIconService? _trayIconService;
    private IScreenSampler? _screenSampler;
    private ICursorSnapshotProvider? _cursorSnapshotProvider;
    private LocalizationService? _localizationService;
    private bool _forceExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Fluxor is already running.", "Fluxor", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsStore = new JsonSettingsStore();
        _templateCatalog = new ShaderTemplateCatalog();
        try
        {
            _templateCatalog.EnsureCatalog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Fluxor could not fully refresh the local plugin catalog.\n\nThe app will continue using the existing catalog files when possible.\n\n{ex.Message}",
                "Fluxor Catalog Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        var settings = _settingsStore.Load();
        _localizationService = new LocalizationService();
        _localizationService.Apply(settings.Localization);

        var effectManager = new EffectManager();
        var trailEffect = new TrailEffect(settings.Trail);
        var glowEffect = new GlowEffect(settings.Glow);
        var clickRippleEffect = new ClickRippleEffect(settings.Ripple);
        _screenSampler = new GdiScreenSampler();
        _cursorSnapshotProvider = new Win32CursorSnapshotProvider();
        var templateEffect = new TemplateEffect(_screenSampler);
        _customPluginEffect = new CustomPluginEffect(new PluginRuntimeLoader(_templateCatalog.CatalogDirectory));

        effectManager.Register(trailEffect);
        effectManager.Register(glowEffect);
        effectManager.Register(clickRippleEffect);
        effectManager.Register(templateEffect);
        effectManager.Register(_customPluginEffect);

        _mouseTracker = new MouseTracker();
        _clickMonitor = new ClickMonitor();
        _windowStateMonitor = new WindowStateMonitor();
        _overlayWindow = new OverlayWindow(effectManager);

        _engine = new CursorFxEngine(
            _overlayWindow,
            effectManager,
            _mouseTracker,
            _clickMonitor,
            _windowStateMonitor,
            _screenSampler,
            _cursorSnapshotProvider,
            _customPluginEffect,
            settings.General.TargetFps);

        _mainViewModel = new MainViewModel(
            settings,
            trailEffect,
            glowEffect,
            clickRippleEffect,
            templateEffect,
            _customPluginEffect,
            _engine,
            _settingsStore,
            _templateCatalog,
            _localizationService);

        var mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };
        mainWindow.Closing += OnMainWindowClosing;
        mainWindow.StateChanged += OnMainWindowStateChanged;

        MainWindow = mainWindow;
        _trayIconService = new TrayIconService(ShowMainWindow, ExitApplication, AppIconPath);
        mainWindow.Show();
        Dispatcher.BeginInvoke(new Action(StartRuntimeSafely), DispatcherPriority.Background);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _mainViewModel?.Dispose();
        _engine?.Dispose();
        _windowStateMonitor?.Dispose();
        _clickMonitor?.Dispose();
        _mouseTracker?.Dispose();
        _customPluginEffect?.Dispose();
        _screenSampler?.Dispose();
        _cursorSnapshotProvider?.Dispose();
        _overlayWindow?.Close();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void StartRuntimeSafely()
    {
        try
        {
            _overlayWindow?.Show();
            _engine?.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Fluxor could not fully initialize cursor rendering.\n\n{ex.Message}",
                "Fluxor Startup Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceExit || _mainViewModel is null || MainWindow is null)
        {
            return;
        }

        if (!_mainViewModel.RunInBackgroundEnabled)
        {
            _forceExit = true;
            Shutdown();
            return;
        }

        e.Cancel = true;
        MainWindow.Hide();
        _trayIconService?.ShowBalloon("Fluxor", "Fluxor continues running in the background.");
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainViewModel is null || MainWindow is null || !_mainViewModel.RunInBackgroundEnabled)
        {
            return;
        }

        if (MainWindow.WindowState != WindowState.Minimized)
        {
            return;
        }

        MainWindow.Hide();
        _trayIconService?.ShowBalloon("Fluxor", "The main window was minimized to the tray.");
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    private void ExitApplication()
    {
        _forceExit = true;
        MainWindow?.Close();
        Shutdown();
    }
}
