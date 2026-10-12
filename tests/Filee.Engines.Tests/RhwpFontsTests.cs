// Fonts rhwp cannot embed (variable fonts with CFF2 outlines) are swapped for installed ones before HWPX → PDF.

using System.IO.Compression;
using System.Xml.Linq;
using Filee.Engines.Hwp;

namespace Filee.Engines.Tests;

public class RhwpFontsTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    // What the fake computer has: true = usable, false = installed but a CFF2 variable font, missing = not installed.
    private static readonly Dictionary<string, bool> Installed = new()
    {
        ["思源宋体 VF Medium"] = false,
        ["Source Han Sans SC VF"] = false,
        ["Some VF"] = false,
        ["SimSun"] = true,
        ["Microsoft YaHei"] = true,
        ["Malgun Gothic"] = true,
        ["Arial"] = true,
    };

    private static bool? Usable(string face) => Installed.TryGetValue(face, out var usable) ? usable : null;

    [Fact]
    public void Unusable_fonts_get_an_installed_font_of_the_same_script_and_style()
    {
        // Issue #61: a Chinese Word document in 思源宋体 VF (Source Han Serif) came out as blank pages.
        var map = RhwpFonts.Substitutions(["思源宋体 VF Medium", "Source Han Sans SC VF", "Some VF", "SimSun", "Not Installed"], Usable);

        Assert.Equal("SimSun", map["思源宋体 VF Medium"]);       // Simplified Chinese serif
        Assert.Equal("Microsoft YaHei", map["Source Han Sans SC VF"]); // Simplified Chinese sans
        Assert.Equal("Arial", map["Some VF"]);                   // no script in the name: Latin sans
        Assert.Equal(3, map.Count);                              // usable and missing fonts stay (rhwp falls back itself)
    }

    [Theory]
    [InlineData("思源宋体 VF Medium", "zh-Hans", true)]
    [InlineData("Source Han Sans SC VF", "zh-Hans", false)]
    [InlineData("Noto Serif TC", "zh-Hant", true)]
    [InlineData("Source Han Serif K VF", "ko", true)]
    [InlineData("함초롬돋움", "ko", false)]
    [InlineData("Source Han Sans JP", "ja", false)]
    [InlineData("Inter", "", false)]
    public void Script_and_style_come_from_the_font_name(string face, string script, bool serif)
    {
        Assert.Equal(script, RhwpFonts.Script(face));
        Assert.Equal(serif, RhwpFonts.IsSerif(face));
    }

    [Fact]
    public async Task A_copy_of_the_hwpx_names_the_replacement_and_still_converts()
    {
        var dir = fx.NewFolder();
        var hwpx = await fx.MakeHwpxAsync(dir);
        var faces = Faces(hwpx);
        var broken = faces.First();

        var copy = RhwpFonts.Prepare(hwpx, dir, face => face == broken ? false : face == "Malgun Gothic" ? true : null);

        Assert.NotNull(copy);
        Assert.DoesNotContain(broken, Faces(copy));
        Assert.Contains("Malgun Gothic", Faces(copy));
        Assert.Equal(faces, Faces(hwpx)); // the original is untouched
        using (var zip = ZipFile.OpenRead(copy))
            Assert.Equal("mimetype", zip.Entries[0].FullName); // still a valid HWPX package
        Assert.Null(RhwpFonts.Prepare(hwpx, dir, _ => true)); // nothing to replace: no copy
    }

    private static List<string> Faces(string hwpx)
    {
        using var zip = ZipFile.OpenRead(hwpx);
        using var stream = zip.GetEntry("Contents/header.xml")!.Open();
        return XDocument.Load(stream).Descendants().Where(e => e.Name.LocalName == "font")
            .Select(e => (string?)e.Attribute("face")).OfType<string>().Distinct().ToList();
    }
}
