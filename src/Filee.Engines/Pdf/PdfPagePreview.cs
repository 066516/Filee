// Small PDF page previews shared by interactive document tools. Rendering stays in Engines because PDFtoImage and
// its native PDFium runtime already live here; the Avalonia app receives ordinary PNG bytes.

using PDFtoImage;
using SkiaSharp;

// PDFtoImage supports every desktop platform Filee targets.
#pragma warning disable CA1416

namespace Filee.Engines.Pdf;

public sealed record RenderedPdfPage(int PageNumber, int Width, int Height, byte[] Png);

public static class PdfPagePreviewRenderer
{
    public static IReadOnlyList<RenderedPdfPage> Render(string path, int maxLongEdge = 1400,
        CancellationToken cancellationToken = default)
    {
        var bytes = File.ReadAllBytes(path);
        var count = Conversion.GetPageCount(bytes);
        var result = new List<RenderedPdfPage>(count);
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = Conversion.ToImage(bytes, index, options: new RenderOptions(
                Dpi: 112, WithAnnotations: true, WithFormFill: true, BackgroundColor: SKColors.White));
            using var shown = Downscale(page, maxLongEdge);
            using var png = shown.Encode(SKEncodedImageFormat.Png, 92);
            result.Add(new RenderedPdfPage(index + 1, shown.Width, shown.Height, png.ToArray()));
        }
        return result;
    }

    private static SKBitmap Downscale(SKBitmap source, int maxLongEdge)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxLongEdge)
            return source.Copy();
        var factor = (double)maxLongEdge / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * factor));
        var height = Math.Max(1, (int)Math.Round(source.Height * factor));
        return source.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("The PDF preview could not be resized.");
    }
}
