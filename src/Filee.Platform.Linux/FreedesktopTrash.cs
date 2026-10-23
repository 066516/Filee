// The freedesktop.org Trash (Nautilus, Dolphin, Thunar, Nemo and `gio trash` all use it): the file moves into
// Trash/files and a Trash/info/<name>.trashinfo records where it came from, so the file manager can restore it
// (OutputRule.ReplaceOriginal, #332). Only the home Trash is used, and only for files on the same filesystem; others
// are left where they are.

using System.Globalization;
using System.Text;

namespace Filee.Platform.Linux;

internal static class FreedesktopTrash
{
    /// <summary>Null when <paramref name="path"/> is in <paramref name="trash"/>, otherwise why it was left where it is.</summary>
    public static string? Move(string path, string trash)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            return "the file is gone";
        var files = Path.Combine(trash, "files");
        var info = Path.Combine(trash, "info");
        try
        {
            Directory.CreateDirectory(files);
            Directory.CreateDirectory(info);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "there is no Trash folder";
        }
        if (MountOf(full) != MountOf(files))
            return "the Trash is on another drive";

        var name = Path.GetFileName(full);
        for (var n = 2; File.Exists(Path.Combine(files, name)) || File.Exists(Path.Combine(info, name + ".trashinfo")); n++)
            name = $"{Path.GetFileNameWithoutExtension(full)} {n}{Path.GetExtension(full)}";
        var record = Path.Combine(info, name + ".trashinfo");
        try
        {
            // The record first, as the spec asks: a crash in between leaves no file in the Trash without one.
            using (var stream = new FileStream(record, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write($"[Trash Info]\nPath={Escape(full)}\nDeletionDate={DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}\n");
            }
            File.Move(full, Path.Combine(files, name));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(record); } catch (IOException) { }
            return File.Exists(full) ? "it couldn't be moved to the Trash" : null;
        }
    }

    /// <summary>The path in the record: each part URL-encoded, the slashes kept.</summary>
    internal static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    /// <summary>The mount point a path is on: the longest mounted root that contains it.</summary>
    private static string MountOf(string path)
    {
        try
        {
            return DriveInfo.GetDrives().Select(d => d.RootDirectory.FullName)
                .Where(r => path.StartsWith(r, StringComparison.Ordinal))
                .OrderByDescending(r => r.Length).FirstOrDefault() ?? "/";
        }
        catch (IOException)
        {
            return "/";
        }
    }
}
