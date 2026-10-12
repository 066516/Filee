// Fonts rhwp cannot put into a PDF. rhwp uses the fonts installed on the computer; a variable font with CFF2 outlines
// (Source Han Serif VF, "思源宋体 VF" in Chinese Word documents) fails its font subsetter, and the page came out blank
// (issue #61). Such a font is swapped for an installed font of the same script and style before rhwp runs. Nothing is
// downloaded or shipped: the replacement is always a font the computer already has.

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Xml.Linq;
using SkiaSharp;

namespace Filee.Engines.Hwp;

internal static class RhwpFonts
{
    private static readonly string[] Styles =
        ["Thin", "ExtraLight", "Light", "Normal", "Regular", "Medium", "SemiBold", "Bold", "Heavy", "Black", "ExtraBold"];

    /// <summary>Installed fonts in order of preference, by script, serif first then sans.</summary>
    private static readonly Dictionary<string, (string[] Serif, string[] Sans)> Candidates = new()
    {
        ["zh-Hans"] = (["SimSun", "NSimSun", "Noto Serif SC", "Songti SC", "Noto Serif CJK SC"],
            ["Microsoft YaHei", "DengXian", "Noto Sans SC", "PingFang SC", "Noto Sans CJK SC", "SimHei"]),
        ["zh-Hant"] = (["PMingLiU", "MingLiU", "Noto Serif TC", "Songti TC", "Noto Serif CJK TC"],
            ["Microsoft JhengHei", "Noto Sans TC", "PingFang TC", "Noto Sans CJK TC"]),
        ["ja"] = (["Yu Mincho", "MS Mincho", "Noto Serif JP", "Hiragino Mincho ProN", "Noto Serif CJK JP"],
            ["Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans JP", "Hiragino Sans", "Noto Sans CJK JP"]),
        ["ko"] = (["Batang", "HCR Batang", "Noto Serif KR", "AppleMyungjo", "Noto Serif CJK KR"],
            ["Malgun Gothic", "Noto Sans KR", "Apple SD Gothic Neo", "NanumGothic", "Noto Sans CJK KR"]),
        [""] = (["Times New Roman", "Georgia", "Noto Serif", "DejaVu Serif", "Liberation Serif"],
            ["Arial", "Segoe UI", "Noto Sans", "Helvetica", "DejaVu Sans", "Liberation Sans"]),
    };

