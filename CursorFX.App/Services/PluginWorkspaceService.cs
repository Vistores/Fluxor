using System.IO;

namespace CursorFX.App.Services;

public sealed class PluginWorkspaceService
{
    public PluginWorkspaceService()
    {
        WorkspaceDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Plugins"));
    }

    public string WorkspaceDirectory { get; }

    public void EnsureWorkspace()
    {
        Directory.CreateDirectory(WorkspaceDirectory);
    }
}
