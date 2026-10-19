// Generated video/photo fixtures: real bidirectional conversions and independent ffprobe checks of Apple timing.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Engines.Infrastructure;
using Filee.Engines.Media;
using ImageMagick;

namespace Filee.Engines.Tests;

[Collection(nameof(FfmpegCollection))]
public class LivePhotoTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static (string Ffmpeg, string Ffprobe) Tools()
    {
        var tools = FfmpegConverter.Locate();
        Assert.SkipUnless(tools is not null, "FFmpeg is not installed.");
        return tools.Value;
    }

    private async Task<string> VideoAsync(bool audio = true)
    {
        var tools = Tools();
        var input = Path.Combine(fx.NewFolder(), "clip ü 中文.mp4");
        var result = await FfmpegRunner.RunAsync(tools.Ffmpeg, [
            .. MediaEncoding.Common, "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=30",
            .. (audio ? new[] { "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000" } : []),
            "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", input,
        ], null, TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return input;
    }

    private static string Output(ConversionJob job)
    {
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey}: {f.ErrorDetail}")));
        return Assert.Single(job.Outputs);
    }

    [Theory]
    [InlineData("livp", true)]
    [InlineData("livp", false)]
    [InlineData("motionphoto", true)]
    [InlineData("motionphoto", false)]
    public async Task Video_to_photo_and_back_preserves_motion_audio_and_source(string format, bool audio)
    {
        var tools = Tools();
        var input = await VideoAsync(audio);
        var original = await File.ReadAllBytesAsync(input, TestContext.Current.CancellationToken);
        var photoJob = await fx.ConvertAsync([input], new Preset
        {
            TargetFormat = format,
            Media = { LivePhotoStart = 0.2, LivePhotoDuration = 1.2, LivePhotoCover = 0.4 },
        });
        var photo = Output(photoJob);
        Assert.Equal(format, FormatRegistry.Detect(photo)!.Id);
        Assert.NotNull(photoJob.Files.Single().NoticeKey);
        var movie = Output(await fx.ConvertAsync([photo], new Preset { TargetFormat = "mp4" }));
        var info = await MediaInfo.ProbeAsync(tools.Ffprobe, movie, TestContext.Current.CancellationToken);
        Assert.NotNull(info.Video);
        Assert.Equal(audio, info.Audio is not null);
        Assert.InRange(info.Duration!.Value.TotalSeconds, 1.1, 1.35);
        Assert.Equal(original, await File.ReadAllBytesAsync(input, TestContext.Current.CancellationToken));

        if (format == "motionphoto")
        {
            using var still = new MagickImage(photo);
            Assert.Equal(160u, still.Width);
            var xmp = Encoding.UTF8.GetString(still.GetXmpProfile()!.ToByteArray());
            Assert.Contains("MotionPhotoPresentationTimestampUs=\"400000\"", xmp);
        }
        else
        {
            var folder = fx.NewFolder();
            ZipFile.ExtractToDirectory(photo, folder);
            var jpg = Directory.GetFiles(folder, "*.jpg").Single();
            var mov = Directory.GetFiles(folder, "*.mov").Single();
            var id = AppleLivePhoto.ImageIdentifier(jpg);
            Assert.NotNull(id);
            Assert.Equal(id, AppleLivePhoto.MovieIdentifier(mov));
            // The extractor must also accept camera-style adjacent files, without requiring a LIVP archive.
            Output(await fx.ConvertAsync([jpg], new Preset { TargetFormat = "mov", Media = { ExtractMotionPhoto = true } }));
            var probe = await ProcessRunner.RunAsync(tools.Ffprobe,
                ["-v", "error", "-select_streams", "d", "-show_streams", "-show_packets", "-of", "json", mov],
                TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.True(probe.ExitCode == 0, probe.StandardError);
            using var json = JsonDocument.Parse(probe.StandardOutput);
            var stream = Assert.Single(json.RootElement.GetProperty("streams").EnumerateArray());
            Assert.Equal("mebx", stream.GetProperty("codec_tag_string").GetString());
            var packet = Assert.Single(json.RootElement.GetProperty("packets").EnumerateArray());
            Assert.InRange(double.Parse(packet.GetProperty("pts_time").GetString()!, CultureInfo.InvariantCulture), 0.399, 0.401);
            // Same names are insufficient: an unrelated movie must not be paired with the still.
            File.Copy(input, mov, overwrite: true);
            var badPair = await fx.ConvertAsync([jpg], new Preset { TargetFormat = "mp4", Media = { ExtractMotionPhoto = true } });
            Assert.NotEqual(JobState.Completed, badPair.State);
        }
    }

    [Fact]
    public async Task A_renamed_android_photo_still_extracts_and_plain_jpg_does_not_silently_become_motion()
    {
        var input = await VideoAsync();
        var photo = Output(await fx.ConvertAsync([input], new Preset { TargetFormat = "motionphoto" }));
        var renamed = Path.Combine(fx.NewFolder(), "camera.JPG");
        File.Copy(photo, renamed);
        Output(await fx.ConvertAsync([renamed], new Preset { TargetFormat = "mp4", Media = { ExtractMotionPhoto = true } }));
        var plain = EngineFixture.MakeImage(fx.NewFolder(), "plain.jpg", MagickFormat.Jpeg);
        var failed = await fx.ConvertAsync([plain], new Preset { TargetFormat = "mp4", Media = { ExtractMotionPhoto = true } });
        Assert.Equal("error.no_motion_photo", failed.Files.Single().ErrorKey);
        var png = EngineFixture.MakeImage(fx.NewFolder(), "plain.png", MagickFormat.Png);
        var unsupported = await fx.ConvertAsync([png], new Preset { TargetFormat = "mp4", Media = { ExtractMotionPhoto = true } });
        Assert.Equal("error.no_route", unsupported.Files.Single().ErrorKey);
        Output(await fx.ConvertAsync([plain], new Preset { TargetFormat = "mp4" }));
    }

    [Theory]
    [InlineData("livp")]
    [InlineData("motionphoto")]
    public async Task Silent_output_and_end_of_source_cover_are_valid(string format)
    {
        var input = await VideoAsync();
        var photo = Output(await fx.ConvertAsync([input], new Preset
        {
            TargetFormat = format,
            Media = { LivePhotoStart = 1.7, LivePhotoDuration = 3, LivePhotoCover = 9, RemoveAudio = true },
        }));
        var movie = Output(await fx.ConvertAsync([photo], new Preset { TargetFormat = "mp4" }));
        var info = await MediaInfo.ProbeAsync(Tools().Ffprobe, movie, TestContext.Current.CancellationToken);
        Assert.Null(info.Audio);
        Assert.InRange(info.Duration!.Value.TotalSeconds, 0.25, 0.4);
    }

    [Fact]
    public void Photo_edges_are_direct_and_existing_routes_stay_still_images()
    {
        var planner = fx.Catalog.CreatePlanner(["ffmpeg"]);
        Assert.Single(planner.Plan("mp4", "livp")!.Steps);
        Assert.Single(planner.Plan("mov", "motionphoto")!.Steps);
        Assert.Null(planner.Plan("jpg", "livp"));
        Assert.Null(planner.Plan("livp", "gif"));
        Assert.Null(planner.Plan("wav", "motionphoto"));
        Assert.Null(planner.Plan("png", "mp4", motionPhoto: true));
        Assert.Equal(2, planner.Plan("jpg", "mp4")!.Steps.Count);
        Assert.Single(planner.Plan("jpg", "mp4", motionPhoto: true)!.Steps);
        Assert.Equal("camera", FormatRegistry.NameWithoutExtension("camera.motion.jpg"));
    }

    [Theory]
    [InlineData("livp")]
    [InlineData("motionphoto")]
    public async Task Existing_outputs_obey_overwrite_and_skip(string format)
    {
        var input = await VideoAsync();
        var preset = new Preset { TargetFormat = format, Output = { Conflict = ConflictPolicy.Overwrite } };
        var first = Output(await fx.ConvertAsync([input], preset));
        var second = Output(await fx.ConvertAsync([input], preset));
        Assert.Equal(first, second);
        if (format == "livp")
        {
            using var zip = ZipFile.OpenRead(second);
            Assert.Equal(2, zip.Entries.Count);
        }
        preset.Output.Conflict = ConflictPolicy.Skip;
        var skipped = await fx.ConvertAsync([input], preset);
        Assert.Empty(skipped.Outputs);
        Assert.Equal(FileState.Skipped, skipped.Files.Single().State);
    }

    [Fact]
    public async Task Archive_names_never_escape_the_private_work_directory()
    {
        var input = await VideoAsync();
        var original = Output(await fx.ConvertAsync([input], new Preset { TargetFormat = "livp" }));
        var malicious = Path.Combine(fx.NewFolder(), "traversal.livp");
        using (var source = ZipFile.OpenRead(original))
        using (var target = ZipFile.Open(malicious, ZipArchiveMode.Create))
        {
            foreach (var item in source.Entries)
            {
                await using var reader = item.Open();
                await using var writer = target.CreateEntry("../../outside/" + item.Name).Open();
                await reader.CopyToAsync(writer, TestContext.Current.CancellationToken);
            }
        }
        var work = fx.NewFolder();
        var extracted = await LivePhotoSource.ExtractAsync(malicious, work, TestContext.Current.CancellationToken);
        Assert.Equal(work, Path.GetDirectoryName(extracted));
        Assert.Equal(2, Directory.GetFiles(work).Length);
        Assert.False(Directory.Exists(Path.Combine(fx.Root, "outside")));
    }

    [Theory]
    [InlineData(-1, 3, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 11, 1)]
    [InlineData(9, 3, 1)]
    [InlineData(0, 3, -1)]
    [InlineData(double.NaN, 3, 1)]
    public void Invalid_clips_fail_before_writing(double start, double duration, double cover) =>
        Assert.Throws<ConversionRefusedException>(() => LivePhotoConversion.Clip(new MediaOptions
        {
            LivePhotoStart = start,
            LivePhotoDuration = duration,
            LivePhotoCover = cover,
        }, 5));

    [Fact]
    public async Task Missing_mismatched_and_multiple_archive_pairs_are_rejected()
    {
        var input = await VideoAsync();
        var photo = Output(await fx.ConvertAsync([input], new Preset { TargetFormat = "livp" }));
        using (var zip = ZipFile.Open(photo, ZipArchiveMode.Update))
            zip.CreateEntry("other.mov");
        await Assert.ThrowsAsync<InvalidDataException>(() => LivePhotoSource.ExtractAsync(photo, fx.NewFolder(), TestContext.Current.CancellationToken));
        var empty = Path.Combine(fx.NewFolder(), "empty.livp");
        using (var zip = ZipFile.Open(empty, ZipArchiveMode.Create))
            zip.CreateEntry("../../escape.mov");
        await Assert.ThrowsAsync<InvalidDataException>(() => LivePhotoSource.ExtractAsync(empty, fx.NewFolder(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Xmp_is_namespace_aware_and_stale_or_hostile_metadata_is_rejected()
    {
        var jpeg = EngineFixture.MakeImage(fx.NewFolder(), "still.jpg", MagickFormat.Jpeg);
        var video = MovieBoxes.Box("ftyp", "isom\0\0\0\0isom"u8.ToArray());
        var length = video.Length;
        var xmp = $"<x:xmpmeta xmlns:x='adobe:ns:meta/' xmlns:c='http://ns.google.com/photos/1.0/camera/' c:MicroVideo='1' c:MicroVideoOffset='{length}'/>";
        using var legacy = new MemoryStream(JpegWithXmp(await File.ReadAllBytesAsync(jpeg, TestContext.Current.CancellationToken), xmp).Concat(video).ToArray());
        var found = MotionPhoto.FindVideo(legacy);
        Assert.Equal(length, found!.Value.Length);
        using var stale = new MemoryStream(JpegWithXmp(await File.ReadAllBytesAsync(jpeg, TestContext.Current.CancellationToken), xmp));
        Assert.Throws<InvalidDataException>(() => MotionPhoto.FindVideo(stale));
        using var disabled = new MemoryStream(JpegWithXmp(await File.ReadAllBytesAsync(jpeg, TestContext.Current.CancellationToken), xmp.Replace("c:MicroVideo='1'", "c:MotionPhoto='0' c:MicroVideo='1'")).Concat(video).ToArray());
        Assert.Null(MotionPhoto.FindVideo(disabled));
        using var hostile = new MemoryStream(JpegWithXmp(await File.ReadAllBytesAsync(jpeg, TestContext.Current.CancellationToken), "<!DOCTYPE x [<!ENTITY leak SYSTEM 'file:///secret'>]><x>&leak;</x>"));
        Assert.Throws<System.Xml.XmlException>(() => MotionPhoto.FindVideo(hostile));
    }

    private static byte[] JpegWithXmp(byte[] jpeg, string xml)
    {
        var payload = Encoding.UTF8.GetBytes("http://ns.adobe.com/xap/1.0/\0" + xml);
        return [0xff, 0xd8, 0xff, 0xe1, .. MovieBoxes.U16((ushort)(payload.Length + 2)), .. payload, .. jpeg.Skip(2)];
    }

    [Fact]
    public void Invalid_and_extended_media_boxes_are_bounded()
    {
        using var beyond = new MemoryStream([.. MovieBoxes.U32(100), .. "mpvd"u8.ToArray()]);
        Assert.Throws<InvalidDataException>(() => MotionPhoto.FindVideo(beyond));
        using var huge = new MemoryStream([.. MovieBoxes.U32(1), .. "mpvd"u8.ToArray(), .. MovieBoxes.U64(ulong.MaxValue)]);
        Assert.Throws<InvalidDataException>(() => MotionPhoto.FindVideo(huge));
        using var heic = new MemoryStream([.. MovieBoxes.Box("ftyp", "heic\0\0\0\0heic"u8.ToArray()), .. MovieBoxes.Box("mpvd", MovieBoxes.Box("ftyp", "isom\0\0\0\0isom"u8.ToArray()))]);
        var video = MotionPhoto.FindVideo(heic);
        Assert.NotNull(video);
        MotionPhoto.ValidateVideo(heic, video.Value.Offset, video.Value.Length);
    }
}
