// What Filee changes in an HWPX before rhwp renders it to PDF, in a copy (the user's file is never touched):
//
// - Fonts (RhwpFonts): installed fonts for the text's language and characters, no CFF2 variable fonts (#61, #64).
// - Shape sizes: a box inside a text box stores its size relative to the column (hp:sz widthRelTo="COLUMN", 9920 =
//   99.2 %); rhwp takes the number as absolute there and drew the exam paper's "보기" boxes 35 mm wide with their
//   pictures shrunk to a few pixels (#65). The size 한글 laid the object out at (hp:curSz) is written instead.
// - Equation scripts: 한글 2022 saves a command glued to the next letter ("f CIRCf", "A SMALLINTERB", "lek", "rootx")
//   and writes != and +- for ≠ and ±. rhwp printed those as italic text ("fCIRCf"); they are spaced and spelled out.
//
// rhwp's equation fonts are Latin Modern Math, STIX Two and Times New Roman; without the first two or Arial Unicode MS
// (which Office installs) ∪ ∈ ⊂ ∘ ∼ came out empty, so Cambria Math (part of Windows) is asked for then.

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Filee.Engines.Hwp;

internal static partial class RhwpInput
{
    /// <summary>Equation commands 한글 separates from a following identifier (found in 한글 2022 documents).</summary>
    private static readonly string[] UpperCommands =
    [
        "CIRC", "SMALLINTER", "SMALLUNION", "SUBSET", "SUPERSET", "IN", "NOTIN", "OWNS", "INTER", "UNION", "APPROX",
        "EQUIV", "CONG", "DIV", "PLUSMINUS", "MINUSPLUS", "CDOT", "BULLET", "OPLUS", "FORALL", "EXIST", "THEREFORE",
        "BECAUSE", "DEG", "INF", "PARTIAL", "RARROW", "LARROW", "LRARROW", "EMPTYSET", "PROPTO",
    ];

    /// <summary>Lowercase commands: only split before one or two letters or digits ("lek", "le28", "rootxy").</summary>
    private static readonly string[] LowerCommands =
        ["le", "ge", "root", "sqrt", "circ", "subset", "superset", "in", "notin", "approx", "equiv", "div", "pm", "mp", "cdot"];

    /// <summary>Longer commands that start like one of the above and stay whole.</summary>
    private static readonly HashSet<string> Whole = new(StringComparer.Ordinal)
    {
        "left", "leq", "geq", "int", "inf", "inter", "cdots", "CDOTS", "SUBSETEQ", "SUPSETEQ", "INTER", "INF", "NOTIN",
        "notin", "pmatrix", "LEFT", "RIGHT", "LEQ", "GEQ", "INT", "INFTY", "infty", "DIVIDE",
    };

    private static readonly (string Text, string Command)[] Operators =
        [("!=", " NEQ "), ("<=", " LEQ "), (">=", " GEQ "), ("+-", " PLUSMINUS "), ("-+", " MINUSPLUS ")];

    private static readonly string[] SizedObjects =
        ["rect", "ellipse", "arc", "polygon", "curve", "line", "connectLine", "container", "pic", "ole", "textart"];

