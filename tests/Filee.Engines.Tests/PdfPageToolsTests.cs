using Filee.Core.Presets;
using Filee.Engines.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Filee.Engines.Tests;

public class PdfPageToolsTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static string MakePdf(string folder, string name, params (double Width, double Height, int Rotation)[] pages)
    {
        var path = Path.Combine(folder, name);
        using var document = new PdfDocument();
        foreach (var (width, height, rotation) in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(height);
            page.Rotate = rotation;
        }
        document.Save(path);
        return path;
    }

    [Fact]
    public void Save_reorders_combines_and_rotates_pages_without_rasterising()
    {
        var folder = fx.NewFolder();
        var first = MakePdf(folder, "first.pdf", (200, 500, 0), (300, 500, 0));
        var second = MakePdf(folder, "second.pdf", (400, 500, 90));
        var output = Path.Combine(folder, "edited.pdf");

        PdfPageTools.Save([
            new(second, 0, -90),
            new(first, 1, 90),
            new(first, 0),
        ], output, TestContext.Current.CancellationToken);

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(3, result.PageCount);
        Assert.Equal([400d, 300d, 200d], result.Pages.Cast<PdfPage>().Select(p => p.Width.Point));
        Assert.Equal([0, 90, 0], result.Pages.Cast<PdfPage>().Select(p => p.Rotate));
    }

    [Fact]
    public void Crop_applies_displayed_proportions_to_every_page_and_respects_rotation()
    {
        var folder = fx.NewFolder();
        var source = MakePdf(folder, "source.pdf", (600, 800, 0), (600, 800, 90));
        var output = Path.Combine(folder, "cropped.pdf");

        PdfPageTools.CropAll(source, output, x: .1, y: .2, width: .5, height: .6,
            TestContext.Current.CancellationToken);

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(2, result.PageCount);
        AssertBox(result.Pages[0].CropBox, 60, 160, 360, 640);
        AssertBox(result.Pages[1].CropBox, 120, 80, 480, 480);
    }

    [Theory]
    [InlineData(PdfCompression.None)]
    [InlineData(PdfCompression.Ebook)]
    public async Task Pdf_rotation_preset_turns_all_pages_losslessly(PdfCompression compression)
    {
        var folder = fx.NewFolder();
        var source = MakePdf(folder, "source.pdf", (200, 500, 0), (300, 500, 90));
        var preset = new Preset
        {
            TargetFormat = "pdf",
            Output = { FileNamePattern = "{name}_right" },
            Pdf = { Rotation = PdfRotation.Right90, Compression = compression },
        };

        var job = await fx.ConvertAsync([source], preset);

        Assert.Equal(Filee.Core.Conversion.JobState.Completed, job.State);
        using var result = PdfReader.Open(Assert.Single(job.Outputs), PdfDocumentOpenMode.Import);
        Assert.Equal([90, 180], result.Pages.Cast<PdfPage>().Select(p => p.Rotate));
    }

    private static void AssertBox(PdfRectangle actual, double x1, double y1, double x2, double y2)
    {
        Assert.Equal(x1, actual.X1, 6);
        Assert.Equal(y1, actual.Y1, 6);
        Assert.Equal(x2, actual.X2, 6);
        Assert.Equal(y2, actual.Y2, 6);
    }
}
