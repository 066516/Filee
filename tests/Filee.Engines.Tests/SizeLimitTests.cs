// "Size limit" (#553): a picture or a video over the preset's limit is made smaller until it fits, or refused. The
// picture tests use ImageMagick in-process; the video ones run only when Filee's FFmpeg is installed
// (`pwsh build/fetch-engines.ps1 -Only ffmpeg`), on inputs FFmpeg generates itself.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Infrastructure;
using Filee.Engines.Magick;
using Filee.Engines.Media;
using ImageMagick;

namespace Filee.Engines.Tests;

[Collection(nameof(FfmpegCollection))]
public class SizeLimitTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static readonly (string Ffmpeg, string Ffprobe)? Tools = FfmpegConverter.Locate();

    /// <summary>Noise doesn't compress: a big file for its pixel count.</summary>
    private static MagickImage Noisy(uint width, uint height)
    {
        var image = new MagickImage(MagickColors.Gray, width, height);
        image.AddNoise(NoiseType.Random, 2);
        return image;
    }

    [Fact]
    public void A_picture_over_the_limit_is_saved_at_a_lower_quality_then_smaller_until_it_fits()
    {
        var path = Path.Combine(fx.NewFolder(), "noise.jpg");
        using var image = Noisy(1600, 1200);
        image.Quality = 95;
        image.Write(path, MagickFormat.Jpeg);
        var before = new FileInfo(path).Length;
        const long limit = 200 * 1024;
        Assert.True(before > limit * 3, $"the test picture is only {before} bytes");

        ImageEncoder.FitToSize(image, path, MagickFormat.Jpeg, limit);

        Assert.InRange(new FileInfo(path).Length, 1, limit);
        using var fitted = new MagickImage(path);
        // Quality alone doesn't get noise this small: it was made smaller too, keeping its shape.
        Assert.True(fitted.Width < 1600);
        Assert.Equal(1600.0 / 1200, (double)fitted.Width / fitted.Height, 1);
        // The picture handed in is left as it was (the next frame or engine may use it).
        Assert.Equal(1600u, image.Width);
    }

    [Fact]
    public void A_picture_under_the_limit_is_left_as_it_was_written()
    {
        var path = Path.Combine(fx.NewFolder(), "small.png");
        using var image = new MagickImage(MagickColors.White, 64, 64);
        image.Write(path, MagickFormat.Png);
        var written = File.ReadAllBytes(path);

        ImageEncoder.FitToSize(image, path, MagickFormat.Png, 1024 * 1024);

        Assert.Equal(written, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_limit_no_picture_can_meet_is_refused_rather_than_broken()
    {
        var path = Path.Combine(fx.NewFolder(), "noise.png");
        using var image = Noisy(800, 600);
        image.Write(path, MagickFormat.Png);

        var refused = Assert.Throws<ConversionRefusedException>(() => ImageEncoder.FitToSize(image, path, MagickFormat.Png, 100));
        Assert.Equal("error.over_size_limit", refused.ErrorKey);
    }

    [Fact]
    public void The_bitrate_that_fits_leaves_room_for_the_sound_and_the_container()
    {
        // 16 MB over 60 s with 96 kbit/s of sound: 16 × 1024² × 8 × 0.95 / 1000 / 60 = 2125 kbit/s, less 96.
        Assert.Equal(2029, MediaEncoding.FitBitrateKbps(16L * 1024 * 1024, 60, 96));
        // An hour in 10 MB would be a smear: refused instead.
        Assert.Equal(0, MediaEncoding.FitBitrateKbps(10L * 1024 * 1024, 3600, 96));
    }

    [Fact]
    public void A_constant_quality_becomes_a_bitrate_with_room_for_peaks()
    {
        FfmpegPass[] passes = [new(["-c:v", "libx264", "-crf", "23", "-c:a", "aac", "-q:a", "2"], 1),
                               new(["-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "32"], 1)];

        var fitted = MediaEncoding.AtBitrate(passes, 1000);

        Assert.Equal(["-c:v", "libx264", "-b:v", "1000k", "-maxrate", "1500k", "-bufsize", "2000k", "-c:a", "aac", "-q:a", "2"], fitted[0].Arguments);
        Assert.Equal(["-c:v", "libvpx-vp9", "-b:v", "1000k", "-maxrate", "1500k", "-bufsize", "2000k"], fitted[1].Arguments);
    }

    /// <summary>Eight seconds of a noisy 720p pattern with sound: several MB even as H.264.</summary>
    private async Task<string> MakeVideoAsync()
    {
        var path = Path.Combine(fx.NewFolder(), "noisy.mp4");
        var result = await ProcessRunner.RunAsync(Tools!.Value.Ffmpeg,
        [
            "-hide_banner", "-nostdin", "-v", "error", "-y",
            "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30:duration=8,noise=alls=40:allf=t",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=8", "-ac", "2",
            "-c:v", "libx264", "-crf", "16", "-pix_fmt", "yuv420p", "-c:a", "aac", path,
        ], TimeSpan.FromMinutes(2), CancellationToken.None);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return path;
    }

    [Fact]
    public async Task A_video_over_the_limit_is_encoded_again_until_it_fits()
    {
        Assert.SkipUnless(Tools is not null, "FFmpeg is not installed (pwsh build/fetch-engines.ps1 -Only ffmpeg)");
        var source = await MakeVideoAsync();
        Assert.True(new FileInfo(source).Length > 3 * 1024 * 1024, $"the test video is only {new FileInfo(source).Length} bytes");

        // MP4 → MP4 at Balanced would copy the video as it is: the limit has to encode it anyway.
        var job = await fx.ConvertAsync([source], new Preset { TargetFormat = "mp4", Media = { MaxSizeMb = 1 }, Output = { FileNamePattern = "{name}_fit" } });

        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        Assert.InRange(new FileInfo(Assert.Single(job.Outputs)).Length, 1, 1024 * 1024);
    }

    [Fact]
    public async Task A_video_too_long_for_its_limit_is_refused()
    {
        Assert.SkipUnless(Tools is not null, "FFmpeg is not installed (pwsh build/fetch-engines.ps1 -Only ffmpeg)");
        var source = await MakeVideoAsync();

        var job = await fx.ConvertAsync([source], new Preset { TargetFormat = "mp4", Media = { MaxSizeMb = 0.05 }, Output = { FileNamePattern = "{name}_fit" } });

        Assert.Equal("error.over_size_limit", Assert.Single(job.Files).ErrorKey);
        Assert.Empty(job.Outputs);
    }
}
