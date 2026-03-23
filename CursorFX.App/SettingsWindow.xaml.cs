using System.Windows;
using CursorFX.Core.Models;

namespace CursorFX.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(GeneralSettings settings)
    {
        InitializeComponent();

        LaunchOnStartup = settings.LaunchOnStartup;
        RunInBackground = settings.RunInBackground;
        PauseWhenCursorHidden = settings.PauseWhenCursorHidden;
    }

    public bool LaunchOnStartup { get; set; }

    public bool RunInBackground { get; set; }

    public bool PauseWhenCursorHidden { get; set; }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
