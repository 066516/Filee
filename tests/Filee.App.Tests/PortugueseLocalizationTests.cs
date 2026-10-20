// Portuguese locale selection, live resources and regional formatting, independent of the host OS language.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.ViewModels.Pages;
using Filee.Core.Localization;
using Filee.Core.Presets;

namespace Filee.App.Tests;

public class PortugueseLocalizationTests
{
    [Theory]
    [InlineData("auto", "pt-PT", "pt-PT")]
    [InlineData("AUTO", "pt-BR", "pt-BR")]
    [InlineData("auto", "PT-br", "pt-BR")]
    [InlineData("auto", "pt-Latn-BR", "pt-BR")]
    [InlineData("auto", "pt", "pt-PT")]
    [InlineData("auto", "pt-AO", "pt-PT")]
    [InlineData("auto", "pt-MZ", "pt-PT")]
    [InlineData("auto", "pt-CV", "pt-PT")]
    [InlineData("pt-PT", "pt-BR", "pt-PT")]
    [InlineData("PT-br", "pt-PT", "pt-BR")]
    [InlineData("en", "pt-BR", "en")]
    [InlineData("invalid", "pt-BR", "pt-BR")]
    [InlineData("auto", "de-DE", "en")]
    [InlineData("auto", "", "en")]
    [InlineData("auto", "ptx", "en")]
    [InlineData("auto", "ja-JP", "ja")]
    [InlineData("auto", "es-MX", "es")]
    public void Language_selection_respects_the_region_and_manual_override(string setting, string system, string expected) =>
        Assert.Equal(expected, LocalizationService.Resolve(setting, system));

    [Theory]
    [InlineData("pt-PT", "Português (Portugal)")]
    [InlineData("pt-BR", "Português (Brasil)")]
    public void Both_variants_are_offered_under_their_native_names(string code, string name)
    {
        Assert.Contains((code, name), LocalizationService.Languages);
        Assert.Equal(code, LocalizationService.Resolve(code.ToUpperInvariant()));
    }

