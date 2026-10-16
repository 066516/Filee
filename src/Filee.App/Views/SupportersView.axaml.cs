using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Filee.App.Services;

namespace Filee.App.Views;

/// <summary>A sponsor's logo (or its name when the logo is missing) and where it leads.</summary>
public sealed record SupporterItem(string Name, string? Url, Bitmap? Logo);

/// <summary>The sponsors' logos and the backers' names shipped with this release; hidden while there are none.</summary>
public partial class SupportersView : UserControl
{
    public SupportersView()
    {
        InitializeComponent();
        Show(Supporters.Bundled, BundledLogo);
    }

    /// <summary>Shows <paramref name="list"/>, the logos read by <paramref name="logo"/> (null when missing).</summary>
    public void Show(SupporterList list, Func<string, Bitmap?> logo)
    {
        IsVisible = !list.IsEmpty;
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        Logos.ItemsSource = list.Sponsors
            .Select(s => new SupporterItem(s.Name, s.Url,
                (dark ? s.LogoDark ?? s.Logo : s.Logo) is { } file ? logo(file) : null))
            .ToList();
        Logos.IsVisible = list.Sponsors.Count > 0;
        Names.Text = string.Join(" · ", list.Backers.Select(b => b.Name));
        Names.IsVisible = list.Backers.Count > 0;
    }

    private static Bitmap? BundledLogo(string file)
    {
        var uri = new Uri(Supporters.Folder + file);
        if (!AssetLoader.Exists(uri))
            return null;
        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is string url && TopLevel.GetTopLevel(this) is { } top)
            await top.Launcher.LaunchUriAsync(new Uri(url));
    }
}
