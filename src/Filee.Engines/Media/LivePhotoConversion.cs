// Video → Apple LIVP or standard Android JPEG Motion Photo, using the existing FFmpeg installation and options.

using System.Globalization;
using System.IO.Compression;
using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using ImageMagick;

namespace Filee.Engines.Media;

internal static class LivePhotoConversion
{
    public static async Task<IReadOnlyList<string>> CreateAsync(string ffmpeg, string ffprobe, ConversionStep step,
        IProgress<double>? progress, CancellationToken ct)
    {
        var info = await MediaInfo.ProbeAsync(ffprobe, step.InputPath, ct);
        if (info.Video is null || info.Duration is not { TotalSeconds: > 0 } duration)
            throw new ConversionRefusedException("error.live_photo_clip");
        if (info.Video.IsHdr)
            throw new ConversionRefusedException("error.live_photo_hdr");
        var (start, length, cover) = Clip(step.Preset.Media, duration.TotalSeconds);
        var output = step.Output.Allocate(FormatRegistry.Get(step.To).PrimaryExtension);
        if (output is null)
            return [];
        var token = Guid.NewGuid().ToString("N");
        var movie = Path.Combine(step.WorkDirectory, token + (step.To == "livp" ? ".mov" : ".mp4"));
        var png = Path.Combine(step.WorkDirectory, token + ".png");
        var jpeg = Path.Combine(step.WorkDirectory, token + ".jpg");
        var media = step.Preset.Media.Clone();
        media.VideoCodec = VideoCodec.Default;
        media.HardwareEncoding = false;
        if (info.Audio is { Channels: > 2 })
            media.AudioChannels = 2;
        // A new clip must be decoded even if an ordinary container conversion could copy its codec. Otherwise
        // stream-copy seeking starts on an earlier keyframe and -r cannot normalize variable frame rates.
        var encodedInfo = info with { Video = info.Video with { Codec = null }, Audio = info.Audio is { } audio ? audio with { Codec = null } : null };
        var pass = MediaEncoding.Plan(step.InputPath, movie, step.To == "livp" ? "mov" : "mp4", media, encodedInfo, step.WorkDirectory).Single();
        var args = pass.Arguments.ToList();
        var inputAt = args.IndexOf("-i");
        args.InsertRange(inputAt, ["-ss", Number(start)]);
        var movFlagsAt = args.IndexOf("-movflags");
        if (movFlagsAt >= 0)
            args.RemoveRange(movFlagsAt, 2); // Keep moov at EOF so adding the Apple marker never moves media samples.
        args.InsertRange(args.Count - 1, ["-t", Number(length), "-r", "30"]);
        if (info.Audio is not null && !media.RemoveAudio)
            args.InsertRange(args.Count - 1, ["-ar", "48000"]);
        await FfmpegConverter.RunPassAsync(ffmpeg, new FfmpegPass(args, 0.8), TimeSpan.FromSeconds(length), progress, 0, ct);
        // Select by frame number: rounding a fractional seek to microseconds could choose the following frame.
        await FfmpegConverter.RunPassAsync(ffmpeg, new FfmpegPass([
            .. MediaEncoding.Common, "-i", movie, "-map", "0:v:0", "-vf",
            "select=eq(n\\," + ((int)Math.Round(cover * 30)).ToString(CultureInfo.InvariantCulture) + ")",
            "-fps_mode", "vfr", "-frames:v", "1", "-update", "1", "-f", "image2", png,
        ], 0.1), null, progress, 0.8, ct);
        ct.ThrowIfCancellationRequested();
        try
        {
            if (step.To == "livp")
            {
                var id = Guid.NewGuid().ToString("D").ToUpperInvariant();
                AppleLivePhoto.WriteCover(png, jpeg, id);
                AppleLivePhoto.TagMovie(movie, id, cover);
                await using var archiveFile = File.Create(output);
                using var zip = new ZipArchive(archiveFile, ZipArchiveMode.Create, leaveOpen: true);
                await AddEntryAsync(zip, jpeg, id + ".jpg", ct);
                await AddEntryAsync(zip, movie, id + ".mov", ct);
            }
            else
            {
                using (var image = new MagickImage(png))
                {
                    image.Quality = 90;
                    image.Write(jpeg, MagickFormat.Jpeg);
                }
                await MotionPhoto.WriteAsync(jpeg, movie, output, (long)Math.Round(cover * 1_000_000), ct);
            }
            step.Notice?.Invoke(step.To == "livp" ? "notice.live_photo_import" : "notice.motion_photo_compatibility");
            progress?.Report(1);
            return [output];
        }
        catch
        {
            // Encoding only writes private scratch files. Remove an incomplete final archive/photo on failure.
            File.Delete(output);
            throw;
        }
    }

    internal static (double Start, double Duration, double Cover) Clip(MediaOptions options, double duration)
    {
        var start = options.LivePhotoStart;
        var length = options.LivePhotoDuration;
        var cover = options.LivePhotoCover;
        if (!double.IsFinite(start) || !double.IsFinite(length) || !double.IsFinite(cover) || start < 0
            || length < 0.1 || length > 10 || cover < 0 || start >= duration)
            throw new ConversionRefusedException("error.live_photo_clip");
        length = Math.Min(length, duration - start);
        if (length < 1.0 / 30)
            throw new ConversionRefusedException("error.live_photo_clip");
        cover = Math.Floor(Math.Min(cover, Math.Max(0, length - 1.0 / 30)) * 30) / 30;
        return (start, length, cover);
    }

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static async Task AddEntryAsync(ZipArchive zip, string path, string name, CancellationToken ct)
    {
        await using var source = File.OpenRead(path);
        await using var target = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
        await source.CopyToAsync(target, ct);
    }
}
