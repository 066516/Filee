// Renders every page of Setup and the uninstaller headlessly (Skia) in Filee's languages, light and dark: catches XAML
// and binding errors, text that doesn't fit, and leaves screenshots in bin/.../screenshots for review.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Filee.Setup.Installation;
using Filee.Setup.Payload;
using Filee.Setup.ViewModels;
using Filee.Setup.Views;

namespace Filee.Setup.Tests;

public class SetupWindowTests
{
    /// <summary>Every language Setup offers.</summary>
    public static TheoryData<string> Languages => new(SetupTexts.Languages.Select(l => l.Code));

    private sealed class FakeRunner : ISetupRunner
    {
        public InstallChoices? Installed { get; private set; }
        public bool? DeletedSettings { get; private set; }
        public InstallResult Result { get; set; } = new(new LaunchRequest(@"C:\Program Files\Filee\Filee.exe", "--background"), []);

        public InstallResult Install(InstallChoices choices, IProgress<SetupProgress> progress)
        {
            Installed = choices;
            progress.Report(new SetupProgress(SetupStep.Copying, 0.5));
            return Result;
        }

        public Exception? Uninstall(bool deleteSettings, IProgress<SetupProgress> progress)
        {
            DeletedSettings = deleteSettings;
            return null;
        }
    }

    private static readonly EngineOffer[] Offers =
    [
        new("libreoffice", ["libreoffice", "jre"], 520_000_000, 1_265_000_000, false),
        new("pandoc", ["pandoc"], 42_000_000, 236_000_000, true),
        new("ghostscript", ["ghostscript", "vcruntime"], 20_000_000, 31_000_000, false),
        new("ffmpeg", ["ffmpeg"], 99_000_000, 272_000_000, false),
        new("calibre", ["calibre"], 226_000_000, 663_000_000, false),
    ];

