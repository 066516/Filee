using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.Core.Localization;
using Filee.Core.Settings;

namespace Filee.App.Views;

public partial class MainWindow : Window
{
    private bool _asking;

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

    /// <summary>
    /// Closing the window keeps Filee running in the tray, quits, or asks which (<see cref="AppSettings.CloseAction"/>).
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.CloseReason is not WindowCloseReason.WindowClosing)
            return;
        e.Cancel = true;
        // Close() from code only hides, as before; the setting is for the title bar's close button and Alt+F4.
        if (e.IsProgrammatic)
            Hide();
        else
            CloseAsTheUserChose();
    }

    /// <summary>What the user's close does: hide, quit or ask (<see cref="AppSettings.CloseAction"/>).</summary>
    internal void CloseAsTheUserChose()
    {
        var store = AppHost.Get<UserDataStore>();
        switch (store.Settings.CloseAction)
        {
            case CloseAction.Tray:
                Hide();
                break;
            case CloseAction.Quit:
                Quit();
                break;
            default:
                _ = AskAsync(store);
                break;
        }
    }

    private async Task AskAsync(UserDataStore store)
    {
        if (_asking)
            return;
        _asking = true;
        try
        {
            var dialog = new CloseAskWindow();
            if (await dialog.ShowDialog<CloseAction?>(this) is not { } action)
                return;
            if (dialog.DontAskAgain)
            {
                store.Settings.CloseAction = action;
                store.SaveSettings();
            }
            if (action == CloseAction.Quit)
                Quit();
            else
                Hide();
        }
        finally
        {
            _asking = false;
        }
    }

    // Posted: shutting down closes this window again, which must not happen inside its own Closing handler.
    private static void Quit() => Dispatcher.UIThread.Post(() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
}
