using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace CursorFX.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(
        Action openAction,
        Action exitAction,
        string? iconPath = null,
        string? openText = null,
        string? exitText = null,
        string? tooltipText = null)
    {
        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add(string.IsNullOrWhiteSpace(openText) ? "Open Fluxor" : openText, null, (_, _) => openAction());
        contextMenu.Items.Add(string.IsNullOrWhiteSpace(exitText) ? "Exit" : exitText, null, (_, _) => exitAction());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = ResolveIcon(iconPath),
            Text = string.IsNullOrWhiteSpace(tooltipText) ? "Fluxor" : tooltipText,
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => openAction();
    }

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(2200);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon ResolveIcon(string? iconPath)
    {
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/FluxorIco.ico"));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
        }

        return SystemIcons.Information;
    }
}
