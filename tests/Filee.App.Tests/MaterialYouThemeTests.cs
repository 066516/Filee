// Tests for Material You theme: dynamic tonal palette generation, theme application, window styling, and localization.

using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class MaterialYouThemeTests
{
    [Fact]
    public void ThemeSettings_persists_ThemeStyle()
    {
        var settings = new AppSettings
        {
            Theme = new ThemeSettings
            {
                Style = ThemeStyle.MaterialYou,
                Accent = "#6750A4",
            },
        };

        var json = JsonSerializer.Serialize(settings, FileeJsonContext.Default.AppSettings);
        Assert.Contains("\"style\": \"MaterialYou\"", json);

        var restored = JsonSerializer.Deserialize(json, FileeJsonContext.Default.AppSettings);
        Assert.NotNull(restored);
        Assert.Equal(ThemeStyle.MaterialYou, restored.Theme.Style);
        Assert.Equal("#6750A4", restored.Theme.Accent);
    }

    [Fact]
    public void Palette_generates_distinct_tonal_roles_for_light_and_dark()
    {
        var seed = Color.Parse("#6750A4");
        var light = MaterialYouPalette.Generate(seed, isDark: false);
        var dark = MaterialYouPalette.Generate(seed, isDark: true);

        // Light mode surfaces are bright, containers are soft pastels
        Assert.True(light.Surface.R > 230 && light.Surface.G > 230 && light.Surface.B > 230);
        Assert.True(light.OnSurface.R < 50 && light.OnSurface.G < 50 && light.OnSurface.B < 50);

        // Dark mode surfaces are deep dark, containers are rich dark
        Assert.True(dark.Surface.R < 40 && dark.Surface.G < 40 && dark.Surface.B < 40);
        Assert.True(dark.OnSurface.R > 200 && dark.OnSurface.G > 200 && dark.OnSurface.B > 200);

        // Primary is distinct from primary container
        Assert.NotEqual(light.Primary, light.PrimaryContainer);
        Assert.NotEqual(dark.Primary, dark.PrimaryContainer);
    }

    [AvaloniaFact]
    public void ThemeService_applies_MaterialYou_and_marks_windows()
    {
        TestServices.EnsureInitialized("en");
        var themeService = AppHost.Get<ThemeService>();
        var theme = new ThemeSettings
        {
            Style = ThemeStyle.MaterialYou,
            Accent = "#6750A4",
        };

        var window = new Window();

        try
        {
            themeService.Apply(theme, "en");
            window.Show();

            Assert.Equal(ThemeStyle.MaterialYou, themeService.CurrentStyle);
            Assert.True(themeService.IsMaterialYou);
            Assert.Contains("material-you", window.Classes);

            // Brushes and corner radius are written into Application resources
            var app = Application.Current!;
            Assert.True(app.Resources.ContainsKey("PrimaryContainerBrush"));
            Assert.True(app.Resources.ContainsKey("SecondaryContainerBrush"));
            Assert.True(app.Resources.ContainsKey("SliderTrackFill"));
            Assert.True(app.Resources.ContainsKey("ProgressBarTrackFill"));
            Assert.True(app.Resources.ContainsKey("ToggleSwitchFillOn"));

            var radius = Assert.IsType<CornerRadius>(app.Resources["SoftRadiusSmall"]);
            Assert.Equal(12, radius.TopLeft); // Smooth non-clipping inputs in Material You
            var pill = Assert.IsType<CornerRadius>(app.Resources["PillRadius"]);
            Assert.Equal(999, pill.TopLeft); // Full pill for chips & badges
        }
        finally
        {
            window.Close();
            themeService.Apply(new ThemeSettings(), "en");
            var app = Application.Current!;
            Assert.False(app.Resources.ContainsKey("SliderTrackFill"));
        }
    }

    [AvaloniaFact]
    public void DonutMenu_reflects_IsMaterialYou_property()
    {
        TestServices.EnsureInitialized("en");
        var donut = new DonutMenu
        {
            IsMaterialYou = true,
        };

        Assert.True(donut.IsMaterialYou);
        donut.IsMaterialYou = false;
        Assert.False(donut.IsMaterialYou);
    }
}
