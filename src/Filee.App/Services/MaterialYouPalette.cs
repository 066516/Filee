// Material You (Material Design 3) dynamic tonal palette generator.
// Generates tonal color roles (Primary, PrimaryContainer, SecondaryContainer, SurfaceContainer, etc.)
// from a seed color for Light and Dark variants.

using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Filee.App.Services;

/// <summary>Resolved Material Design 3 tonal color tokens.</summary>
public sealed class MaterialYouPalette
{
    public required Color Primary { get; init; }
    public required Color OnPrimary { get; init; }
    public required Color PrimaryContainer { get; init; }
    public required Color OnPrimaryContainer { get; init; }

    public required Color Secondary { get; init; }
    public required Color OnSecondary { get; init; }
    public required Color SecondaryContainer { get; init; }
    public required Color OnSecondaryContainer { get; init; }

    public required Color Surface { get; init; }
    public required Color SurfaceDim { get; init; }
    public required Color SurfaceBright { get; init; }
    public required Color SurfaceContainerLowest { get; init; }
    public required Color SurfaceContainerLow { get; init; }
    public required Color SurfaceContainer { get; init; }
    public required Color SurfaceContainerHigh { get; init; }
    public required Color SurfaceContainerHighest { get; init; }

    public required Color OnSurface { get; init; }
    public required Color OnSurfaceVariant { get; init; }

    public required Color Outline { get; init; }
    public required Color OutlineVariant { get; init; }

    public required Color Danger { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }

    public required BoxShadows CardShadow { get; init; }

    /// <summary>Calculates standard sRGB relative luminance.</summary>
    public static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    /// <summary>Calculates dynamic text contrast (dark charcoal vs crisp white) based on background luminance.</summary>
    public static Color TextOn(Color bg)
    {
        return Luminance(bg) > 0.42 ? Color.Parse("#141E11") : Colors.White;
    }

