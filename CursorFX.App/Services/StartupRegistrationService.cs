using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace CursorFX.App.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CursorFX";

    public void Apply(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open Windows startup registry key.");

        if (enabled)
        {
            runKey.SetValue(AppName, BuildStartupCommand());
            return;
        }

        if (runKey.GetValue(AppName) is not null)
        {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    private static string BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Unable to determine current process path.");
        }

        if (string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new InvalidOperationException("Unable to determine application DLL for startup registration.");
            }

            return $"\"{processPath}\" \"{entryAssemblyPath}\"";
        }

        return $"\"{processPath}\"";
    }
}
