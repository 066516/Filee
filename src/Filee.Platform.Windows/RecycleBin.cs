// Moves a file to the Recycle Bin (OutputRule.ReplaceOriginal, #332). SHFileOperation with FOF_ALLOWUNDO deletes a
// file for good on a drive without a Recycle Bin (network shares, most USB sticks), so only local fixed drives are
// tried, and a file another program holds open is refused before Windows is asked.

using System.Runtime.InteropServices;

namespace Filee.Platform.Windows;

internal static class RecycleBin
{
    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x4;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOERRORUI = 0x400;

    /// <summary>Null when <paramref name="path"/> is in the Recycle Bin, otherwise why it was left where it is.</summary>
    public static string? Move(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            return "the file is gone";
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal)
            || new DriveInfo(root).DriveType != DriveType.Fixed)
            return "this drive has no Recycle Bin";
        try
        {
            using (new FileStream(full, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }
        }
        catch (IOException)
        {
            return "the file is open in another program";
        }
        catch (UnauthorizedAccessException)
        {
            return "the file is read-only or not yours to remove";
        }

        var operation = new SHFILEOPSTRUCTW
        {
            wFunc = FO_DELETE,
            pFrom = full + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT,
        };
        var result = SHFileOperationW(ref operation);
        if (result != 0 || operation.fAnyOperationsAborted || File.Exists(full))
            return $"Windows couldn't move it to the Recycle Bin (0x{result:X})";
        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCTW
    {
        public nint hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public nint hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCTW operation);
}
