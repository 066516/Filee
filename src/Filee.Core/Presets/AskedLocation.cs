// "Ask every time" (#521): what the save dialog suggests, and the preset as it runs once the user has answered it.

using Filee.Core.Formats;

namespace Filee.Core.Presets;

public static class AskedLocation
{
    /// <summary>
    /// The name the save dialog suggests for <paramref name="source"/>: the preset's name pattern and renames, and the
    /// target's extension (the source's own for "same as source").
    /// </summary>
    public static string SuggestedName(Preset preset, string presetName, string source)
    {
        var tokens = new OutputPathResolver.Tokens(source, presetName, 1, DateTime.Now);
        var name = OutputPathResolver.Sanitize(
            OutputPathResolver.Rename(OutputPathResolver.ExpandPattern(preset.Output.FileNamePattern, tokens), preset.Output.Renames),
            FormatRegistry.NameWithoutExtension(source));
        var extension = Extension(preset, source);
        return extension.Length == 0 ? name : $"{name}.{extension}";
    }

    /// <summary>The extension the result gets, without the dot; empty for a folder ("Extract").</summary>
    public static string Extension(Preset preset, string source) =>
        preset.TargetFormat == BuiltInData.SameAsSource
            ? FormatRegistry.ExtensionOf(source)
            : FormatRegistry.FindById(preset.TargetFormat)?.Extensions.FirstOrDefault() ?? "";

    /// <summary>
    /// One result, saved as <paramref name="chosenPath"/>: that folder and that name, without the pattern or renames
    /// on top. The dialog has already asked about replacing a file there, so it is replaced.
    /// </summary>
    public static Preset ForFile(Preset preset, string chosenPath)
    {
        var copy = InFolder(preset, Path.GetDirectoryName(Path.GetFullPath(chosenPath))!);
        // shortcut: a chosen name with a pattern token in it ("{date}") is expanded, there is no escape; rare enough.
        copy.Output.FileNamePattern = Path.GetFileNameWithoutExtension(chosenPath);
        copy.Output.Renames = [];
        copy.Output.Conflict = ConflictPolicy.Overwrite;
        return copy;
    }

    /// <summary>Several results, all saved in <paramref name="folder"/> under the preset's own names.</summary>
    public static Preset ForFolder(Preset preset, string folder) => InFolder(preset, folder);

    private static Preset InFolder(Preset preset, string folder)
    {
        var copy = preset.Clone();
        copy.Output.Location = OutputLocation.CustomFolder;
        copy.Output.CustomFolder = folder;
        copy.Output.KeepFoldersFrom = "";
        copy.Output.SubfolderOnlyForSeveralFiles = false;
        return copy;
    }
}
