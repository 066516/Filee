// Tab flips the open donut to the back face of its profile: in click mode as a key of the focused window, in drag
// mode through the global hook. Arrows still move between slices, an empty or unusable back face doesn't flip, the
// back face greys out what can't convert the files like the front, and "Reduce animations" swaps the faces at once.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.ViewModels.Pages;
using Filee.App.Views;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using SharpHook.Data;

namespace Filee.App.Tests;

[Collection("Motion")]
public class DonutFlipTests
{
    private static readonly string[] Photos = [@"C:\photos\a.jpg", @"C:\photos\b.jpg"];

    private static UserDataStore Store => AppHost.Get<UserDataStore>();

    private static ToolbarProfile Images => Store.Profiles.Single(p => p.Id == "images");

    /// <summary>The image donut's back face, as the library has it now.</summary>
    private static List<string> ImageBack => Images.BackPresetIds!.Where(id => Store.FindPreset(id) is not null).ToList();

    /// <summary>
    /// Runs <paramref name="test"/> with animations on or off and the image donut's back face as given (null: the
    /// built-in one); everything is put back afterwards.
    /// </summary>
    private static void With(bool animations, List<string>? back, Action test, string language = "en")
    {
        TestServices.EnsureInitialized(language);
        var images = Images;
        var (front, before) = (images.PresetIds.ToList(), images.BackPresetIds?.ToList());
        try
        {
            if (back is not null)
                images.BackPresetIds = back;
            Motion.Configure(!animations, () => false);
            test();
        }
        finally
        {
            Motion.Configure(false, () => false);
            images.PresetIds = front;
            images.BackPresetIds = before;
        }
    }

    /// <summary>The donut for <paramref name="files"/>, opened like the selection shortcut opens it.</summary>
    private static (RadialViewModel Vm, RadialWindow Window, DonutMenu Donut) OpenClickMode(params string[] files)
    {
        var vm = AppHost.Get<RadialViewModel>();
        vm.Load(files.Length > 0 ? files : Photos, clickMode: true);
        vm.FitToThisDonut();
        var window = new RadialWindow { DataContext = vm };
        HintLikeTheController(vm, window.DonutControl);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (vm, window, window.DonutControl);
    }

    /// <summary>The hub says what the slice under the pointer or the keyboard does, as RadialController has it.</summary>
    private static void HintLikeTheController(RadialViewModel vm, DonutMenu donut) =>
        donut.ActiveIndexChanged += (_, index) => vm.ShowHint(index, donut.ActiveSubIndex, donut.IsCenterHot);

    [AvaloniaFact]
    public void Tab_flips_to_the_back_and_shift_tab_back_to_the_front_in_click_mode() => With(animations: true, back: null, () =>
    {
        var loc = AppHost.Get<ILocalizer>();
        var (vm, window, donut) = OpenClickMode();
        var front = vm.Items.Select(i => i.Id).ToList();
        Assert.True(vm.CanFlip);
        Assert.Equal(loc["donut.flip_to_back"], vm.FlipHint);
        Assert.Equal(vm.FlipHint, donut.CenterHint);

        Assert.True(donut.HandleKey(Key.Tab));
        Assert.True(donut.IsFlipping);
        donut.Advance(DonutMenu.FlipDuration * 0.3);
        Assert.InRange(donut.FlipScale, 0.01, 0.99); // turning, still the front
        Assert.False(vm.IsBackFace);
        Assert.Equal(front, vm.Items.Select(i => i.Id));

        donut.Advance(DonutMenu.FlipDuration * 0.3); // past edge-on: the back
        Assert.True(vm.IsBackFace);
        Assert.Equal(ImageBack, vm.Items.Select(i => i.Id));
        Assert.Equal(loc["donut.flip_to_front"], donut.CenterHint);
        Assert.Equal(loc["donut.close_hint"], vm.CenterSubtitle);
        donut.Advance(DonutMenu.FlipDuration);
        Assert.False(donut.IsFlipping);
        Assert.Equal(1, donut.FlipScale);

        Assert.True(donut.HandleKey(Key.Tab, KeyModifiers.Shift));
        donut.Advance(DonutMenu.FlipDuration);
        Assert.False(vm.IsBackFace);
        Assert.Equal(front, vm.Items.Select(i => i.Id));
        window.Close();
    });

