// Service that loads images and executes cropping via ImageMagick.

using System.IO;
using Avalonia.Media.Imaging;
using Filee.Core.Presets;
using ImageMagick;

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

            // Generate an in-memory PNG for the Avalonia viewport
            // If the source image is huge (e.g. 40MP camera RAW/photo), scale preview down for UI fluidity
            var maxDisplayDim = 2560;
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
    /// Crops the image at <paramref name="inputPath"/> with the given pixel coordinates and saves the result.
    /// </summary>
    public static async Task<string> CropFileAsync(string inputPath, int x, int y, int width, int height, Preset? preset = null)
    {
        return await Task.Run(() =>
        {
            var dir = Path.GetDirectoryName(inputPath) ?? "";
            var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
            var ext = Path.GetExtension(inputPath);

            var pattern = preset?.Output.FileNamePattern ?? "{name}_cropped";
            var outputName = pattern.Replace("{name}", nameWithoutExt) + ext;
            var outputPath = Path.Combine(dir, outputName);

            // Avoid collisions
            var counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(dir, $"{pattern.Replace("{name}", nameWithoutExt)}_{counter}{ext}");
                counter++;
            }

            using var image = new MagickImage(inputPath);
            image.AutoOrient();

            var clampedX = Math.Clamp(x, 0, Math.Max(0, (int)image.Width - 1));
            var clampedY = Math.Clamp(y, 0, Math.Max(0, (int)image.Height - 1));
            var clampedW = (uint)Math.Clamp(width, 1, Math.Max(1, (int)image.Width - clampedX));
            var clampedH = (uint)Math.Clamp(height, 1, Math.Max(1, (int)image.Height - clampedY));

            image.Crop(new MagickGeometry(clampedX, clampedY, clampedW, clampedH));
            image.ResetPage();

            // Quality
            if (preset?.Image.Quality is > 0 and <= 100)
                image.Quality = (uint)preset.Image.Quality;

            image.Write(outputPath);
            return outputPath;
        });
    }
}
