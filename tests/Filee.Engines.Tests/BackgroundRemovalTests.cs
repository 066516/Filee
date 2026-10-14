// "Remove background": signatures, stamps and logos on paper or a plain backdrop become transparent PNGs.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Magick;
using ImageMagick;
using ImageMagick.Drawing;

namespace Filee.Engines.Tests;

public class BackgroundRemovalTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static IMagickColor<byte> Pixel(IMagickImage<byte> image, int x, int y) =>
        image.GetPixels().GetPixel(x, y).ToColor()!;

    /// <summary>A "signature": a thick stroke and a ring (an "o") in <paramref name="ink"/> on <paramref name="paper"/>.</summary>
    private static MagickImage Signature(string paper, string ink)
    {
        var image = new MagickImage(new MagickColor(paper), 200, 120);
        new Drawables()
            .StrokeColor(new MagickColor(ink)).StrokeWidth(6).FillColor(MagickColors.Transparent)
            .Line(20, 60, 100, 60)
            .Circle(150, 60, 150, 85)
            .Draw(image);
        return image;
    }

    private static MagickImage Removed(MagickImage image, int tolerance = 25, bool keepEnclosed = false)
    {
        BackgroundRemover.Apply(image, tolerance, keepEnclosed);
        return image;
    }

    [Fact]
    public void Paper_becomes_transparent_and_black_ink_stays_opaque()
    {
        using var image = Removed(Signature("white", "black"));

        Assert.True(image.HasAlpha);
        Assert.Equal(0, Pixel(image, 5, 5).A); // paper
        Assert.Equal(0, Pixel(image, 150, 60).A); // paper inside the ring
        var ink = Pixel(image, 60, 60);
        Assert.Equal(255, ink.A);
        Assert.True(ink.R < 40 && ink.G < 40 && ink.B < 40, ink.ToString());
    }

    [Theory]
    [InlineData("#1E3CB4")] // ballpoint blue
    [InlineData("#C8282D")] // stamp red
    public void Coloured_ink_keeps_its_colour(string inkColor)
    {
        using var image = Removed(Signature("white", inkColor));

        var expected = new MagickColor(inkColor);
        var ink = Pixel(image, 60, 60);
        Assert.Equal(255, ink.A);
        Assert.InRange(ink.R, expected.R - 8, expected.R + 8);
        Assert.InRange(ink.G, expected.G - 8, expected.G + 8);
        Assert.InRange(ink.B, expected.B - 8, expected.B + 8);
        Assert.Equal(0, Pixel(image, 5, 5).A);
    }

    [Fact]
    public void Yellowish_paper_is_removed_too()
    {
        using var image = Removed(Signature("#F2EAD3", "black"));

        Assert.Equal(0, Pixel(image, 5, 5).A);
        Assert.Equal(0, Pixel(image, 100, 100).A);
        Assert.Equal(255, Pixel(image, 60, 60).A);
    }

    [Fact]
    public void Uneven_lighting_across_the_paper_is_removed()
    {
        // A phone photo: the paper darkens from white on the left to a shadowed grey on the right.
        using var image = new MagickImage("gradient:white-#C8C8C8", 120, 300);
        image.Rotate(-90); // left to right
        new Drawables().StrokeColor(MagickColors.Black).StrokeWidth(6).Line(30, 60, 270, 60).Draw(image);

        Removed(image);

        foreach (var x in new[] { 5, 100, 200, 295 })
            Assert.True(Pixel(image, x, 100).A < 10, $"paper at x={x}: alpha {Pixel(image, x, 100).A}");
        foreach (var x in new[] { 40, 150, 260 })
            Assert.Equal(255, Pixel(image, x, 60).A);
    }

    [Fact]
    public void Anti_aliased_edges_are_un_mixed_instead_of_left_as_a_light_fringe()
    {
        // Paper | 50 % grey (a half-covered edge pixel of black ink) | black ink.
        using var image = new MagickImage(MagickColors.White, 60, 20);
        new Drawables().FillColor(new MagickColor("#808080")).Rectangle(20, 0, 29, 19)
            .FillColor(MagickColors.Black).Rectangle(30, 0, 39, 19).Draw(image);

        Removed(image);

        var edge = Pixel(image, 25, 10);
        Assert.InRange((int)edge.A, 64, 200); // partly transparent ...
        Assert.True(edge.R < 64, edge.ToString()); // ... and black, not grey
    }

    [Fact]
    public void Keep_enclosed_areas_leaves_the_inside_of_a_shape()
    {
        using var image = Removed(Signature("white", "black"), keepEnclosed: true);

        Assert.Equal(0, Pixel(image, 5, 5).A);
        var inside = Pixel(image, 150, 60);
        Assert.Equal(255, inside.A);
        Assert.True(inside.R > 240, inside.ToString());
    }

    [Fact]
    public void A_dark_backdrop_is_removed_and_light_content_kept()
    {
        using var image = Removed(Signature("#101010", "white"));

        Assert.Equal(0, Pixel(image, 5, 5).A);
        var ink = Pixel(image, 60, 60);
        Assert.Equal(255, ink.A);
        Assert.True(ink.R > 230, ink.ToString());
    }

    [Fact]
    public void Grayscale_and_cmyk_sources_work()
    {
        using var gray = Signature("white", "black");
        gray.ColorType = ColorType.Grayscale;
        using var cmyk = Signature("white", "#1E3CB4");
        cmyk.ColorSpace = ColorSpace.CMYK;

        foreach (var image in new[] { gray, cmyk })
        {
            Removed(image);
            Assert.Equal(0, Pixel(image, 5, 5).A);
            Assert.Equal(255, Pixel(image, 60, 60).A);
        }
    }

    [Fact]
    public void Higher_tolerance_removes_faint_smudges()
    {
        // A faint grey smudge (a fold or eraser mark) next to real ink.
        using var low = new MagickImage(MagickColors.White, 120, 60);
        new Drawables().FillColor(new MagickColor("#C8C8C8")).Rectangle(10, 10, 40, 50)
            .FillColor(MagickColors.Black).Rectangle(70, 10, 100, 50).Draw(low);
        using var high = (MagickImage)low.Clone();

        Removed(low, tolerance: 0);
        Removed(high, tolerance: 100);

        Assert.True(Pixel(low, 25, 30).A > 0);
        Assert.Equal(0, Pixel(high, 25, 30).A);
        Assert.Equal(255, Pixel(high, 85, 30).A);
    }

    [Fact]
    public async Task Jpg_signature_becomes_a_transparent_png_with_the_built_in_preset()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "signature.jpg");
        using (var image = Signature("white", "#1E3CB4"))
        {
            image.Quality = 85;
            image.Write(source, MagickFormat.Jpg);
        }
        var preset = BuiltInData.CreatePresets().Single(p => p.Id == "remove-background");

        var job = await fx.ConvertAsync([source], preset);

        Assert.Equal(JobState.Completed, job.State);
        var output = job.Outputs.Single();
        Assert.Equal("signature_transparent.png", Path.GetFileName(output));
        using var result = new MagickImage(output);
        Assert.Equal(MagickFormat.Png, result.Format);
        Assert.True(result.HasAlpha);
        Assert.Equal(0, Pixel(result, 5, 5).A); // JPEG noise on the paper is gone as well
        Assert.Equal(0, Pixel(result, 100, 110).A);
        Assert.Equal(255, Pixel(result, 60, 60).A);
    }

    [Fact]
    public async Task Jpg_target_gets_the_background_colour_instead_of_transparency()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "logo.png");
        using (var image = Signature("white", "black"))
            image.Write(source, MagickFormat.Png);

        var job = await fx.ConvertAsync([source], new Preset
        {
            TargetFormat = "jpg",
            Image = { RemoveBackground = true, Background = "#FF0000", Quality = 100 },
        });

        Assert.Equal(JobState.Completed, job.State);
        using var result = new MagickImage(job.Outputs.Single());
        var paper = Pixel(result, 5, 5);
        Assert.True(paper.R > 240 && paper.G < 20 && paper.B < 20, paper.ToString());
    }

    [Fact]
    public async Task Off_by_default_leaves_the_image_alone()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "plain.png");
        using (var image = Signature("white", "black"))
            image.Write(source, MagickFormat.Png);

        var job = await fx.ConvertAsync([source], new Preset { TargetFormat = "png" });

        Assert.Equal(JobState.Completed, job.State);
        using var result = new MagickImage(job.Outputs.Single());
        Assert.Equal(255, Pixel(result, 5, 5).A);
        Assert.Equal(255, Pixel(result, 5, 5).R);
    }
}
