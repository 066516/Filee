// Renders PDF pages to raster images with PDFium (via PDFtoImage, MIT / PDFium BSD-3): one image per page, one
// multi-page TIFF or GIF, or (PdfOptions.CombinePages) the pages stacked into one tall image.

using Filee.Core.Conversion;
using Filee.Engines.Infrastructure;
using Filee.Engines.Magick;
using ImageMagick;
using PDFtoImage;
using SkiaSharp;

// PDFtoImage marks its API for the platforms PDFium ships for (Windows, macOS, Linux, mobile).
// Filee only targets desktop platforms that are all covered.
#pragma warning disable CA1416

namespace Filee.Engines.Pdf;

/// <summary>PDF → every raster format ImageMagick writes (PNG, JPG, WEBP, TIFF, …, PSD, TGA, PPM).</summary>
public sealed class PdfiumConverter : IConverter
{
    private static readonly HashSet<string> MultiPageTargets = ["tiff", "gif"];

    /// <summary>A tall image beyond this many pixels is scaled down (a 300-page PDF at 300 dpi would need gigabytes).</summary>
    internal const long MaxStackedPixels = 200_000_000;

    public string Id => "pdfium";
    public string DisplayName => "PDFium";

    /// <summary>PDFium is not thread-safe.</summary>
    public int MaxParallelism => 1;

    public IReadOnlyList<ConversionEdge> Edges { get; } =
        ImageEncoder.Writable.Select(target => new ConversionEdge("pdf", target)).ToList();

    public EngineStatus GetStatus() => EngineStatus.Available(version: EngineVersions.Library("PDFtoImage", typeof(Conversion)));

    public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            var pdf = File.ReadAllBytes(step.InputPath);
            var pageCount = Conversion.GetPageCount(pdf, null);
            var pages = PageRange.Parse(step.Preset.Pdf.PageRange, pageCount);
            var dpi = Math.Clamp(step.Preset.Pdf.RenderDpi, 36, 1200);
            var options = new RenderOptions(Dpi: dpi, WithAnnotations: true, WithFormFill: true, BackgroundColor: SKColors.White);
            var written = new List<string>();
            var collected = new List<IMagickImage<byte>>();
            var (format, extension) = ImageEncoder.OutputFormat(step.To, step.Preset.Image);
            var stack = step.Preset.Pdf.CombinePages && pages.Count > 1 && !MultiPageTargets.Contains(step.To);

            try
            {
                var index = 0;
                foreach (var bitmap in Conversion.ToImages(pdf, pages, null, options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pageNumber = pages[index] + 1;
                    index++;

                    var frame = ToMagick(bitmap, dpi);
                    if (stack)
                    {
                        collected.Add(frame); // stacked after the loop; the image options apply to the tall image
                        progress?.Report((double)index / pages.Count * 0.8);
                        continue;
                    }
                    ImageEncoder.ApplyOptions(frame, step.Preset.Image, step.To);

                    if (MultiPageTargets.Contains(step.To))
                    {
                        collected.Add(frame); // one multi-page file, written after the loop
                    }
                    else
                    {
                        using (frame)
                        {
                            var path = step.Output.Allocate(extension, pages.Count > 1 ? $"_p{pageNumber}" : null);
                            if (path is not null)
                            {
                                frame.Write(path, format);
                                written.Add(path);
                            }
                        }
                    }
                    progress?.Report((double)index / pages.Count);
                }

                if (stack)
                {
                    using var tall = Stack(collected, step.To);
                    ImageEncoder.ApplyOptions(tall, step.Preset.Image, step.To);
                    if (step.Output.Allocate(extension, null) is { } path)
                    {
                        tall.Write(path, format);
                        written.Add(path);
                    }
                }
                else if (collected.Count > 0)
                {
                    written.AddRange(ImageEncoder.Write(collected, step.To, step.Output, step.Preset.Image));
                }
            }
            finally
            {
                foreach (var frame in collected)
                    frame.Dispose();
            }
            return written;
        }, cancellationToken);

    /// <summary>
    /// The pages one under another, centred on a white strip as wide as the widest page. Scaled down when the result
    /// would be taller than the format stores (JPEG 65,500 px, WebP 16,383 px) or larger than
    /// <see cref="MaxStackedPixels"/>.
    /// </summary>
    internal static IMagickImage<byte> Stack(IReadOnlyList<IMagickImage<byte>> pages, string target)
    {
        double width = pages.Max(p => p.Width);
        double height = pages.Sum(p => (double)p.Height);
        var scale = Math.Min(1, Math.Min(MaxHeight(target) / height, Math.Sqrt(MaxStackedPixels / (width * height))));
        var stripWidth = (uint)Math.Max(1, Math.Floor(width * scale));
        using var strip = new MagickImageCollection();
        foreach (var page in pages)
        {
            var copy = page.Clone();
            if (scale < 1)
                copy.Resize((uint)Math.Max(1, Math.Floor(copy.Width * scale)), (uint)Math.Max(1, Math.Floor(copy.Height * scale)));
            copy.BackgroundColor = MagickColors.White;
            copy.Extent(stripWidth, copy.Height, Gravity.North);
            strip.Add(copy);
        }
        var tall = strip.AppendVertically();
        tall.Density = pages[0].Density;
        return tall;
    }

    /// <summary>The tallest image a format stores; PNG, TIFF and the rest take more than a stack ever needs.</summary>
    internal static double MaxHeight(string target) => target switch
    {
        "jpg" or "jpeg" => 65_500,
        "webp" => 16_383,
        "avif" or "heic" => 16_384,
        _ => 1_000_000,
    };

    private static MagickImage ToMagick(SKBitmap bitmap, int dpi)
    {
        using (bitmap)
        using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
        {
            var image = new MagickImage(data.ToArray());
            image.Density = new Density(dpi, dpi, DensityUnit.PixelsPerInch);
            return image;
        }
    }
}
