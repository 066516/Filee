// Plays an animated WebP (Avalonia shows only its first frame): SkiaSharp's codec decodes one frame at a time into a
// bitmap, timed by the frames' own durations. Only the current frame is kept in memory. Without animations it shows
// one representative frame. A minimal player for Setup's feature clips; an app-wide AnimatedImage control could
// replace it later.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace Filee.Setup.Controls;

public sealed class AnimatedWebp : Control, IDisposable
{
    /// <summary>An avares:// URI.</summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AnimatedWebp, string?>(nameof(Source));

    private SKData? _data;
    private SKCodec? _codec;
    private SKCodecFrameInfo[] _frames = [];
    private SKBitmap? _canvas;
    private WriteableBitmap? _bitmap;
    private DispatcherTimer? _timer;
    private int _frame = -1;

    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>The frame shown now (tests).</summary>
    public int Frame => _frame;

    public int FrameCount => Math.Max(1, _frames.Length);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Load();
            InvalidateMeasure();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_codec is null)
            Load();
        Schedule();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
    }

    private void Load()
    {
        Release();
        if (string.IsNullOrEmpty(Source))
            return;
        using (var stream = AssetLoader.Open(new Uri(Source)))
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            _data = SKData.CreateCopy(copy.ToArray());
        }
        _codec = SKCodec.Create(_data);
        if (_codec is null)
            return;
        _frames = _codec.FrameInfo;
        var info = new SKImageInfo(_codec.Info.Width, _codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _canvas = new SKBitmap(info);
        _bitmap = new WriteableBitmap(new PixelSize(info.Width, info.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        // Without animations: a frame from the middle, where the clip shows what it is about.
        ShowFrame(SetupMotion.Enabled ? 0 : FrameCount * 45 / 100);
    }

    /// <summary>Decodes frame <paramref name="index"/>, from the frame shown now when the codec needs it.</summary>
    internal void ShowFrame(int index)
    {
        if (_codec is null || _canvas is null || _bitmap is null)
            return;
        index = Math.Clamp(index, 0, FrameCount - 1);
        // A frame that builds on earlier ones needs them decoded first (in order).
        var required = _frames.Length > index ? _frames[index].RequiredFrame : -1;
        if (required >= 0 && !(_frame >= required && _frame == index - 1))
        {
            for (var i = 0; i < index; i++)
                Decode(i);
        }
        Decode(index);
        using (var target = _bitmap.Lock())
        {
            var rowBytes = Math.Min(target.RowBytes, _canvas.RowBytes);
            for (var y = 0; y < _canvas.Height; y++)
            {
                unsafe
                {
                    Buffer.MemoryCopy((byte*)_canvas.GetPixels() + y * _canvas.RowBytes, (byte*)target.Address + y * target.RowBytes, target.RowBytes, rowBytes);
                }
            }
        }
        InvalidateVisual();
    }

    private void Decode(int index)
    {
        var required = _frames.Length > index ? _frames[index].RequiredFrame : -1;
        var prior = required >= 0 && _frame >= required && _frame < index ? _frame : -1;
        var options = new SKCodecOptions(index, prior);
        _codec!.GetPixels(_canvas!.Info, _canvas.GetPixels(), options);
        _frame = index;
    }

    private void Schedule()
    {
        if (!SetupMotion.Enabled || _frames.Length < 2 || VisualRoot is null)
            return;
        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) => Next());
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _frames[Math.Max(0, _frame)].Duration));
        _timer.Start();
    }

    private void Next()
    {
        _timer?.Stop();
        if (VisualRoot is null)
            return;
        if (TopLevel.GetTopLevel(this) is not Window { WindowState: WindowState.Minimized })
            ShowFrame((_frame + 1) % FrameCount);
        Schedule();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_bitmap is null)
            return default;
        var size = _bitmap.Size;
        var scale = Math.Min(
            double.IsInfinity(availableSize.Width) ? 1 : availableSize.Width / size.Width,
            double.IsInfinity(availableSize.Height) ? 1 : availableSize.Height / size.Height);
        return size * scale;
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap is null)
            return;
        var size = _bitmap.Size;
        var scale = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
        var drawn = size * scale;
        var target = new Rect((Bounds.Width - drawn.Width) / 2, (Bounds.Height - drawn.Height) / 2, drawn.Width, drawn.Height);
        context.DrawImage(_bitmap, new Rect(size), target);
    }

    private void Release()
    {
        _timer?.Stop();
        _codec?.Dispose();
        _codec = null;
        _data?.Dispose();
        _data = null;
        _canvas?.Dispose();
        _canvas = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _frames = [];
        _frame = -1;
    }

    public void Dispose() => Release();
}
