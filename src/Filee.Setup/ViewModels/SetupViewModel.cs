// The wizard: Welcome (with the language) → Options (folder, Explorer menu, start at sign-in, PATH, desktop icon) →
// Engines (fresh installs, when there is something to download) → Install (progress) → Done ("Launch Filee").
// An update shows only what it may change (desktop icon, PATH); Filee's own settings stay as they are. The uninstaller
// uses the same window: Uninstall (keep or delete the settings) → progress → Done.

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Setup.Installation;
using Filee.Setup.Payload;

namespace Filee.Setup.ViewModels;

public enum SetupPage
{
    Welcome,
    Options,
    Engines,
    Progress,
    Done,
    Failed,
    Uninstall,
    Uninstalled,
}

/// <summary>What the wizard does when the user goes ahead (the real installer, or a fake in tests).</summary>
public interface ISetupRunner
{
    InstallResult Install(InstallChoices choices, IProgress<SetupProgress> progress);

    /// <summary>Uninstalls; null when it worked, else the error.</summary>
    Exception? Uninstall(bool deleteSettings, IProgress<SetupProgress> progress);
}

/// <summary>A language in the picker.</summary>
public sealed record LanguageItem(string Code, string Name)
{
    public override string ToString() => Name;
}

/// <summary>An engine package on the Engines page.</summary>
public sealed partial class EngineItem(EngineOffer offer, bool installed) : ObservableObject
{
    public EngineOffer Offer { get; } = offer;
    public string Id => Offer.Id;
    public bool Installed { get; } = installed;
    public bool CanSelect => !Installed;

    [ObservableProperty] private bool _selected = !installed && offer.Suggested;
    [ObservableProperty] private string _name = offer.Id;
    [ObservableProperty] private string _detail = "";
}

/// <summary>A step in the left column.</summary>
public sealed partial class StepItem(string key, SetupPage[] pages) : ObservableObject
{
    public string Key { get; } = key;
    public SetupPage[] Pages { get; } = pages;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isDone;
}

public sealed partial class SetupViewModel : ObservableObject
{
    private readonly SetupTexts _texts;
    private readonly ISetupRunner _runner;
    private readonly PreviousInstall? _previous;
    private readonly InstallChoices _initial;
    private readonly long _neededSpace;
    private bool _languagePicked;
    private InstallResult? _result;

