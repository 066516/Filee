// Lossless PDF page tools used by the interactive editor: PDFium supplies previews, while PDFsharp changes page
// boxes or copies pages without rasterising their text and drawings.

using PDFtoImage;
using PDFtoImage.Exceptions;
using Filee.Core.Platform;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

#pragma warning disable CA1416 // PDFium ships for every desktop platform Filee targets.

namespace Filee.Engines.Pdf;

public sealed record PdfPagePreview(byte[] Png, int Width, int Height);

/// <summary>One page in an edited document. <see cref="Rotation"/> is an additional clockwise turn in degrees.</summary>
public sealed record PdfPageReference(string Path, int PageIndex, int Rotation = 0);

public static class PdfPageTools
{
    public static int PageCount(string path)
    {
        using var document = PdfSharpConverter.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    /// <summary>Copies pages in the supplied order, optionally from several PDFs, and applies their lossless turns.</summary>
    public static void Save(IReadOnlyList<PdfPageReference> pages, string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (pages.Count == 0)
            throw new InvalidOperationException("A PDF must contain at least one page.");
        var sources = new Dictionary<string, PdfDocument>(FileSystemPaths.Comparer);
        try
        {
            using var output = new PdfDocument();
            foreach (var item in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!sources.TryGetValue(item.Path, out var source))
                {
                    source = PdfSharpConverter.Open(item.Path, PdfDocumentOpenMode.Import);
                    sources.Add(item.Path, source);
                }
                if (item.PageIndex < 0 || item.PageIndex >= source.PageCount)
                    throw new InvalidDataException($"Page {item.PageIndex + 1} no longer exists in {Path.GetFileName(item.Path)}.");
                var page = output.AddPage(source.Pages[item.PageIndex]);
                if (item.Rotation != 0)
                    page.Rotate = ((page.Rotate + item.Rotation) % 360 + 360) % 360;
            }
            output.Save(outputPath);
        }
        finally
        {
            foreach (var source in sources.Values)
                source.Dispose();
        }
    }

    /// <summary>Renders one page for the crop UI, bounded so a poster-sized page cannot make an enormous bitmap.</summary>
    public static PdfPagePreview Preview(string path, int pageIndex = 0, int maxDimension = 2560)
    {
        var pdf = File.ReadAllBytes(path);
        try
        {
            var sizes = Conversion.GetPageSizes(pdf, null);
            if (pageIndex < 0 || pageIndex >= sizes.Count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            var size = sizes[pageIndex];
            var longest = Math.Max(size.Width, size.Height);
            var dpi = longest <= 0 ? 96 : Math.Clamp((int)Math.Floor(maxDimension * 72 / longest), 36, 150);
            var options = new RenderOptions(Dpi: dpi, WithAnnotations: true, WithFormFill: true, BackgroundColor: SKColors.White);
            using var bitmap = Conversion.ToImages(pdf, [pageIndex], null, options).Single();
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return new PdfPagePreview(encoded.ToArray(), bitmap.Width, bitmap.Height);
        }
        catch (PdfPasswordProtectedException)
        {
            throw PdfDocumentReader.PasswordProtected();
        }
    }

    /// <summary>
    /// Applies one crop rectangle, in the rendered page's normalised top-left coordinates, to every page. Only the
    /// page crop boxes change; text, vectors, links and pictures remain PDF objects.
    /// </summary>
    public static void CropAll(string inputPath, string outputPath, double x, double y, double width, double height,
        CancellationToken cancellationToken = default)
    {
        (x, y, width, height) = Clamp(x, y, width, height);
        using var source = PdfSharpConverter.Open(inputPath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        foreach (var sourcePage in source.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = output.AddPage(sourcePage);
            var box = page.EffectiveCropBoxReadOnly;
            var (left, bottom, right, top) = MapFromDisplayed(page.Rotate, x, y, width, height);
            page.CropBox = new PdfRectangle(
                new XPoint(box.X1 + left * box.Width, box.Y1 + bottom * box.Height),
                new XPoint(box.X1 + right * box.Width, box.Y1 + top * box.Height));
        }
        output.Save(outputPath);
    }

    private static (double X, double Y, double Width, double Height) Clamp(double x, double y, double width, double height)
    {
        x = Math.Clamp(x, 0, 1);
        y = Math.Clamp(y, 0, 1);
        width = Math.Clamp(width, 0.0001, 1 - x);
        height = Math.Clamp(height, 0.0001, 1 - y);
        return (x, y, width, height);
    }

    /// <summary>Displayed top-left coordinates to the unrotated PDF box (left, bottom, right, top).</summary>
    private static (double Left, double Bottom, double Right, double Top) MapFromDisplayed(
        int rotation, double x, double y, double width, double height)
    {
        var u0 = x;
        var u1 = x + width;
        var v0 = y;
        var v1 = y + height;
        return ((rotation % 360 + 360) % 360) switch
        {
            90 => (v0, u0, v1, u1),
            180 => (1 - u1, v0, 1 - u0, v1),
            270 => (1 - v1, 1 - u1, 1 - v0, 1 - u0),
            _ => (u0, 1 - v1, u1, 1 - v0),
        };
    }
}
