// The languages Filee ships (Assets/i18n/<code>.json). The installer (src/Filee.Setup) links this file, so a language
// added here is offered by Setup too.

namespace Filee.App.Services;

internal static class AppLanguages
{
    /// <summary>Code → native name (shown in the language pickers of the app and of Setup).</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> All =
    [
        ("en", "English"),
        ("es", "Español"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("zh-CN", "简体中文"),
        ("pl", "Polski"),
        ("hi", "हिन्दी"),
        ("ru", "Русский"),
        ("ar", "العربية"),
        ("pt-PT", "Português (Portugal)"),
        ("pt-BR", "Português (Brasil)"),
    ];

    /// <summary>Shared Portuguese region policy for the app and Setup, including legacy Inno language codes.</summary>
    public static string? PortugueseVariant(string name)
    {
        var normalized = name.Trim().Replace('_', '-');
        if (normalized.Equals("ptpt", StringComparison.OrdinalIgnoreCase))
            return "pt-PT";
        if (normalized.Equals("ptbr", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";
        var parts = normalized.Split('-');
        if (!parts[0].Equals("pt", StringComparison.OrdinalIgnoreCase))
            return null;
        // A script subtag must not hide Brazil (pt-Latn-BR). Neutral Portuguese and other regions use pt-PT.
        return parts.Skip(1).Any(p => p.Equals("BR", StringComparison.OrdinalIgnoreCase)) ? "pt-BR" : "pt-PT";
    }
}
