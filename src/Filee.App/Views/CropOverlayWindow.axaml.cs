using Avalonia.Controls;
using Avalonia.Input;
using Filee.App.ViewModels;

namespace Filee.App.Views;

public partial class CropOverlayWindow : Window
{
    public CropOverlayWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CropOverlayViewModel vm)
        {
            vm.CloseRequested += Close;
            vm.ResetRequested += () => Canvas.ResetCrop();
            vm.AspectRatioChanged += ratio => Canvas.SetAspectRatio(ratio);
            Canvas.CropRectChanged += rect => vm.UpdateDimensions(rect);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Allow dragging the window when clicking on empty header space
        var pt = e.GetPosition(this);
        if (pt.Y < 50 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
