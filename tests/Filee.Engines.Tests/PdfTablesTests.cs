// PDF → XLSX (#267): each table drawn with lines on its own sheet, with numbers and dates as real values, merged
// cells and column widths, a table going on over the next page joined, and a clear failure when there is no table.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Office.Sheets;
using Filee.Engines.Pdf;

namespace Filee.Engines.Tests;

public class PdfTablesTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    /// <summary>A grid of rules with text in its cells; a null cell is covered by the one to its left (no rule between).</summary>
    private static void Table(PdfBuilder pdf, double top, double[] xs, string?[][] rows, bool boldHeader = true)
    {
        var y = top;
        foreach (var row in rows)
        {
            pdf.Rule(xs[0], y, xs[^1], y);
            for (var c = 0; c < row.Length; c++)
            {
                if (c == 0 || row[c] is not null)
                    pdf.Rule(xs[c], y, xs[c], y + 20);
                if (row[c] is { } text)
                    pdf.Line(text, 10, pdf.Font(bold: boldHeader && y == top), xs[c] + 4, y + 14);
            }
            pdf.Rule(xs[^1], y, xs[^1], y + 20);
            y += 20;
        }
        pdf.Rule(xs[0], y, xs[^1], y);
    }

    private async Task<Workbook> ConvertAsync(string pdf)
    {
        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "xlsx" });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        return XlsxReader.Read(job.Outputs.Single());
    }

    [Fact]
    public async Task Numbers_dates_and_merged_cells_reach_the_sheet()
    {
        var pdf = new PdfBuilder().NewPage();
        pdf.Paragraph("Sales by quarter, as reported.", 11, pdf.Font(), 50, 80, 495, 14);
        Table(pdf, 110, [50, 200, 350, 545],
        [
            ["Item", "Amount", "Date"],
            ["Paper", "1,234.50", "2026-10-23"],
            ["Ink", "(120)", "2026/1/5"],
            ["Share", "12.5%", "007"],
            ["Total for the year", null, "$9.99"],
        ]);
        var book = await ConvertAsync(pdf.Save(Path.Combine(fx.NewFolder(), "sales.pdf")));

        var sheet = Assert.Single(book.Sheets);
        Assert.Equal("Item", sheet.Get(0, 0)!.Text);
        Assert.Equal((1234.5, "#,##0.00"), (sheet.Get(1, 1)!.Value!.Value, sheet.Get(1, 1)!.NumberFormat));
        Assert.Equal(-120, sheet.Get(2, 1)!.Value);
        Assert.Equal(0.125, sheet.Get(3, 1)!.Value!.Value, 9);
        Assert.Equal(new DateOnly(2026, 10, 23).DayNumber - new DateOnly(1899, 12, 30).DayNumber, sheet.Get(1, 2)!.Value);
        Assert.Equal(new DateOnly(2026, 1, 5).DayNumber - new DateOnly(1899, 12, 30).DayNumber, sheet.Get(2, 2)!.Value);
        Assert.Null(sheet.Get(3, 2)!.Value); // a code with a leading zero stays text
        Assert.Equal("007", sheet.Get(3, 2)!.Text);
        Assert.Equal(9.99, sheet.Get(4, 2)!.Value);
        Assert.Contains(new CellRange(4, 0, 4, 1), sheet.Merges);
        Assert.Null(sheet.Get(0, 0)!.Value); // the paragraph above the table isn't on the sheet
        Assert.DoesNotContain(sheet.Rows.Values.SelectMany(r => r.Values), c => c.Text.Contains("Sales by quarter"));
        // Column widths follow the PDF: the last column is the widest.
        Assert.True(sheet.ColumnWidth(2) > sheet.ColumnWidth(0));
    }

    [Fact]
    public async Task A_table_going_on_over_the_next_page_is_one_sheet_and_another_table_its_own()
    {
        double[] xs = [50, 250, 545];
        var pdf = new PdfBuilder().NewPage();
        Table(pdf, 600, xs, [["Name", "Size"], ["a.pdf", "10"], ["b.pdf", "20"]]);
        pdf.NewPage();
        Table(pdf, 60, xs, [["Name", "Size"], ["c.pdf", "30"]]); // header repeated on the new page
        Table(pdf, 300, [50, 150, 300, 545], [["Code", "City", "Country"], ["SEL", "Seoul", "Korea"]]);
        var book = await ConvertAsync(pdf.Save(Path.Combine(fx.NewFolder(), "files.pdf")));

        Assert.Equal(2, book.Sheets.Count);
        var files = book.Sheets[0];
        Assert.Equal(["Name", "a.pdf", "b.pdf", "c.pdf"], Enumerable.Range(0, 4).Select(r => files.Get(r, 0)!.Text));
        Assert.Equal(30, files.Get(3, 1)!.Value);
        Assert.Null(files.Get(4, 0));
        Assert.Equal("Seoul", book.Sheets[1].Get(1, 1)!.Text);
    }

    [Fact]
    public async Task A_pdf_without_tables_fails_with_a_message()
    {
        var pdf = new PdfBuilder().NewPage();
        pdf.Paragraph("Only a letter, no table here.", 11, pdf.Font(), 50, 80, 495, 14);

        var job = await fx.ConvertAsync([pdf.Save(Path.Combine(fx.NewFolder(), "letter.pdf"))], new Preset { TargetFormat = "xlsx" });

        Assert.Equal(JobState.Failed, job.State);
        Assert.Equal("error.pdf_no_tables", job.Files[0].ErrorKey);
    }

    [Theory]
    [InlineData("1,234", 1234d, "#,##0")]
    [InlineData("-7", -7d, "0")]
    [InlineData("0.5", 0.5, "0.0")]
    [InlineData("€1,000.00", 1000d, "\"€\"#,##0.00")]
    [InlineData("(1,200.50)", -1200.5, "#,##0.00;(#,##0.00)")]
    [InlineData("50%", 0.5, "0%")]
    public void Numbers_keep_their_shown_format(string text, double value, string format)
    {
        var number = PdfTablesConverter.Number(text);
        Assert.NotNull(number);
        Assert.Equal(value, number.Value.Value, 9);
        Assert.Equal(format, number.Value.Format);
    }

    [Theory]
    [InlineData("012")]
    [InlineData("1,23")]
    [InlineData("1.234,50")]
    [InlineData("12 KB")]
    [InlineData("(5")]
    [InlineData("1234567890123456")]
    [InlineData("10/23/2026")]
    public void Other_text_stays_text(string text)
    {
        Assert.Null(PdfTablesConverter.Number(text));
        Assert.Null(PdfTablesConverter.Date(text));
    }

    [Theory]
    [InlineData("2026-10-23", "yyyy-mm-dd")]
    [InlineData("2026/1/5", "yyyy/m/d")]
    [InlineData("2026. 10. 23.", "yyyy. mm. dd.")]
    public void Dates_with_the_year_first_become_dates(string text, string format)
    {
        var date = PdfTablesConverter.Date(text);
        Assert.NotNull(date);
        Assert.Equal(format, date.Value.Format);
        Assert.Null(PdfTablesConverter.Date("2026-02-30")); // not a day
    }
}
