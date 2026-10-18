// Caches engines keep between conversions (typefaces looked up for HWP and CAD text, Skia's glyph and resource
// caches). The app releases them while no conversion runs, so a Filee waiting in the tray gives the memory back.

using Filee.Engines.Cad;
using Filee.Engines.Hwp;
using SkiaSharp;

namespace Filee.Engines.Infrastructure;

public static class EngineCaches
{
    /// <summary>
    /// Drops what the engines cache between conversions. Call only while no conversion runs: the next one looks
    /// everything up again.
    /// </summary>
    public static void Release()
    {
        RhwpFonts.ReleaseCache();
        CadFonts.ReleaseCache();
        // Skia's caches are shared with the UI's renderer and guarded by Skia itself; they refill on demand.
        SKGraphics.PurgeAllCaches();
    }
}
