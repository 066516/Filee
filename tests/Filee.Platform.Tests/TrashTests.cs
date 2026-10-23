// Moving an original to the Recycle Bin / Trash for "Replace the original" (#332): it works where there is one, and
// anything else leaves the file where it is with a reason.

using Filee.Platform.Linux;
using Filee.Platform.MacOS;
using Filee.Platform.Windows;

namespace Filee.Platform.Tests;

public sealed class TrashTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "filee-trash-" + Guid.NewGuid().ToString("N"));

    public TrashTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_file_open_elsewhere_stays_in_the_Recycle_Bins_place()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Requires Windows.");
            return;
        }
        var file = Path.Combine(_root, "open.png");
        File.WriteAllText(file, "x");

        string? reason;
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            reason = new WindowsPlatformServices().MoveToTrash(file);

        Assert.Equal("the file is open in another program", reason);
        Assert.Equal("x", File.ReadAllText(file));
    }

    [Fact]
    public void A_missing_file_gives_a_reason()
    {
        var missing = Path.Combine(_root, "gone.png");
        if (OperatingSystem.IsWindows())
            Assert.NotNull(new WindowsPlatformServices().MoveToTrash(missing));
        else if (OperatingSystem.IsMacOS())
            Assert.NotNull(new MacOSPlatformServices().MoveToTrash(missing));
        Assert.NotNull(FreedesktopTrash.Move(missing, Path.Combine(_root, "Trash")));
    }

    [Fact]
    public void The_freedesktop_Trash_records_where_the_file_came_from()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Requires Linux paths.");
            return;
        }
        var trash = Path.Combine(_root, "Trash");
        var first = Path.Combine(_root, "my photo.png");
        File.WriteAllText(first, "1");
        Assert.Null(FreedesktopTrash.Move(first, trash));
        File.WriteAllText(first, "2");
        Assert.Null(FreedesktopTrash.Move(first, trash));

        Assert.False(File.Exists(first));
        Assert.Equal("1", File.ReadAllText(Path.Combine(trash, "files", "my photo.png")));
        Assert.Equal("2", File.ReadAllText(Path.Combine(trash, "files", "my photo 2.png")));
        var info = File.ReadAllText(Path.Combine(trash, "info", "my photo 2.png.trashinfo"));
        Assert.StartsWith("[Trash Info]\nPath=" + FreedesktopTrash.Escape(first) + "\nDeletionDate=", info);
        Assert.Contains("my%20photo.png", info);
    }

    [Fact]
    public void The_macOS_Trash_takes_the_file()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Skip("Requires macOS.");
            return;
        }
        var file = Path.Combine(_root, "trash-me.png");
        File.WriteAllText(file, "x");

        Assert.Null(new MacOSPlatformServices().MoveToTrash(file));
        Assert.False(File.Exists(file));
    }
}