    [AvaloniaFact]
    public void Resources_and_toggle_labels_follow_live_switches_between_both_variants()
    {
        TestServices.EnsureInitialized("en");
        var loc = AppHost.Get<LocalizationService>();
        var changes = new List<string>();
        void Changed(object? sender, EventArgs args) => changes.Add(loc.Language);
        loc.LanguageChanged += Changed;
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, text.GetResourceObservable("common.save"));
        var toggle = new ToggleSwitch();
        var window = new Window { Content = new StackPanel { Children = { text, toggle } } };
        try
        {
            window.Show();
            foreach (var (code, save) in new[] { ("pt-PT", "Guardar"), ("pt-BR", "Salvar") })
            {
                Assert.True(loc.NeedsUpdate(code));
                loc.SetLanguage(code);
                Assert.False(loc.NeedsUpdate(code));
                Assert.Equal(save, text.Text);
                Assert.Equal("Ativado", toggle.OnContent);
                Assert.Equal("Desativado", toggle.OffContent);
                Assert.Equal(code, CultureInfo.CurrentUICulture.Name);
                Assert.Equal(code, CultureInfo.DefaultThreadCurrentUICulture!.Name);
                Assert.Equal("unknown.translation.key", loc["unknown.translation.key"]);
            }
            Assert.Equal(["pt-PT", "pt-BR"], changes);
        }
        finally
        {
            window.Close();
            loc.LanguageChanged -= Changed;
            TestServices.EnsureInitialized("en");
        }
    }

    [AvaloniaTheory]
    [InlineData("pt-PT", "pt-PT", "12,5/s · 25")]
    [InlineData("pt-BR", "pt-BR", "12,5/s · 25")]
    [InlineData("pt-PT", "en-US", "12.5/s · 25")]
    [InlineData("pt-BR", "en-US", "12.5/s · 25")]
    public void Ui_language_preserves_the_users_regional_number_format(string language, string region, string expected)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(region);
            TestServices.EnsureInitialized(language);
            Assert.Equal(region, CultureInfo.CurrentCulture.Name);
            Assert.Equal(expected, AppHost.Get<LocalizationService>().Format("download.badge", 12.5, 25));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            TestServices.EnsureInitialized("en");
        }
    }

    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Catalogs_preserve_format_items_tokens_and_line_breaks(string language)
    {
        TestServices.EnsureInitialized(language);
        var english = LocalizationService.LoadStrings("en");
        var translated = LocalizationService.LoadStrings(language);
        Assert.Equal(english.Keys.Order(), translated.Keys.Order());
        foreach (var (key, source) in english)
        {
            var value = translated[key];
            // Count repeated placeholders too, and retain the named filename-pattern tokens verbatim.
            Assert.Equal(Tokens(source), Tokens(value));
            Assert.Equal(source.Count(c => c == '\n'), value.Count(c => c == '\n'));
            Assert.Equal(value.Normalize(NormalizationForm.FormC), value);
            if (Regex.IsMatch(source, @"\{\d"))
            {
                var format = CompositeFormat.Parse(value);
                var args = Enumerable.Repeat<object>(123, format.MinimumArgumentCount).ToArray();
                Assert.DoesNotContain("{", string.Format(CultureInfo.GetCultureInfo(language), format, args));
            }
        }
        // These are real folder names/CLI arguments, not translatable labels.
        Assert.Contains("\"originals\"", translated["watch.originals_move"]);
        Assert.Contains("\"converted\"", translated["watch.output_placeholder"]);
        Assert.Contains("filee watch <folder> --to <format>", translated["watch.cli_hint"]);
        Assert.Contains(@"IMG_(\d+)", translated["watch.rename_find"]);
        Assert.Contains("$1", translated["watch.rename_replace"]);
    }

    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Descriptive_engine_names_are_localized_and_brands_are_preserved(string language)
    {
        TestServices.EnsureInitialized(language);
        var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>());
        try
        {
            vm.Navigate("engines");
            var engines = ((EnginesPageViewModel)vm.CurrentPage).Engines;
            Assert.Equal("Dados (integrado)", engines.Single(e => e.Converter.Id == "data").Name);
            Assert.Equal("ImageMagick", engines.Single(e => e.Converter.Id == "magick").Name);
            Assert.DoesNotContain(engines, e => e.Name.Contains("built-in", StringComparison.Ordinal));
        }
        finally
        {
            vm.Dispose();
        }
    }

    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Count_labels_work_with_zero_one_and_many_without_parenthesized_plurals(string language)
    {
        TestServices.EnsureInitialized(language);
        var loc = AppHost.Get<LocalizationService>();
        foreach (var count in new[] { 0, 1, 2, 21 })
        {
            Assert.Equal($"Conversões no histórico: {count}", loc.Format("general.history_count", count));
            Assert.Equal($"Conversões: {count}, sem falhas", loc.Format("watch.results_ok", count));
            Assert.Equal($"Conversões: {count} · Falhas: 1", loc.Format("watch.results_failed", count, 1));
        }
    }

    private static string[] Tokens(string value) =>
        Regex.Matches(value, @"\{[^{}]+\}").Select(m => m.Value).Order().ToArray();

    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Preset_numbers_use_regional_separators_without_changing_saved_values(string region)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(region);
            TestServices.EnsureInitialized(region);
            var loc = AppHost.Get<ILocalizer>();
            var preset = new Preset { Name = "Animation", TargetFormat = "gif" };
            var vm = new PresetEditorViewModel(preset, loc);
            Assert.Equal("0,5×", vm.AnimationSpeeds.Single(s => s.Value == 0.5).Label);
            Assert.Contains(MediaOptions.MaxFrames.ToString("N0", CultureInfo.CurrentCulture), vm.FrameEstimate);
            vm.AnimationSpeed = vm.AnimationSpeeds.Single(s => s.Value == 0.5);
            Assert.True(vm.Apply());
            Assert.Equal(0.5, preset.Image.PlaybackSpeed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            TestServices.EnsureInitialized("en");
        }
    }
}
