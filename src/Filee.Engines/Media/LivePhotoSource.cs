// Reads LIVP pairs and embedded Android motion videos without unpacking archive paths or changing camera files.

using System.IO.Compression;
using Filee.Core.Conversion;
using Filee.Core.Formats;

namespace Filee.Engines.Media;

internal static class LivePhotoSource
{
    private const long MaxVideoBytes = 512L * 1024 * 1024;
    private const long MaxImageBytes = 64L * 1024 * 1024;

    public static async Task<string> ExtractAsync(string input, string workDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var format = FormatRegistry.Detect(input)?.Id;
        if (format == "livp")
            return await ExtractPackageAsync(input, workDirectory, ct);
        await using var file = File.OpenRead(input);
        if (MotionPhoto.FindVideo(file) is { } video)
        {
            if (video.Length > MaxVideoBytes)
                throw new InvalidDataException("Motion Photo video exceeds 512 MiB.");
            MotionPhoto.ValidateVideo(file, video.Offset, video.Length);
            var output = Path.Combine(workDirectory, Guid.NewGuid().ToString("N") + ".mp4");
            await using var target = File.Create(output);
            file.Position = video.Offset;
            await MovieBoxes.CopyRangeAsync(file, target, video.Length, ct);
            return output;
        }
        if (format is "jpg" or "heic")
        {
            var id = AppleLivePhoto.ImageIdentifier(input);
            if (id is not null)
            {
                var name = Path.GetFileNameWithoutExtension(input);
                var siblings = Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(input))!)
                    .Where(p => Path.GetExtension(p).Equals(".mov", StringComparison.OrdinalIgnoreCase)
                        && Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (siblings.Count == 1 && id.Equals(AppleLivePhoto.MovieIdentifier(siblings[0]), StringComparison.OrdinalIgnoreCase))
                    return siblings[0];
            }
        }
        throw new ConversionRefusedException("error.no_motion_photo");
    }

    private static async Task<string> ExtractPackageAsync(string path, string workDirectory, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(path);
        var movies = zip.Entries.Where(e => Path.GetExtension(e.Name).Equals(".mov", StringComparison.OrdinalIgnoreCase)).ToList();
        var images = zip.Entries.Where(e => Path.GetExtension(e.Name).ToLowerInvariant() is ".jpg" or ".jpeg" or ".heic").ToList();
        if (movies.Count != 1 || images.Count != 1
            || !Path.GetFileNameWithoutExtension(movies[0].Name).Equals(Path.GetFileNameWithoutExtension(images[0].Name), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A LIVP must contain exactly one matching photo/video pair.");
        var token = Guid.NewGuid().ToString("N");
        var movie = Path.Combine(workDirectory, token + ".mov");
        var image = Path.Combine(workDirectory, token + Path.GetExtension(images[0].Name).ToLowerInvariant());
        await CopyEntryAsync(movies[0], movie, MaxVideoBytes, ct);
        await CopyEntryAsync(images[0], image, MaxImageBytes, ct);
        var id = AppleLivePhoto.ImageIdentifier(image);
        if (id is null || !id.Equals(AppleLivePhoto.MovieIdentifier(movie), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The Live Photo image and movie identifiers do not match.");
        return movie;
    }

    private static async Task CopyEntryAsync(ZipArchiveEntry entry, string target, long limit, CancellationToken ct)
    {
        if (entry.Length <= 0 || entry.Length > limit)
            throw new InvalidDataException("Live Photo archive entry is empty or too large.");
        // Never use an entry name as an output path; even a traversal entry stays in the job's scratch directory.
        await using var source = entry.Open();
        await using var output = File.Create(target);
        await MovieBoxes.CopyRangeAsync(source, output, entry.Length, ct);
    }
}
