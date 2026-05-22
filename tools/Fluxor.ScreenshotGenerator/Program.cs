using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CursorFX.App;
using CursorFX.App.Services;
using CursorFX.Core.Models;

namespace Fluxor.ScreenshotGenerator;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var outputDir = Path.Combine(repoRoot, "docs", "screenshots");
        Directory.CreateDirectory(outputDir);

        var appHost = new CursorFX.App.App();
        typeof(CursorFX.App.App)
            .GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .Invoke(appHost, null);
        var localization = new LocalizationService();
        localization.Apply(new LocalizationSettings
        {
            UseSystemLanguage = false,
            LanguageCode = "en"
        });

        var guidePath = Path.Combine(repoRoot, "CursorFX.App", "Templates", "plugin-authoring-guide.txt");
        var pluginWorkspacePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Fluxor",
            "Plugins");

        CaptureWindow(
            new SettingsWindow(
                AppSettings.CreateDefault().General,
                new LocalizationSettings { UseSystemLanguage = false, LanguageCode = "en" },
                localization,
                guidePath,
                pluginWorkspacePath),
            Path.Combine(outputDir, "settings-window.png"));

        CaptureWindow(
            new SaveProfileWindow(
                "Minimal Suite Copy",
                "A custom variant with adjusted trail and glow balance.",
                localization),
            Path.Combine(outputDir, "save-profile-window.png"));

        CaptureWindow(
            new PluginDiagnosticsDetailsWindow(
                "Reload the plugin first. If the error returns, switch to the safe profile and replace the DLL build.",
                "Status: Runtime error\nAssembly: Fluxor.PluginRetroTrace.dll\nEntry type: Fluxor.PluginRetroTrace.RetroTracePlugin\nLast error: Example render exception for screenshot preview.",
                localization),
            Path.Combine(outputDir, "plugin-diagnostics-window.png"));

        CaptureWindow(
            new ColorPickerWindow("Trail Color", "#FF67E8F9", localization),
            Path.Combine(outputDir, "color-picker-window.png"));

        var importWindow = new ImportPluginWindow(
            pluginWorkspacePath,
            localization,
            [
                new PluginImportMatch(
                    "firefly-swarm",
                    "Firefly Swarm",
                    "Fluxor.PluginFireflySwarm.FireflySwarmPlugin",
                    "Matching plugin ID")
            ])
        {
            AssemblyPath = ResolveExistingPluginDll(repoRoot)
        };
        CaptureWindow(
            importWindow,
            Path.Combine(outputDir, "import-plugin-window.png"));

        CaptureWindow(
            new ArchiveImportPreviewWindow(CreateArchiveInspectionSample(), localization),
            Path.Combine(outputDir, "archive-preview-window.png"));

        CaptureWindow(
            new PluginAuthoringGuideWindow(guidePath, localization),
            Path.Combine(outputDir, "plugin-authoring-guide-window.png"));

        appHost.Shutdown();
        return 0;
    }

    private static ProfileArchiveInspectionResult CreateArchiveInspectionSample()
    {
        var existing = new ShaderTemplateDefinition
        {
            Id = "retro-trace",
            Name = "Retro Trace",
            Description = "Existing profile placeholder for archive preview.",
            IconGlyph = "R",
            AccentColor = "#8CF7FF",
            RuntimeKind = TemplateRuntimeKind.ExternalAssembly,
            AssemblyFileName = "Fluxor.PluginRetroTrace.dll",
            EntryTypeName = "Fluxor.PluginRetroTrace.RetroTracePlugin",
            Kind = TemplateEffectKind.MatrixCascade,
            Trigger = TemplateTrigger.FollowCursor
        };

        return new ProfileArchiveInspectionResult
        {
            ArchivePath = @"C:\Temp\RetroTrace.fluxorprofile",
            FileName = "RetroTrace.fluxorprofile",
            Template = new ShaderTemplateDefinition
            {
                Id = "retro-trace",
                Name = "Retro Trace",
                Description = "A CRT-inspired noise afterimage effect that leaves phosphor-like imprints behind the cursor.",
                IconGlyph = "R",
                AccentColor = "#8CF7FF",
                RuntimeKind = TemplateRuntimeKind.ExternalAssembly,
                AssemblyFileName = "Fluxor.PluginRetroTrace.dll",
                EntryTypeName = "Fluxor.PluginRetroTrace.RetroTracePlugin",
                Kind = TemplateEffectKind.MatrixCascade,
                Trigger = TemplateTrigger.FollowCursor,
                Parameters =
                [
                    new TemplateParameterDefinition
                    {
                        Key = "opacity",
                        DisplayName = "Opacity",
                        Section = PluginParameterSection.Trail,
                        SectionName = "Retro Trace",
                        Type = TemplateParameterType.Number,
                        Min = 0.05,
                        Max = 1,
                        Step = 0.01,
                        DefaultNumber = 0.82,
                        DefaultColor = "#FFFFFF"
                    }
                ]
            },
            ExistingById = existing,
            ExistingByName = existing,
            HasIcon = true,
            HasAssembly = true,
            Warnings =
            [
                "A profile with ID 'retro-trace' already exists. Fluxor can replace it or import the archive as a copy.",
                "The archive contains an external plugin DLL and can restore the runtime together with the profile."
            ]
        };
    }

    private static string ResolveExistingPluginDll(string repoRoot)
    {
        var candidates = new[]
        {
            Path.Combine(repoRoot, "Plugins", "Fluxor.PluginRetroTrace", "bin", "Debug", "net9.0-windows", "Fluxor.PluginRetroTrace.dll"),
            Path.Combine(repoRoot, "Plugins", "Fluxor.PluginFireflySwarm", "bin", "Debug", "net9.0-windows", "Fluxor.PluginFireflySwarm.dll"),
            Path.Combine(repoRoot, "Plugins", "Fluxor.PluginFireflySwarm", "bin", "Release", "net9.0-windows", "Fluxor.PluginFireflySwarm.dll")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static void CaptureWindow(Window window, string path)
    {
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = 120;
        window.Top = 100;
        window.Topmost = true;
        window.Show();
        window.Activate();
        PumpUi();
        Thread.Sleep(350);
        PumpUi();

        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            throw new InvalidOperationException($"Could not resolve composition target for '{window.Title}'.");
        }

        var transform = source.CompositionTarget.TransformToDevice;
        var left = (int)Math.Round(window.Left * transform.M11);
        var top = (int)Math.Round(window.Top * transform.M22);
        var width = (int)Math.Round(window.ActualWidth * transform.M11);
        var height = (int)Math.Round(window.ActualHeight * transform.M22);

        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
        }

        bitmap.Save(path, ImageFormat.Png);
        window.Close();
        PumpUi();
        Thread.Sleep(150);
        PumpUi();
    }

    private static void PumpUi()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }),
            null);
        Dispatcher.PushFrame(frame);
    }
}
