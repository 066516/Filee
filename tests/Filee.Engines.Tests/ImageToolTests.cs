// Rotate and flip presets: the image turns and mirrors the way the preset names say.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using ImageMagick;
using ImageMagick.Drawing;

namespace Filee.Engines.Tests;

public class ImageToolTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    /// <summary>A 4 × 2 white image with one red pixel in the top left corner, run through a built-in preset.</summary>
    [Theory]
    [InlineData("rotate-right", "a_right.png", 2, 4, 1, 0)]
    [InlineData("rotate-left", "a_left.png", 2, 4, 0, 3)]
    [InlineData("rotate-180", "a_180.png", 4, 2, 3, 1)]
    [InlineData("flip-horizontal", "a_mirrored.png", 4, 2, 3, 0)]
    [InlineData("flip-vertical", "a_flipped.png", 4, 2, 0, 1)]
    public async Task The_corner_pixel_moves_where_the_preset_says(string presetId, string name, int width, int height, int x, int y)
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "a.png");
        using (var image = new MagickImage(MagickColors.White, 4, 2))
        {
            new Drawables().FillColor(MagickColors.Red).Point(0, 0).Draw(image);
            image.Write(source, MagickFormat.Png);
        }

        var job = await fx.ConvertAsync([source], BuiltInData.CreatePresets().Single(p => p.Id == presetId));

        Assert.Equal(JobState.Completed, job.State);
        var output = job.Outputs.Single();
        Assert.Equal(name, Path.GetFileName(output));
        using var result = new MagickImage(output);
        Assert.Equal(((uint)width, (uint)height), (result.Width, result.Height));
        var red = result.GetPixels().GetPixel(x, y).ToColor()!;
        Assert.True(red.R > 200 && red.G < 60, $"{presetId}: ({x},{y}) is {red}");
    }

    private static Preset Tool(string id) => BuiltInData.CreatePresets().Single(p => p.Id == id);

    /// <summary>A 40 × 20 white image with a red 10 × 10 square in the top left corner.</summary>
    private static MagickImage Marked()
    {
        var image = new MagickImage(MagickColors.White, 40, 20);
        new Drawables().FillColor(MagickColors.Red).Rectangle(0, 0, 9, 9).Draw(image);
        return image;
    }

    private static bool IsRed(IMagickImage<byte> image, int x, int y) =>
        image.GetPixels().GetPixel(x, y).ToColor() is { } c && c.R > 200 && c.G < 80 && c.B < 80;

    [Fact]
    public async Task A_phone_photo_turns_the_way_it_looks_and_says_so_in_its_orientation()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "phone.jpg");
        using (var image = Marked())
        {
            // Stored landscape, shown turned right (EXIF orientation 6): on screen 20 × 40, red at the top right.
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)OrientationType.RightTop);
            image.SetProfile(exif);
            image.Orientation = OrientationType.RightTop; // ImageMagick writes this into the profile
            image.Write(source, MagickFormat.Jpeg);
        }
        using (var written = new MagickImage(source))
            Assert.Equal(OrientationType.RightTop, written.Orientation); // the test photo is what it says

        var job = await fx.ConvertAsync([source], Tool("rotate-left"));

        Assert.Equal(JobState.Completed, job.State);
        using var result = new MagickImage(job.Outputs.Single());
        // Turned left from how it looked: back to 40 × 20 with red at the top left, and no tag turning it again.
        Assert.Equal((40u, 20u), (result.Width, result.Height));
        Assert.True(IsRed(result, 3, 3));
        Assert.True(result.Orientation is OrientationType.TopLeft or OrientationType.Undefined, $"{result.Orientation}");
    }

    [Fact]
    public async Task Every_frame_of_an_animation_turns()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "anim.gif");
        using (var frames = new MagickImageCollection())
        {
            for (var i = 0; i < 2; i++)
            {
                var frame = Marked();
                frame.AnimationDelay = 20;
                frames.Add(frame);
            }
            frames.Write(source, MagickFormat.Gif);
        }

        var job = await fx.ConvertAsync([source], Tool("rotate-right"));

        Assert.Equal(JobState.Completed, job.State);
        using var result = new MagickImageCollection(job.Outputs.Single());
        Assert.Equal(2, result.Count);
        Assert.All(result, frame =>
        {
            Assert.Equal((20u, 40u), (frame.Width, frame.Height));
            Assert.True(IsRed(frame, 16, 3)); // top left went to the top right
        });
    }

    [Fact]
    public async Task A_PDF_is_not_copied_as_if_rotated_it_fails_and_says_why()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "doc.pdf");
        using (var pdf = new PdfSharp.Pdf.PdfDocument())
        {
            pdf.AddPage();
            pdf.Save(source);
        }

        var job = await fx.ConvertAsync([source], Tool("rotate-left"));

        Assert.Equal(JobState.Failed, job.State);
        Assert.Equal("error.cannot_turn", job.Files.Single().ErrorKey);
        Assert.False(File.Exists(Path.Combine(dir, "doc_left.pdf")));
    }

    [Fact]
    public async Task An_image_into_a_PDF_is_turned_on_the_way()
    {
        var dir = fx.NewFolder();
        var source = Path.Combine(dir, "wide.png");
        using (var image = Marked())
            image.Write(source, MagickFormat.Png);
        var preset = Tool("rotate-left");
        preset.TargetFormat = "pdf";

        var job = await fx.ConvertAsync([source], preset);

        Assert.Equal(JobState.Completed, job.State);
        using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(job.Outputs.Single(), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.True(pdf.Pages[0].Height.Point > pdf.Pages[0].Width.Point); // landscape image, portrait page
    }
}
