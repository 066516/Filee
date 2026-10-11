// Settings → Engines → "When LibreOffice is installed": presentations → PDF go through LibreOffice by default (slide
// backgrounds and shapes as in PowerPoint), HWP → PDF only when chosen (rhwp usually lays HWP out more like 한글).

using Filee.Core.Conversion;
using Filee.Core.Settings;
using Filee.Engines.Office;

namespace Filee.Engines.Tests;

public class LibreOfficeRouteTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    /// <summary>The built-in engines with a LibreOffice that is installed (with H2Orestart), whatever this PC has.</summary>
    private RoutePlanner Planner(bool slides, bool hwpToPdf)
    {
        var libreOffice = new InstalledLibreOffice(LibreOfficeConverter.BuildEdges(hwp: true, slides, hwpToPdf));
        var converters = fx.Converters.Where(c => c.Id != "libreoffice").Append(libreOffice).ToList();
        var priority = new AppSettings().EnginePriority;
        return new RoutePlanner(converters, c => Math.Min(priority.IndexOf(c.Id) is >= 0 and var i ? i : priority.Count, 9));
    }

    [Theory]
    [InlineData("pptx")]
    [InlineData("ppt")]
    [InlineData("odp")]
    public void Presentations_go_to_pdf_through_libreoffice_when_it_is_chosen(string from)
    {
        var route = Planner(slides: true, hwpToPdf: false).Plan(from, "pdf");

        Assert.Equal("libreoffice", Assert.Single(route!.Steps).Converter.Id);
    }

    [Fact]
    public void Without_the_choice_pptx_goes_the_built_in_way()
    {
        var route = Planner(slides: false, hwpToPdf: false).Plan("pptx", "pdf");

        Assert.Equal(["hwpx-writer", "rhwp"], route!.Steps.Select(s => s.Converter.Id));
    }

    [Theory]
    [InlineData("hwp")]
    [InlineData("hwpx")]
    public void Hwp_goes_to_pdf_through_rhwp_unless_libreoffice_is_chosen(string from)
    {
        Assert.Equal("rhwp", Assert.Single(Planner(slides: true, hwpToPdf: false).Plan(from, "pdf")!.Steps).Converter.Id);
        Assert.Equal("libreoffice", Assert.Single(Planner(slides: true, hwpToPdf: true).Plan(from, "pdf")!.Steps).Converter.Id);
    }

    private sealed class InstalledLibreOffice(IReadOnlyList<ConversionEdge> edges) : IConverter
    {
        public string Id => "libreoffice";
        public string DisplayName => "LibreOffice";
        public IReadOnlyList<ConversionEdge> Edges => edges;
        public int MaxParallelism => 1;
        public EngineStatus GetStatus() => EngineStatus.Available("test");

        public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
