// The languages Takaconv ships (Assets/i18n/<code>.json). The installer (src/Filee.Setup) links this file, so a language
// added here is offered by Setup too. Takaconv is an internal build for EL Djazair Takaful and ships only the three
// languages that company works in; every other system language falls back to English.

namespace Filee.App.Services;

internal static class AppLanguages
{
    /// <summary>Code → native name (shown in the language pickers of the app and of Setup).</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> All =
    [
        ("en", "English"),
        ("ar", "العربية"),
        ("fr", "Français"),
    ];

    /// <summary>
    /// True for a Chinese culture name written in Traditional characters: "zh-TW", "zh-HK", "zh-Hant-TW", "zh-CHT" …
    /// An explicit script wins over the region (macOS has "zh-Hans-TW", Simplified in Taiwan); a bare "zh" is Simplified.
    /// Kept for the font chain (SoftPalette): Chinese is no longer a shipped language, but a converted file or a system
    /// font list can still name a Traditional face.
    /// </summary>
    public static bool IsTraditionalChinese(string culture)
    {
        var parts = culture.Trim().Replace('_', '-').Split('-');
        if (!parts[0].Equals("zh", StringComparison.OrdinalIgnoreCase))
            return false;
        var rest = parts.Skip(1).ToArray();
        if (rest.Contains("Hans", StringComparer.OrdinalIgnoreCase))
            return false;
        return rest.Intersect(["Hant", "TW", "HK", "MO", "CHT"], StringComparer.OrdinalIgnoreCase).Any();
    }
}