    private static (SetupViewModel Model, SetupTexts Texts, FakeRunner Runner) Model(string language, bool update = false, bool uninstall = false)
    {
        var texts = new SetupTexts(language);
        App.ApplyLook(Application.Current!, texts);
        var runner = new FakeRunner();
        var previous = update
            ? new PreviousInstall(@"C:\Program Files\Filee", "1.12.0", InstallScope.Machine, true, SetupTasks.Defaults, "en", null)
            : null;
        var choices = new InstallChoices(@"C:\Program Files\Filee", InstallScope.Machine, SetupTasks.Defaults, ["pandoc"], texts.Language);
        var engines = update || uninstall ? [] : Offers.Select(o => (o, o.Id == "ffmpeg")).ToList();
        var model = new SetupViewModel(texts, runner, "2.0.0", choices, previous, engines, modernMenuSupported: true,
            neededSpace: 420_000_000, uninstall: uninstall)
        {
            LogFile = @"C:\Users\me\AppData\Local\Temp\Filee-Setup-20261020-101500.log",
            SettingsFolder = @"C:\Users\me\AppData\Roaming\Filee",
        };
        return (model, texts, runner);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Languages))]
    public async Task Every_install_page_renders_and_fits(string language)
    {
        var (model, _, runner) = Model(language);
        var window = Show(model);
        Check(window, $"setup-1-welcome-{language}.png");
        Assert.True(model.IsWelcome);

        await model.NextCommand.ExecuteAsync(null);
        Assert.True(model.IsOptions);
        Check(window, $"setup-2-options-{language}.png");

        await model.NextCommand.ExecuteAsync(null);
        Assert.True(model.IsEngines);
        // FFmpeg is installed already: shown, not offered.
        Assert.False(model.Engines.Single(e => e.Id == "ffmpeg").CanSelect);
        Check(window, $"setup-3-engines-{language}.png", mayScroll: true);

        model.Page = SetupPage.Progress;
        model.StatusText = new SetupTexts(language)["installer.status.copying"];
        model.Percent = 62;
        Check(window, $"setup-4-progress-{language}.png");

        runner.Result = new InstallResult(new LaunchRequest(@"C:\Program Files\Filee\Filee.exe", "--install-engines=pandoc"),
        [
            new ExplorerMenuWarning("Deployment failed with HRESULT: 0x80073CF3, Package failed updates, dependency or conflict validation."),
            new RuntimeWarning([new MissingRuntime(@"engines\rhwp\rhwp.exe", "vcruntime140.dll")]),
        ]);
        model.Page = SetupPage.Engines;
        await model.NextCommand.ExecuteAsync(null);
        Pump();
        Assert.True(model.IsDone);
        Check(window, $"setup-5-done-{language}.png", mayScroll: true);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Update_and_failure_pages_render()
    {
        foreach (var language in new[] { "en", "ko", "ja", "hi" })
        {
            var (model, _, runner) = Model(language, update: true);
            var window = Show(model);
            Assert.False(model.HasEnginesPage);
            Check(window, $"setup-update-1-welcome-{language}.png");
            await model.NextCommand.ExecuteAsync(null);
            Check(window, $"setup-update-2-options-{language}.png");
            // An update offers only what it may change.
            Assert.Equal(model.NextText, new SetupTexts(language)["installer.update"]);

            runner.Result = new InstallResult(null, [], new SetupException("installer.error.space", "space", "C:\\", "420 MB", "12 MB"), RolledBack: false);
            await model.NextCommand.ExecuteAsync(null);
            Pump();
            Assert.True(model.IsFailed);
            Assert.Equal(4, model.ExitCode);
            Check(window, $"setup-update-3-failed-{language}.png");
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Uninstall_pages_render_and_keep_the_settings_by_default()
    {
        foreach (var language in new[] { "en", "ko", "ja", "zh-CN", "pl" })
        {
            var (model, _, runner) = Model(language, uninstall: true);
            var window = Show(model);
            Assert.True(model.IsUninstallPage);
            Assert.False(model.DeleteSettings);
            Assert.Contains(@"C:\Users\me\AppData\Roaming\Filee", model.SettingsText);
            Check(window, $"uninstall-1-confirm-{language}.png");
            await model.NextCommand.ExecuteAsync(null);
            Pump();
            Assert.True(model.IsUninstalled);
            Assert.False(runner.DeletedSettings);
            Assert.Equal(0, model.ExitCode);
            Check(window, $"uninstall-2-done-{language}.png");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dark_windows_follow_the_system()
    {
        var app = Application.Current!;
        try
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;
            var (model, _, _) = Model("en");
            var window = Show(model);
            Check(window, "setup-dark-welcome-en.png");
            model.Page = SetupPage.Options;
            Check(window, "setup-dark-options-en.png");
            window.Close();
            var (uninstall, _, _) = Model("ko", uninstall: true);
            window = Show(uninstall);
            Check(window, "uninstall-dark-confirm-ko.png");
            window.Close();
        }
        finally
        {
            app.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaFact]
    public void Picking_a_language_changes_every_text_and_is_handed_to_filee()
    {
        var (model, texts, _) = Model("en");
        var window = Show(model);
        model.SelectedLanguage = model.Languages.Single(l => l.Code == "ko");
        Pump();
        Assert.Equal("ko", texts.Language);
        Assert.Equal(texts["installer.next"], model.NextText);
        Assert.Equal(texts["installer.step.welcome"], model.Steps[0].Title);
        Assert.Contains("2.0.0", model.WelcomeText);
        var choices = model.Choices();
        Assert.Equal("ko", choices.Language);
        Assert.True(choices.LanguagePicked);
        Check(window, "setup-language-switched-ko.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Choices_follow_the_checkboxes()
    {
        var (model, _, _) = Model("en");
        model.ContextMenu = false;
        Assert.False(model.ModernMenuEnabled);
        model.DesktopIcon = true;
        model.Engines.Single(e => e.Id == "pandoc").Selected = false;
        model.Engines.Single(e => e.Id == "calibre").Selected = true;
        var choices = model.Choices();
        Assert.False(choices.Tasks.ContextMenu);
        Assert.False(choices.Tasks.ModernMenu);
        Assert.True(choices.Tasks.DesktopIcon);
        Assert.Equal(["calibre"], choices.Engines);
        Assert.False(choices.LanguagePicked);
    }

    [AvaloniaFact]
    public void Keyboard_escape_cancels_and_enter_goes_on()
    {
        var (model, _, _) = Model("en");
        var window = Show(model);
        var closed = false;
        model.CloseRequested += (_, _) => closed = true;
        var next = window.FindControl<Button>("NextButton")!;
        Assert.True(next.IsDefault);
        next.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        Assert.True(model.IsOptions);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        Assert.True(closed);
        Assert.Equal(2, model.ExitCode);
    }

    [AvaloniaFact]
    public void Closing_the_window_before_installing_cancels()
    {
        var (model, _, _) = Model("en");
        model.LaunchFilee = true;
        model.WindowClosing();
        Assert.Equal(2, model.ExitCode);
        Assert.Null(model.Launch);
    }

    private static Window Show(SetupViewModel model)
    {
        var window = new SetupWindow(model, silent: false);
        window.Show();
        Pump();
        return window;
    }

    /// <summary>Saves a screenshot; the page must fit the window (no scrolling) and no button may cut its text.</summary>
    private static void Check(Window window, string name, bool mayScroll = false)
    {
        Pump();
        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        if (!mayScroll)
            Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 1, $"{name}: the page needs scrolling ({scroller.Extent.Height} > {scroller.Viewport.Height})");
        // Push buttons only: check boxes wrap their text.
        foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(b => b is not ToggleButton && b.IsEffectivelyVisible))
        {
            button.Measure(Size.Infinity);
            var wanted = button.DesiredSize.Width - button.Margin.Left - button.Margin.Right;
            button.InvalidateMeasure();
            Assert.True(wanted <= button.Bounds.Width + 1, $"{name}: a button cuts its text ({button.Content})");
        }
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var folder = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        using var stream = File.Create(Path.Combine(folder, name));
        frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static void Pump()
    {
        for (var i = 0; i < 40; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
