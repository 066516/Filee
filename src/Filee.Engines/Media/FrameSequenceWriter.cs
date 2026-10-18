// Video → every frame as a picture ("Save every frame", MediaOptions.SaveEveryFrame): numbered PNG or JPG files in
// one folder, for focus stacking or picking the sharpest shot. The pictures are written into a hidden folder next to
// the output and moved into place when FFmpeg is done, so a cancelled or failed run leaves nothing behind.

using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Engines.Archives;

namespace Filee.Engines.Media;

/// <summary>Writes the frames of a video as numbered pictures into a folder.</summary>
internal static class FrameSequenceWriter
{
    /// <summary>Name suffix of the output folder: "clip.mp4" → "clip_frames".</summary>
    public const string FolderSuffix = "_frames";

    /// <summary>
    /// Saves the frames of <paramref name="input"/> as <see cref="ConversionStep.To"/> pictures (frame_000001.png, …)
    /// into the folder the output rule names: "clip_frames", "clip_frames (2)" when that exists, or skipped, like any
    /// other output.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ConvertAsync(string ffmpeg, ConversionStep step, string input, MediaInfo info,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var video = info.Video ?? throw new InvalidOperationException("The file has no video stream.");
        var output = step.Output.Allocate("", FolderSuffix);
        if (output is null)
            return [];

        // Same folder as the output (same drive, so the move is instant); the frames go into a visible folder inside
        // it, which becomes the output.
        var staging = Path.Combine(Path.GetDirectoryName(output)!, $".{Path.GetFileName(output)}.filee-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(staging).Attributes |= FileAttributes.Hidden;
        try
        {
            var frames = Directory.CreateDirectory(Path.Combine(staging, "frames")).FullName;
            var extension = FormatRegistry.Get(step.To).PrimaryExtension;
            // FFmpeg reads % in the whole path as part of the number pattern: a literal one is written %%.
            var pattern = Path.Combine(frames.Replace("%", "%%", StringComparison.Ordinal), $"frame_%06d.{extension}");
            var arguments = MediaEncoding.FrameSequence(input, pattern, extension, video, step.Preset.Image.Quality,
                step.Preset.Media.FrameInterval, step.Preset.Media.FramesPerSecond);

            await FfmpegConverter.RunPassAsync(ffmpeg, new FfmpegPass(arguments, 1), info.Duration, progress, 0, cancellationToken);
            if (!Directory.EnumerateFiles(frames).Any())
                throw new InvalidOperationException("FFmpeg did not write any frames.");

            ArchiveFiles.MoveFolder(frames, output);
            progress?.Report(1);
            return [output];
        }
        finally
        {
            // FFmpeg has exited by now (FfmpegRunner waits for it after killing it), so nothing holds these files.
            ArchiveFiles.TryDeleteFolder(staging);
        }
    }
}
