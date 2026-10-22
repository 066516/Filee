// Font preflight for ZIP-based office documents. Renderers otherwise substitute absent families silently, which can
// change wrapping, pagination and table geometry. The scan is deliberately read-only and bounded; a malformed or
// unusually large XML part never prevents conversion.

using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Filee.Engines.Hwp;

namespace Filee.Engines.Office;

internal static class DocumentFonts
{
    private const long MaxPartBytes = 16 * 1024 * 1024;
    private const long MaxTotalBytes = 64 * 1024 * 1024;

    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif", "sans-serif", "sans serif", "monospace", "cursive", "fantasy", "system-ui", "emoji", "math",
    };

    /// <summary>Substitutes designed to preserve the original family's metrics closely enough not to warn.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> MetricAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial"] = ["Liberation Sans"],
            ["Calibri"] = ["Carlito"],
            ["Calibri Light"] = ["Carlito"],
            ["Cambria"] = ["Caladea"],
            ["Courier New"] = ["Liberation Mono"],
            ["Times New Roman"] = ["Liberation Serif"],
        };

    public static IReadOnlyList<string> Missing(string path) => Missing(path, Installed);

    internal static IReadOnlyList<string> Missing(string path, Func<string, bool> installed)
    {
        try
        {
            var referenced = Referenced(path);
            return referenced.Where(face => !installed(face)
                    && (!MetricAliases.TryGetValue(face, out var aliases) || !aliases.Any(installed)))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or XmlException)
        {
            return [];
        }
    }

    private static bool Installed(string face) => RhwpFonts.Find(face) is { Usable: true };

    internal static IReadOnlyList<string> Referenced(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var embedded = EmbeddedFonts(zip);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries.Where(IsDocumentXml))
        {
            if (entry.Length > MaxPartBytes || (total += entry.Length) > MaxTotalBytes)
                continue;
            using var stream = entry.Open();
            var document = XDocument.Load(XmlReader.Create(stream, SafeXml()), LoadOptions.None);
            foreach (var element in document.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    // WordprocessingML run fonts.
                    case "rFonts":
                        AddAttributes(element, result, "ascii", "hAnsi", "eastAsia", "cs");
                        break;
                    // DrawingML fonts used by PowerPoint and by drawing/chart parts in every OOXML format.
                    case "latin" or "ea" or "cs":
                        Add(element.Attribute("typeface")?.Value, result);
                        break;
                    // SpreadsheetML: <font><name val="Aptos"/></font>.
                    case "name" when element.Parent?.Name.LocalName == "font":
                        Add(element.Attribute("val")?.Value, result);
                        break;
                    // HWPX header fonts: <hh:font face="맑은 고딕" .../>.
                    case "font" when element.Attribute("face") is { } face:
                        Add(face.Value, result);
                        break;
                }
            }
        }
        result.ExceptWith(embedded);
        return result.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static HashSet<string> EmbeddedFonts(ZipArchive zip)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entry = zip.GetEntry("word/fontTable.xml");
        if (entry is null || entry.Length > MaxPartBytes)
            return result;
        using var stream = entry.Open();
        var document = XDocument.Load(XmlReader.Create(stream, SafeXml()), LoadOptions.None);
        foreach (var font in document.Descendants().Where(e => e.Name.LocalName == "font"
                     && e.Descendants().Any(d => d.Name.LocalName.StartsWith("embed", StringComparison.OrdinalIgnoreCase))))
            Add(font.Attributes().FirstOrDefault(a => a.Name.LocalName == "name")?.Value, result);
        return result;
    }

    private static bool IsDocumentXml(ZipArchiveEntry entry) => entry.Length > 0
        && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        && (entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
            || entry.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            || entry.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase)
            || entry.FullName.StartsWith("Contents/", StringComparison.OrdinalIgnoreCase));

    private static XmlReaderSettings SafeXml() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxPartBytes,
    };

    private static void AddAttributes(XElement element, HashSet<string> result, params string[] names)
    {
        foreach (var name in names)
            Add(element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value, result);
    }

    private static void Add(string? face, HashSet<string> result)
    {
        face = face?.Trim().Trim('\'', '"');
        if (string.IsNullOrWhiteSpace(face) || face.StartsWith('+') || Generic.Contains(face))
            return;
        result.Add(face);
    }
}
