// Tests for CropService: output resolving, conflict policies, and thumbnail handling.

using System.IO;
using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Settings;
using ImageMagick;

namespace Filee.App.Tests;

public class CropServiceTests
{
    private static string CreateTestImage(string folder, string name, int width = 100, int height = 80)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Green, (uint)width, (uint)height);
        image.Write(path, MagickFormat.Png);
        return path;
    }

    [AvaloniaFact]
    public async Task CropFileAsync_resolves_output_path_and_crops_image()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("filee-crop-test-").FullName;
        try
        {
            var source = CreateTestImage(folder, "photo.png", 200, 100);
            var store = AppHost.Get<UserDataStore>();
            var loc = AppHost.Get<ILocalizer>();
            var preset = BuiltInData.CreatePresets().Single(p => p.Id == "tool-crop");

            var result = await CropService.CropFileAsync(source, 10, 10, 50, 40, preset, store, loc);

            Assert.Equal(CropStatus.Success, result.Status);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.EndsWith("photo_cropped.png", result.OutputPath);

            using var cropped = new MagickImage(result.OutputPath);
            Assert.Equal((uint)50, cropped.Width);
            Assert.Equal((uint)40, cropped.Height);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    [AvaloniaFact]
    public async Task CropFileAsync_honors_conflict_policies()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("filee-crop-conflict-").FullName;
        try
        {
            var source = CreateTestImage(folder, "sample.png", 100, 100);
            var expectedOutput = Path.Combine(folder, "sample_cropped.png");
            File.WriteAllBytes(expectedOutput, [0x89, 0x50, 0x4E, 0x47]); // Existing file

            var store = AppHost.Get<UserDataStore>();
            var loc = AppHost.Get<ILocalizer>();

            // 1. ConflictPolicy.Rename -> generates (2)
            var renamePreset = BuiltInData.CreatePresets().Single(p => p.Id == "tool-crop").Clone();
            renamePreset.Output.Conflict = ConflictPolicy.Rename;
            var renameResult = await CropService.CropFileAsync(source, 0, 0, 50, 50, renamePreset, store, loc);

            Assert.Equal(CropStatus.Success, renameResult.Status);
            Assert.NotNull(renameResult.OutputPath);
            Assert.EndsWith("sample_cropped (2).png", renameResult.OutputPath);
            Assert.True(File.Exists(renameResult.OutputPath));

            // 2. ConflictPolicy.Skip -> returns SkippedExists and null path
            var skipPreset = BuiltInData.CreatePresets().Single(p => p.Id == "tool-crop").Clone();
            skipPreset.Output.Conflict = ConflictPolicy.Skip;
            var skipResult = await CropService.CropFileAsync(source, 0, 0, 50, 50, skipPreset, store, loc);

            Assert.Equal(CropStatus.SkippedExists, skipResult.Status);
            Assert.Null(skipResult.OutputPath);
            Assert.Equal("toast.skipped", skipResult.ErrorKey);

            // 3. ConflictPolicy.Overwrite -> overwrites existing file
            var overwritePreset = BuiltInData.CreatePresets().Single(p => p.Id == "tool-crop").Clone();
            overwritePreset.Output.Conflict = ConflictPolicy.Overwrite;
            var overwriteResult = await CropService.CropFileAsync(source, 0, 0, 30, 30, overwritePreset, store, loc);

            Assert.Equal(CropStatus.Success, overwriteResult.Status);
            Assert.Equal(expectedOutput, overwriteResult.OutputPath);
            using var overwritten = new MagickImage(expectedOutput);
            Assert.Equal((uint)30, overwritten.Width);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
