// What Filee fixes in an HWPX copy before rhwp renders it (issue #65: an exam paper written in 한글 2022).

using System.Xml.Linq;
using Filee.Engines.Hwp;

namespace Filee.Engines.Tests;

public class RhwpInputTests
{
    [Theory]
    [InlineData("f ^{2} = f CIRCf", "f ^{2} = f CIRC f")]
    [InlineData("n(A SMALLINTERB)", "n(A SMALLINTER B)")]
    [InlineData("A SUBSETB", "A SUBSET B")]
    [InlineData("1 lek le28", "1 le k le 28")]
    [InlineData("rootx + rootxy", "root x + root xy")]
    [InlineData("P CUP Q!=U", "P CUP Q NEQ U")]
    [InlineData("4 +- root 6", "4  PLUSMINUS  root 6")]
    public void Commands_glued_to_the_next_letter_are_spaced(string script, string expected)
    {
        Assert.Equal(expected, RhwpInput.NormaliseEquation(script));
    }

    [Theory]
    [InlineData("left ( x right ) leq 3")]
    [InlineData("index line inside lemma")]  // words, not commands
    [InlineData("\"CIRCf != x\" + y")]       // quoted text is literal
    [InlineData("x SUBSETEQ y")]
    public void Commands_and_words_that_are_whole_stay(string script)
    {
        Assert.Equal(script, RhwpInput.NormaliseEquation(script));
    }

    [Fact]
    public void Shapes_sized_relative_to_the_column_get_their_laid_out_size()
    {
        // A "보기" box inside a text box: 9920 means 99.2 % of the column, which rhwp read as 9920 HWPUNIT.
        XNamespace hp = "http://www.hancom.co.kr/hwpml/2011/paragraph";
        var section = new XDocument(new XElement(hp + "sec",
            new XElement(hp + "rect",
                new XElement(hp + "sz", new XAttribute("width", 9920), new XAttribute("widthRelTo", "COLUMN"), new XAttribute("height", 3206), new XAttribute("heightRelTo", "ABSOLUTE")),
                new XElement(hp + "curSz", new XAttribute("width", 23053), new XAttribute("height", 24841))),
            new XElement(hp + "pic",
                new XElement(hp + "sz", new XAttribute("width", 5000), new XAttribute("widthRelTo", "ABSOLUTE"), new XAttribute("height", 4000), new XAttribute("heightRelTo", "ABSOLUTE")),
                new XElement(hp + "curSz", new XAttribute("width", 6000), new XAttribute("height", 4800)))));

        Assert.True(RhwpInput.FixShapeSizes(section));

        var sizes = section.Descendants(hp + "sz").ToList();
        Assert.Equal(("23053", "24841", "ABSOLUTE", "ABSOLUTE"),
            ((string)sizes[0].Attribute("width")!, (string)sizes[0].Attribute("height")!, (string)sizes[0].Attribute("widthRelTo")!, (string)sizes[0].Attribute("heightRelTo")!));
        Assert.Equal("5000", (string)sizes[1].Attribute("width")!); // absolute sizes are left alone
        Assert.False(RhwpInput.FixShapeSizes(section));             // nothing left to fix
    }

    [Fact]
    public void Cambria_math_draws_equations_only_when_no_symbol_font_is_installed()
    {
        static InstalledFont Font() => new(true, _ => true);
        Assert.Equal(["--equation-font", "Cambria Math"], RhwpInput.PdfOptions(name => name == "Cambria Math" ? Font() : null));
        Assert.Empty(RhwpInput.PdfOptions(name => name is "Cambria Math" or "Arial Unicode MS" ? Font() : null));
        Assert.Empty(RhwpInput.PdfOptions(_ => null));
    }
}
