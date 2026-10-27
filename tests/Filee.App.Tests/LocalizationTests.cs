using System.Text.Json;
using System.Text.RegularExpressions;
using Filee.App.Services;

namespace Filee.App.Tests;

/// <summary>Keeps shipped languages in sync so none silently falls back to English.</summary>
public partial class LocalizationTests
{
    private static string I18nDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Filee.slnx")))
                dir = dir.Parent;
            return Path.Combine(dir!.FullName, "src", "Filee.App", "Assets", "i18n");
        }
    }

    private static Dictionary<string, string> Load(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(I18nDirectory, code + ".json")))!;

    public static TheoryData<string> Languages => new(LocalizationService.Languages.Where(l => l.Code != "en").Select(l => l.Code));

    [Fact]
    public void Shared_language_registry_covers_every_shipped_translation_once()
    {
        var codes = LocalizationService.Languages.Select(l => l.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Directory.GetFiles(I18nDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).Order(), codes.Order());
    }

    [Fact]
    public void Arabic_is_offered_under_its_own_name_and_reads_right_to_left()
    {
        Assert.Contains(("ar", "العربية"), LocalizationService.Languages);
        Assert.Equal("ar", LocalizationService.Resolve("AR"));
        Assert.Contains("fonts:Takaconv#Noto Sans Arabic", ThemeService.FontFamilies("ar"));
        Assert.True(SoftPalette.IsRightToLeft("ar"));
        Assert.All(LocalizationService.Languages.Where(l => l.Code != "ar"), l => Assert.False(SoftPalette.IsRightToLeft(l.Code)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Catalogs_have_no_duplicate_keys_or_empty_translations(string code)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(I18nDirectory, code + ".json")));
        var entries = json.RootElement.EnumerateObject().ToList();
        Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Value.GetString()), $"{code}:{e.Name}"));
    }


    [Fact]
    public void The_flip_hint_leaves_its_arrow_to_the_donut()
    {
        // The hub draws the arrow itself (#554): as a character, fonts without "↻" (Linux's bundled Noto Sans) showed
        // a box. The key stays first, so every language still reads "Tab back".
        foreach (var code in LocalizationService.Languages.Select(l => l.Code))
        {
            var strings = Load(code);
            foreach (var key in new[] { "donut.flip_to_back", "donut.flip_to_front" })
            {
                Assert.DoesNotContain("↻", strings[key]);
                Assert.StartsWith("{0} ", strings[key]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_exactly_the_english_keys(string code)
    {
        var english = Load("en").Keys.ToHashSet();
        var other = Load(code).Keys.ToHashSet();

        Assert.Empty(english.Except(other));
        Assert.Empty(other.Except(english));
    }

    [Fact]
    public void Toolbar_tabs_and_reused_categories_exist_in_every_language()
    {
        string[] required =
        [
            "toolbar.tab.all",
            "toolbar.tab.media",
            "toolbar.tab.edit",
            "toolbar.tab.model3d_cad",
            "format.category.Image",
            "format.category.Document",
            "format.category.Geo",
            "format.category.Archive",
            "format.category.Font"
        ];

        foreach (var language in LocalizationService.Languages)
        {
            var translations = Load(language.Code);
            foreach (var key in required)
            {
                Assert.True(translations.TryGetValue(key, out var value), $"{language.Code}:{key} is missing");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language.Code}:{key} is empty");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Format_placeholders_match_english(string code)
    {
        var english = Load("en");
        foreach (var (key, value) in Load(code))
            Assert.True(
                Placeholders(english[key]).SetEquals(Placeholders(value)),
                $"{code}:{key} placeholders differ from English");
    }

    [Fact]
    public void Every_key_used_in_xaml_exists()
    {
        var english = Load("en");
        var src = Path.GetFullPath(Path.Combine(I18nDirectory, "..", ".."));
        var missing = Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(f => DynamicResourceKey().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(k => k.Contains('.') && !k.StartsWith("Icon.", StringComparison.Ordinal))
            .Where(k => !english.ContainsKey(k))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("ar-SA", "ar")]
    [InlineData("fr-FR", "fr")]
    [InlineData("fr-CA", "fr")]
    [InlineData("frr-DE", "en")] // North Frisian is not French
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")] // not a language Takaconv ships: English
    [InlineData("ja-JP", "en")]
    [InlineData("pt-BR", "en")]
    [InlineData("zh-TW", "en")]
    [InlineData("it-IT", "en")]
    [InlineData("", "en")] // Linux without LANG: the invariant culture
    public void Auto_follows_the_os_language_and_falls_back_to_english(string os, string expected) =>
        Assert.Equal(expected, LocalizationService.Resolve("auto", () => os));

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("fr")]
    public void A_chosen_language_is_kept_whatever_the_os_is_in(string chosen) =>
        // The OS isn't even asked: the user picked the language in Settings.
        Assert.Equal(chosen, LocalizationService.Resolve(chosen, () => throw new InvalidOperationException("asked the OS")));

    [Fact]
    public void Auto_is_not_the_language_takaconv_switched_to()
    {
        // Takaconv sets the current UI culture to the language it shows; "auto" must still mean the user's own.
        var before = LocalizationService.Resolve("auto");
        var shown = before == "fr" ? "ar" : "fr";
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(shown);
            Assert.Equal(before, LocalizationService.Resolve("auto"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = culture;
        }
    }

    [Fact]
    public void French_is_offered_under_its_own_name()
    {
        Assert.Contains(("fr", "Français"), LocalizationService.Languages);
        Assert.Equal("fr", LocalizationService.Resolve("FR"));
    }

    /// <summary>Han characters look different in Japanese, Korean and Chinese fonts: the UI language's come first.</summary>
    [Theory]
    [InlineData("ja", "Noto Sans JP", "Yu Gothic UI")]
    [InlineData("ko", "Noto Sans KR", "Malgun Gothic")]
    [InlineData("zh-CN", "Noto Sans SC", "Microsoft YaHei")]
    [InlineData("en", "Noto Sans KR", "Malgun Gothic")]
    public void The_ui_languages_cjk_fonts_come_first(string language, string embedded, string system)
    {
        var families = ThemeService.FontFamilies(language).Split(", ").Select(f => f.Replace("fonts:Takaconv#", "")).ToList();
        string[] embeddedCjk = ["Noto Sans JP", "Noto Sans KR", "Noto Sans SC"];
        string[] systemCjk = ["Yu Gothic UI", "Malgun Gothic", "Microsoft YaHei"];

        Assert.Equal(embedded, families.First(embeddedCjk.Contains));
        Assert.Equal(system, families.First(systemCjk.Contains));
        Assert.True(embeddedCjk.Max(families.IndexOf) < systemCjk.Min(families.IndexOf), "embedded fonts before system ones");
    }

    /// <summary>The embedded Noto Sans SC has Simplified Han shapes: zh-TW lists the system Traditional fonts before it.</summary>
    [Fact]
    public void Traditional_chinese_takes_the_system_traditional_fonts_before_the_embedded_simplified_one()
    {
        var families = ThemeService.FontFamilies("zh-TW").Split(", ").Select(f => f.Replace("fonts:Takaconv#", "")).ToList();

        Assert.True(families.IndexOf("Microsoft JhengHei UI") is >= 0 and var windows && windows < families.IndexOf("Noto Sans SC"));
        Assert.True(families.IndexOf("PingFang TC") is >= 0 and var mac && mac < families.IndexOf("Noto Sans SC"));
        Assert.DoesNotContain("PingFang TC", ThemeService.FontFamilies("zh-CN"));
    }

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{DynamicResource ([\w.]+)\}")]
    private static partial Regex DynamicResourceKey();
}
