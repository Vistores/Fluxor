using System.IO;

namespace CursorFX.App.Services;

public sealed class PluginWorkspaceService
{
    public PluginWorkspaceService()
    {
        WorkspaceDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Fluxor",
            "Plugins");
    }

    public string WorkspaceDirectory { get; }

    public void EnsureWorkspace()
    {
        Directory.CreateDirectory(WorkspaceDirectory);
    }
}
