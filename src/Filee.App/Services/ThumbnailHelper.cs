// Extracts high-quality file and video thumbnails using Windows Shell and fallbacks.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;

namespace Filee.App.Services;

public static class ThumbnailHelper
{
    public static Bitmap? LoadThumbnail(string path, int targetSize = 96)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // 1. Direct decode for standard image formats
        if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif" or ".ico")
        {
            try
            {
                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, targetSize);
            }
            catch
            {
                // Fall through to shell extractor if image was locked or specialized
            }
        }

        // 2. Windows Shell native thumbnail extraction (handles MP4, MKV, AVI, PDF, DOCX, etc.)
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var bmp = WindowsShellThumbnail.GetThumbnail(path, targetSize);
                if (bmp is not null)
                    return bmp;
            }
            catch
            {
                // Fall through
            }
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static class WindowsShellThumbnail
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            nint pbc,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItemImageFactory ppv);

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out nint phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE(int cx, int cy)
        {
            public int cx = cx;
            public int cy = cy;
        }

        [Flags]
        private enum SIIGBF
        {
            SIIGBF_RESIZETOFIT = 0x00,
            SIIGBF_BIGGERSIZEOK = 0x01,
            SIIGBF_MEMORYONLY = 0x02,
            SIIGBF_ICONONLY = 0x04,
            SIIGBF_THUMBNAILONLY = 0x08,
            SIIGBF_INCACHEONLY = 0x10,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public nint bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetObject(nint hgdiobj, int cbBuffer, out BITMAP lpvObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(nint hdc, nint hbm, uint start, uint lines, [Out] byte[]? lpBits, ref BITMAPINFO bi, uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(nint hObject);

        [DllImport("user32.dll")]
        private static extern nint GetDC(nint hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(nint hWnd, nint hDC);

        private static readonly Guid ShellItemImageFactoryGuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        public static Bitmap? GetThumbnail(string path, int size)
        {
            var hr = SHCreateItemFromParsingName(path, 0, ShellItemImageFactoryGuid, out var factory);
            if (hr != 0 || factory is null)
                return null;

            nint hBitmap = 0;
            try
            {
                // First try bigger size ok, thumbnail only
                hr = factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_THUMBNAILONLY, out hBitmap);
                if (hr != 0 || hBitmap == 0)
                {
                    // Fallback to resize-to-fit
                    hr = factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);
                }

                if (hr != 0 || hBitmap == 0)
                    return null;

                if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out var bmpHeader) == 0 || bmpHeader.bmWidth <= 0 || bmpHeader.bmHeight <= 0)
                    return null;

                var width = bmpHeader.bmWidth;
                var height = Math.Abs(bmpHeader.bmHeight);
                var pixelBytes = new byte[width * height * 4];

                var bi = new BITMAPINFO();
                bi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                bi.bmiHeader.biWidth = width;
                bi.bmiHeader.biHeight = height; // positive for bottom-up BMP
                bi.bmiHeader.biPlanes = 1;
                bi.bmiHeader.biBitCount = 32;
                bi.bmiHeader.biCompression = 0; // BI_RGB
                bi.bmiHeader.biSizeImage = (uint)pixelBytes.Length;

                var hdc = GetDC(0);
                if (hdc == 0)
                    return null;

                try
                {
                    if (GetDIBits(hdc, hBitmap, 0, (uint)height, pixelBytes, ref bi, 0) == 0)
                        return null;
                }
                finally
                {
                    ReleaseDC(0, hdc);
                }

                // Construct a standard 54-byte BMP stream
                var ms = new MemoryStream(54 + pixelBytes.Length);
                using (var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
                {
                    // BITMAPFILEHEADER (14 bytes)
                    writer.Write((ushort)0x4D42); // "BM"
                    writer.Write((uint)(54 + pixelBytes.Length));
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((uint)54);

                    // BITMAPINFOHEADER (40 bytes)
                    writer.Write((uint)40);
                    writer.Write(width);
                    writer.Write(height);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)0);
                    writer.Write((uint)pixelBytes.Length);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write((uint)0);
                    writer.Write((uint)0);

                    // Pixels
                    writer.Write(pixelBytes);
                }

                ms.Position = 0;
                return new Bitmap(ms);
            }
            finally
            {
                if (hBitmap != 0)
                    DeleteObject(hBitmap);
            }
        }
    }
}
