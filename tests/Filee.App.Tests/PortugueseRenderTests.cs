// Review Portuguese at the minimum supported window width, in both themes, with real Skia text layout.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Localization;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class PortugueseRenderTests
{
    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Icon_actions_have_localized_accessible_names(string language)
    {
        TestServices.EnsureInitialized(language);
        var loc = AppHost.Get<ILocalizer>();
        var vm = new MainWindowViewModel(AppHost.Services, loc, AppHost.Get<UpdateService>());
        vm.Navigate("presets");
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            RenderTests.Pump();
            foreach (var (name, key) in new[] { ("DuplicatePresetButton", "presets.duplicate"), ("DeletePresetButton", "presets.delete") })
            {
                var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);
                Assert.Equal(loc[key], AutomationProperties.GetName(button));
            }
        }
        finally
        {
            window.Close();
        }
    }

    public static TheoryData<string, string, bool, double> Pages
    {
        get
        {
            var data = new TheoryData<string, string, bool, double>();
            foreach (var language in new[] { "pt-PT", "pt-BR" })
                foreach (var page in new[] { "home", "general", "triggers", "toolbar", "presets", "theme", "engines", "about", "watch" })
                    foreach (var dark in new[] { false, true })
                        foreach (var scale in new[] { 1.0, 2.0 })
                            data.Add(language, page, dark, scale);
            return data;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Pages))]
    public void Pages_render_at_minimum_width_in_both_themes(string language, string page, bool dark, double scale)
    {
        TestServices.EnsureInitialized(language);
        var theme = AppHost.Get<ThemeService>();
        var settings = AppHost.Get<UserDataStore>().Settings.Theme;
        theme.Apply(new ThemeSettings { Mode = dark ? ThemeMode.Dark : ThemeMode.Light, ReduceMotion = true }, language);
        var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>());
        vm.Navigate(page);
        var window = new MainWindow { DataContext = vm, Width = 820, Height = 820 };
        try
        {
            window.Show();
            window.SetRenderScaling(scale);
            RenderTests.Pump();
            var name = $"portuguese-{page}-{language}-{(dark ? "dark" : "light")}{(scale == 2 ? "-2x" : "")}";
            RenderTests.Save(window, name + ".png");
            var scroller = window.FindControl<ScrollViewer>("PageScroller")!;
            // Complex editors keep a usable minimum width. On small windows every column remains reachable
            // by scrolling, instead of the grid crushing its middle column and drawing controls on top of it.
            Assert.True(scroller.Extent.Width <= scroller.Viewport.Width + 1,
                $"{name}: page content exceeds the outer viewport");
            var editor = window.GetVisualDescendants().OfType<ScrollViewer>().SingleOrDefault(s => s.Name == "EditorScroller");
            if (editor?.Content is Control content)
                Assert.Equal(Math.Max(content.MinWidth, editor.Viewport.Width), content.Bounds.Width, precision: 1);
            if (editor is not null && editor.Extent.Width > editor.Viewport.Width + 1)
            {
                editor.Offset = new Vector(editor.Extent.Width, 0);
                RenderTests.Pump();
                Assert.True(editor.Offset.X > 0, $"{name}: overflowing content is unreachable");
                RenderTests.Save(window, name + "-right.png");
                editor.Offset = default;
            }
            if (scroller.Extent.Height > scroller.Viewport.Height)
            {
                scroller.ScrollToEnd();
                RenderTests.Pump();
                RenderTests.Save(window, name + "-bottom.png");
            }
        }
        finally
        {
            window.Close();
            theme.Apply(settings, language);
        }
    }

    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Close_dialog_actions_fit_inside_the_window(string language)
    {
        TestServices.EnsureInitialized(language);
        var window = new CloseAskWindow();
        try
        {
            window.Show();
            RenderTests.Pump();
            RenderTests.Save(window, $"portuguese-close-{language}.png");
            foreach (var button in window.GetVisualDescendants().OfType<Button>())
            {
                var start = button.TranslatePoint(default, window)!.Value;
                Assert.True(start.X >= 0 && start.X + button.Bounds.Width <= window.Bounds.Width,
                    $"{language}: close action '{button.Content}' exceeds the dialog");
                foreach (var text in button.GetVisualDescendants().OfType<TextBlock>())
                    Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 1,
                        $"{language}: '{text.Text}' is cut off");
            }
        }
        finally
        {
            window.Close();
        }
    }
}
