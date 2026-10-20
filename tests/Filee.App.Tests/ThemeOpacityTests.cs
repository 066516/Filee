// Issue #276: the opacity example in Theme must control and preview the persisted donut opacity.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.App.ViewModels.Pages;
using Filee.App.Views.Pages;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class ThemeOpacityTests
{
    [AvaloniaTheory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Opacity_slider_updates_the_real_preview_and_survives_reopening(ThemeMode mode)
    {
        TestServices.EnsureInitialized();
        var folder = Directory.CreateTempSubdirectory("filee-opacity-").FullName;
        var store = new UserDataStore(folder);
        store.Load();
        store.Settings.Theme.Mode = mode;
        store.Settings.Donut.Opacity = 0.72;
        AppHost.Get<ThemeService>().Apply(store.Settings.Theme, "en");
        var model = new ThemePageViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>());
        var page = new ThemePage { DataContext = model };
        var window = new Window { Content = page, Width = 980, Height = 760 };
        try
        {
            window.Show();
            RenderTests.Pump();
            var slider = page.FindControl<Slider>("OpacitySlider")!;
            var preview = page.FindControl<DonutMenu>("PreviewDonut")!;
            Assert.Equal(72, slider.Value);
            Assert.Equal(0.72, preview.SliceOpacity, 6);

            slider.Value = 30;
            RenderTests.Pump();
            Assert.Equal(0.30, preview.SliceOpacity, 6);
            Assert.Equal(0.30, store.Settings.Donut.Opacity, 6);
            RenderTests.Save(window, $"issue276-opacity-30-{mode}.png");

            slider.Value = 100;
            RenderTests.Pump();
            Assert.Equal(1, preview.SliceOpacity);
            RenderTests.Save(window, $"issue276-opacity-100-{mode}.png");

            var reloaded = new UserDataStore(folder);
            reloaded.Load();
            var reopened = new ThemePageViewModel(reloaded, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>());
            Assert.Equal(100, reopened.DonutOpacityPercent);
            Assert.Equal(1, reopened.DonutSliceOpacity);
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized();
            Directory.Delete(folder, true);
        }
    }
}
