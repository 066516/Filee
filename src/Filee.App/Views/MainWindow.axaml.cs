using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.Core.Localization;

namespace Filee.App.Views;

public partial class MainWindow : Window
{
    private bool _releasePosted;
    private bool _releasing;

    public MainWindow()
    {
        InitializeComponent();
        Motion.Track(this);
    }

    private async void OnOpenGitHub(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await Launcher.LaunchUriAsync(new Uri(vm.RepositoryUrl));
    }

    private async void OnSponsor(object? sender, RoutedEventArgs e) =>
        await SponsorWindow.Create(AppHost.Get<ILocalizer>()).ShowDialog(this);

    /// <summary>Closing the window keeps Filee running in the tray (Quit is in the tray menu).</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.CloseReason is WindowCloseReason.WindowClosing && !_releasing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    /// <summary>
    /// Leaves Filee in the tray: hides the window at once, then closes it for real, so its pages, visuals and images
    /// don't stay in memory until the next time (issue #119). <see cref="WindowService.ShowMain"/> opens a new one.
    /// </summary>
    internal void HideToTray()
    {
        Hide();
        if (_releasePosted)
            return;
        _releasePosted = true;
        // Posted: a window can't close inside its own Closing handler.
        Dispatcher.UIThread.Post(() =>
        {
            _releasePosted = false;
            if (IsVisible)
                return; // shown again meanwhile
            _releasing = true;
            Close();
        });
    }
}
