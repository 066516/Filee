using System.IO.Compression;
using System.Xml.Linq;
using Filee.Engines.Office.Sheets;
using static Filee.Engines.Tests.XlsxBuilder;

namespace Filee.Engines.Tests;

public class SpreadsheetPrintPlanTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Inspector_reads_real_content_and_the_workbooks_print_settings()
    {
        var folder = Directory.CreateTempSubdirectory("filee-print-plan-").FullName;
        try
        {
            var builder = new XlsxBuilder();
            builder.Sheet("Hidden", Row(1, builder.Text("A1", "x")), hidden: true);
            builder.Sheet("4 course",
                Row(1, builder.Text("A1", "name"), builder.Text("EI1", "last")) +
                "<row r=\"72\" hidden=\"1\"><c r=\"A72\" s=\"1\"/></row>",
                before: "<cols><col min=\"3\" max=\"4\" hidden=\"1\"/></cols>",
                after: "<pageMargins left=\"0.25\" right=\"0.25\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>" +
                       "<pageSetup paperSize=\"9\" orientation=\"landscape\" scale=\"10\" pageOrder=\"overThenDown\"/>");
            var path = builder.Save(Path.Combine(folder, "courses.xlsx"));
            AddDefinedNames(path,
                "<definedName name=\"_xlnm.Print_Area\" localSheetId=\"1\">'4 course'!$A$1:$EI$154</definedName>" +
                "<definedName name=\"_xlnm.Print_Titles\" localSheetId=\"1\">'4 course'!$1:$2</definedName>");

            var workbook = SpreadsheetPrintInspector.Read(path);

            Assert.Equal(2, workbook.Sheets.Count);
            var sheet = workbook.Sheets[1];
            Assert.Equal("A1:EI1", sheet.UsedRange); // the styled empty A72 does not enlarge real content
            Assert.Equal("A1:EI154", sheet.PrintArea);
            Assert.Equal("1:2", sheet.RepeatRows);
            Assert.Equal(139, sheet.UsedColumns);
            Assert.Equal(1, sheet.HiddenRows);
            Assert.Equal(2, sheet.HiddenColumns);
            Assert.Equal(SpreadsheetPaper.A4, sheet.PageSetup.Paper);
            Assert.Equal(SpreadsheetPageOrientation.Landscape, sheet.PageSetup.Orientation);
            Assert.Equal(10, sheet.PageSetup.ScalePercent);
            Assert.Equal(SpreadsheetPageOrder.OverThenDown, sheet.PageSetup.Order);
            Assert.True(sheet.HasSuspiciousScale);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Temporary_copy_changes_only_the_chosen_regions_print_layout()
    {
        var folder = Directory.CreateTempSubdirectory("filee-print-copy-").FullName;
        try
        {
            var builder = new XlsxBuilder();
            builder.Sheet("First", Row(1, builder.Text("A1", "leave me")));
            builder.Sheet("Plan's page", Row(1, builder.Text("A1", "show me")) +
                "<row r=\"2\" hidden=\"1\"><c r=\"A2\" t=\"inlineStr\"><is><t>hidden</t></is></c></row>",
                after: "<rowBreaks count=\"1\" manualBreakCount=\"1\"><brk id=\"2\" max=\"16383\" man=\"1\"/></rowBreaks>");
            var source = builder.Save(Path.Combine(folder, "source.xlsx"));
            var original = File.ReadAllBytes(source);
            var book = SpreadsheetPrintInspector.Read(source);
            var selected = book.Sheets[1];
            var plan = SpreadsheetPrintRegion.FromSource(selected) with
            {
                Range = "A1:C20",
                Paper = SpreadsheetPaper.A3,
                Orientation = SpreadsheetPageOrientation.Landscape,
                ScaleMode = SpreadsheetScaleMode.FitToPages,
                FitWidth = 2,
                FitHeight = 0,
                Order = SpreadsheetPageOrder.DownThenOver,
                MarginLeftMm = 8,
                MarginRightMm = 8,
                MarginTopMm = 10,
                MarginBottomMm = 10,
                IncludeHidden = true,
                KeepManualBreaks = false,
            };
            var output = Path.Combine(folder, "temporary.xlsx");

            SpreadsheetPrintPackage.Create(source, output, book, plan);

            Assert.Equal(original, File.ReadAllBytes(source));
            using var zip = ZipFile.OpenRead(output);
            var workbook = Xml(zip, "xl/workbook.xml").Root!;
            var sheets = workbook.Element(S + "sheets")!.Elements(S + "sheet").ToList();
            Assert.Single(sheets);
            Assert.Equal("Plan's page", (string?)sheets[0].Attribute("name"));
            Assert.Null(sheets[0].Attribute("state"));
            var area = workbook.Element(S + "definedNames")!.Elements(S + "definedName").Single();
            Assert.Equal("'Plan''s page'!$A$1:$C$20", area.Value);
            Assert.Equal("0", (string?)area.Attribute("localSheetId"));

            var sheet = Xml(zip, "xl/worksheets/sheet2.xml").Root!;
            Assert.Null(sheet.Element(S + "sheetData")!.Elements(S + "row").Last().Attribute("hidden"));
            Assert.Null(sheet.Element(S + "rowBreaks"));
            Assert.Equal("8", (string?)sheet.Element(S + "pageSetup")!.Attribute("paperSize"));
            Assert.Equal("landscape", (string?)sheet.Element(S + "pageSetup")!.Attribute("orientation"));
            Assert.Equal("2", (string?)sheet.Element(S + "pageSetup")!.Attribute("fitToWidth"));
            Assert.Equal("0", (string?)sheet.Element(S + "pageSetup")!.Attribute("fitToHeight"));
            Assert.Equal("downThenOver", (string?)sheet.Element(S + "pageSetup")!.Attribute("pageOrder"));
            Assert.Equal("1", (string?)sheet.Element(S + "sheetPr")!.Element(S + "pageSetUpPr")!.Attribute("fitToPage"));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    [Theory]
    [InlineData("A1", true)]
    [InlineData("$A$1:$S$52", true)]
    [InlineData("A1:S52,T1:Z80", true)]
    [InlineData("A0:S52", false)]
    [InlineData("XFE1", false)]
    [InlineData("A1048577", false)]
    [InlineData("not a range", false)]
    public void Print_ranges_are_validated(string range, bool valid) =>
        Assert.Equal(valid, SpreadsheetPrintInspector.IsValidRange(range));

    [Fact]
    public void Pdf_preview_preserves_the_page_shape()
    {
        var folder = Directory.CreateTempSubdirectory("filee-sheet-preview-").FullName;
        try
        {
            var path = Path.Combine(folder, "mixed.pdf");
            using (var pdf = new PdfSharp.Pdf.PdfDocument())
            {
                var portrait = pdf.AddPage();
                portrait.Width = PdfSharp.Drawing.XUnit.FromMillimeter(210);
                portrait.Height = PdfSharp.Drawing.XUnit.FromMillimeter(297);
                var landscape = pdf.AddPage();
                landscape.Width = PdfSharp.Drawing.XUnit.FromMillimeter(297);
                landscape.Height = PdfSharp.Drawing.XUnit.FromMillimeter(210);
                pdf.Save(path);
            }

            var pages = Filee.Engines.Pdf.PdfPagePreviewRenderer.Render(path, 400, TestContext.Current.CancellationToken);

            Assert.Equal(2, pages.Count);
            Assert.True(pages[0].Height > pages[0].Width);
            Assert.True(pages[1].Width > pages[1].Height);
            Assert.All(pages, page => Assert.Equal(400, Math.Max(page.Width, page.Height)));
            Assert.All(pages, page => Assert.NotEmpty(page.Png));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static XDocument Xml(ZipArchive zip, string path)
    {
        using var stream = zip.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }

    private static void AddDefinedNames(string path, string names)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = zip.GetEntry("xl/workbook.xml")!;
        XDocument document;
        using (var stream = entry.Open())
            document = XDocument.Load(stream);
        document.Root!.Element(S + "sheets")!.AddAfterSelf(XElement.Parse($"<definedNames xmlns=\"{S}\">{names}</definedNames>"));
        entry.Delete();
        using var output = zip.CreateEntry("xl/workbook.xml").Open();
        document.Save(output);
    }
}
