// Applies ThemeSettings (light/dark, accent colour, corner radius, font order, reduced motion, theme style) and the user's
// theme.json colours at runtime by writing resources into Application.Resources. All styles reference these with
// DynamicResource.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.Services;

/// <summary>Runtime theming.</summary>
public sealed class ThemeService
{
    private readonly IPlatformServices _platform;

    private static readonly List<WeakReference<Window>> s_openWindows = [];

    public ThemeService(IPlatformServices platform)
    {
        _platform = platform;
        Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) =>
        {
            lock (s_openWindows)
            {
                s_openWindows.RemoveAll(r => !r.TryGetTarget(out var target) || target == w);
                s_openWindows.Add(new WeakReference<Window>(w));
            }
            if (IsMaterialYou)
                w.Classes.Set("material-you", true);
        });
        Window.WindowClosedEvent.AddClassHandler<Window>((w, _) =>
        {
            lock (s_openWindows)
            {
                s_openWindows.RemoveAll(r => !r.TryGetTarget(out var target) || target == w);
            }
        });
    }

    /// <summary>Accent swatches offered in the Theme page for Soft theme.</summary>
    public static readonly IReadOnlyList<string> Swatches =
        ["#7F77DD", "#1D9E75", "#D85A30", "#378ADD", "#D4537E", "#BA7517", "#639922", "#5F5E5A", "#111111", "#FFFFFF"];

    /// <summary>Curated Material 3 seed swatches for Material You theme.</summary>
    public static readonly IReadOnlyList<string> MaterialYouSwatches =
        ["#6750A4", "#0B57D0", "#006C4C", "#B3261E", "#825500", "#7F77DD", "#006A6A", "#4C616B", "#111111", "#FFFFFF"];

    /// <summary>Inkora pastel preset definition with coordinated light and dark seeds.</summary>
    public sealed record InkoraPastelPreset(string Key, string Name, string LightHex, string DarkHex);

    /// <summary>Curated Inkora pastel and monochrome presets with name and light/dark color mappings.</summary>
    public static readonly IReadOnlyList<InkoraPastelPreset> InkoraPresets =
    [
        new("monochrome", "Black & White", "#111111", "#FFFFFF"),
        new("blushPeach", "Blush & Peach", "#E07A70", "#F29B91"),
        new("strawberryMilk", "Strawberry Milk", "#E86483", "#F79BB0"),
        new("matchaLatte", "Matcha Latte", "#5A8E51", "#8DC983"),
        new("lavenderMist", "Lavender Mist", "#8B7AB8", "#BCAEE5"),
        new("sageMint", "Sage & Mint", "#529B7D", "#88D5B1"),
        new("honeyAmber", "Honey & Amber", "#CB852A", "#F0BA6A"),
        new("cloudSky", "Cloud & Sky", "#5B8BE3", "#98B8F6"),
        new("goldenParchment", "Golden Parchment", "#8A6B24", "#D4AF37"),
    ];

    /// <summary>Curated pastel palette add-on swatches from Inkora for both Material You and System/Soft themes.</summary>
    public static readonly IReadOnlyList<string> PastelSwatches =
        ["#E07A70", "#E86483", "#5A8E51", "#8B7AB8", "#529B7D", "#CB852A", "#5B8BE3", "#8A6B24",
         "#F29B91", "#F79BB0", "#8DC983", "#BCAEE5", "#88D5B1", "#F0BA6A", "#98B8F6", "#D4AF37"];

    /// <summary>Currently active theme style.</summary>
    public ThemeStyle CurrentStyle { get; private set; } = ThemeStyle.Soft;

    /// <summary>True when the Material You theme is currently applied.</summary>
    public bool IsMaterialYou => CurrentStyle == ThemeStyle.MaterialYou;

    /// <summary>
    /// theme.json colour names → the brushes in Styles/Soft.axaml they replace. Must cover exactly
    /// <see cref="CustomTheme.Keys"/> (a test checks).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> CustomBrushKeys = new Dictionary<string, string>
    {
        ["page"] = "PageBrush",
        ["surface"] = "SurfaceBrush",
        ["surfaceAlt"] = "SurfaceAltBrush",
        ["border"] = "SubtleBorderBrush",
        ["text"] = "TextPrimaryBrush",
        ["textSecondary"] = "TextSecondaryBrush",
        ["danger"] = "DangerBrush",
        ["success"] = "SuccessBrush",
        ["warning"] = "WarningBrush",
    };

    public void Apply(ThemeSettings theme, string language, CustomTheme? custom = null)
    {
        var app = Application.Current ?? throw new InvalidOperationException("No application.");
        var res = app.Resources;

        CurrentStyle = theme.Style;
        var isM3 = theme.Style == ThemeStyle.MaterialYou;
        res["IsMaterialYouTheme"] = isM3;

        app.RequestedThemeVariant = theme.Mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        // Accent colour + derived shades. Fluent controls read the System* keys.
        var accent = Color.TryParse(theme.Accent, out var parsed) ? parsed : Color.Parse(isM3 ? "#6750A4" : SoftPalette.DefaultAccent);

        var isDarkVariant = theme.Mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => app.ActualThemeVariant == ThemeVariant.Dark
        };

        if (isM3)
        {
            var lightM3 = MaterialYouPalette.Generate(accent, isDark: false);
            var darkM3 = MaterialYouPalette.Generate(accent, isDark: true);
            var activeM3 = isDarkVariant ? darkM3 : lightM3;

            res["AccentColor"] = activeM3.Primary;
            res["AccentBrush"] = new SolidColorBrush(activeM3.Primary);
            res["AccentSoftBrush"] = new SolidColorBrush(activeM3.PrimaryContainer);
            res["AccentFaintBrush"] = new SolidColorBrush(activeM3.SecondaryContainer);
            res["AccentStrongBrush"] = new SolidColorBrush(activeM3.OnPrimaryContainer);
            res["OnAccentBrush"] = new SolidColorBrush(activeM3.OnPrimary);

            res["PrimaryContainerBrush"] = new SolidColorBrush(activeM3.PrimaryContainer);
            res["OnPrimaryContainerBrush"] = new SolidColorBrush(activeM3.OnPrimaryContainer);
            res["SecondaryContainerBrush"] = new SolidColorBrush(activeM3.SecondaryContainer);
            res["OnSecondaryContainerBrush"] = new SolidColorBrush(activeM3.OnSecondaryContainer);

            res["SystemAccentColor"] = activeM3.Primary;
            res["SystemAccentColorLight1"] = SoftPalette.Shade(activeM3.Primary, 0.15);
            res["SystemAccentColorLight2"] = SoftPalette.Shade(activeM3.Primary, 0.30);
            res["SystemAccentColorLight3"] = SoftPalette.Shade(activeM3.Primary, 0.45);
            res["SystemAccentColorDark1"] = SoftPalette.Shade(activeM3.Primary, -0.15);
            res["SystemAccentColorDark2"] = SoftPalette.Shade(activeM3.Primary, -0.30);
            res["SystemAccentColorDark3"] = SoftPalette.Shade(activeM3.Primary, -0.45);

            // Fluent control brush keys harmonized with M3 roles
            res["SliderTrackFill"] = new SolidColorBrush(activeM3.Primary);
            res["SliderTrackValueFill"] = new SolidColorBrush(activeM3.Primary);
            res["SliderThumbBackground"] = new SolidColorBrush(activeM3.Primary);
            res["SliderThumbBorderBrush"] = new SolidColorBrush(activeM3.Primary);
            res["SliderTrackBackground"] = new SolidColorBrush(activeM3.SurfaceContainerHighest);
            res["ProgressBarTrackFill"] = new SolidColorBrush(activeM3.Primary);
            res["ProgressBarBackground"] = new SolidColorBrush(activeM3.SurfaceContainerHighest);
            res["ProgressBarProgress"] = new SolidColorBrush(activeM3.Primary);
            res["ToggleSwitchFillOn"] = new SolidColorBrush(activeM3.Primary);
            res["ToggleSwitchFillOff"] = new SolidColorBrush(activeM3.SurfaceContainerHighest);
            res["ToggleSwitchKnobFillOn"] = new SolidColorBrush(activeM3.OnPrimary);
            res["ToggleSwitchKnobFillOff"] = new SolidColorBrush(activeM3.Outline);
            res["ToggleSwitchStrokeOff"] = new SolidColorBrush(activeM3.Outline);
            res["CheckBoxCheckBackgroundFillChecked"] = new SolidColorBrush(activeM3.Primary);
            res["CheckBoxCheckBackgroundStrokeChecked"] = new SolidColorBrush(activeM3.Primary);
            res["CheckBoxCheckGlyphForegroundChecked"] = new SolidColorBrush(activeM3.OnPrimary);
            res["AccentButtonBackground"] = new SolidColorBrush(activeM3.Primary);
            res["AccentButtonForeground"] = new SolidColorBrush(activeM3.OnPrimary);
            res["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(activeM3.Secondary);
            res["AccentButtonForegroundPointerOver"] = new SolidColorBrush(activeM3.OnSecondary);
            res["AccentButtonBackgroundPressed"] = new SolidColorBrush(activeM3.PrimaryContainer);
            res["AccentButtonForegroundPressed"] = new SolidColorBrush(activeM3.OnPrimaryContainer);

            // "Softness": Material You expressive pill and rounded containers
            var r = Math.Clamp(theme.CornerRadius, 0, 28);
            res["SoftRadius"] = new CornerRadius(Math.Max(16, r));
            res["SoftRadiusSmall"] = new CornerRadius(12);
            res["SoftRadiusLarge"] = new CornerRadius(24);
            res["PillRadius"] = new CornerRadius(999);
            res["ControlCornerRadius"] = new CornerRadius(12);
            res["OverlayCornerRadius"] = new CornerRadius(20);

            ApplyMaterialYouTheme(res, ThemeVariant.Light, lightM3, custom?.Light);
            ApplyMaterialYouTheme(res, ThemeVariant.Dark, darkM3, custom?.Dark);
        }
        else
        {
            SoftPalette.ApplyAccent(res, accent);
            SoftPalette.ApplyCornerRadius(res, theme.CornerRadius);
            res["PillRadius"] = new CornerRadius(999);

            res["DropZoneBackgroundBrush"] = isDarkVariant
                ? new SolidColorBrush(Color.Parse("#252523"))
                : new SolidColorBrush(Color.Parse("#F5F3ED"));
            res["DropZoneBorderBrush"] = new SolidColorBrush(accent, 0.25);

            res["AccentButtonBackground"] = res["AccentBrush"];
            res["AccentButtonForeground"] = res["OnAccentBrush"];
            res["AccentButtonBackgroundPointerOver"] = res["AccentStrongBrush"];
            res["AccentButtonForegroundPointerOver"] = res["OnAccentBrush"];
            res["AccentButtonBackgroundPressed"] = res["AccentFaintBrush"];
            res["AccentButtonForegroundPressed"] = res["OnAccentBrush"];

            // Clean up M3 Fluent overrides if present
            res.Remove("SliderTrackFill");
            res.Remove("SliderTrackValueFill");
            res.Remove("SliderThumbBackground");
            res.Remove("SliderThumbBorderBrush");
            res.Remove("SliderTrackBackground");
            res.Remove("ProgressBarTrackFill");
            res.Remove("ProgressBarBackground");
            res.Remove("ProgressBarProgress");
            res.Remove("ToggleSwitchFillOn");
            res.Remove("ToggleSwitchFillOff");
            res.Remove("ToggleSwitchKnobFillOn");
            res.Remove("ToggleSwitchKnobFillOff");
            res.Remove("ToggleSwitchStrokeOff");
            res.Remove("CheckBoxCheckBackgroundFillChecked");
            res.Remove("CheckBoxCheckBackgroundStrokeChecked");
            res.Remove("CheckBoxCheckGlyphForegroundChecked");

            ApplyCustom(res, ThemeVariant.Light, custom?.Light);
            ApplyCustom(res, ThemeVariant.Dark, custom?.Dark);
        }

        res["AppFontFamily"] = SoftPalette.FontFamilyFor(language);

        // Update all active windows
        lock (s_openWindows)
        {
            s_openWindows.RemoveAll(r => !r.TryGetTarget(out _));
            foreach (var r in s_openWindows)
                if (r.TryGetTarget(out var window))
                    window.Classes.Set("material-you", isM3);
        }
        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
                window.Classes.Set("material-you", isM3);
        }

        Motion.Configure(theme.ReduceMotion, () => _platform.PrefersReducedMotion);
    }

    /// <summary>Applies Material You generated colors to the theme dictionary for one variant.</summary>
    private static void ApplyMaterialYouTheme(IResourceDictionary res, ThemeVariant variant, MaterialYouPalette m3, IReadOnlyDictionary<string, string>? custom)
    {
        var dict = new ResourceDictionary
        {
            ["PageBrush"] = new SolidColorBrush(m3.SurfaceContainerLowest),
            ["SurfaceBrush"] = new SolidColorBrush(m3.Surface),
            ["SurfaceAltBrush"] = new SolidColorBrush(m3.SurfaceContainerHigh),
            ["SubtleBorderBrush"] = new SolidColorBrush(m3.OutlineVariant),
            ["TextPrimaryBrush"] = new SolidColorBrush(m3.OnSurface),
            ["TextSecondaryBrush"] = new SolidColorBrush(m3.OnSurfaceVariant),
            ["DangerBrush"] = new SolidColorBrush(m3.Danger),
            ["SuccessBrush"] = new SolidColorBrush(m3.Success),
            ["WarningBrush"] = new SolidColorBrush(m3.Warning),
            ["CardShadow"] = m3.CardShadow,
        };

        // Custom colours from theme.json take precedence if present
        foreach (var (name, value) in custom ?? new Dictionary<string, string>())
            if (CustomBrushKeys.TryGetValue(name, out var key) && Color.TryParse(value, out var color))
                dict[key] = new SolidColorBrush(color);

        res.ThemeDictionaries[variant] = dict;
    }

    /// <summary>The UI font chain for <paramref name="language"/> (see <see cref="SoftPalette.FontFamilies"/>).</summary>
    internal static string FontFamilies(string language) => SoftPalette.FontFamilies(language);

    /// <summary>Replaces (or, with no colours, removes) the user's colours for one variant.</summary>
    private static void ApplyCustom(IResourceDictionary res, ThemeVariant variant, IReadOnlyDictionary<string, string>? colors)
    {
        var dictionary = new ResourceDictionary();
        foreach (var (name, value) in colors ?? new Dictionary<string, string>())
            if (CustomBrushKeys.TryGetValue(name, out var key) && Color.TryParse(value, out var color))
                dictionary[key] = new SolidColorBrush(color);

        if (dictionary.Count == 0)
            res.ThemeDictionaries.Remove(variant);
        else
            res.ThemeDictionaries[variant] = dictionary;
    }

    /// <summary>Lightens (amount &gt; 0) or darkens (amount &lt; 0) a colour.</summary>
    public static Color Shade(Color c, double amount) => SoftPalette.Shade(c, amount);

    /// <summary>Relative luminance 0..1 (sRGB approximation).</summary>
    public static double Luminance(Color c) => SoftPalette.Luminance(c);
}
