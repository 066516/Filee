using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Settings;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Filee.App.Tests;

public class PdfPageEditorTests
{
    private static string MakePdf(string folder, string name, params (double Width, int Rotation)[] pages)
    {
        var path = Path.Combine(folder, name);
        using var document = new PdfDocument();
        foreach (var (width, rotation) in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(500);
            page.Rotate = rotation;
        }
        document.Save(path);
        return path;
    }

    [AvaloniaFact]
    public async Task Editor_adds_reorders_rotates_deletes_and_saves_pages()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("filee-pdf-pages-").FullName;
        try
        {
            var first = MakePdf(folder, "source.pdf", (200, 0), (300, 0));
            var added = MakePdf(folder, "added.pdf", (400, 90));
            var store = new UserDataStore(Path.Combine(folder, "data"));
            store.Load();
            var preset = BuiltInData.CreatePresets().Single(p => p.Id == "tool-pdf-pages");
            preset.Output.Location = OutputLocation.CustomFolder;
            preset.Output.CustomFolder = folder;
            var vm = new PdfPageEditorViewModel([first], preset, AppHost.Get<ILocalizer>(), store);

            await vm.AddFilesAsync([added]);
            vm.SelectedPage = vm.Pages[2];
            vm.MoveUpCommand.Execute(null);
            vm.MoveUpCommand.Execute(null);
            vm.RotateLeftCommand.Execute(null);
            vm.SelectedPage = vm.Pages[2];
            vm.DeleteCommand.Execute(null);

            Assert.Equal([400d, 200d], vm.Pages.Select(p => p.Path == added ? 400d : 200d));
            Assert.Equal("Rotated −90°", vm.Pages[0].RotationText.Replace('-', '−'));
            Assert.True(await vm.SaveAsync());

            var output = Path.Combine(folder, "source_edited.pdf");
            using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
            Assert.Equal([400d, 200d], result.Pages.Cast<PdfPage>().Select(p => p.Width.Point));
            Assert.Equal([0, 0], result.Pages.Cast<PdfPage>().Select(p => p.Rotate));
            Assert.Contains(store.History, h => h.Outputs.Contains(output));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    [AvaloniaFact]
    public void Editor_window_renders_all_actions_without_clipping()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("filee-pdf-pages-ui-").FullName;
        try
        {
            var source = MakePdf(folder, "Annual report.pdf", (595, 0), (595, 0), (842, 0), (595, 0));
            var store = new UserDataStore(Path.Combine(folder, "data"));
            store.Load();
            var preset = BuiltInData.CreatePresets().Single(p => p.Id == "tool-pdf-pages");
            var vm = new PdfPageEditorViewModel([source], preset, AppHost.Get<ILocalizer>(), store)
            {
                SelectedPage = null,
            };
            vm.SelectedPage = vm.Pages[1];
            vm.RotateRightCommand.Execute(null);
            var window = new PdfPageEditorWindow { DataContext = vm };
            window.Show();

            RenderTests.Pump();
            RenderTests.Save(window, "pdf-page-editor-en.png");

            Assert.True(window.PageOrder.Bounds.Width > 400);
            Assert.Equal(4, window.PageOrder.ItemsView.Count);
            window.Close();
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
