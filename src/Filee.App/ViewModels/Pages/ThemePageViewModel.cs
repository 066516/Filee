// Theme: light/dark, style (Soft / Material You), accent colour, softness, reduce animations. Changes apply live.

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

public sealed partial class ThemePageViewModel : ObservableObject
{
    private readonly UserDataStore _store;
    private readonly bool _loading;
    private MaterialYouPalette? _currentPalette;

    public ThemePageViewModel(UserDataStore store, ILocalizer loc, IPlatformServices platform)
    {
        _store = store;
        _loading = true;
        var theme = store.Settings.Theme;

        Styles = Enum.GetValues<ThemeStyle>().Select(s => new Choice<ThemeStyle>(s, loc[$"theme.style.{s}"])).ToList();
        Style = Styles.FirstOrDefault(s => s.Value == theme.Style) ?? Styles[0];

        Modes = Enum.GetValues<ThemeMode>().Select(m => new Choice<ThemeMode>(m, loc[$"theme.mode.{m}"])).ToList();
        Mode = Modes.First(m => m.Value == theme.Mode);

        Accent = Color.TryParse(theme.Accent, out var c) ? c : (theme.Style == ThemeStyle.MaterialYou ? Color.Parse("#6750A4") : Colors.MediumPurple);
        AccentHex = $"#{Accent.R:X2}{Accent.G:X2}{Accent.B:X2}";
        CornerRadius = theme.CornerRadius;
        DonutOpacity = Math.Clamp(Math.Round(store.Settings.Donut.Opacity * 100), 10, 100);
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

        UpdateTonalPalette();
        _loading = false;
    }

    public IReadOnlyList<Choice<ThemeStyle>> Styles { get; }
    public IReadOnlyList<Choice<ThemeMode>> Modes { get; }
    public IReadOnlyList<DonutItem> PreviewItems { get; }

    public sealed record InkoraPresetItem(string Key, string Name, Color LightColor, Color DarkColor, Color ActiveColor);

    public IReadOnlyList<InkoraPresetItem> InkoraPresets
    {
        get
        {
            var isDark = Mode?.Value == ThemeMode.Dark || (Mode?.Value == ThemeMode.System && Application.Current?.ActualThemeVariant == ThemeVariant.Dark);
            return ThemeService.InkoraPresets.Select(p =>
            {
                var lc = Color.Parse(p.LightHex);
                var dc = Color.Parse(p.DarkHex);
                return new InkoraPresetItem(p.Key, p.Name, lc, dc, isDark ? dc : lc);
            }).ToList();
        }
    }

    public IReadOnlyList<Color> StandardSwatches =>
        (IsMaterialYou ? ThemeService.MaterialYouSwatches : ThemeService.Swatches).Select(Color.Parse).ToList();

    public IReadOnlyList<Color> PastelSwatches =>
        ThemeService.PastelSwatches.Select(Color.Parse).ToList();

    public IReadOnlyList<Color> Swatches => StandardSwatches;

    public bool IsMaterialYou => Style?.Value == ThemeStyle.MaterialYou;

    [ObservableProperty] private Choice<ThemeStyle> _style = null!;
    [ObservableProperty] private Choice<ThemeMode> _mode = null!;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AccentBrush))] private Color _accent;
    [ObservableProperty] private string _accentHex = "";

    public IBrush AccentBrush => new SolidColorBrush(Accent);
    [ObservableProperty] private double _cornerRadius;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DonutSliceOpacity))] private double _donutOpacity = 96;
    public double DonutSliceOpacity => DonutOpacity / 100.0;
    [ObservableProperty] private bool _reduceMotion;

    partial void OnDonutOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(DonutSliceOpacity));
        if (!_loading)
        {
            var clamped = Math.Clamp(value, 10, 100);
            _store.Settings.Donut.Opacity = Math.Round(clamped) / 100.0;
            _store.SaveSettings();
        }
    }

    // Material You Tonal Palette preview tokens
    public IBrush TonalPrimary => new SolidColorBrush(_currentPalette?.Primary ?? Accent);
    public IBrush TonalPrimaryContainer => new SolidColorBrush(_currentPalette?.PrimaryContainer ?? Accent);
    public IBrush TonalSecondaryContainer => new SolidColorBrush(_currentPalette?.SecondaryContainer ?? Accent);
    public IBrush TonalSurfaceContainer => new SolidColorBrush(_currentPalette?.SurfaceContainer ?? Colors.Gray);
    public IBrush TonalSurface => new SolidColorBrush(_currentPalette?.Surface ?? Colors.White);

    partial void OnStyleChanged(Choice<ThemeStyle>? oldValue, Choice<ThemeStyle> newValue)
    {
        OnPropertyChanged(nameof(IsMaterialYou));
        OnPropertyChanged(nameof(StandardSwatches));
        OnPropertyChanged(nameof(Swatches));
        UpdateTonalPalette();
        if (!Choices.Restore(oldValue, newValue, v => Style = v))
            Save(t => t.Style = newValue.Value);
    }

    partial void OnModeChanged(Choice<ThemeMode>? oldValue, Choice<ThemeMode> newValue)
    {
        OnPropertyChanged(nameof(InkoraPresets));
        UpdateTonalPalette();
        if (!Choices.Restore(oldValue, newValue, v => Mode = v))
            Save(t => t.Mode = newValue.Value);
    }

    partial void OnAccentChanged(Color value)
    {
        var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        if (!string.Equals(AccentHex, hex, StringComparison.OrdinalIgnoreCase))
            AccentHex = hex;
        UpdateTonalPalette();
        Save(t => t.Accent = hex);
    }

    // Custom colour typed as #RRGGBB; ignored until it parses.
    partial void OnAccentHexChanged(string value)
    {
        if (Color.TryParse(value.Trim(), out var color) && color != Accent)
            Accent = Color.FromRgb(color.R, color.G, color.B);
    }

    partial void OnCornerRadiusChanged(double value) => Save(t => t.CornerRadius = Math.Round(value));
    partial void OnReduceMotionChanged(bool value) => Save(t => t.ReduceMotion = value);

    [RelayCommand]
    private void PickSwatch(Color color) => Accent = color;

    [RelayCommand]
    private void PickInkoraPreset(InkoraPresetItem preset) => Accent = preset.ActiveColor;

    [RelayCommand]
    private void SetStyle(ThemeStyle target)
    {
        var match = Styles.FirstOrDefault(s => s.Value == target);
        if (match != null && Style != match)
            Style = match;
    }

    private void UpdateTonalPalette()
    {
        var isDark = Mode?.Value == ThemeMode.Dark ||
                     (Mode?.Value == ThemeMode.System && Application.Current?.ActualThemeVariant == ThemeVariant.Dark);
        _currentPalette = MaterialYouPalette.Generate(Accent, isDark);
        OnPropertyChanged(nameof(TonalPrimary));
        OnPropertyChanged(nameof(TonalPrimaryContainer));
        OnPropertyChanged(nameof(TonalSecondaryContainer));
        OnPropertyChanged(nameof(TonalSurfaceContainer));
        OnPropertyChanged(nameof(TonalSurface));
    }

    private void Save(Action<ThemeSettings> change)
    {
        if (_loading)
            return;
        change(_store.Settings.Theme);
        _store.SaveSettings(); // App re-applies the theme on SettingsChanged
    }
}