    /// <param name="version">The Filee version Setup installs (or the uninstaller removes).</param>
    /// <param name="initial">The choices from the command line and the defaults.</param>
    /// <param name="engines">Packages offered on a fresh install, with whether each is installed already.</param>
    /// <param name="modernMenuSupported">Windows 11 x64 and administrator rights: the top-level menu can be offered.</param>
    /// <param name="uninstall">The uninstaller's window.</param>
    public SetupViewModel(SetupTexts texts, ISetupRunner runner, string version, InstallChoices initial, PreviousInstall? previous,
        IReadOnlyList<(EngineOffer Offer, bool Installed)> engines, bool modernMenuSupported, long neededSpace, bool uninstall = false,
        bool languagePicked = false)
    {
        _texts = texts;
        _runner = runner;
        _previous = previous;
        _initial = initial;
        _neededSpace = neededSpace;
        _languagePicked = languagePicked;
        Version = version;
        IsUninstall = uninstall;
        IsUpdate = previous is not null && !uninstall;
        ModernMenuSupported = modernMenuSupported;
        Languages = SetupTexts.Languages.Select(l => new LanguageItem(l.Code, l.NativeName)).ToList();
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == texts.Language) ?? Languages[0];
        _folder = initial.Folder;
        _contextMenu = initial.Tasks.ContextMenu;
        _modernMenu = initial.Tasks.ModernMenu && modernMenuSupported;
        _startup = initial.Tasks.Startup;
        _cli = initial.Tasks.Cli;
        _desktopIcon = initial.Tasks.DesktopIcon;
        Engines = new ObservableCollection<EngineItem>(engines.Select(e => new EngineItem(e.Offer, e.Installed)));
        foreach (var engine in Engines)
        {
            engine.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EngineItem.Selected))
                    UpdateEngineSelection();
            };
        }
        var steps = new List<StepItem>();
        if (uninstall)
        {
            steps.Add(new StepItem("installer.step.uninstall", [SetupPage.Uninstall]));
            steps.Add(new StepItem("installer.step.remove", [SetupPage.Progress, SetupPage.Failed]));
            steps.Add(new StepItem("installer.step.done", [SetupPage.Uninstalled]));
        }
        else
        {
            steps.Add(new StepItem("installer.step.welcome", [SetupPage.Welcome]));
            steps.Add(new StepItem("installer.step.options", [SetupPage.Options]));
            if (HasEnginesPage)
                steps.Add(new StepItem("installer.step.engines", [SetupPage.Engines]));
            steps.Add(new StepItem("installer.step.install", [SetupPage.Progress, SetupPage.Failed]));
            steps.Add(new StepItem("installer.step.done", [SetupPage.Done]));
        }
        Steps = steps;
        _page = uninstall ? SetupPage.Uninstall : SetupPage.Welcome;
        texts.LanguageChanged += (_, _) => RefreshTexts();
        RefreshTexts();
    }

    public string Version { get; }
    public bool IsUninstall { get; }
    public bool IsUpdate { get; }
    public bool IsFresh => !IsUpdate && !IsUninstall;
    public bool ModernMenuSupported { get; }
    public IReadOnlyList<LanguageItem> Languages { get; }
    public ObservableCollection<EngineItem> Engines { get; }
    public IReadOnlyList<StepItem> Steps { get; }

    /// <summary>Fresh installs offer the engines that aren't downloaded yet.</summary>
    public bool HasEnginesPage => IsFresh && Engines.Any(e => !e.Installed);

    /// <summary>Raised when the window should close (finished, cancelled).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Asks the view for a folder (the folder picker); null when cancelled.</summary>
    public Func<string, Task<string?>>? PickFolder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsOptions), nameof(IsEngines), nameof(IsProgress), nameof(IsDone),
        nameof(IsFailed), nameof(IsUninstallPage), nameof(IsUninstalled), nameof(CanGoBack), nameof(ShowCancel), nameof(NextText),
        nameof(ShowNext))]
    private SetupPage _page;

    public bool IsWelcome => Page == SetupPage.Welcome;
    public bool IsOptions => Page == SetupPage.Options;
    public bool IsEngines => Page == SetupPage.Engines;
    public bool IsProgress => Page == SetupPage.Progress;
    public bool IsDone => Page == SetupPage.Done;
    public bool IsFailed => Page == SetupPage.Failed;
    public bool IsUninstallPage => Page == SetupPage.Uninstall;
    public bool IsUninstalled => Page == SetupPage.Uninstalled;

    [ObservableProperty] private LanguageItem _selectedLanguage;
    [ObservableProperty] private string _folder;
    [ObservableProperty] private bool _contextMenu;
    [ObservableProperty] private bool _modernMenu;
    [ObservableProperty] private bool _startup;
    [ObservableProperty] private bool _cli;
    [ObservableProperty] private bool _desktopIcon;
    [ObservableProperty] private bool _launchFilee = true;
    [ObservableProperty] private bool _deleteSettings;

    [ObservableProperty] private string _welcomeText = "";
    [ObservableProperty] private string _spaceText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private double _percent;
    [ObservableProperty] private string _progressTitle = "";
    [ObservableProperty] private string _doneText = "";
    [ObservableProperty] private string _warningText = "";
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _errorTitle = "";
    [ObservableProperty] private string _uninstallText = "";
    [ObservableProperty] private string _uninstalledText = "";
    [ObservableProperty] private string _engineSelectionText = "";
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _settingsText = "";

    /// <summary>Setup's log file (named on the error page).</summary>
    public string? LogFile
    {
        get;
        set
        {
            field = value;
            RefreshTexts();
        }
    }

    /// <summary>Filee's settings folder (named on the uninstall page).</summary>
    public string SettingsFolder
    {
        get;
        set
        {
            field = value;
            RefreshTexts();
        }
    } = "";

    /// <summary>The page has a working Back button.</summary>
    public bool CanGoBack => Page is SetupPage.Options or SetupPage.Engines;

    public bool ShowCancel => Page is SetupPage.Welcome or SetupPage.Options or SetupPage.Engines or SetupPage.Uninstall;

    public bool ShowNext => Page != SetupPage.Progress;

    public string NextText => Page switch
    {
        SetupPage.Options when !HasEnginesPage => _texts[IsUpdate ? "installer.update" : "installer.install"],
        SetupPage.Engines => _texts["installer.install"],
        SetupPage.Done => _texts["installer.finish"],
        SetupPage.Failed or SetupPage.Uninstalled => _texts["common.close"],
        SetupPage.Uninstall => _texts["installer.uninstall.button"],
        _ => _texts["installer.next"],
    };

    /// <summary>The fresh install's top-level menu option needs the Explorer menu option.</summary>
    public bool ModernMenuEnabled => ContextMenu;

    /// <summary>Exit code for Setup (Inno Setup's: 0 done, 2 cancelled before installing, 4 failed during installing).</summary>
    public int ExitCode { get; private set; } = 2;

    /// <summary>What to start when the window closes (Done with "Launch Filee").</summary>
    public LaunchRequest? Launch { get; private set; }

    public InstallResult? Result => _result;

    partial void OnSelectedLanguageChanged(LanguageItem value)
    {
        if (value is null || value.Code == _texts.Language)
            return;
        _languagePicked = true;
        _texts.SetLanguage(value.Code);
    }

    partial void OnContextMenuChanged(bool value) => OnPropertyChanged(nameof(ModernMenuEnabled));

    partial void OnPageChanged(SetupPage value)
    {
        var current = Steps.ToList().FindIndex(s => s.Pages.Contains(value));
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsCurrent = i == current;
            Steps[i].IsDone = i < current;
        }
    }

    /// <summary>Recomputes the texts with arguments (after a language change).</summary>
    private void RefreshTexts()
    {
        WelcomeText = IsUpdate
            ? _texts.Format("installer.welcome.update", _previous?.Version ?? "?", Version)
            : _texts.Format("installer.welcome.fresh", Version);
        SpaceText = _texts.Format("installer.options.space", Installer.FormatSize(_neededSpace));
        UninstallText = _texts.Format("installer.uninstall.text", Version);
        SettingsText = _texts.Format("installer.uninstall.settings_where", SettingsFolder);
        LogText = LogFile is null ? "" : _texts.Format("installer.error.log", LogFile);
        foreach (var step in Steps)
            step.Title = _texts[step.Key];
        foreach (var engine in Engines)
        {
            engine.Name = _texts[$"engines.package.{engine.Id}.name"];
            engine.Detail = engine.Installed
                ? _texts["engines.state.installed"]
                : _texts.Format("engines.package.size", Installer.FormatSize(engine.Offer.DownloadSize), Installer.FormatSize(engine.Offer.InstalledSize));
        }
        UpdateEngineSelection();
        ErrorTitle = _texts[IsUninstall ? "installer.error.uninstall_title" : "installer.error.title"];
        ProgressTitle = _texts[IsUninstall ? "installer.uninstall.progress" : IsUpdate ? "installer.progress.update" : "installer.progress.install"];
        OnPropertyChanged(nameof(NextText));
        OnPageChanged(Page);
    }

    private void UpdateEngineSelection()
    {
        var selected = Engines.Where(e => e.Selected).Select(e => e.Name).ToList();
        EngineSelectionText = selected.Count == 0 ? _texts["setup.nothing_selected"] : _texts.Format("setup.selection", string.Join(", ", selected));
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (PickFolder is null)
            return;
        if (await PickFolder(Folder) is { } picked)
        {
            // A folder of its own, like Inno Setup: picking "D:\Apps" installs into "D:\Apps\Filee".
            Folder = Path.GetFileName(Path.TrimEndingDirectorySeparator(picked)).Equals("Filee", StringComparison.OrdinalIgnoreCase)
                ? picked
                : Path.Combine(picked, "Filee");
        }
    }

    [RelayCommand]
    private void Back()
    {
        Page = Page switch
        {
            SetupPage.Engines => SetupPage.Options,
            SetupPage.Options => SetupPage.Welcome,
            _ => Page,
        };
    }

    /// <summary>The window's close button: Cancel before installing, Finish on the Done page.</summary>
    public void WindowClosing()
    {
        if (Page == SetupPage.Done)
            Launch = LaunchFilee ? _result?.Launch : null;
        else if (Page is SetupPage.Welcome or SetupPage.Options or SetupPage.Engines or SetupPage.Uninstall)
            ExitCode = 2;
    }

    [RelayCommand]
    private void Cancel()
    {
        ExitCode = 2;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        switch (Page)
        {
            case SetupPage.Welcome:
                Page = SetupPage.Options;
                break;
            case SetupPage.Options when HasEnginesPage:
                Page = SetupPage.Engines;
                break;
            case SetupPage.Options or SetupPage.Engines:
                await InstallAsync();
                break;
            case SetupPage.Uninstall:
                await UninstallAsync();
                break;
            case SetupPage.Done:
                Launch = LaunchFilee ? _result?.Launch : null;
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
            default:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>The choices as they stand on the pages.</summary>
    public InstallChoices Choices()
    {
        var tasks = new SetupTasks(ContextMenu, ContextMenu && ModernMenu && ModernMenuSupported, Startup, Cli, DesktopIcon);
        return _initial with
        {
            Folder = Folder,
            Tasks = tasks,
            Engines = IsFresh ? Engines.Where(e => e.Selected && !e.Installed).Select(e => e.Id).ToList() : [],
            Language = _texts.Language,
            LanguagePicked = _languagePicked,
        };
    }

    /// <summary>Installs with the current choices (also what /SILENT does right away).</summary>
    public async Task InstallAsync()
    {
        RefreshTexts();
        var choices = Choices();
        Page = SetupPage.Progress;
        Percent = 0;
        var progress = new SyncProgress<SetupProgress>(p => Dispatcher.UIThread.Post(() => ShowProgress(p)));
        _result = await Task.Run(() => _runner.Install(choices, progress));
        if (_result.Succeeded)
        {
            ExitCode = 0;
            DoneText = _texts[IsUpdate ? "installer.done.updated" : "installer.done.text"]
                       + (choices.Engines.Count > 0 ? Environment.NewLine + Environment.NewLine + _texts["installer.done.engines"] : "");
            WarningText = string.Join(Environment.NewLine + Environment.NewLine, _result.Warnings.Select(WarningMessage));
            Page = SetupPage.Done;
        }
        else
        {
            ExitCode = 4;
            ErrorText = ErrorMessage(_result.Error!) + Environment.NewLine + Environment.NewLine
                        + _texts[_result.RolledBack ? IsUpdate ? "installer.error.rolled_back" : "installer.error.undone" : "installer.error.nothing_changed"];
            Page = SetupPage.Failed;
        }
    }

    /// <summary>Uninstalls (also what a silent uninstall does right away).</summary>
    public async Task UninstallAsync()
    {
        RefreshTexts();
        var deleteSettings = DeleteSettings;
        Page = SetupPage.Progress;
        Percent = 0;
        var progress = new SyncProgress<SetupProgress>(p => Dispatcher.UIThread.Post(() => ShowProgress(p)));
        var error = await Task.Run(() => _runner.Uninstall(deleteSettings, progress));
        if (error is null)
        {
            ExitCode = 0;
            UninstalledText = _texts["installer.uninstall.done"]
                              + (deleteSettings ? "" : Environment.NewLine + Environment.NewLine + _texts["installer.uninstall.settings_kept"]);
            Page = SetupPage.Uninstalled;
        }
        else
        {
            ExitCode = 1;
            ErrorText = ErrorMessage(error);
            Page = SetupPage.Failed;
        }
    }

    private void ShowProgress(SetupProgress progress)
    {
        Percent = Math.Round(progress.Fraction * 100, 1);
        StatusText = _texts[progress.Step switch
        {
            SetupStep.Verifying => "installer.status.verifying",
            SetupStep.Closing => "installer.status.closing",
            SetupStep.RemovingOld => "installer.status.removing_old",
            SetupStep.Copying => "installer.status.copying",
            SetupStep.ExplorerMenu => "installer.status.explorer_menu",
            SetupStep.RollingBack => "installer.status.rolling_back",
            SetupStep.Removing => "installer.status.removing",
            _ => "installer.status.finishing",
        }];
    }

    /// <summary>A warning in the UI language.</summary>
    public string WarningMessage(SetupWarning warning) => SetupMessages.Warning(_texts, warning);

    /// <summary>An error in the UI language.</summary>
    public string ErrorMessage(Exception error) => SetupMessages.Error(_texts, error);
}

/// <summary>Warnings and errors in the UI language.</summary>
public static class SetupMessages
{
    public static string Warning(SetupTexts texts, SetupWarning warning) => warning switch
    {
        ExplorerMenuWarning menu => texts.Format("installer.warning.explorer_menu", menu.Error),
        RuntimeWarning runtime => texts.Format("installer.warning.runtime",
            string.Join(", ", runtime.Missing.Select(m => Path.GetFileName(m.Program)).Distinct(StringComparer.OrdinalIgnoreCase)),
            string.Join(", ", runtime.Missing.Select(m => m.Dll).Distinct(StringComparer.OrdinalIgnoreCase)),
            SetupNames.VcRedistUrl),
        _ => warning.ToString(),
    };

    /// <summary>The technical message only for errors nobody expected.</summary>
    public static string Error(SetupTexts texts, Exception error) => error switch
    {
        SetupException known => texts.Format(known.TextKey, [.. known.Arguments]),
        InvalidDataException => texts["installer.error.damaged"],
        UnauthorizedAccessException or IOException => texts.Format("installer.error.files", error.Message),
        _ => texts.Format("installer.error.unexpected", error.Message),
    };
}