    [AvaloniaFact]
    public void The_window_hands_tab_to_the_donut() => With(animations: false, back: null, () =>
    {
        var (vm, window, _) = OpenClickMode();
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsBackFace);
        Assert.True(window.IsVisible);
        window.Close();
    });

    [AvaloniaFact]
    public void Tab_pressed_again_during_a_flip_flips_once_more_after_it() => With(animations: true, back: null, () =>
    {
        var (vm, window, donut) = OpenClickMode();
        donut.HandleKey(Key.Tab);
        donut.Advance(0.02);
        donut.HandleKey(Key.Tab); // before the first turn ended
        for (var i = 0; i < 40; i++)
            donut.Advance(1 / 60.0);
        Assert.False(donut.IsFlipping);
        Assert.False(vm.IsBackFace); // turned over twice
        window.Close();
    });

    [AvaloniaFact]
    public void Arrows_still_move_between_slices_and_start_over_on_the_other_face() => With(animations: false, back: null, () =>
    {
        var (vm, window, donut) = OpenClickMode();
        Assert.True(donut.HandleKey(Key.Right));
        Assert.True(donut.HandleKey(Key.Right));
        Assert.Equal(1, donut.ActiveIndex);
        Assert.Contains(vm.Items[1].Label, vm.CenterSubtitle);

        Assert.True(donut.HandleKey(Key.Tab));
        Assert.True(vm.IsBackFace);
        Assert.Equal(-1, donut.ActiveIndex); // the selection starts over on the back
        Assert.Equal(AppHost.Get<ILocalizer>()["donut.close_hint"], vm.CenterSubtitle);

        Assert.True(donut.HandleKey(Key.Left)); // wraps to the last slice of the back
        Assert.Equal(vm.Items.Count - 1, donut.ActiveIndex);
        Assert.Equal(ImageBack[^1], vm.Items[donut.ActiveIndex].Id);
        Assert.Equal(ImageBack[^1], vm.PresetAt(donut.ActiveIndex, -1)!.Id); // what Enter converts with
        window.Close();
    });

    [AvaloniaFact]
    public void With_animations_off_the_faces_swap_at_once() => With(animations: false, back: null, () =>
    {
        var (vm, window, donut) = OpenClickMode();
        Assert.True(donut.Flip());
        Assert.False(donut.IsFlipping);
        Assert.Equal(1, donut.FlipScale);
        Assert.True(vm.IsBackFace);
        Assert.False(donut.Advance(0)); // nothing left to animate
        window.Close();
    });

    [AvaloniaFact]
    public void A_donut_without_a_back_face_does_not_flip() => With(animations: true, back: [], () =>
    {
        var (vm, window, donut) = OpenClickMode();
        Assert.False(vm.CanFlip);
        Assert.Null(vm.FlipHint);
        Assert.False(donut.CanFlip);

        Assert.True(donut.HandleKey(Key.Tab)); // still Tab's: it doesn't move the focus off the donut
        Assert.False(donut.IsFlipping);
        Assert.False(donut.Flip());
        Assert.False(vm.IsBackFace);
        Assert.False(vm.ToggleFace());
        window.Close();
    });

    [AvaloniaFact]
    public void The_back_face_greys_out_what_cannot_convert_the_files_and_one_with_nothing_left_does_not_flip()
    {
        var group = new Preset
        {
            Id = "flip-test-sounds",
            Name = "Sounds",
            Kind = PresetKind.Group,
            TargetFormat = Preset.GroupTarget,
            SubPresetIds = ["to-wav", "to-flac"],
        };
        With(animations: false, back: ["to-wav", "flip-test-sounds"], () =>
        {
            Store.Presets.Add(group);
            try
            {
                // Pictures can't become sounds: nothing on the back is for them, so there is nothing to flip to.
                var (vm, window, donut) = OpenClickMode();
                Assert.False(vm.CanFlip);
                Assert.Null(vm.FlipHint);
                Assert.False(donut.Flip());
                window.Close();

                // One slice for them is enough; the others are greyed out with the reason, as on the front.
                Images.BackPresetIds = ["to-wav", "flip-test-sounds", "to-gif"];
                (vm, window, donut) = OpenClickMode();
                Assert.True(vm.CanFlip);
                Assert.True(donut.Flip());
                Assert.Equal(["to-wav", "flip-test-sounds", "to-gif"], vm.Items.Select(i => i.Id));
                Assert.False(vm.Items[0].IsEnabled);
                Assert.NotNull(vm.Items[0].DisabledReason);
                Assert.True(vm.Items[1].IsGroup);
                Assert.False(vm.Items[1].IsEnabled); // every slice of its ring is greyed out
                Assert.All(vm.Items[1].Children!, child => Assert.False(child.IsEnabled));
                Assert.True(vm.Items[2].IsEnabled);
                Assert.Null(vm.PresetAt(0, -1));
                Assert.Equal("to-gif", vm.PresetAt(2, -1)!.Id);
                window.Close();
            }
            finally
            {
                Store.Presets.Remove(group);
            }
        });
    }

    [AvaloniaFact]
    public void An_open_group_ring_folds_before_the_donut_turns() => With(animations: true, back: null, () =>
    {
        Images.PresetIds = [.. Images.PresetIds, "rotate"];
        var (vm, window, donut) = OpenClickMode();
        Assert.True(vm.ReserveSubRing);
        var group = vm.Items.Count - 1;
        donut.SetExternalHit(new DonutHit(DonutHitKind.Segment, group));
        donut.ExpandPendingGroup();
        Assert.Equal(DonutHitKind.SubSegment, donut.HitTest(donut.SubSliceCenter(group, 1)).Kind);

        donut.HandleKey(Key.Tab);
        Assert.NotEqual(DonutHitKind.SubSegment, donut.HitTest(donut.SubSliceCenter(group, 1)).Kind);
        Assert.False(vm.IsBackFace); // the ring folds first, the faces swap halfway through the turn
        donut.Advance(DonutMenu.FlipDuration);
        Assert.True(vm.IsBackFace);
        window.Close();
    });

    [AvaloniaFact]
    public void A_drag_resting_on_a_slice_lights_up_the_slice_there_on_the_other_face() => With(animations: false, back: null, () =>
    {
        var vm = AppHost.Get<RadialViewModel>();
        vm.ResetForDrag();
        var window = new RadialWindow { DataContext = vm };
        HintLikeTheController(vm, window.DonutControl);
        window.Show();
        vm.Load(Photos, clickMode: false);
        Dispatcher.UIThread.RunJobs();
        var donut = window.DonutControl;
        var n = vm.Items.Count;
        var center = new Point(donut.Bounds.Width / 2, donut.Bounds.Height / 2);
        var at = DonutGeometry.PointAt(center, vm.OuterRadius * (1 + vm.HoleRatio) / 2, DonutGeometry.MidAngle(5, n));
        donut.SetExternalHit(donut.HitTest(at), at);
        Assert.Equal(5, donut.ActiveIndex);

        Assert.True(donut.Flip());

        var back = vm.Items.Count;
        var expected = DonutGeometry.HitTest(center, at, vm.OuterRadius * vm.HoleRatio, vm.OuterRadius, back, 7).Index;
        Assert.Equal(expected, donut.ActiveIndex);
        Assert.Equal(AppHost.Get<ILocalizer>().Format("donut.drop_to", vm.Items[expected].Label), vm.CenterSubtitle);
        window.Close();
    });

    /// <summary>
    /// Drag mode: the donut's window has no keyboard focus during the OS drag, so Tab comes from the global hook (here a
    /// stand-in for libuiohook): it flips the donut while files are on it, and is left alone once the donut is gone.
    /// </summary>
    [AvaloniaFact]
    public void In_drag_mode_tab_from_the_global_hook_flips_the_donut() => With(animations: false, back: null, () =>
    {
        var provider = new TriggerServiceLifecycleTests.DelayedProvider();
        provider.AllowExit.Set();
        using var triggers = TriggerServiceLifecycleTests.Create(provider, () => { });
        triggers.Start();
        Assert.True(SpinWait.SpinUntil(() => provider.Runs == 1, 2000));
        var vm = AppHost.Get<RadialViewModel>();
        var radial = new RadialController(vm, AppHost.Get<ConversionService>(), AppHost.Get<WindowService>(), Store,
            AppHost.Get<IPlatformServices>(), triggers, NullLogger<RadialController>.Instance);
        var folder = Directory.CreateTempSubdirectory("filee-flip-");
        try
        {
            var file = Path.Combine(folder.FullName, "photo.jpg");
            File.WriteAllBytes(file, [0xFF, 0xD8, 0xFF]);

            radial.ShowForDrag(400, 400);
            var window = radial.Window!;
            provider.PostKey(EventType.KeyPressed, KeyCode.VcTab); // before files: nothing to flip, not even watched
            provider.PostKey(EventType.KeyReleased, KeyCode.VcTab);
            Assert.False(TriggerServiceLifecycleTests.Settle(() => triggers.WatchFlipKey));

            var data = new DataTransfer();
            var item = window.StorageProvider.TryGetFileFromPathAsync(file).GetAwaiter().GetResult();
            Assert.NotNull(item);
            data.Add(DataTransferItem.CreateFile(item));
            var at = new Point(window.Bounds.Width / 2 + 60, window.Bounds.Height / 2);
            window.DragDrop(at, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { file }, vm.Files);
            Assert.False(vm.IsBackFace);
            Assert.True(triggers.WatchFlipKey);

            provider.PostKey(EventType.KeyPressed, KeyCode.VcTab, EventMask.LeftCtrl);
            Assert.True(TriggerServiceLifecycleTests.Settle(() => vm.IsBackFace));
            Assert.Equal(ImageBack, vm.Items.Select(i => i.Id));
            provider.PostKey(EventType.KeyReleased, KeyCode.VcTab, EventMask.LeftCtrl);
            provider.PostKey(EventType.KeyPressed, KeyCode.VcTab, EventMask.LeftCtrl);
            Assert.False(TriggerServiceLifecycleTests.Settle(() => vm.IsBackFace));

            // The drag moved on: Tab is the user's again.
            radial.Close(animate: false);
            Assert.False(triggers.WatchFlipKey);
            provider.PostKey(EventType.KeyReleased, KeyCode.VcTab);
            provider.PostKey(EventType.KeyPressed, KeyCode.VcTab);
            Assert.False(TriggerServiceLifecycleTests.Settle(() => vm.IsBackFace));
            window.Close();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    });

    /// <summary>The image donut's back face, light and dark, and the ring halfway through its turn.</summary>
    [AvaloniaTheory]
    [InlineData("en", false)]
    [InlineData("ko", false)]
    [InlineData("ko", true)]
    [InlineData("zh-CN", false)]
    [InlineData("ja", false)]
    public void The_back_face_renders(string language, bool dark) => With(animations: true, back: null, () =>
    {
        var theme = AppHost.Get<ThemeService>();
        try
        {
            if (dark)
                theme.Apply(new ThemeSettings { Mode = ThemeMode.Dark, CornerRadius = 16 }, language);
            // Applying a theme follows the system's animation setting again, and build servers report it off.
            Motion.Configure(false, () => false);
            var (vm, window, donut) = OpenClickMode(@"C:\photos\a.jpg", @"C:\photos\b.jpg", @"C:\photos\c.jpg");
            var suffix = $"{language}{(dark ? "-dark" : "")}";
            donut.FinishAnimations();
            RenderTests.Pump();
            RenderTests.Save(window, $"donut-front-hint-{suffix}.png");

            // Drawn right here, not by the render loop: that would play the rest of the turn first.
            donut.HandleKey(Key.Tab);
            donut.Advance(DonutMenu.FlipDuration * 0.3);
            Assert.InRange(donut.FlipScale, 0.2, 0.8);
            var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
            using (var turning = new Avalonia.Media.Imaging.RenderTargetBitmap(size))
            {
                turning.Render(window);
                turning.Save(Path.Combine(TestServices.ScreenshotDirectory, $"donut-flip-midway-{suffix}.png"));
            }

            donut.FinishAnimations();
            Assert.True(vm.IsBackFace);
            donut.SetExternalHighlight(2);
            RenderTests.Pump();
            Assert.Equal(1, donut.FlipScale);
            RenderTests.Save(window, $"donut-back-{suffix}.png");
            window.Close();
        }
        finally
        {
            if (dark)
                theme.Apply(new ThemeSettings(), language);
        }
    }, language);

    /// <summary>
    /// The editor's Front / Back switch: the donut shows and edits the back face, the front stays as it was, and
    /// "Reset to default" puts the built-in back face back.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("en", false)]
    [InlineData("ko", false)]
    [InlineData("ko", true)]
    [InlineData("zh-CN", false)]
    [InlineData("ja", false)]
    public void The_editor_switches_between_the_front_and_the_back(string language, bool dark) => With(animations: false, back: null, () =>
    {
        var theme = AppHost.Get<ThemeService>();
        var snapshot = Store.Profiles.Select(p => p.Clone()).ToList();
        Avalonia.Controls.Window? window = null;
        try
        {
            if (dark)
                theme.Apply(new ThemeSettings { Mode = ThemeMode.Dark }, language);
            var main = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>());
            main.Navigate("toolbar");
            window = new MainWindow { DataContext = main, Width = 1080, Height = 1000 };
            window.Show();
            var page = (ToolbarPageViewModel)main.CurrentPage!;
            page.SelectedProfile = page.Profiles.Single(p => p.Profile.Id == "images");
            RenderTests.Pump();
            var front = Images.PresetIds.ToList();
            var switcher = window.GetVisualDescendants().OfType<Avalonia.Controls.Border>().Single(b => b.Name == "FaceSwitch");
            var faces = switcher.GetVisualDescendants().OfType<Avalonia.Controls.RadioButton>().ToList();
            Assert.Equal(2, faces.Count);
            Assert.True(faces[0].IsChecked);
            Assert.Equal(front, page.DonutItems.Select(i => i.Id));

            // The switch shows the back face, and every edit goes there.
            faces[1].IsChecked = true;
            RenderTests.Pump();
            Assert.True(page.EditingBack);
            Assert.False(faces[0].IsChecked);
            Assert.Equal(ImageBack, page.DonutItems.Select(i => i.Id));
            Assert.Equal(ImageBack.Count, page.BackCount);
            Assert.DoesNotContain(page.Palette, chip => ImageBack.Contains(chip.PresetId));
            Assert.Contains(page.Palette, chip => chip.PresetId == "to-png"); // on the front, still free for the back
            Assert.Contains(AppHost.Get<ILocalizer>()["toolbar.face_back"], page.CenterSubtitle);
            RenderTests.Save(window, $"toolbar-back-face-{language}{(dark ? "-dark" : "")}.png");

            var back = ImageBack;
            Assert.True(page.Insert("to-psd", 0));
            page.Move(0, 2);
            page.RemoveAt(page.SliceCount - 1);
            List<string> edited = [.. back[..2], "to-psd", .. back[2..^1]];
            Assert.Equal(edited, Images.BackPresetIds);
            Assert.Equal(front, Images.PresetIds); // the front as it was
            Assert.Equal(edited, page.DonutItems.Select(i => i.Id));

            page.Reset(page.SelectedProfile!);
            Assert.Equal(BuiltInData.CreateProfiles().Single(p => p.Id == "images").BackPresetIds, Images.BackPresetIds);
            Assert.Equal(Images.BackPresetIds, page.DonutItems.Select(i => i.Id)); // still on the back

            faces[0].IsChecked = true;
            RenderTests.Pump();
            Assert.False(page.EditingBack);
            Assert.Equal(front, page.DonutItems.Select(i => i.Id));
        }
        finally
        {
            window?.Close();
            if (dark)
                theme.Apply(new ThemeSettings(), language);
            Store.Profiles.Clear();
            Store.Profiles.AddRange(snapshot);
            Store.SaveLibrary();
        }
    }, language);
}
