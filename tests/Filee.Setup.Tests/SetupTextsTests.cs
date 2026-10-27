// Setup speaks Filee's languages from the app's own translation files.

extern alias app;
using System.Text.Json;
using System.Text.RegularExpressions;
using Filee.Engines.Infrastructure;
using Filee.Setup.Platform;
using LocalizationService = app::Filee.App.Services.LocalizationService;

namespace Filee.Setup.Tests;

public partial class SetupTextsTests
{
    private static string Repository
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(dir, "Filee.slnx")))
                dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("Repository root not found.");
            return dir;
        }
    }

    [Fact]
    public void Setup_offers_every_language_takaconv_has()
    {
        Assert.Equal(LocalizationService.Languages.Select(l => (l.Code, l.NativeName)), SetupTexts.Languages);
        Assert.Contains(("ar", "العربية"), SetupTexts.Languages);
        Assert.Contains(("fr", "Français"), SetupTexts.Languages);
        Assert.Equal("Installer", new SetupTexts("fr")["installer.install"]);
    }

    [Theory]
    [InlineData("fr", "en-US", "fr")]
    [InlineData("FR", "en-US", "fr")]
    [InlineData("auto", "fr-FR", "fr")]
    [InlineData(null, "fr-CA", "fr")]
    [InlineData(null, "frr", "en")]
    [InlineData("ar-IQ", "en-US", "ar")]
    [InlineData("AR_sa", "en-US", "ar")]
    [InlineData("auto", "ar-EG", "ar")]
    [InlineData("en", "ar-SA", "en")]
    [InlineData("xx", "fr-BE", "fr")]
    // Takaconv ships English, Arabic and French only: every other system language reads English.
    [InlineData(null, "de-DE", "en")]
    [InlineData("de", "en-US", "en")]
    [InlineData("auto", "de-CH", "en")]
    [InlineData(null, "ja-JP", "en")]
    [InlineData("ko", "en-US", "en")]
    [InlineData(null, "pt-BR", "en")]
    [InlineData(null, "zh-TW", "en")]
    [InlineData(null, "zhcn", "en")]
    [InlineData("xx", "it-IT", "en")]
    [InlineData("klingon", "pl-PL", "en")]
    public void Languages_resolve_like_the_app(string? setting, string windows, string expected) =>
        Assert.Equal(expected, SetupTexts.Resolve(setting, () => windows));

    [Fact]
    public void Texts_fall_back_to_english_and_format()
    {
        var texts = new SetupTexts("fr");
        Assert.Equal("fr", texts.Language);
        Assert.NotEqual("installer.install", texts["installer.install"]);
        Assert.Contains("9.9.9", texts.Format("installer.welcome.fresh", "9.9.9"));
        Assert.Equal("no.such.key", texts["no.such.key"]);
        var changed = 0;
        texts.LanguageChanged += (_, _) => changed++;
        texts.SetLanguage("en");
        Assert.Equal(1, changed);
        Assert.Equal("Install", texts["installer.install"]);
    }

    [Fact]
    public void Every_key_setup_uses_exists_in_english()
    {
        var english = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Repository, "src", "Filee.App", "Assets", "i18n", "en.json")))!;
        var source = Path.Combine(Repository, "src", "Filee.Setup");
        var used = Directory.EnumerateFiles(source, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(f => DynamicResource().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Concat(Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => KeyLiteral().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value)))
            .Where(k => k.Contains('.') && !k.EndsWith('.') && !k.StartsWith("Setup.", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        Assert.Contains("installer.welcome.fresh", used);
        Assert.Contains("general.context_menu", used);
        Assert.DoesNotContain(used, k => !english.ContainsKey(k));
        // Engine names come from the app's own keys.
        Assert.All(EngineDownloads.Packages, p => Assert.True(english.ContainsKey($"engines.package.{p.Id}.name"), p.Id));
    }

    [Fact]
    public void Setup_looks_for_downloaded_engines_where_the_app_puts_them()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The installer uses Windows settings and engine paths.");
        Assert.Equal(EngineEnvironment.DownloadRoot, SetupPaths.Real().EnginesFolder);
        Assert.Equal(Filee.Core.Settings.UserDataStore.DefaultDirectory, SetupPaths.Real().SettingsFolder);
    }

    [GeneratedRegex(@"\{DynamicResource ([\w.]+)\}")]
    private static partial Regex DynamicResource();

    [GeneratedRegex(@"""((?:installer|setup|engines|general|common)\.[\w.]+)""")]
    private static partial Regex KeyLiteral();
}
