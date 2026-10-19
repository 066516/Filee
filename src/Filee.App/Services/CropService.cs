// Loads image previews and performs crops using ImageMagick and OutputPathResolver.

using System.IO;
using Avalonia.Media.Imaging;
using Filee.Core.History;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Settings;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public static class CropService
{
    /// <summary>
    /// Loads an image as a standard Avalonia Bitmap along with its true pixel dimensions,
    /// properly oriented according to EXIF.
    /// </summary>
    public static async Task<(Bitmap Bitmap, int Width, int Height)> LoadPreviewAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            using var image = new MagickImage(inputPath);
            image.AutoOrient();
            var origWidth = (int)image.Width;
            var origHeight = (int)image.Height;

            // Generate an in-memory PNG for the viewport, downscaling huge images for UI fluidity.
            const int maxDisplayDim = 2560;
            if (image.Width > maxDisplayDim || image.Height > maxDisplayDim)
            {
                image.Resize(new MagickGeometry((uint)maxDisplayDim, (uint)maxDisplayDim) { Greater = true });
            }

            using var mem = new MemoryStream();
            image.Write(mem, MagickFormat.Png);
            mem.Position = 0;
            var bitmap = new Bitmap(mem);

            return (bitmap, origWidth, origHeight);
        });
    }

    /// <summary>
    /// Crops the image at <paramref name="inputPath"/> with the given pixel coordinates and saves the result using
    /// the preset's output rule and the application's default output settings.
    /// </summary>
    public static async Task<string?> CropFileAsync(
        string inputPath,
        int x, int y, int width, int height,
        Preset preset,
        UserDataStore store,
        ILocalizer loc,
        ILogger? log = null)
    {
        return await Task.Run(() =>
        {
            var ext = Path.GetExtension(inputPath).TrimStart('.');
            var appliedPreset = store.Settings.WithDefaultOutput(preset);
            var tokens = new OutputPathResolver.Tokens(inputPath, loc.DisplayName(appliedPreset), 1, DateTime.Now);
            var outputPath = OutputPathResolver.Resolve(appliedPreset.Output, tokens, ext, File.Exists);
            if (outputPath is null)
            {
                log?.LogInformation("Skipping crop of {Input} as conflict policy resolved to null", inputPath);
                return null;
            }

            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            using var image = new MagickImage(inputPath);
            image.AutoOrient();

            var clampedX = Math.Clamp(x, 0, Math.Max(0, (int)image.Width - 1));
            var clampedY = Math.Clamp(y, 0, Math.Max(0, (int)image.Height - 1));
            var clampedW = (uint)Math.Clamp(width, 1, Math.Max(1, (int)image.Width - clampedX));
            var clampedH = (uint)Math.Clamp(height, 1, Math.Max(1, (int)image.Height - clampedY));

            image.Crop(new MagickGeometry(clampedX, clampedY, clampedW, clampedH));
            image.ResetPage();

            if (appliedPreset.Image.Quality is > 0 and <= 100)
                image.Quality = (uint)appliedPreset.Image.Quality;

            image.Write(outputPath);

            if (appliedPreset.Output.KeepDates || store.Settings.KeepFileDates)
            {
                try
                {
                    File.SetCreationTimeUtc(outputPath, File.GetCreationTimeUtc(inputPath));
                    File.SetLastWriteTimeUtc(outputPath, File.GetLastWriteTimeUtc(inputPath));
                }
                catch
                {
                    // Ignore date preservation failures on read-only/virtual filesystems
                }
            }

            if (store.Settings.KeepHistory)
            {
                store.AddHistory(new HistoryEntry
                {
                    JobId = Guid.NewGuid().ToString("N")[..12],
                    FinishedAt = DateTime.Now,
                    PresetId = preset.Id,
                    PresetName = loc.DisplayName(preset),
                    State = Filee.Core.Conversion.JobState.Completed,
                    Sources = [inputPath],
                    Outputs = [outputPath],
                });
            }

            return outputPath;
        });
    }
}