    /// <summary>
    /// A copy of the HWPX in <paramref name="workDirectory"/> with the changes above, or null when nothing needs
    /// changing (or the file is not an HWPX package Filee can read).
    /// </summary>
    /// <param name="lookup">Installed fonts by name (tests pass a fake computer).</param>
    public static string? Prepare(string hwpx, string workDirectory, Func<string, InstalledFont?>? lookup = null)
    {
        try
        {
            var changed = new Dictionary<string, XDocument>(StringComparer.Ordinal);
            XDocument? header = null;
            var sections = new List<(string Name, XDocument Xml)>();
            using (var zip = ZipFile.OpenRead(hwpx))
            {
                if (zip.GetEntry("Contents/header.xml") is not { } entry)
                    return null;
                header = Load(entry);
                foreach (var section in zip.Entries.Where(e => e.FullName.StartsWith("Contents/section", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
                    sections.Add((section.FullName, Load(section)));
            }

            var text = new StringBuilder();
            foreach (var (_, xml) in sections)
                foreach (var t in xml.Descendants().Where(e => e.Name.LocalName == "t"))
                    if (text.Length < 200_000)
                        text.Append(t.Value);
            if (ApplyFonts(header, text.ToString(), lookup ?? RhwpFonts.Find))
                changed["Contents/header.xml"] = header;

            foreach (var (name, xml) in sections)
                if (FixShapeSizes(xml) | NormaliseEquations(xml))
                    changed[name] = xml;
            if (changed.Count == 0)
                return null;

            var copy = Path.Combine(workDirectory, $"rhwp-{Guid.NewGuid():N}.hwpx");
            File.Copy(hwpx, copy);
            using (var zip = ZipFile.Open(copy, ZipArchiveMode.Update))
                foreach (var (name, xml) in changed)
                {
                    zip.GetEntry(name)!.Delete();
                    using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                    xml.Save(stream, SaveOptions.DisableFormatting);
                }
            return copy;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            return null; // not a package Filee can read: rhwp reports the problem itself
        }
    }

    /// <summary>
    /// Options for <c>rhwp export-pdf</c>: Cambria Math for equations when none of the fonts rhwp draws their symbols
    /// with is installed.
    /// </summary>
    public static IReadOnlyList<string> PdfOptions(Func<string, InstalledFont?>? lookup = null)
    {
        lookup ??= RhwpFonts.Find;
        var hasSymbols = new[] { "Latin Modern Math", "STIX Two Math", "Arial Unicode MS" }.Any(f => lookup(f) is { Usable: true });
        return !hasSymbols && lookup("Cambria Math") is { Usable: true } ? ["--equation-font", "Cambria Math"] : [];
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace); // spaces in hp:t are text
    }

    private static bool ApplyFonts(XDocument header, string text, Func<string, InstalledFont?> lookup)
    {
        var fonts = header.Descendants()
            .Where(e => e.Name.LocalName == "font" && e.Attribute("face") is not null && e.Parent?.Name.LocalName == "fontface")
            .Select(e => (Element: e, Slot: (string?)e.Parent!.Attribute("lang") ?? "", Face: (string)e.Attribute("face")!))
            .ToList();
        var plan = RhwpFonts.Plan(fonts.Select(f => (f.Slot, f.Face)).ToList(), text, lookup);
        foreach (var font in fonts)
            if (plan.TryGetValue((font.Slot, font.Face), out var replacement))
                font.Element.SetAttributeValue("face", replacement);
        return plan.Count > 0;
    }

    /// <summary>
    /// Writes the laid-out size (hp:curSz) of objects sized relative to the column, paragraph or page as their absolute
    /// size. True when something changed.
    /// </summary>
    internal static bool FixShapeSizes(XDocument section)
    {
        var changed = false;
        foreach (var shape in section.Descendants().Where(e => SizedObjects.Contains(e.Name.LocalName)))
        {
            var size = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "sz");
            var current = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "curSz");
            if (size is null || current is null)
                continue;
            var relative = (string?)size.Attribute("widthRelTo") is { } w && w != "ABSOLUTE"
                           || (string?)size.Attribute("heightRelTo") is { } h && h != "ABSOLUTE";
            if (!relative || !int.TryParse((string?)current.Attribute("width"), out var width) || width <= 0
                || !int.TryParse((string?)current.Attribute("height"), out var height) || height <= 0)
                continue;
            size.SetAttributeValue("width", width);
            size.SetAttributeValue("height", height);
            size.SetAttributeValue("widthRelTo", "ABSOLUTE");
            size.SetAttributeValue("heightRelTo", "ABSOLUTE");
            changed = true;
        }
        return changed;
    }

    private static bool NormaliseEquations(XDocument section)
    {
        var changed = false;
        foreach (var script in section.Descendants().Where(e => e.Name.LocalName == "script" && e.Parent?.Name.LocalName == "equation"))
        {
            var normalised = NormaliseEquation(script.Value);
            if (normalised != script.Value)
            {
                script.Value = normalised;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// The script as rhwp reads it: commands glued to the next identifier spaced ("f CIRCf" → "f CIRC f"), and !=, +-
    /// written as NEQ, PLUSMINUS. Quoted text stays as it is.
    /// </summary>
    internal static string NormaliseEquation(string script)
    {
        var parts = script.Split('"');
        for (var i = 0; i < parts.Length; i += 2) // odd parts are inside quotes
        {
            var part = Identifier().Replace(parts[i], m => Split(m.Value));
            foreach (var (text, command) in Operators)
                part = part.Replace(text, command, StringComparison.Ordinal);
            parts[i] = part;
        }
        return string.Join('"', parts);
    }

    private static string Split(string identifier)
    {
        var words = new List<string>();
        while (true)
        {
            if (Whole.Contains(identifier))
            {
                words.Add(identifier);
                break;
            }
            var command = UpperCommands.Where(c => identifier.StartsWith(c, StringComparison.Ordinal)).MaxBy(c => c.Length);
            if (command is null)
            {
                var lower = LowerCommands.Where(c => identifier.StartsWith(c, StringComparison.Ordinal)).MaxBy(c => c.Length);
                var rest = lower is null ? "" : identifier[lower.Length..];
                command = rest.Length > 0 && (rest.Length <= 2 || rest.All(char.IsAsciiDigit)) ? lower : null;
            }
            if (command is null || command.Length == identifier.Length || Whole.Any(w => identifier.StartsWith(w, StringComparison.Ordinal) && w.Length > command.Length))
            {
                words.Add(identifier);
                break;
            }
            words.Add(command);
            identifier = identifier[command.Length..];
        }
        return string.Join(' ', words);
    }

    [GeneratedRegex("[A-Za-z][A-Za-z0-9]*")]
    private static partial Regex Identifier();
}
