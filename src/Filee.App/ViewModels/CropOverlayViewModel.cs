// ViewModel driving the zero-window Crop overlay dialog.

using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Localization;
using Filee.Core.Presets;

namespace Filee.App.ViewModels;

public sealed partial class CropOverlayViewModel : ObservableObject
{
    private readonly IReadOnlyList<string> _files;
    private readonly Preset _preset;
    private readonly ILocalizer _loc;

    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private string _currentFileName = "";
    [ObservableProperty] private string _counterText = "";
    [ObservableProperty] private bool _hasMultipleFiles;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private int _originalWidth;
    [ObservableProperty] private int _originalHeight;
    [ObservableProperty] private string _dimensionsText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _aspectRatio;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAspectFree))]
    [NotifyPropertyChangedFor(nameof(IsAspectOriginal))]
    [NotifyPropertyChangedFor(nameof(IsAspectSquare))]
    [NotifyPropertyChangedFor(nameof(IsAspect45))]
    [NotifyPropertyChangedFor(nameof(IsAspect916))]
    [NotifyPropertyChangedFor(nameof(IsAspect169))]
    [NotifyPropertyChangedFor(nameof(IsAspect43))]
    private string _selectedAspect = "free";

    public bool IsAspectFree => SelectedAspect == "free";
    public bool IsAspectOriginal => SelectedAspect == "original";
    public bool IsAspectSquare => SelectedAspect == "1:1";
    public bool IsAspect45 => SelectedAspect == "4:5";
    public bool IsAspect916 => SelectedAspect == "9:16";
    public bool IsAspect169 => SelectedAspect == "16:9";
    public bool IsAspect43 => SelectedAspect == "4:3";

    public event Action? CloseRequested;
    public event Action? ResetRequested;
    public event Action<double>? AspectRatioChanged;

    public Rect CurrentCropRect { get; set; } = new(0.1, 0.1, 0.8, 0.8);

    public CropOverlayViewModel(IReadOnlyList<string> files, Preset preset, ILocalizer loc)
    {
        _files = files;
        _preset = preset;
        _loc = loc;
        _hasMultipleFiles = files.Count > 1;

        _ = LoadFileAsync(0);
    }

    public async Task LoadFileAsync(int index)
    {
        if (index < 0 || index >= _files.Count)
        {
            CloseRequested?.Invoke();
            return;
        }

        IsBusy = true;
        CurrentIndex = index;
        var path = _files[index];
        CurrentFileName = Path.GetFileName(path);
        CounterText = _loc.Format("crop.page_counter", index + 1, _files.Count);

        try
        {
            var (bitmap, origW, origH) = await CropService.LoadPreviewAsync(path);
            PreviewImage = bitmap;
            OriginalWidth = origW;
            OriginalHeight = origH;
            SelectedAspect = "free";
            AspectRatio = 0;
            CurrentCropRect = new Rect(0.05, 0.05, 0.9, 0.9);
            UpdateDimensions(CurrentCropRect);
            ResetRequested?.Invoke();
        }
        catch (Exception)
        {
            // If failed to load this file, advance or close
            if (index < _files.Count - 1)
                await LoadFileAsync(index + 1);
            else
                CloseRequested?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void UpdateDimensions(Rect norm)
    {
        CurrentCropRect = norm;
        var pixelW = Math.Max(1, (int)Math.Round(norm.Width * OriginalWidth));
        var pixelH = Math.Max(1, (int)Math.Round(norm.Height * OriginalHeight));
        DimensionsText = $"{pixelW} × {pixelH} px";
    }

    [RelayCommand]
    public async Task CropAndSaveAsync()
    {
        if (IsBusy || CurrentIndex >= _files.Count)
            return;

        IsBusy = true;
        var path = _files[CurrentIndex];
        var x = (int)Math.Round(CurrentCropRect.X * OriginalWidth);
        var y = (int)Math.Round(CurrentCropRect.Y * OriginalHeight);
        var w = (int)Math.Round(CurrentCropRect.Width * OriginalWidth);
        var h = (int)Math.Round(CurrentCropRect.Height * OriginalHeight);

        try
        {
            await CropService.CropFileAsync(path, x, y, w, h, _preset);
        }
        catch (Exception)
        {
            // Log/ignore and proceed
        }
        finally
        {
            IsBusy = false;
        }

        if (CurrentIndex < _files.Count - 1)
        {
            await LoadFileAsync(CurrentIndex + 1);
        }
        else
        {
            CloseRequested?.Invoke();
        }
    }

    [RelayCommand]
    public void Reset()
    {
        SelectedAspect = "free";
        AspectRatio = 0;
        AspectRatioChanged?.Invoke(0);
        ResetRequested?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    public void SetAspect(string mode)
    {
        SelectedAspect = mode;
        double ratio = mode switch
        {
            "free" => 0,
            "original" => OriginalHeight > 0 ? (double)OriginalWidth / OriginalHeight : 0,
            "1:1" => 1.0,
            "4:5" => 4.0 / 5.0,
            "9:16" => 9.0 / 16.0,
            "16:9" => 16.0 / 9.0,
            "4:3" => 4.0 / 3.0,
            _ => 0,
        };
        AspectRatio = ratio;
        AspectRatioChanged?.Invoke(ratio);
    }
}
