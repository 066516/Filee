using System.IO.Compression;
using System.Text;
using Filee.Engines.Office;

namespace Filee.Engines.Tests;

public class DocumentFontsTests
{
    [Fact]
    public void Finds_fonts_used_by_ooxml_and_hwpx_but_not_embedded_or_theme_placeholders()
    {
        var path = Package(new Dictionary<string, string>
        {
            ["word/document.xml"] = "<root><rFonts ascii=\"Calibri\" hAnsi=\"Absent Serif\" eastAsia=\"+mj-ea\"/></root>",
            ["word/fontTable.xml"] = "<root><font name=\"Embedded Face\"><embedRegular/></font></root>",
            ["word/styles.xml"] = "<root><rFonts ascii=\"Embedded Face\"/></root>",
            ["xl/styles.xml"] = "<root><font><name val=\"Sheet Face\"/></font></root>",
            ["ppt/slides/slide1.xml"] = "<root><latin typeface=\"Slide Face\"/><ea typeface=\"\"/></root>",
            ["Contents/header.xml"] = "<root><font face=\"HWPX Face\"/></root>",
        });
        try
        {
            Assert.Equal(["Absent Serif", "Calibri", "HWPX Face", "Sheet Face", "Slide Face"], DocumentFonts.Referenced(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Installed_metric_compatible_families_suppress_the_warning()
    {
        var path = Package(new Dictionary<string, string>
        {
            ["word/document.xml"] = "<root><rFonts ascii=\"Calibri\" hAnsi=\"Times New Roman\" cs=\"Unknown Face\"/></root>",
        });
        try
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carlito", "Liberation Serif" };
            Assert.Equal(["Unknown Face"], DocumentFonts.Missing(path, installed.Contains));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_non_package_never_prevents_conversion()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not a package");
            Assert.Empty(DocumentFonts.Missing(path, _ => false));
        }
        finally { File.Delete(path); }
    }

    private static string Package(IReadOnlyDictionary<string, string> entries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"filee-document-fonts-{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, xml) in entries)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(Encoding.UTF8.GetBytes(xml));
        }
        return path;
    }
}
