// Simple background removal: turns the paper or plain backdrop of signatures, stamps, logos and scans transparent.
// No model and no network: the backdrop colour is estimated region by region, and every pixel is "un-mixed" from it.

using ImageMagick;

namespace Filee.Engines.Magick;

/// <summary>
/// Makes a plain (single-colour) background transparent. Built for signatures, stamps and logos on paper or a flat
/// backdrop; photos of people, hair or busy scenes need a segmentation model and are out of scope.
/// </summary>
/// <remarks>
/// <para>
/// 1. The backdrop colour is the per-channel median of the border pixels, which shrugs off ink touching the edge and
///    JPEG noise. 2. A light backdrop is then estimated again per block, so a shadow or a lamp's falloff across a phone
///    photo of paper is removed as well; a block only counts pixels that are a brighter or darker shade of the border
///    colour, so a pale logo colour is not taken for paper. 3. Each pixel is treated as ink of some colour laid over
///    the backdrop with some opacity (as GIMP's "Color to Alpha"): the opacity is how far the pixel is from the
///    backdrop towards black or white, and the ink colour is recovered from it. Anti-aliased stroke edges therefore
///    stay smooth and lose their white fringe instead of becoming a jagged cut-out. 4. Lone visible pixels (noise,
///    paper grain) are cleared.
/// </para>
/// <para>
/// Optionally only the backdrop reachable from the image edge is removed, so the white inside a logo stays. This is
/// off by default because it would also keep the paper inside the loops of handwriting ("o", "a", "e").
/// </para>
/// </remarks>
public static class BackgroundRemover
{
    /// <summary>Opacity below which a pixel is fully removed at tolerance 100 (it scales linearly from 0).</summary>
    private const float MaxCutoff = 0.48f;

    /// <summary>Narrowest band between fully removed and fully kept, in opacity.</summary>
    private const float MinSoftBand = 0.15f;

    /// <summary>
    /// Smallest headroom between the backdrop and black or white used for the opacity. Without it, noise on nearly
    /// white paper (250 → 255) would count as fully opaque white ink.
    /// </summary>
    private const float MinHeadroom = 96f;

