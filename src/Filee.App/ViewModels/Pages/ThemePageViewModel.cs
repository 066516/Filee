// Theme: light/dark, softness (corner radius), the donut's opacity, reduce animations, and the window size (UiZoom).
// Changes apply live. Takaconv's colours are fixed by the company theme, so there is nothing here to recolour.

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.ViewModels.Pages;

public sealed partial class ThemePageViewModel : ObservableObject, IDisposable
{
    private readonly UserDataStore _store;
    private readonly ILocalizer _loc;
    private readonly bool _loading;

    public ThemePageViewModel(UserDataStore store, ILocalizer loc, IPlatformServices platform)
    {
        _store = store;
        _loc = loc;
        _loading = true;
        var theme = store.Settings.Theme;
        Styles = Enum.GetValues<ThemeStyle>().Select(s => new Choice<ThemeStyle>(s, loc[$"theme.style.{s}"])).ToList();
        Style = Styles.FirstOrDefault(s => s.Value == theme.Style) ?? Styles[0];
        Modes = Enum.GetValues<ThemeMode>().Select(m => new Choice<ThemeMode>(m, loc[$"theme.mode.{m}"])).ToList();
        Mode = Modes.First(m => m.Value == theme.Mode);
        Accent = Color.Parse(SoftPalette.DefaultAccent);
        CornerRadius = theme.CornerRadius;
        DonutOpacity = Math.Round(Math.Clamp(store.Settings.Donut.Opacity, DonutSettings.MinOpacity, 1) * 100);
        ReduceMotion = theme.ReduceMotion ?? platform.PrefersReducedMotion;
        PreviewItems =
        [
            new DonutItem { Id = "a", Label = "PNG" },
            new DonutItem { Id = "b", Label = "PDF" },
            new DonutItem { Id = "c", Label = "WEBP" },
            new DonutItem { Id = "d", Label = "HWPX" },
            new DonutItem { Id = "e", Label = "JPG 70%", Caption = "JPG" },
            new DonutItem { Id = "f", Label = "TIFF" },
        ];
        _loading = false;
        UiZoom.Changed += OnZoomChanged;
        loc.LanguageChanged += OnLanguageChanged;
    }

    public IReadOnlyList<Choice<ThemeStyle>> Styles { get; }
    public IReadOnlyList<Choice<ThemeMode>> Modes { get; }
    public IReadOnlyList<DonutItem> PreviewItems { get; }

    public bool IsMaterialYou => Style?.Value == ThemeStyle.MaterialYou;

    [ObservableProperty] private Choice<ThemeStyle> _style = null!;
    [ObservableProperty] private Choice<ThemeMode> _mode = null!;

    /// <summary>The fixed company accent, used by the preview donut.</summary>
    [ObservableProperty] private Color _accent;

    public IBrush AccentBrush => new SolidColorBrush(Accent);

    /// <summary>
    /// What the preview donut floats on, standing in for the desktop: a gradient of the accent, so the slices'
    /// opacity shows (on the plain card, slices of its own colour look the same at any opacity).
    /// </summary>
    public IBrush PreviewBackdrop => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(ThemeService.Shade(Accent, 0.55), 0),
            new GradientStop(Accent, 0.5),
            new GradientStop(ThemeService.Shade(Accent, -0.45), 1),
        },
    };
    [ObservableProperty] private double _cornerRadius;
    [ObservableProperty] private bool _reduceMotion;

    /// <summary>
    /// The donut toolbar's opacity in percent: the one setting Donut toolbar → Appearance → Opacity edits too (each
    /// page reads it when it opens). The preview donut follows it at once, the toolbar the next time it opens.
    /// </summary>
    [ObservableProperty] private double _donutOpacity;

    /// <summary>The window size in percent (<see cref="UiZoom.Scale"/>): the same for every window, saved by UiZoom.</summary>
    public double WindowSize => Math.Round(UiZoom.Scale * 100);

    /// <summary>Where the shortcuts do the same, with the platform's key: Ctrl, or Cmd on a Mac.</summary>
    public string WindowSizeHint => _loc.Format("theme.window_size_hint", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl");

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut() => UiZoom.ZoomOut();

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn() => UiZoom.ZoomIn();

    [RelayCommand(CanExecute = nameof(CanResetZoom))]
    private void ResetZoom() => UiZoom.Reset();

    private static bool CanZoomOut() => UiZoom.Scale > UiZoom.Min + 1e-9;
    private static bool CanZoomIn() => UiZoom.Scale < UiZoom.Max - 1e-9;
    private static bool CanResetZoom() => Math.Abs(UiZoom.Scale - 1) > 1e-9;

    // Ctrl + wheel in any window changes it too.
    private void OnZoomChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WindowSize));
        ZoomOutCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ResetZoomCommand.NotifyCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(WindowSizeHint));

    public void Dispose()
    {
        UiZoom.Changed -= OnZoomChanged;
        _loc.LanguageChanged -= OnLanguageChanged;
    }

    partial void OnStyleChanged(Choice<ThemeStyle>? oldValue, Choice<ThemeStyle> newValue)
    {
        OnPropertyChanged(nameof(IsMaterialYou));
        if (!Choices.Restore(oldValue, newValue, v => Style = v))
            Save(t => t.Style = newValue.Value);
    }

    partial void OnModeChanged(Choice<ThemeMode>? oldValue, Choice<ThemeMode> newValue)
    {
        if (!Choices.Restore(oldValue, newValue, v => Mode = v))
            Save(t => t.Mode = newValue.Value);
    }

    [RelayCommand]
    private void SetStyle(ThemeStyle target)
    {
        var match = Styles.FirstOrDefault(s => s.Value == target);
        if (match != null && Style != match)
            Style = match;
    }

    partial void OnCornerRadiusChanged(double value) => Save(t => t.CornerRadius = Math.Round(value));
    partial void OnReduceMotionChanged(bool value) => Save(t => t.ReduceMotion = value);

    partial void OnDonutOpacityChanged(double value)
    {
        if (_loading)
            return;
        _store.Settings.Donut.Opacity = Math.Round(value) / 100;
        _store.SaveSettings();
    }

    private void Save(Action<ThemeSettings> change)
    {
        if (_loading)
            return;
        change(_store.Settings.Theme);
        _store.SaveSettings(); // App re-applies the theme on SettingsChanged
    }
}
