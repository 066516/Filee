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
}