    /// <summary>Removes the background of <paramref name="image"/> in place.</summary>
    /// <param name="tolerance">0-100: how different from the backdrop a pixel may be and still be removed.</param>
    /// <param name="keepEnclosed">Only remove the backdrop that is connected to the image edge.</param>
    public static void Apply(IMagickImage<byte> image, int tolerance, bool keepEnclosed)
    {
        var width = (int)image.Width;
        var height = (int)image.Height;
        if (width < 2 || height < 2)
            return;

        // CMYK and gray sources are read as RGB so the colour arithmetic below holds.
        if (image.ColorSpace is not (ColorSpace.sRGB or ColorSpace.RGB))
            image.ColorSpace = ColorSpace.sRGB;
        var pixels = image.GetPixels().ToByteArray(PixelMapping.RGBA);
        if (pixels is null)
            return;

        if (BorderColor(pixels, width, height) is not { } border)
            return; // the edge is already transparent: nothing to tell the backdrop by

        var cutoff = Math.Clamp(tolerance, 0, 100) / 100f * MaxCutoff;
        var backdrop = new Backdrop(pixels, width, height, border, tolerance / 100f);

        var count = width * height;
        var raw = new float[count]; // opacity before the thresholds, also used by the edge flood fill
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var o = (y * width + x) * 4;
                var (br, bg, bb) = backdrop.At(x, y);
                raw[y * width + x] = Math.Max(Opacity(pixels[o], br), Math.Max(Opacity(pixels[o + 1], bg), Opacity(pixels[o + 2], bb)));
            }
        }
        var full = InkOpacity(raw, cutoff);

        var result = new byte[pixels.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                var o = i * 4;
                var alpha = Math.Clamp((raw[i] - cutoff) / (full - cutoff), 0f, 1f);
                if (alpha > 0)
                {
                    // Un-mix: pixel = alpha × ink + (1 − alpha) × backdrop. Laid back over the backdrop, the result
                    // looks like the original wherever alpha is at least the raw opacity.
                    var (br, bg, bb) = backdrop.At(x, y);
                    result[o] = Unmix(pixels[o], br, alpha);
                    result[o + 1] = Unmix(pixels[o + 1], bg, alpha);
                    result[o + 2] = Unmix(pixels[o + 2], bb, alpha);
                }
                result[o + 3] = (byte)Math.Round(alpha * pixels[o + 3]);
            }
        }

        RemoveSpecks(result, width, height);
        if (keepEnclosed)
            RestoreEnclosed(pixels, result, raw, width, height, (cutoff + full) / 2);

        image.Alpha(AlphaOption.Set);
        image.ImportPixels(result, new PixelImportSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
    }

    /// <summary>
    /// The raw opacity that counts as fully opaque: that of the ink itself (75th percentile of the clearly visible
    /// pixels, so the core of a stroke is solid and only its edges are not). Black ink reaches about 0.9 and its
    /// anti-aliased edges keep their partial alpha; faint pencil reaches only about 0.4, and its strokes still become
    /// solid instead of half transparent.
    /// </summary>
    private static float InkOpacity(float[] raw, float cutoff)
    {
        var floor = Math.Min(1f, cutoff + MinSoftBand);
        var histogram = new int[256];
        var total = 0;
        foreach (var a in raw)
        {
            if (a < floor)
                continue;
            histogram[(int)(Math.Min(a, 1f) * 255)]++;
            total++;
        }
        if (total == 0)
            return floor;
        var seen = 0;
        for (var bin = 0; bin < histogram.Length; bin++)
        {
            seen += histogram[bin];
            if (seen * 4 >= total * 3)
                return Math.Max(floor, bin / 255f);
        }
        return 1f;
    }

    /// <summary>How opaque ink of channel value <paramref name="value"/> over <paramref name="backdrop"/> must at least be.</summary>
    private static float Opacity(byte value, float backdrop)
    {
        var d = value - backdrop;
        return d >= 0 ? d / Math.Max(255f - backdrop, MinHeadroom) : -d / Math.Max(backdrop, MinHeadroom);
    }

    private static byte Unmix(byte value, float backdrop, float alpha) =>
        (byte)Math.Clamp(Math.Round(backdrop + (value - backdrop) / alpha), 0, 255);

    /// <summary>Per-channel median of the opaque border pixels, or null when the border is transparent.</summary>
    private static (float R, float G, float B)? BorderColor(byte[] pixels, int width, int height)
    {
        var r = new List<byte>();
        var g = new List<byte>();
        var b = new List<byte>();
        void Add(int x, int y)
        {
            var o = (y * width + x) * 4;
            if (pixels[o + 3] < 128)
                return;
            r.Add(pixels[o]);
            g.Add(pixels[o + 1]);
            b.Add(pixels[o + 2]);
        }
        for (var x = 0; x < width; x++)
        {
            Add(x, 0);
            Add(x, height - 1);
        }
        for (var y = 1; y < height - 1; y++)
        {
            Add(0, y);
            Add(width - 1, y);
        }
        return r.Count == 0 ? null : (Median(r), Median(g), Median(b));
    }

    private static float Median(List<byte> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    /// <summary>
    /// Clears lone visible pixels (sensor noise, paper grain, dust) that have at most one visible neighbour. Strokes
    /// are at least a line of pixels, so every pixel of one has two neighbours or more.
    /// </summary>
    private static void RemoveSpecks(byte[] result, int width, int height)
    {
        var specks = new List<int>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (result[(y * width + x) * 4 + 3] == 0)
                    continue;
                var neighbours = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx != 0 || dy != 0) && nx >= 0 && nx < width && ny >= 0 && ny < height
                            && result[(ny * width + nx) * 4 + 3] != 0)
                            neighbours++;
                    }
                }
                if (neighbours <= 1)
                    specks.Add(y * width + x);
            }
        }
        foreach (var i in specks)
            result[i * 4 + 3] = 0;
    }

    /// <summary>
    /// Gives the pixels inside closed shapes back their original colour: removed pixels count only when the removed
    /// area they belong to touches the image edge (4-connected flood fill that stops at pixels at least half opaque).
    /// </summary>
    private static void RestoreEnclosed(byte[] source, byte[] result, float[] raw, int width, int height, float wall)
    {
        var reached = new bool[raw.Length];
        var queue = new Queue<int>();
        void Visit(int i)
        {
            if (!reached[i] && raw[i] < wall)
            {
                reached[i] = true;
                queue.Enqueue(i);
            }
        }
        for (var x = 0; x < width; x++)
        {
            Visit(x);
            Visit((height - 1) * width + x);
        }
        for (var y = 0; y < height; y++)
        {
            Visit(y * width);
            Visit(y * width + width - 1);
        }
        while (queue.TryDequeue(out var i))
        {
            var x = i % width;
            if (x > 0) Visit(i - 1);
            if (x < width - 1) Visit(i + 1);
            if (i >= width) Visit(i - width);
            if (i < raw.Length - width) Visit(i + width);
        }
        for (var i = 0; i < raw.Length; i++)
        {
            if (!reached[i])
                Array.Copy(source, i * 4, result, i * 4, 4);
        }
    }

    /// <summary>
    /// The backdrop colour at each pixel: the border colour, corrected block by block for uneven lighting when the
    /// backdrop is light (paper). Blocks with too little backdrop (dense ink) borrow from their neighbours.
    /// </summary>
    private sealed class Backdrop
    {
        /// <summary>Blocks along the longer side.</summary>
        private const int Blocks = 48;

        private readonly (float R, float G, float B) _border;
        private readonly (float R, float G, float B)[]? _grid;
        private readonly int _block, _columns, _rows;
        private readonly float _darkest;

        public Backdrop(byte[] pixels, int width, int height, (float R, float G, float B) border, float tolerance)
        {
            _border = border;
            // Dark or coloured backdrops are rarely lit unevenly, and "a shade of" a near-black colour is mostly noise.
            if (Luma(border) < 96)
                return;
            _block = Math.Max(8, (int)Math.Ceiling(Math.Max(width, height) / (double)Blocks));
            _columns = (width + _block - 1) / _block;
            _rows = (height + _block - 1) / _block;
            if (_columns < 2 && _rows < 2)
                return;
            // Higher tolerance accepts deeper shadows; at 0 a light grey area (a smudge, a pale logo) is not paper.
            _darkest = 0.85f - 0.35f * Math.Clamp(tolerance, 0, 1);
            _grid = Estimate(pixels, width, height);
        }

        public (float R, float G, float B) At(int x, int y)
        {
            if (_grid is null)
                return _border;
            // Bilinear between block centres.
            var fx = Math.Clamp((x + 0.5f) / _block - 0.5f, 0, _columns - 1);
            var fy = Math.Clamp((y + 0.5f) / _block - 0.5f, 0, _rows - 1);
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Math.Min(x0 + 1, _columns - 1), y1 = Math.Min(y0 + 1, _rows - 1);
            float tx = fx - x0, ty = fy - y0;
            var a = _grid[y0 * _columns + x0];
            var b = _grid[y0 * _columns + x1];
            var c = _grid[y1 * _columns + x0];
            var d = _grid[y1 * _columns + x1];
            float Mix(float p, float q, float s, float t) =>
                (p * (1 - tx) + q * tx) * (1 - ty) + (s * (1 - tx) + t * tx) * ty;
            return (Mix(a.R, b.R, c.R, d.R), Mix(a.G, b.G, c.G, d.G), Mix(a.B, b.B, c.B, d.B));
        }

        private static float Luma((float R, float G, float B) c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

        private (float R, float G, float B)[] Estimate(byte[] pixels, int width, int height)
        {
            var grid = new (float R, float G, float B)?[_columns * _rows];
            var dot = _border.R * _border.R + _border.G * _border.G + _border.B * _border.B;
            var r = new List<byte>();
            var g = new List<byte>();
            var b = new List<byte>();
            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _columns; column++)
                {
                    r.Clear();
                    g.Clear();
                    b.Clear();
                    var total = 0;
                    var yEnd = Math.Min(height, (row + 1) * _block);
                    var xEnd = Math.Min(width, (column + 1) * _block);
                    for (var y = row * _block; y < yEnd; y++)
                    {
                        for (var x = column * _block; x < xEnd; x++)
                        {
                            total++;
                            var o = (y * width + x) * 4;
                            if (pixels[o + 3] < 128)
                                continue;
                            float pr = pixels[o], pg = pixels[o + 1], pb = pixels[o + 2];
                            // A shade of the border colour: k × border, with k from a shadow to a highlight. The border
                            // can itself lie in shadow, so the centre of the sheet may be much brighter than its median.
                            var k = (pr * _border.R + pg * _border.G + pb * _border.B) / dot;
                            if (k < _darkest || k > 1.6f)
                                continue;
                            var off = Math.Max(Math.Abs(pr - k * _border.R), Math.Max(Math.Abs(pg - k * _border.G), Math.Abs(pb - k * _border.B)));
                            if (off > 20)
                                continue;
                            r.Add(pixels[o]);
                            g.Add(pixels[o + 1]);
                            b.Add(pixels[o + 2]);
                        }
                    }
                    // Mostly ink: the median would drift towards the ink, so let the neighbours decide.
                    if (r.Count * 5 >= total * 2)
                        grid[row * _columns + column] = (Median(r), Median(g), Median(b));
                }
            }
            return Smooth(Fill(grid));
        }

        /// <summary>Gives blocks without an estimate the average of their estimated neighbours, ring by ring.</summary>
        private (float R, float G, float B)[] Fill((float R, float G, float B)?[] grid)
        {
            if (grid.All(c => c is null))
                return Enumerable.Repeat(_border, grid.Length).ToArray();
            while (grid.Any(c => c is null))
            {
                var next = ((float R, float G, float B)?[])grid.Clone();
                for (var i = 0; i < grid.Length; i++)
                {
                    if (grid[i] is not null)
                        continue;
                    var known = Neighbours(i).Select(n => grid[n]).OfType<(float R, float G, float B)>().ToList();
                    if (known.Count > 0)
                        next[i] = (known.Average(c => c.R), known.Average(c => c.G), known.Average(c => c.B));
                }
                grid = next;
            }
            return grid.Select(c => c!.Value).ToArray();
        }

        /// <summary>One 3 × 3 box blur, so a single odd block does not leave a visible patch.</summary>
        private (float R, float G, float B)[] Smooth((float R, float G, float B)[] grid) =>
            Enumerable.Range(0, grid.Length).Select(i =>
            {
                var all = Neighbours(i).Append(i).Select(n => grid[n]).ToList();
                return (all.Average(c => c.R), all.Average(c => c.G), all.Average(c => c.B));
            }).ToArray();

        private IEnumerable<int> Neighbours(int i)
        {
            int column = i % _columns, row = i / _columns;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    int c = column + dx, r = row + dy;
                    if ((dx != 0 || dy != 0) && c >= 0 && c < _columns && r >= 0 && r < _rows)
                        yield return r * _columns + c;
                }
            }
        }
    }
}
