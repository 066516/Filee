// "Size limit" (#553): only the result has to fit, so only the step that writes it sees the limit; and the
// ready-made presets for where a file goes are offered by Add only, never put in the library on their own.

using Filee.Core.Conversion;
using Filee.Core.Presets;

namespace Filee.Core.Tests;

public class SizeLimitTests
{
    [Fact]
    public async Task Only_the_step_that_writes_the_result_gets_the_size_limit()
    {
        using var dir = new TempDir();
        var photo = dir.File("photo.jpg");
        var first = new FakeConverter("magick", new ConversionEdge("jpg", "png"));
        var second = new FakeConverter("other", new ConversionEdge("png", "webp"));
        await using var queue = new JobQueue(new ConverterCatalog([first, second]));
        var preset = new Preset { TargetFormat = "webp", Image = { MaxSizeMb = 2 }, Media = { MaxSizeMb = 5 } };

        await queue.RunAsync(new ConversionJob([photo], preset, "WEBP"));

        var middle = Assert.Single(first.Presets);
        Assert.Equal((0.0, 0.0), (middle.Image.MaxSizeMb, middle.Media.MaxSizeMb));
        var last = Assert.Single(second.Presets);
        Assert.Equal((2.0, 5.0), (last.Image.MaxSizeMb, last.Media.MaxSizeMb));
        Assert.Equal(2, preset.Image.MaxSizeMb); // the user's preset itself is never changed
    }

    [Fact]
    public void The_presets_for_where_a_file_goes_are_offered_but_not_installed()
    {
        var places = BuiltInData.PlacePresets();
        var library = BuiltInData.CreatePresets().Select(p => p.Id).ToHashSet();

        Assert.Equal(["for-email", "for-discord", "for-whatsapp", "for-instagram"], places.Select(p => p.Id));
        Assert.DoesNotContain(places, p => library.Contains(p.Id));
        Assert.All(places.Where(p => p.TargetFormat == "mp4"), p => Assert.True(p.Media.MaxSizeMb > 0));
        Assert.Equal(720, places.Single(p => p.Id == "for-whatsapp").Media.MaxHeight);
        Assert.Equal((ResizeMode.Width, 1080), places.Single(p => p.Id == "for-instagram").Image is var i ? (i.Resize, i.Width) : default);
        // A new list each time: adding one never changes the next.
        Assert.NotSame(places[0], BuiltInData.PlacePresets()[0]);
    }
}