    /// <summary>Alpha-blends a foreground color over a background color (matching Inkora theme engine).</summary>
    public static Color AlphaBlend(Color foreground, Color background)
    {
        double fA = foreground.A / 255.0;
        double bA = background.A / 255.0;
        double rA = fA + bA * (1.0 - fA);
        if (rA <= 1e-5) return Colors.Transparent;
        byte r = (byte)Math.Clamp(Math.Round((foreground.R * fA + background.R * bA * (1.0 - fA)) / rA), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round((foreground.G * fA + background.G * bA * (1.0 - fA)) / rA), 0, 255);
        byte b = (byte)Math.Clamp(Math.Round((foreground.B * fA + background.B * bA * (1.0 - fA)) / rA), 0, 255);
        byte a = (byte)Math.Clamp(Math.Round(rA * 255.0), 0, 255);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>Generates the complete Material 3 tonal color system from a seed accent color, with Inkora pastel presets.</summary>
    public static MaterialYouPalette Generate(Color seed, bool isDark)
    {
        // Check for matching Inkora curated pastel presets first
        if (TryGetInkoraPreset(seed, isDark, out var presetPalette))
            return presetPalette;

        var (h, s, l) = RgbToHsl(seed);
        var baseSat = Math.Clamp(s, 0.25, 0.85);

        if (!isDark)
        {
            // ── Light Mode (Inkora alpha blend tinted paper surfaces) ──
            var primary = seed;
            var onPrimary = TextOn(primary);
            var primaryContainer = HslToRgb(h, Math.Min(baseSat * 0.65, 0.50), 0.92);
            var onPrimaryContainer = TextOn(primaryContainer);

            var secondary = HslToRgb(h, Math.Min(baseSat * 0.45, 0.38), 0.45);
            var onSecondary = TextOn(secondary);
            var secondaryContainer = HslToRgb(h, Math.Min(baseSat * 0.35, 0.28), 0.94);
            var onSecondaryContainer = TextOn(secondaryContainer);

            // Inkora light tinted surface stack
            var surface = AlphaBlend(Color.FromArgb((byte)(0.03 * 255), seed.R, seed.G, seed.B), Color.Parse("#FAF9FC"));
            var surfaceDim = AlphaBlend(Color.FromArgb((byte)(0.05 * 255), seed.R, seed.G, seed.B), Color.Parse("#F5F3F9"));
            var surfaceBright = Colors.White;
            var surfaceContainerLowest = Colors.White;
            var surfaceContainerLow = AlphaBlend(Color.FromArgb((byte)(0.06 * 255), seed.R, seed.G, seed.B), Color.Parse("#F3F1F7"));
            var surfaceContainer = AlphaBlend(Color.FromArgb((byte)(0.09 * 255), seed.R, seed.G, seed.B), Color.Parse("#EBE8F2"));
            var surfaceContainerHigh = AlphaBlend(Color.FromArgb((byte)(0.13 * 255), seed.R, seed.G, seed.B), Color.Parse("#E3DFEC"));
            var surfaceContainerHighest = AlphaBlend(Color.FromArgb((byte)(0.17 * 255), seed.R, seed.G, seed.B), Color.Parse("#DCD7E7"));

            var onSurface = Color.Parse("#1E1B22");
            var onSurfaceVariant = Color.Parse("#534F5C");

            var outline = AlphaBlend(Color.FromArgb((byte)(0.30 * 255), seed.R, seed.G, seed.B), Color.Parse("#8E889B"));
            var outlineVariant = AlphaBlend(Color.FromArgb((byte)(0.18 * 255), seed.R, seed.G, seed.B), Color.Parse("#D5CFE3"));

            return new MaterialYouPalette
            {
                Primary = primary,
                OnPrimary = onPrimary,
                PrimaryContainer = primaryContainer,
                OnPrimaryContainer = onPrimaryContainer,
                Secondary = secondary,
                OnSecondary = onSecondary,
                SecondaryContainer = secondaryContainer,
                OnSecondaryContainer = onSecondaryContainer,
                Surface = surface,
                SurfaceDim = surfaceDim,
                SurfaceBright = surfaceBright,
                SurfaceContainerLowest = surfaceContainerLowest,
                SurfaceContainerLow = surfaceContainerLow,
                SurfaceContainer = surfaceContainer,
                SurfaceContainerHigh = surfaceContainerHigh,
                SurfaceContainerHighest = surfaceContainerHighest,
                OnSurface = onSurface,
                OnSurfaceVariant = onSurfaceVariant,
                Outline = outline,
                OutlineVariant = outlineVariant,
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000"),
            };
        }
        else
        {
            // ── Dark Mode (Inkora alpha blend tinted deep charcoal surfaces) ──
            var primary = seed;
            var onPrimary = TextOn(primary);
            var primaryContainer = HslToRgb(h, Math.Min(baseSat * 0.60, 0.42), 0.28);
            var onPrimaryContainer = TextOn(primaryContainer);

            var secondary = HslToRgb(h, Math.Min(baseSat * 0.40, 0.32), 0.72);
            var onSecondary = TextOn(secondary);
            var secondaryContainer = HslToRgb(h, Math.Min(baseSat * 0.30, 0.25), 0.24);
            var onSecondaryContainer = TextOn(secondaryContainer);

            // Inkora dark tinted surface stack
            var surface = AlphaBlend(Color.FromArgb((byte)(0.08 * 255), seed.R, seed.G, seed.B), Color.Parse("#16151B"));
            var surfaceDim = Color.Parse("#110F15");
            var surfaceBright = AlphaBlend(Color.FromArgb((byte)(0.22 * 255), seed.R, seed.G, seed.B), Color.Parse("#322E3F"));
            var surfaceContainerLowest = Color.Parse("#110F15");
            var surfaceContainerLow = AlphaBlend(Color.FromArgb((byte)(0.12 * 255), seed.R, seed.G, seed.B), Color.Parse("#1D1B24"));
            var surfaceContainer = AlphaBlend(Color.FromArgb((byte)(0.16 * 255), seed.R, seed.G, seed.B), Color.Parse("#24222E"));
            var surfaceContainerHigh = AlphaBlend(Color.FromArgb((byte)(0.20 * 255), seed.R, seed.G, seed.B), Color.Parse("#2E2B3A"));
            var surfaceContainerHighest = AlphaBlend(Color.FromArgb((byte)(0.24 * 255), seed.R, seed.G, seed.B), Color.Parse("#383547"));

            var onSurface = Color.Parse("#EDE8F4");
            var onSurfaceVariant = Color.Parse("#C5BFCF");

            var outline = AlphaBlend(Color.FromArgb((byte)(0.35 * 255), seed.R, seed.G, seed.B), Color.Parse("#686477"));
            var outlineVariant = AlphaBlend(Color.FromArgb((byte)(0.20 * 255), seed.R, seed.G, seed.B), Color.Parse("#403C4D"));

            return new MaterialYouPalette
            {
                Primary = primary,
                OnPrimary = onPrimary,
                PrimaryContainer = primaryContainer,
                OnPrimaryContainer = onPrimaryContainer,
                Secondary = secondary,
                OnSecondary = onSecondary,
                SecondaryContainer = secondaryContainer,
                OnSecondaryContainer = onSecondaryContainer,
                Surface = surface,
                SurfaceDim = surfaceDim,
                SurfaceBright = surfaceBright,
                SurfaceContainerLowest = surfaceContainerLowest,
                SurfaceContainerLow = surfaceContainerLow,
                SurfaceContainer = surfaceContainer,
                SurfaceContainerHigh = surfaceContainerHigh,
                SurfaceContainerHighest = surfaceContainerHighest,
                OnSurface = onSurface,
                OnSurfaceVariant = onSurfaceVariant,
                Outline = outline,
                OutlineVariant = outlineVariant,
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000"),
            };
        }
    }

    private static bool ColorMatches(Color a, Color b) =>
        Math.Abs(a.R - b.R) <= 3 && Math.Abs(a.G - b.G) <= 3 && Math.Abs(a.B - b.B) <= 3;

    private static bool TryGetInkoraPreset(Color seed, bool isDark, out MaterialYouPalette palette)
    {
        // 0. Monochrome (Pure Black & White)
        if (ColorMatches(seed, Color.Parse("#111111")) || ColorMatches(seed, Color.Parse("#000000")) ||
            ColorMatches(seed, Colors.White) || ColorMatches(seed, Color.Parse("#F5F5F5")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#F5F5F5"),
                OnPrimary = Color.Parse("#111111"),
                PrimaryContainer = Color.Parse("#282828"),
                OnPrimaryContainer = Color.Parse("#F5F5F5"),
                Secondary = Color.Parse("#D4D4D4"),
                OnSecondary = Color.Parse("#111111"),
                SecondaryContainer = Color.Parse("#202020"),
                OnSecondaryContainer = Color.Parse("#E0E0E0"),
                Surface = Color.Parse("#0D0D0D"),
                SurfaceDim = Color.Parse("#060606"),
                SurfaceBright = Color.Parse("#1E1E1E"),
                SurfaceContainerLowest = Color.Parse("#050505"),
                SurfaceContainerLow = Color.Parse("#141414"),
                SurfaceContainer = Color.Parse("#1B1B1B"),
                SurfaceContainerHigh = Color.Parse("#242424"),
                SurfaceContainerHighest = Color.Parse("#2E2E2E"),
                OnSurface = Color.Parse("#F5F5F5"),
                OnSurfaceVariant = Color.Parse("#B5B5B5"),
                Outline = Color.Parse("#4C4C4C"),
                OutlineVariant = Color.Parse("#2E2E2E"),
                Danger = Color.Parse("#FF6B6B"),
                Success = Color.Parse("#69DB7C"),
                Warning = Color.Parse("#FFD43B"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #60000000, 0 8 20 0 #40000000"),
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#111111"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#EAEAEA"),
                OnPrimaryContainer = Color.Parse("#111111"),
                Secondary = Color.Parse("#333333"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#F0F0F0"),
                OnSecondaryContainer = Color.Parse("#111111"),
                Surface = Colors.White,
                SurfaceDim = Color.Parse("#F5F5F5"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#F8F8F8"),
                SurfaceContainer = Color.Parse("#F0F0F0"),
                SurfaceContainerHigh = Color.Parse("#E8E8E8"),
                SurfaceContainerHighest = Color.Parse("#DFDFDF"),
                OnSurface = Color.Parse("#111111"),
                OnSurfaceVariant = Color.Parse("#4A4A4A"),
                Outline = Color.Parse("#B0B0B0"),
                OutlineVariant = Color.Parse("#E0E0E0"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #18000000, 0 4 12 1 #10000000"),
            };
            return true;
        }

        // 1. Blush & Peach (Light: #E07A70, Dark: #F29B91)
        if (ColorMatches(seed, Color.Parse("#E07A70")) || ColorMatches(seed, Color.Parse("#F29B91")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#F29B91"),
                OnPrimary = Color.Parse("#441814"),
                PrimaryContainer = Color.Parse("#5D2D27"),
                OnPrimaryContainer = Color.Parse("#FDE8E5"),
                Secondary = Color.Parse("#9EC4B5"),
                OnSecondary = Color.Parse("#13362B"),
                SecondaryContainer = Color.Parse("#26443A"),
                OnSecondaryContainer = Color.Parse("#DBEDE6"),
                Surface = Color.Parse("#1D1413"),
                SurfaceDim = Color.Parse("#140E0D"),
                SurfaceBright = Color.Parse("#382725"),
                SurfaceContainerLowest = Color.Parse("#140E0D"),
                SurfaceContainerLow = Color.Parse("#231A18"),
                SurfaceContainer = Color.Parse("#2A201E"),
                SurfaceContainerHigh = Color.Parse("#352927"),
                SurfaceContainerHighest = Color.Parse("#413331"),
                OnSurface = Color.Parse("#F8ECE9"),
                OnSurfaceVariant = Color.Parse("#CBB0AB"),
                Outline = Color.Parse("#7D6561"),
                OutlineVariant = Color.Parse("#51403D"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#E07A70"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#FDECE8"),
                OnPrimaryContainer = Color.Parse("#4A2825"),
                Secondary = Color.Parse("#7FA898"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#E4F0EC"),
                OnSecondaryContainer = Color.Parse("#22362F"),
                Surface = Color.Parse("#FDF9F7"),
                SurfaceDim = Color.Parse("#F5EDE9"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#FAF2EF"),
                SurfaceContainer = Color.Parse("#F6EAE6"),
                SurfaceContainerHigh = Color.Parse("#F0DFDA"),
                SurfaceContainerHighest = Color.Parse("#EAD5CF"),
                OnSurface = Color.Parse("#2D2423"),
                OnSurfaceVariant = Color.Parse("#6B5854"),
                Outline = Color.Parse("#B39893"),
                OutlineVariant = Color.Parse("#E8D5D0"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 2. Strawberry Milk (Light: #E86483, Dark: #F79BB0)
        if (ColorMatches(seed, Color.Parse("#E86483")) || ColorMatches(seed, Color.Parse("#F79BB0")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#F79BB0"),
                OnPrimary = Color.Parse("#471320"),
                PrimaryContainer = Color.Parse("#632434"),
                OnPrimaryContainer = Color.Parse("#FEDFE6"),
                Secondary = Color.Parse("#98C4B0"),
                OnSecondary = Color.Parse("#163829"),
                SecondaryContainer = Color.Parse("#284839"),
                OnSecondaryContainer = Color.Parse("#DBEFE6"),
                Surface = Color.Parse("#1D1517"),
                SurfaceDim = Color.Parse("#140E10"),
                SurfaceBright = Color.Parse("#38272C"),
                SurfaceContainerLowest = Color.Parse("#140E10"),
                SurfaceContainerLow = Color.Parse("#23191C"),
                SurfaceContainer = Color.Parse("#2A1E22"),
                SurfaceContainerHigh = Color.Parse("#35272B"),
                SurfaceContainerHighest = Color.Parse("#413136"),
                OnSurface = Color.Parse("#F7ECEE"),
                OnSurfaceVariant = Color.Parse("#C9B0B7"),
                Outline = Color.Parse("#7C646B"),
                OutlineVariant = Color.Parse("#503E44"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#E86483"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#FFECF0"),
                OnPrimaryContainer = Color.Parse("#4A1824"),
                Secondary = Color.Parse("#76A893"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#E2F0EA"),
                OnSecondaryContainer = Color.Parse("#1E382E"),
                Surface = Color.Parse("#FFF8F9"),
                SurfaceDim = Color.Parse("#F7ECF0"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#FDF1F4"),
                SurfaceContainer = Color.Parse("#F9E8EC"),
                SurfaceContainerHigh = Color.Parse("#F3DDE3"),
                SurfaceContainerHighest = Color.Parse("#EBCFD6"),
                OnSurface = Color.Parse("#2E2225"),
                OnSurfaceVariant = Color.Parse("#6B5359"),
                Outline = Color.Parse("#B8939C"),
                OutlineVariant = Color.Parse("#EBD2D9"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 3. Matcha Latte (Light: #5A8E51, Dark: #8DC983)
        if (ColorMatches(seed, Color.Parse("#5A8E51")) || ColorMatches(seed, Color.Parse("#8DC983")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#8DC983"),
                OnPrimary = Color.Parse("#143710"),
                PrimaryContainer = Color.Parse("#26521F"),
                OnPrimaryContainer = Color.Parse("#DFF5DA"),
                Secondary = Color.Parse("#F1B782"),
                OnSecondary = Color.Parse("#3F240A"),
                SecondaryContainer = Color.Parse("#563515"),
                OnSecondaryContainer = Color.Parse("#FEEDD8"),
                Surface = Color.Parse("#141712"),
                SurfaceDim = Color.Parse("#0E110D"),
                SurfaceBright = Color.Parse("#2E372A"),
                SurfaceContainerLowest = Color.Parse("#0E110D"),
                SurfaceContainerLow = Color.Parse("#1A1F18"),
                SurfaceContainer = Color.Parse("#1F251D"),
                SurfaceContainerHigh = Color.Parse("#293126"),
                SurfaceContainerHighest = Color.Parse("#343E31"),
                OnSurface = Color.Parse("#ECF0E7"),
                OnSurfaceVariant = Color.Parse("#BCC4B6"),
                Outline = Color.Parse("#6B7565"),
                OutlineVariant = Color.Parse("#3F483A"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#5A8E51"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#E7F3E3"),
                OnPrimaryContainer = Color.Parse("#193714"),
                Secondary = Color.Parse("#CC8749"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#FAEEDB"),
                OnSecondaryContainer = Color.Parse("#3E2611"),
                Surface = Color.Parse("#F9FAF5"),
                SurfaceDim = Color.Parse("#F0F2EB"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#F3F5EE"),
                SurfaceContainer = Color.Parse("#ECF0E5"),
                SurfaceContainerHigh = Color.Parse("#E2E7DA"),
                SurfaceContainerHighest = Color.Parse("#D7DDCE"),
                OnSurface = Color.Parse("#242721"),
                OnSurfaceVariant = Color.Parse("#555B4F"),
                Outline = Color.Parse("#8D9686"),
                OutlineVariant = Color.Parse("#D4DCCE"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 4. Lavender Mist (Light: #8B7AB8, Dark: #BCAEE5)
        if (ColorMatches(seed, Color.Parse("#8B7AB8")) || ColorMatches(seed, Color.Parse("#BCAEE5")) || ColorMatches(seed, Color.Parse("#C5B4E3")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#BCAEE5"),
                OnPrimary = Color.Parse("#291E47"),
                PrimaryContainer = Color.Parse("#3F3260"),
                OnPrimaryContainer = Color.Parse("#EFEBF9"),
                Secondary = Color.Parse("#91CFC0"),
                OnSecondary = Color.Parse("#123B32"),
                SecondaryContainer = Color.Parse("#224940"),
                OnSecondaryContainer = Color.Parse("#DBF2EC"),
                Surface = Color.Parse("#181520"),
                SurfaceDim = Color.Parse("#110F17"),
                SurfaceBright = Color.Parse("#332D42"),
                SurfaceContainerLowest = Color.Parse("#110F17"),
                SurfaceContainerLow = Color.Parse("#1E1A27"),
                SurfaceContainer = Color.Parse("#252030"),
                SurfaceContainerHigh = Color.Parse("#302A3D"),
                SurfaceContainerHighest = Color.Parse("#3C354B"),
                OnSurface = Color.Parse("#F1EEF7"),
                OnSurfaceVariant = Color.Parse("#C4BED0"),
                Outline = Color.Parse("#777183"),
                OutlineVariant = Color.Parse("#4A4456"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#8B7AB8"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#EDE8F7"),
                OnPrimaryContainer = Color.Parse("#2F2648"),
                Secondary = Color.Parse("#6FA89A"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#E1F2ED"),
                OnSecondaryContainer = Color.Parse("#1D3630"),
                Surface = Color.Parse("#FAF8FD"),
                SurfaceDim = Color.Parse("#F1ECF7"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#F5F2FA"),
                SurfaceContainer = Color.Parse("#EFEBF7"),
                SurfaceContainerHigh = Color.Parse("#E8E2F2"),
                SurfaceContainerHighest = Color.Parse("#E0D8EC"),
                OnSurface = Color.Parse("#26232E"),
                OnSurfaceVariant = Color.Parse("#5D5769"),
                Outline = Color.Parse("#928B9F"),
                OutlineVariant = Color.Parse("#D8D1E6"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 5. Sage & Mint (Light: #529B7D, Dark: #88D5B1)
        if (ColorMatches(seed, Color.Parse("#529B7D")) || ColorMatches(seed, Color.Parse("#88D5B1")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#88D5B1"),
                OnPrimary = Color.Parse("#103625"),
                PrimaryContainer = Color.Parse("#22513B"),
                OnPrimaryContainer = Color.Parse("#DDF5EB"),
                Secondary = Color.Parse("#F4BE77"),
                OnSecondary = Color.Parse("#412803"),
                SecondaryContainer = Color.Parse("#57390E"),
                OnSecondaryContainer = Color.Parse("#FCEFD9"),
                Surface = Color.Parse("#121A16"),
                SurfaceDim = Color.Parse("#0C120F"),
                SurfaceBright = Color.Parse("#2C3B33"),
                SurfaceContainerLowest = Color.Parse("#0C120F"),
                SurfaceContainerLow = Color.Parse("#17211C"),
                SurfaceContainer = Color.Parse("#1D2A24"),
                SurfaceContainerHigh = Color.Parse("#27332C"),
                SurfaceContainerHighest = Color.Parse("#33423A"),
                OnSurface = Color.Parse("#EBF2EE"),
                OnSurfaceVariant = Color.Parse("#BACCC3"),
                Outline = Color.Parse("#677D72"),
                OutlineVariant = Color.Parse("#3B4D44"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#529B7D"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#E2F3EB"),
                OnPrimaryContainer = Color.Parse("#17382B"),
                Secondary = Color.Parse("#DE9843"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#FDF0DF"),
                OnSecondaryContainer = Color.Parse("#40280D"),
                Surface = Color.Parse("#F7FAF8"),
                SurfaceDim = Color.Parse("#EDF3EF"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#F0F5F2"),
                SurfaceContainer = Color.Parse("#E8F0EC"),
                SurfaceContainerHigh = Color.Parse("#DFEAE5"),
                SurfaceContainerHighest = Color.Parse("#D3E1DA"),
                OnSurface = Color.Parse("#222925"),
                OnSurfaceVariant = Color.Parse("#53605A"),
                Outline = Color.Parse("#889790"),
                OutlineVariant = Color.Parse("#D1DDD7"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 6. Honey & Amber (Light: #CB852A, Dark: #F0BA6A)
        if (ColorMatches(seed, Color.Parse("#CB852A")) || ColorMatches(seed, Color.Parse("#F0BA6A")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#F0BA6A"),
                OnPrimary = Color.Parse("#3F2500"),
                PrimaryContainer = Color.Parse("#5C3908"),
                OnPrimaryContainer = Color.Parse("#FDEDD5"),
                Secondary = Color.Parse("#A5C98E"),
                OnSecondary = Color.Parse("#1D3510"),
                SecondaryContainer = Color.Parse("#2F4A20"),
                OnSecondaryContainer = Color.Parse("#E6F2DE"),
                Surface = Color.Parse("#1B1712"),
                SurfaceDim = Color.Parse("#13100B"),
                SurfaceBright = Color.Parse("#3B3226"),
                SurfaceContainerLowest = Color.Parse("#13100B"),
                SurfaceContainerLow = Color.Parse("#211C16"),
                SurfaceContainer = Color.Parse("#27211B"),
                SurfaceContainerHigh = Color.Parse("#342D24"),
                SurfaceContainerHighest = Color.Parse("#41382E"),
                OnSurface = Color.Parse("#F7F2E9"),
                OnSurfaceVariant = Color.Parse("#CDC2B3"),
                Outline = Color.Parse("#7A6F60"),
                OutlineVariant = Color.Parse("#494034"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#CB852A"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#FBF0DC"),
                OnPrimaryContainer = Color.Parse("#422806"),
                Secondary = Color.Parse("#75995E"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#EBF3E5"),
                OnSecondaryContainer = Color.Parse("#24361A"),
                Surface = Color.Parse("#FDF9F4"),
                SurfaceDim = Color.Parse("#F3ECE0"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#FAF3E9"),
                SurfaceContainer = Color.Parse("#F6EBDE"),
                SurfaceContainerHigh = Color.Parse("#EFE1D0"),
                SurfaceContainerHighest = Color.Parse("#E7D6C0"),
                OnSurface = Color.Parse("#2A251E"),
                OnSurfaceVariant = Color.Parse("#635B4E"),
                Outline = Color.Parse("#9C9281"),
                OutlineVariant = Color.Parse("#E3DACB"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 7. Cloud & Sky (Light: #5B8BE3, Dark: #98B8F6)
        if (ColorMatches(seed, Color.Parse("#5B8BE3")) || ColorMatches(seed, Color.Parse("#98B8F6")) || ColorMatches(seed, Color.Parse("#A0D2EB")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#98B8F6"),
                OnPrimary = Color.Parse("#132B5B"),
                PrimaryContainer = Color.Parse("#223E75"),
                OnPrimaryContainer = Color.Parse("#E3EDFE"),
                Secondary = Color.Parse("#F5A3AA"),
                OnSecondary = Color.Parse("#46171B"),
                SecondaryContainer = Color.Parse("#5D2429"),
                OnSecondaryContainer = Color.Parse("#FDE9EB"),
                Surface = Color.Parse("#131722"),
                SurfaceDim = Color.Parse("#0D101A"),
                SurfaceBright = Color.Parse("#2E374E"),
                SurfaceContainerLowest = Color.Parse("#0D101A"),
                SurfaceContainerLow = Color.Parse("#171C29"),
                SurfaceContainer = Color.Parse("#1D2331"),
                SurfaceContainerHigh = Color.Parse("#272E40"),
                SurfaceContainerHighest = Color.Parse("#333B50"),
                OnSurface = Color.Parse("#EEF3FA"),
                OnSurfaceVariant = Color.Parse("#B7C3D8"),
                Outline = Color.Parse("#65748F"),
                OutlineVariant = Color.Parse("#394357"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#5B8BE3"),
                OnPrimary = Colors.White,
                PrimaryContainer = Color.Parse("#E7EFFD"),
                OnPrimaryContainer = Color.Parse("#17294A"),
                Secondary = Color.Parse("#E57E86"),
                OnSecondary = Colors.White,
                SecondaryContainer = Color.Parse("#FDECEE"),
                OnSecondaryContainer = Color.Parse("#441C20"),
                Surface = Color.Parse("#F8FAFD"),
                SurfaceDim = Color.Parse("#ECF0F7"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#F0F4FA"),
                SurfaceContainer = Color.Parse("#E8EEF7"),
                SurfaceContainerHigh = Color.Parse("#DFE6F2"),
                SurfaceContainerHighest = Color.Parse("#D3DCED"),
                OnSurface = Color.Parse("#22262E"),
                OnSurfaceVariant = Color.Parse("#555C6B"),
                Outline = Color.Parse("#8993A6"),
                OutlineVariant = Color.Parse("#D0D7E5"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        // 8. Golden Parchment (Light: #8A6B24, Dark: #D4AF37 / #E8C85A)
        if (ColorMatches(seed, Color.Parse("#8A6B24")) || ColorMatches(seed, Color.Parse("#D4AF37")) || ColorMatches(seed, Color.Parse("#D5B572")))
        {
            palette = isDark ? new MaterialYouPalette
            {
                Primary = Color.Parse("#E8C85A"),
                OnPrimary = Color.Parse("#252219"),
                PrimaryContainer = Color.Parse("#383523"),
                OnPrimaryContainer = Color.Parse("#FAF6ED"),
                Secondary = Color.Parse("#D4AF37"),
                OnSecondary = Color.Parse("#252219"),
                SecondaryContainer = Color.Parse("#2F2D1F"),
                OnSecondaryContainer = Color.Parse("#FAF6ED"),
                Surface = Color.Parse("#252219"),
                SurfaceDim = Color.Parse("#19180F"),
                SurfaceBright = Color.Parse("#423B2A"),
                SurfaceContainerLowest = Color.Parse("#19180F"),
                SurfaceContainerLow = Color.Parse("#1E1D13"),
                SurfaceContainer = Color.Parse("#262519"),
                SurfaceContainerHigh = Color.Parse("#2E2D1F"),
                SurfaceContainerHighest = Color.Parse("#373525"),
                OnSurface = Color.Parse("#FAF6ED"),
                OnSurfaceVariant = Color.Parse("#C7C0B0"),
                Outline = Color.Parse("#6B654F"),
                OutlineVariant = Color.Parse("#3E3C2F"),
                Danger = Color.Parse("#FFB4AB"),
                Success = Color.Parse("#8CD692"),
                Warning = Color.Parse("#FFBA2F"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #35000000, 0 8 20 0 #20000000")
            } : new MaterialYouPalette
            {
                Primary = Color.Parse("#8A6B24"),
                OnPrimary = Color.Parse("#FFFDF7"),
                PrimaryContainer = Color.Parse("#EFE3C7"),
                OnPrimaryContainer = Color.Parse("#2E2A1A"),
                Secondary = Color.Parse("#9E7E38"),
                OnSecondary = Color.Parse("#FFFDF7"),
                SecondaryContainer = Color.Parse("#E8DCBA"),
                OnSecondaryContainer = Color.Parse("#2E2A1A"),
                Surface = Color.Parse("#FAF6ED"),
                SurfaceDim = Color.Parse("#F2ECE0"),
                SurfaceBright = Colors.White,
                SurfaceContainerLowest = Colors.White,
                SurfaceContainerLow = Color.Parse("#F5EFE0"),
                SurfaceContainer = Color.Parse("#EDE4D1"),
                SurfaceContainerHigh = Color.Parse("#E5D9C2"),
                SurfaceContainerHighest = Color.Parse("#DCCDB0"),
                OnSurface = Color.Parse("#252219"),
                OnSurfaceVariant = Color.Parse("#5A5445"),
                Outline = Color.Parse("#928974"),
                OutlineVariant = Color.Parse("#D8CFBC"),
                Danger = Color.Parse("#BA1A1A"),
                Success = Color.Parse("#1B6D24"),
                Warning = Color.Parse("#825500"),
                CardShadow = BoxShadows.Parse("0 1 3 0 #12000000, 0 4 12 1 #0D000000")
            };
            return true;
        }

        palette = null!;
        return false;
    }


    /// <summary>RGB (0..255) to HSL (H: 0..360, S: 0..1, L: 0..1).</summary>
    public static (double H, double S, double L) RgbToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double h = 0, s = 0, l = (max + min) / 2.0;

        if (Math.Abs(max - min) > 1e-5)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (Math.Abs(max - r) < 1e-5)
                h = (g - b) / d + (g < b ? 6 : 0);
            else if (Math.Abs(max - g) < 1e-5)
                h = (b - r) / d + 2;
            else
                h = (r - g) / d + 4;
            h /= 6.0;
        }
        return (h * 360.0, s, l);
    }

    /// <summary>HSL (H: 0..360, S: 0..1, L: 0..1) to RGB Color.</summary>
    public static Color HslToRgb(double h, double s, double l, byte a = 255)
    {
        h = (h % 360 + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);

        if (s <= 1e-5)
        {
            var v = (byte)Math.Round(l * 255);
            return Color.FromArgb(a, v, v, v);
        }

        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        double hk = h / 360.0;

        byte Component(double tc)
        {
            if (tc < 0) tc += 1;
            if (tc > 1) tc -= 1;
            if (tc < 1.0 / 6.0) return (byte)Math.Round((p + (q - p) * 6.0 * tc) * 255);
            if (tc < 1.0 / 2.0) return (byte)Math.Round(q * 255);
            if (tc < 2.0 / 3.0) return (byte)Math.Round((p + (q - p) * (2.0 / 3.0 - tc) * 6.0) * 255);
            return (byte)Math.Round(p * 255);
        }

        return Color.FromArgb(a, Component(hk + 1.0 / 3.0), Component(hk), Component(hk - 1.0 / 3.0));
    }
}
