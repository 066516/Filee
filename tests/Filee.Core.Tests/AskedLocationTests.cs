// "Ask every time" (#521): what the save dialog suggests, and where the files go once it is answered.

using Filee.Core.Presets;

namespace Filee.Core.Tests;

public class AskedLocationTests
{
    private static readonly OutputPathResolver.Tokens At = new(Path.Combine(Path.GetTempPath(), "photos", "IMG_0412.heic"), "JPG", 1, DateTime.Now);

    private static Preset Asking(string target = "jpg") => new()
    {
        TargetFormat = target,
        Output = { Location = OutputLocation.Ask, FileNamePattern = "{name}_small", Renames = [new RenameStep { Kind = RenameKind.Replace, Find = "IMG_", Replace = "Photo_" }] },
    };

    [Fact]
    public void The_dialog_suggests_the_name_the_preset_would_give()
    {
        Assert.Equal("Photo_0412_small.jpg", AskedLocation.SuggestedName(Asking(), "JPG", At.SourcePath));
        Assert.Equal("Photo_0412_small.heic", AskedLocation.SuggestedName(Asking(BuiltInData.SameAsSource), "Same", At.SourcePath));
    }

    [Fact]
    public void Where_nothing_asks_the_result_goes_next_to_the_source()
    {
        var path = OutputPathResolver.Resolve(Asking().Output, At, "jpg", _ => false);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(At.SourcePath)!, "Photo_0412_small.jpg"), path);
    }

    [Fact]
    public void One_result_is_saved_exactly_as_chosen_replacing_what_the_dialog_asked_about()
    {
        var chosen = Path.Combine(Path.GetTempPath(), "out", "holiday.jpg");
        var preset = Asking();

        var asked = AskedLocation.ForFile(preset, chosen);

        // The name typed in the dialog, without the pattern or renames on top, even though the file exists.
        Assert.Equal(chosen, OutputPathResolver.Resolve(asked.Output, At, "jpg", _ => true));
        Assert.Equal(OutputLocation.Ask, preset.Output.Location); // the preset itself keeps asking
        Assert.Single(preset.Output.Renames);
    }

    [Fact]
    public void Several_results_go_into_the_chosen_folder_under_their_own_names()
    {
        var folder = Path.Combine(Path.GetTempPath(), "chosen");
        var preset = Asking();
        preset.Output.Conflict = ConflictPolicy.Rename;

        var asked = AskedLocation.ForFolder(preset, folder);

        Assert.Equal(Path.Combine(folder, "Photo_0412_small.jpg"), OutputPathResolver.Resolve(asked.Output, At, "jpg", _ => false));
        // An existing file there isn't replaced: only one picked by name in the dialog is.
        Assert.Equal(Path.Combine(folder, "Photo_0412_small (2).jpg"), OutputPathResolver.Resolve(asked.Output, At, "jpg", p => !p.Contains("(2)")));
    }
}
