// The resources behind Filee's look that are computed rather than written in Styles/Soft.axaml: the accent colour
// with its shades, the corner radii and the UI font order. ThemeService applies them with the user's choices; the
// installer (src/Filee.Setup links this file) applies the defaults, so Setup looks like the app.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Filee.App.Services;

internal static class SoftPalette
{
    /// <summary>The default accent colour (the first swatch of the Theme page).</summary>
    public const string DefaultAccent = "#7F77DD";

    /// <summary>The default corner radius (ThemeSettings.CornerRadius).</summary>
    public const double DefaultCornerRadius = 16;

    /// <summary>Embedded Noto fonts (see build/fetch-fonts.ps1). Missing files fall back to system fonts.</summary>
    private const string Latin = "fonts:Filee#Noto Sans";
    private const string Korean = "fonts:Filee#Noto Sans KR";
    private const string Chinese = "fonts:Filee#Noto Sans SC";
    private const string Devanagari = "fonts:Filee#Noto Sans Devanagari";
    private const string SystemFallbacks =
        "Segoe UI, Nirmala UI, Malgun Gothic, Microsoft YaHei, PingFang SC, Apple SD Gothic Neo, Kohinoor Devanagari, sans-serif";

    /// <summary>Accent colour + derived shades. Fluent controls read the System* keys.</summary>
    public static void ApplyAccent(IResourceDictionary res, Color accent)
    {
        res["AccentColor"] = accent;
        res["AccentBrush"] = new SolidColorBrush(accent);
        res["AccentSoftBrush"] = new SolidColorBrush(accent, 0.18);
        res["AccentFaintBrush"] = new SolidColorBrush(accent, 0.09);
        res["AccentStrongBrush"] = new SolidColorBrush(Shade(accent, -0.25));
        res["OnAccentBrush"] = new SolidColorBrush(Luminance(accent) > 0.6 ? Color.Parse("#1F1E1D") : Colors.White);
        res["SystemAccentColor"] = accent;
        res["SystemAccentColorLight1"] = Shade(accent, 0.15);
        res["SystemAccentColorLight2"] = Shade(accent, 0.30);
        res["SystemAccentColorLight3"] = Shade(accent, 0.45);
        res["SystemAccentColorDark1"] = Shade(accent, -0.15);
        res["SystemAccentColorDark2"] = Shade(accent, -0.30);
        res["SystemAccentColorDark3"] = Shade(accent, -0.45);
    }

    /// <summary>"Softness": one radius drives every control.</summary>
    public static void ApplyCornerRadius(IResourceDictionary res, double radius)
    {
        var r = Math.Clamp(radius, 0, 28);
        res["SoftRadius"] = new CornerRadius(r);
        res["SoftRadiusSmall"] = new CornerRadius(Math.Round(r * 0.55));
        res["SoftRadiusLarge"] = new CornerRadius(Math.Round(r * 1.35));
        res["ControlCornerRadius"] = new CornerRadius(Math.Round(r * 0.55));
        res["OverlayCornerRadius"] = new CornerRadius(r);
    }

    /// <summary>
    /// The UI font for <paramref name="language"/>. Han characters look different in Korean and Chinese fonts: the
    /// UI language's font comes first.
    /// </summary>
    public static FontFamily FontFamilyFor(string language)
    {
        var cjk = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? $"{Chinese}, {Korean}"
            : $"{Korean}, {Chinese}";
        return FontFamily.Parse($"{Latin}, {Devanagari}, {cjk}, {SystemFallbacks}");
    }

    /// <summary>Lightens (amount &gt; 0) or darkens (amount &lt; 0) a colour.</summary>
    public static Color Shade(Color c, double amount)
    {
        byte Mix(byte v) => amount >= 0
            ? (byte)Math.Round(v + (255 - v) * amount)
            : (byte)Math.Round(v * (1 + amount));
        return Color.FromArgb(c.A, Mix(c.R), Mix(c.G), Mix(c.B));
    }

    /// <summary>Relative luminance 0..1 (sRGB approximation).</summary>
    public static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
