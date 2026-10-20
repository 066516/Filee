using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Localization;

namespace Filee.App.Tests;

public class LocalizationResourceTests
{
    [AvaloniaFact]
    public void Switching_to_arabic_keeps_navigation_and_page_in_the_same_positions()
    {
        TestServices.EnsureInitialized("en");
        using var model = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>());
        model.Navigate("general");
        var window = new MainWindow { DataContext = model, Width = 1080, Height = 820 };
        window.Show();
        try
        {
            RenderTests.Pump();
            var navigation = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
            var page = window.FindControl<ScrollViewer>("PageScroller")!;
            var navigationPosition = navigation.TranslatePoint(default, window);
            var pagePosition = page.TranslatePoint(default, window);
            Assert.True(pagePosition!.Value.X > navigationPosition!.Value.X);

            foreach (var language in new[] { "ar", "en", "ar" })
            {
                TestServices.EnsureInitialized(language);
                RenderTests.Pump();
                // Fonts may change the header's natural height; the sidebar must remain on the same side.
                Assert.Equal(navigationPosition!.Value.X, navigation.TranslatePoint(default, window)!.Value.X);
                Assert.Equal(pagePosition, page.TranslatePoint(default, window));
                Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
                Assert.Equal(language == "ar" ? "عام" : "General", AppHost.Get<ILocalizer>()["general.title"]);
            }
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized("en");
        }
    }

    [AvaloniaFact]
    public void Arabic_translates_existing_and_new_windows_without_mirroring_the_layout()
    {
        TestServices.EnsureInitialized("en");
        var window = new Window { Content = new TextBlock() };
        window.Show();
        try
        {
            Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
            TestServices.EnsureInitialized("ar");
            Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
            Assert.Equal(FlowDirection.LeftToRight, ((TextBlock)window.Content).FlowDirection);
            Assert.True(Application.Current!.TryGetResource("general.language", null, out var value));
            Assert.Equal("اللغة", value);
            var canvas = new CropCanvas();
            var dialog = new Window { Content = canvas };
            dialog.Show();
            Assert.Equal(FlowDirection.LeftToRight, dialog.FlowDirection);
            Assert.Equal(FlowDirection.LeftToRight, canvas.FlowDirection);
            dialog.Close();
            TestServices.EnsureInitialized("en");
            Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized("en");
        }
    }

    [AvaloniaFact]
    public void Strings_are_available_as_application_resources()
    {
        TestServices.EnsureInitialized("ko");
        Assert.True(Application.Current!.TryGetResource("home.title", null, out var value));
        Assert.Equal("홈", value);
    }

    [AvaloniaFact]
    public void DynamicResource_in_xaml_resolves_and_follows_language_changes()
    {
        TestServices.EnsureInitialized("en");
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, text.GetResourceObservable("home.title"));
        var toggle = new ToggleSwitch();
        var window = new Window { Content = new StackPanel { Children = { text, toggle } } };
        window.Show();
        Assert.Equal("Home", text.Text);

        TestServices.EnsureInitialized("ko");
        Assert.Equal("홈", text.Text);
        TestServices.EnsureInitialized("ja");
        Assert.Equal("ホーム", text.Text);
        Assert.Equal("オン", toggle.OnContent);
        Assert.Equal("オフ", toggle.OffContent);
        window.Close();
    }
}
