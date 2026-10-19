// State of the live donut toolbar: which files are being converted and which presets are offered.

using CommunityToolkit.Mvvm.ComponentModel;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.Core.Formats;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;

namespace Filee.App.ViewModels;

public sealed partial class RadialViewModel(UserDataStore store, ILocalizer loc, PresetAvailability availability) : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<DonutItem> _items = [];
    [ObservableProperty] private string _centerTitle = "";
    [ObservableProperty] private string? _centerSubtitle;
    [ObservableProperty] private double _outerRadius = 150;
    [ObservableProperty] private double _holeRatio = 0.42;
    [ObservableProperty] private double _sliceOpacity = 0.96;

    /// <summary>
    /// The window keeps room for the ring a group fans out. Dragging, the donut is placed before the files (and so
    /// the donut) are known: room when any donut has a group. In click mode only when this one does
    /// (<see cref="FitToThisDonut"/>), so other donuts stay as small, and as close to the cursor at a screen edge, as
    /// without groups.
    /// </summary>
    [ObservableProperty] private bool _reserveSubRing;

    /// <summary>
    /// 0 while a drag gesture waits for files, 1 otherwise. The gesture starts before anyone knows what is being
    /// dragged (a file, text, or nothing: Ctrl + a rubber-band selection), so the donut only appears when files enter.
    /// </summary>
    [ObservableProperty] private double _donutOpacity = 1;

    /// <summary>
    /// Background of the window while the donut is hidden: practically invisible, but not fully transparent, so
    /// Windows still delivers the drag to this window and the files can arrive.
    /// </summary>
    [ObservableProperty] private string _hitBackground = "Transparent";

    [ObservableProperty] private int _currentPage;
    [ObservableProperty] private int _totalPages = 1;

    /// <summary>Maximum number of slices shown on one page of the donut.</summary>
    public const int MaxPageSlices = ToolbarProfile.MaxSlices;

    /// <summary>Files the donut will convert (empty while waiting for a drag to enter).</summary>
    public IReadOnlyList<string> Files { get; private set; } = [];

    /// <summary>Presets on the current page in slice order.</summary>
    public IReadOnlyList<Preset> Presets { get; private set; } = [];

    /// <summary>For each slice on the current page, the presets its group fans out.</summary>
    private IReadOnlyList<IReadOnlyList<Preset>> _subPresets = [];

    private IReadOnlyList<Preset> _allPresets = [];
    private IReadOnlyList<IReadOnlyList<Preset>> _allSubPresets = [];
    private IReadOnlyList<DonutItem> _allItems = [];

    /// <summary>
    /// The preset that slice <paramref name="index"/> converts with, or slice <paramref name="sub"/> of its group's ring;
    /// null when there is none, or when the slice is a navigation page-flip control.
    /// </summary>
    public Preset? PresetAt(int index, int sub)
    {
        if (index < 0 || index >= Items.Count || Items[index].IsNavigation)
            return null;
        if (sub < 0)
            return Items[index] is { IsEnabled: true, IsGroup: false } && index < Presets.Count ? Presets[index] : null;
        return Items[index].Children is { } children && sub < children.Count && children[sub].IsEnabled && index < _subPresets.Count
            ? _subPresets[index][sub]
            : null;
    }

    /// <summary>The preset slice <paramref name="index"/> shows, or slice <paramref name="sub"/> of its group's ring (to edit it).</summary>
    public Preset? PresetShownAt(int index, int sub) =>
        index < 0 || index >= Presets.Count || (index < Items.Count && Items[index].IsNavigation) ? null
        : sub < 0 ? Presets[index]
        : sub < _subPresets[index].Count ? _subPresets[index][sub] : null;

    /// <summary>True when opened with known files (keyboard, context menu, drop zone): slices are clicked.</summary>
    public bool IsClickMode { get; private set; }

    public bool HasFiles => Files.Count > 0;

    /// <summary>True if the slice at <paramref name="index"/> is a page navigation control.</summary>
    public bool IsNavigationAt(int index) =>
        index >= 0 && index < Items.Count && Items[index].IsNavigation;

    /// <summary>Navigates page if the slice at <paramref name="index"/> is a navigation item.</summary>
    public bool NavigateAt(int index)
    {
        if (!IsNavigationAt(index))
            return false;
        if (Items[index].IsNextPage)
            NextPage();
        else if (Items[index].IsPrevPage)
            PrevPage();
        return true;
    }

    public void NextPage()
    {
        if (TotalPages <= 1)
            return;
        CurrentPage = (CurrentPage + 1) % TotalPages;
        ApplyPage();
    }

    public void PrevPage()
    {
        if (TotalPages <= 1)
            return;
        CurrentPage = (CurrentPage - 1 + TotalPages) % TotalPages;
        ApplyPage();
    }

    public void CyclePage(int delta)
    {
        if (TotalPages <= 1)
            return;
        if (delta > 0)
            NextPage();
        else if (delta < 0)
            PrevPage();
    }

    /// <summary>Drag mode before any file entered: an empty ring saying "Bring files here".</summary>
    public void ResetForDrag()
    {
        IsClickMode = false;
        Files = [];
        Presets = [];
        _subPresets = [];
        _allPresets = [];
        _allSubPresets = [];
        _allItems = [];
        CurrentPage = 0;
        TotalPages = 1;
        ApplyAppearance();
        ReserveSubRing = store.Profiles.Any(profile => profile.PresetIds.Any(id => store.FindPreset(id)?.IsGroup == true));
        Items = [];
        CenterTitle = loc["donut.center_drag"];
        CenterSubtitle = null;
        DonutOpacity = 0;
        HitBackground = "#01000000";
    }

    /// <summary>Fills the donut for the given files.</summary>
    public void Load(IReadOnlyList<string> files, bool clickMode, string? profileId = null)
    {
        IsClickMode = clickMode;
        Files = files;
        DonutOpacity = 1;
        HitBackground = "Transparent";
        ApplyAppearance();

        var profile = (!string.IsNullOrEmpty(profileId) ? store.Profiles.FirstOrDefault(p => p.Id == profileId) : null)
                      ?? ProfileSelector.Select(store.Profiles, files);
        var formats = ProfileSelector.DetectFormats(files);
        _allPresets = (profile?.PresetIds ?? [])
            .Select(store.FindPreset)
            .OfType<Preset>()
            .ToList();

        _allSubPresets = _allPresets
            .Select(p => (IReadOnlyList<Preset>)(p.IsGroup ? p.SubPresetIds : [])
                .Select(store.FindPreset).OfType<Preset>().Where(c => !c.IsGroup).Take(Preset.MaxSubPresets).ToList())
            .ToList();

        DonutItem ItemOf(Preset p, bool caption = true)
        {
            var reason = availability.Check(p, formats);
            return new DonutItem
            {
                Id = p.Id,
                Label = caption ? loc.DisplayName(p) : loc.RingName(p),
                Caption = !caption ? null
                    : p.TargetFormat == BuiltInData.SameAsSource
                        ? string.Join("/", formats.Select(f => f.DisplayName).Take(2))
                        : FormatLabels.NameOf(loc, p.TargetFormat),
                IsEnabled = reason is null,
                DisabledReason = reason,
            };
        }

        _allItems = _allPresets.Select((p, i) =>
        {
            if (!p.IsGroup)
                return ItemOf(p);
            var children = _allSubPresets[i].Select(c => ItemOf(c, caption: false)).ToList();
            return new DonutItem
            {
                Id = p.Id,
                Label = loc.DisplayName(p),
                Caption = loc.Format("donut.group_caption", children.Count),
                IsEnabled = children.Any(c => c.IsEnabled),
                DisabledReason = children.Count == 0 ? loc["donut.group_empty"]
                    : children.Select(c => c.DisabledReason).FirstOrDefault(r => r is not null),
                Children = children,
            };
        }).ToList();

        CurrentPage = 0;
        CalculatePages();
        ApplyPage();

        CenterTitle = formats.Count == 1
            ? loc.Format("donut.files_same", files.Count, formats[0].DisplayName)
            : loc.Format("donut.files_mixed", files.Count);
        UpdateCenterSubtitle();
    }

    private void CalculatePages()
    {
        if (_allItems.Count <= MaxPageSlices)
        {
            TotalPages = 1;
            return;
        }

        // Page 0: (MaxPageSlices - 1) items + 1 Next item
        var remaining = _allItems.Count - (MaxPageSlices - 1);
        var pages = 1;
        while (remaining > 0)
        {
            pages++;
            if (remaining <= MaxPageSlices - 1)
                break;
            remaining -= (MaxPageSlices - 2); // Middle pages have both Prev and Next
        }
        TotalPages = Math.Max(1, pages);
    }

    private void ApplyPage()
    {
        if (TotalPages <= 1 || _allItems.Count <= MaxPageSlices)
        {
            Items = _allItems;
            Presets = _allPresets;
            _subPresets = _allSubPresets;
            UpdateCenterSubtitle();
            return;
        }

        var pageItems = new List<DonutItem>();
        var pagePresets = new List<Preset>();
        var pageSubPresets = new List<IReadOnlyList<Preset>>();

        int startIndex;
        int takeCount;
        var hasPrev = CurrentPage > 0;
        var hasNext = CurrentPage < TotalPages - 1;

        if (CurrentPage == 0)
        {
            startIndex = 0;
            takeCount = MaxPageSlices - 1;
        }
        else
        {
            startIndex = (MaxPageSlices - 1) + (CurrentPage - 1) * (MaxPageSlices - 2);
            takeCount = hasNext ? MaxPageSlices - 2 : _allItems.Count - startIndex;
        }

        if (hasPrev)
        {
            pageItems.Add(new DonutItem
            {
                Id = "__nav_prev",
                Label = loc["donut.back"],
                Caption = $"{CurrentPage + 1}/{TotalPages}",
                IsNavigation = true,
                IsPrevPage = true,
                IsEnabled = true,
            });
            pagePresets.Add(new Preset { Id = "__nav_prev", Name = loc["donut.back"] });
            pageSubPresets.Add([]);
        }

        for (var i = 0; i < takeCount && startIndex + i < _allItems.Count; i++)
        {
            var idx = startIndex + i;
            pageItems.Add(_allItems[idx]);
            pagePresets.Add(_allPresets[idx]);
            pageSubPresets.Add(_allSubPresets[idx]);
        }

        if (hasNext)
        {
            pageItems.Add(new DonutItem
            {
                Id = "__nav_next",
                Label = loc["donut.more"],
                Caption = $"{CurrentPage + 1}/{TotalPages}",
                IsNavigation = true,
                IsNextPage = true,
                IsEnabled = true,
            });
            pagePresets.Add(new Preset { Id = "__nav_next", Name = loc["donut.more"] });
            pageSubPresets.Add([]);
        }

        Items = pageItems;
        Presets = pagePresets;
        _subPresets = pageSubPresets;
        UpdateCenterSubtitle();
    }

    private void UpdateCenterSubtitle()
    {
        if (TotalPages > 1)
            CenterSubtitle = loc.Format("donut.nav_hint", CurrentPage + 1, TotalPages);
        else
            CenterSubtitle = IsClickMode ? loc["donut.close_hint"] : null;
    }

    /// <summary>Updates the centre hint for the emphasised slice (and slice <paramref name="sub"/> of its group's ring).</summary>
    public void ShowHint(int index, int sub, bool centerHot)
    {
        if (centerHot)
        {
            CenterSubtitle = loc["donut.cancel"];
        }
        else if (index >= 0 && index < Items.Count && Items[index].IsNavigation)
        {
            CenterSubtitle = loc.Format("donut.nav_hint", CurrentPage + 1, TotalPages);
        }
        else if (index >= 0 && index < Items.Count && Items[index].Children is { Count: > 0 } children &&
                 (sub >= 0 || Items[index].IsEnabled))
        {
            CenterSubtitle = sub >= 0 && sub < children.Count
                ? children[sub].IsEnabled
                    ? loc.Format(IsClickMode ? "donut.click_to" : "donut.drop_to", $"{Items[index].Label} · {children[sub].Label}")
                    : children[sub].DisabledReason
                : loc["donut.pick_in_group"];
        }
        else if (index >= 0 && index < Items.Count)
        {
            CenterSubtitle = Items[index].IsEnabled
                ? loc.Format(IsClickMode ? "donut.click_to" : "donut.drop_to", Items[index].Label)
                : Items[index].DisabledReason;
        }
        else
        {
            UpdateCenterSubtitle();
        }
    }

    /// <summary>Click mode, before the window is placed: room for a group's ring only if this donut has a group.</summary>
    public void FitToThisDonut() => ReserveSubRing = Items.Any(item => item.IsGroup);

    private void ApplyAppearance()
    {
        OuterRadius = Math.Clamp(store.Settings.Donut.OuterRadius, 100, 240);
        HoleRatio = store.Settings.Donut.HoleRatio;
        SliceOpacity = store.Settings.Donut.Opacity;
    }
}