    private static readonly ConcurrentDictionary<string, bool?> Usable = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// For each face that is installed but unusable, an installed replacement. Faces that are fine, or not installed
    /// (rhwp falls back on its own), are left out.
    /// </summary>
    /// <param name="usable">True when rhwp can use the font, false when it can't, null when it is not installed.</param>
    public static Dictionary<string, string> Substitutions(IEnumerable<string> faces, Func<string, bool?>? usable = null)
    {
        usable ??= IsUsable;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var face in faces.Distinct(StringComparer.Ordinal))
        {
            if (face.Length == 0 || usable(face) != false)
                continue;
            var (serif, sans) = Candidates[Script(face)];
            var order = IsSerif(face) ? serif.Concat(sans) : sans.Concat(serif);
            if (order.FirstOrDefault(c => usable(c) == true) is { } replacement)
                result[face] = replacement;
        }
        return result;
    }

    /// <summary>
    /// A copy of the HWPX in <paramref name="workDirectory"/> with unusable fonts replaced, or null when every font is
    /// fine (or the file is not an HWPX package).
    /// </summary>
    public static string? Prepare(string hwpx, string workDirectory, Func<string, bool?>? usable = null)
    {
        try
        {
            XDocument header;
            using (var zip = ZipFile.OpenRead(hwpx))
            {
                if (zip.GetEntry("Contents/header.xml") is not { } entry)
                    return null;
                using var stream = entry.Open();
                header = XDocument.Load(stream);
            }
            var fonts = header.Descendants().Where(e => e.Name.LocalName == "font" && e.Attribute("face") is not null).ToList();
            var substitutions = Substitutions(fonts.Select(f => (string)f.Attribute("face")!), usable);
            if (substitutions.Count == 0)
                return null;
            foreach (var font in fonts)
                if (substitutions.TryGetValue((string)font.Attribute("face")!, out var replacement))
                    font.SetAttributeValue("face", replacement);

            var copy = Path.Combine(workDirectory, $"rhwp-fonts-{Guid.NewGuid():N}.hwpx");
            File.Copy(hwpx, copy);
            using (var zip = ZipFile.Open(copy, ZipArchiveMode.Update))
            {
                zip.GetEntry("Contents/header.xml")!.Delete();
                using var stream = zip.CreateEntry("Contents/header.xml", CompressionLevel.Optimal).Open();
                header.Save(stream, SaveOptions.DisableFormatting);
            }
            return copy;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            return null; // not a package Filee can read: rhwp reports the problem itself
        }
    }

    /// <summary>
    /// Whether an installed font can be embedded by rhwp: false for variable fonts with CFF2 outlines, null when no
    /// installed font has that name. "思源宋体 VF Medium" names a style of "思源宋体 VF", so trailing style words are tried off too.
    /// </summary>
    internal static bool? IsUsable(string face) => Usable.GetOrAdd(face, static name =>
    {
        foreach (var candidate in Names(name))
        {
            using var typeface = SKFontManager.Default.MatchFamily(candidate);
            // Windows matches localized names ("思源宋体 VF") and returns nothing for unknown ones; fontconfig always
            // returns some font, so elsewhere only a font with the requested name counts.
            if (typeface is not null && (OperatingSystem.IsWindows() || string.Equals(typeface.FamilyName, candidate, StringComparison.OrdinalIgnoreCase)))
                return IsUsable(typeface);
        }
        return null;
    });

    /// <summary>False when the font has CFF2 outlines (variable OpenType fonts made from PostScript outlines).</summary>
    internal static bool IsUsable(SKTypeface typeface) => !typeface.GetTableTags().Contains(Tag("CFF2"));

    private static IEnumerable<string> Names(string face)
    {
        yield return face;
        var words = face.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (words.Count > 1 && Styles.Contains(words[^1], StringComparer.OrdinalIgnoreCase))
        {
            words.RemoveAt(words.Count - 1);
            yield return string.Join(' ', words);
        }
    }

    /// <summary>The script a font name points to: "思源宋体" and "SC" are Simplified Chinese, "명조" Korean, ….</summary>
    internal static string Script(string face)
    {
        bool Has(params string[] parts) => parts.Any(p => face.Contains(p, StringComparison.OrdinalIgnoreCase));
        bool Word(params string[] words) => face.Split(' ', '-', '_').Any(w => words.Contains(w, StringComparer.OrdinalIgnoreCase));
        if (Word("SC", "CN", "GB") || Has("简", "宋体", "黑体", "仿宋", "楷体"))
            return "zh-Hans";
        if (Word("TC", "HK", "TW") || Has("繁", "明體", "黑體", "宋體"))
            return "zh-Hant";
        if (Word("JP", "J") || Has("明朝", "ゴシック", "游"))
            return "ja";
        if (Word("KR", "K") || face.Any(c => c is >= '가' and <= '힣'))
            return "ko";
        if (face.Any(c => c is >= '一' and <= '鿿'))
            return "zh-Hans";
        return "";
    }

    /// <summary>Serif (Song/Ming/Mincho/Batang) rather than sans (Hei/Gothic/Dotum).</summary>
    internal static bool IsSerif(string face) =>
        new[] { "Serif", "Song", "Ming", "Mincho", "Myeongjo", "Myungjo", "Batang", "Times", "宋", "明", "바탕", "명조", "仿宋", "楷" }
            .Any(p => face.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static uint Tag(string tag) => (uint)tag[0] << 24 | (uint)tag[1] << 16 | (uint)tag[2] << 8 | tag[3];
}
