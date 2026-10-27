using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Filee.App.Services;
using Filee.App.ViewModels.Pages;
using Filee.App.Views.Pages;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class ThemePageTests
{
    [AvaloniaFact]
    public void Preview_toggle_is_interactive_but_does_not_change_settings()
    {
        TestServices.EnsureInitialized();
        var directory = Directory.CreateTempSubdirectory("filee-theme-preview-").FullName;
        var store = new UserDataStore(directory);
        store.Load();
        AppHost.Get<ThemeService>().Apply(store.Settings.Theme, "en");
        var vm = new ThemePageViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>());
        var page = new ThemePage { DataContext = vm };
        var window = new Window { Content = page, Width = 940, Height = 700 };

        try
        {
            window.Show();
            RenderTests.Pump();

            var toggle = page.FindControl<ToggleSwitch>("PreviewToggle")!;
            Assert.NotNull(toggle);
            Assert.False(toggle.IsChecked);
            Assert.Contains("sample", AutomationProperties.GetName(toggle) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            var settingsBefore = System.Text.Json.JsonSerializer.Serialize(store.Settings);
            var point = toggle.TranslatePoint(
                new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            RenderTests.Pump();

            Assert.True(toggle.IsChecked);
            Assert.Equal(settingsBefore, System.Text.Json.JsonSerializer.Serialize(store.Settings));

            Assert.True(toggle.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            RenderTests.Pump();

            Assert.False(toggle.IsChecked);
            Assert.Equal(settingsBefore, System.Text.Json.JsonSerializer.Serialize(store.Settings));

            foreach (var name in new[] { "PreviewPrimaryButton", "PreviewSecondaryButton" })
            {
                var button = page.FindControl<Button>(name)!;
                Assert.NotNull(button);
                Assert.False(button.Focusable);
                Assert.False(button.IsTabStop);
            }
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized("en");
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Takaconv locks the company palette, so the page offers no way to recolour the app.</summary>
    [AvaloniaFact]
    public void Theme_page_has_no_colour_controls()
    {
        TestServices.EnsureInitialized("en");
        var directory = Directory.CreateTempSubdirectory("filee-theme-colours-").FullName;
        var store = new UserDataStore(directory);
        store.Load();
        var vm = new ThemePageViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>());
        try
        {
            var page = new ThemePage { DataContext = vm };
            var window = new Window { Content = page, Width = 940, Height = 700 };
            try
            {
                window.Show();
                RenderTests.Pump();
                Assert.Null(page.GetVisualDescendants().OfType<ColorPicker>().FirstOrDefault());
                Assert.Null(page.FindControl<ListBox>("AccentSwatches"));
            }
            finally
            {
                window.Close();
            }
            vm.Dispose();
        }
        finally
        {
            TestServices.EnsureInitialized("en");
            Directory.Delete(directory, true);
        }
    }
}
