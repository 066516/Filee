// State for the Excel → PDF print studio: a non-destructive plan per sheet and previews rendered by the same
// LibreOffice path as the final PDF.

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Conversion;
using Filee.Core.History;
using Filee.Core.Localization;
using Filee.Core.Settings;
using Filee.Engines.Office.Sheets;
using Microsoft.Extensions.Logging;

namespace Filee.App.ViewModels;

public sealed record SpreadsheetChoice<T>(T Value, string Label);

public sealed class SpreadsheetPreviewPage(int number, Bitmap image) : IDisposable
{
    public int Number { get; } = number;
    public Bitmap Image { get; } = image;
    public void Dispose() => Image.Dispose();
}

public sealed partial class SpreadsheetSheetPlan : ObservableObject
{
    private readonly Action _changed;
    private readonly ILocalizer _loc;
    public SpreadsheetSheetPlan(SpreadsheetSheetInfo sheet, ILocalizer loc, Action changed,
        SpreadsheetPrintRegion? initial = null, string? name = null)
    {
        Sheet = sheet;
        _loc = loc;
        _changed = changed;
        Name = name ?? sheet.Name;
        var source = initial ?? SpreadsheetPrintRegion.FromSource(sheet);
        _range = source.Range;
        _paper = source.Paper;
        _orientation = source.Orientation;
        _scaleMode = source.ScaleMode;
        _scalePercent = source.ScalePercent;
        _fitWidth = source.FitWidth;
        _fitHeight = source.FitHeight;
        _order = source.Order;
        _marginLeftMm = source.MarginLeftMm;
        _marginRightMm = source.MarginRightMm;
        _marginTopMm = source.MarginTopMm;
        _marginBottomMm = source.MarginBottomMm;
        _keepManualBreaks = source.KeepManualBreaks;
    }

    public SpreadsheetSheetInfo Sheet { get; }
    public string Name { get; }
    public string SourceSummary => $"{Sheet.UsedRange} · {Sheet.UsedRows} × {Sheet.UsedColumns}";
    public bool HasWarning => ScaleMode == SpreadsheetScaleMode.Source
        && (Sheet.HasSuspiciousScale || Sheet.HasWideUnboundedRange);
    public string Warning => !HasWarning ? "" : Sheet.HasSuspiciousScale
        ? _loc.Format("sheet_studio.warning_scale", Sheet.PageSetup.ScalePercent ?? 0)
        : Sheet.HasWideUnboundedRange ? _loc["sheet_studio.warning_wide"] : "";

    [ObservableProperty] private bool _included = true;
    [ObservableProperty] private string _range;
    [ObservableProperty] private SpreadsheetPaper _paper;
    [ObservableProperty] private SpreadsheetPageOrientation _orientation;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning), nameof(Warning))]
    private SpreadsheetScaleMode _scaleMode;
    [ObservableProperty] private int _scalePercent;
    [ObservableProperty] private int _fitWidth;
    [ObservableProperty] private int _fitHeight;
    [ObservableProperty] private SpreadsheetPageOrder _order;
    [ObservableProperty] private double _marginLeftMm;
    [ObservableProperty] private double _marginRightMm;
    [ObservableProperty] private double _marginTopMm;
    [ObservableProperty] private double _marginBottomMm;
    [ObservableProperty] private bool _includeHidden;
    [ObservableProperty] private bool _keepManualBreaks;

    partial void OnIncludedChanged(bool value) => _changed();
    partial void OnRangeChanged(string value) => _changed();
    partial void OnPaperChanged(SpreadsheetPaper value) => _changed();
    partial void OnOrientationChanged(SpreadsheetPageOrientation value) => _changed();
    partial void OnScaleModeChanged(SpreadsheetScaleMode value) => _changed();
    partial void OnScalePercentChanged(int value) => _changed();
    partial void OnFitWidthChanged(int value) => _changed();
    partial void OnFitHeightChanged(int value) => _changed();
    partial void OnOrderChanged(SpreadsheetPageOrder value) => _changed();
    partial void OnMarginLeftMmChanged(double value) => _changed();
    partial void OnMarginRightMmChanged(double value) => _changed();
    partial void OnMarginTopMmChanged(double value) => _changed();
    partial void OnMarginBottomMmChanged(double value) => _changed();
    partial void OnIncludeHiddenChanged(bool value) => _changed();
    partial void OnKeepManualBreaksChanged(bool value) => _changed();

    public SpreadsheetPrintRegion ToRegion() => new(Sheet, Range.Trim(), Paper, Orientation, ScaleMode,
        ScalePercent, FitWidth, FitHeight, Order, MarginLeftMm, MarginRightMm, MarginTopMm, MarginBottomMm,
        IncludeHidden, KeepManualBreaks);
}

public sealed partial class SpreadsheetPrintStudioViewModel : ObservableObject, IDisposable
{
    private readonly SpreadsheetPrintService _service;
    private readonly ILocalizer _loc;
    private readonly UserDataStore _store;
    private readonly ILogger? _log;
    private readonly SpreadsheetWorkbookInfo _workbook;
    private CancellationTokenSource? _previewCancellation;
    private SpreadsheetRenderedRegion? _rendered;
    private bool _closed;

    public SpreadsheetPrintStudioViewModel(string file, SpreadsheetPrintService service, ILocalizer loc, UserDataStore store,
        ILogger<SpreadsheetPrintStudioViewModel>? log = null)
    {
        _service = service;
        _loc = loc;
        _store = store;
        _log = log;
        _workbook = service.Inspect(file);
        Papers = SpreadsheetPaper.All.Select(paper => new SpreadsheetChoice<SpreadsheetPaper>(paper,
            paper == SpreadsheetPaper.Source ? loc["sheet_studio.from_workbook"] : paper.Name)).ToArray();
        Orientations =
        [
            new(SpreadsheetPageOrientation.Source, loc["sheet_studio.from_workbook"]),
            new(SpreadsheetPageOrientation.Portrait, loc["sheet_studio.portrait"]),
            new(SpreadsheetPageOrientation.Landscape, loc["sheet_studio.landscape"]),
        ];
        ScaleModes =
        [
            new(SpreadsheetScaleMode.Source, loc["sheet_studio.from_workbook"]),
            new(SpreadsheetScaleMode.FitToPages, loc["sheet_studio.fit_pages"]),
            new(SpreadsheetScaleMode.Percent, loc["sheet_studio.exact_scale"]),
        ];
        PageOrders =
        [
            new(SpreadsheetPageOrder.Source, loc["sheet_studio.from_workbook"]),
            new(SpreadsheetPageOrder.DownThenOver, loc["sheet_studio.down_over"]),
            new(SpreadsheetPageOrder.OverThenDown, loc["sheet_studio.over_down"]),
        ];
        FileName = Path.GetFileName(file);
        foreach (var sheet in _workbook.Sheets.Where(sheet => !sheet.IsHidden))
            Sheets.Add(new SpreadsheetSheetPlan(sheet, loc, SchedulePreview));
        SelectedSheet = Sheets.FirstOrDefault();
        if (SelectedSheet is not null)
            SchedulePreview();
    }

    public string FileName { get; }
    public ObservableCollection<SpreadsheetSheetPlan> Sheets { get; } = [];
    public ObservableCollection<SpreadsheetPreviewPage> Pages { get; } = [];
    public IReadOnlyList<SpreadsheetChoice<SpreadsheetPaper>> Papers { get; private set; } = [];
    public IReadOnlyList<SpreadsheetChoice<SpreadsheetPageOrientation>> Orientations { get; private set; } = [];
    public IReadOnlyList<SpreadsheetChoice<SpreadsheetScaleMode>> ScaleModes { get; private set; } = [];
    public IReadOnlyList<SpreadsheetChoice<SpreadsheetPageOrder>> PageOrders { get; private set; } = [];

    [ObservableProperty] private SpreadsheetSheetPlan? _selectedSheet;
    [ObservableProperty] private SpreadsheetPreviewPage? _selectedPage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private double _exportProgress;

    public bool CanExport => !IsBusy && !IsExporting && Sheets.Any(sheet => sheet.Included);

    partial void OnSelectedSheetChanged(SpreadsheetSheetPlan? value) => SchedulePreview();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanExport));
    partial void OnIsExportingChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    [RelayCommand]
    private void UseContentRange()
    {
        if (SelectedSheet is { } selected)
            selected.Range = selected.Sheet.UsedRange;
    }

    [RelayCommand]
    private void ResetSheet()
    {
        if (SelectedSheet is not { } selected)
            return;
        var source = SpreadsheetPrintRegion.FromSource(selected.Sheet);
        selected.Range = source.Range;
        selected.Paper = source.Paper;
        selected.Orientation = source.Orientation;
        selected.ScaleMode = source.ScaleMode;
        selected.ScalePercent = source.ScalePercent;
        selected.FitWidth = source.FitWidth;
        selected.FitHeight = source.FitHeight;
        selected.Order = source.Order;
        selected.MarginLeftMm = source.MarginLeftMm;
        selected.MarginRightMm = source.MarginRightMm;
        selected.MarginTopMm = source.MarginTopMm;
        selected.MarginBottomMm = source.MarginBottomMm;
        selected.IncludeHidden = source.IncludeHidden;
        selected.KeepManualBreaks = source.KeepManualBreaks;
    }

    [RelayCommand]
    private void ApplySuggestedFix()
    {
        if (SelectedSheet is not { } selected)
            return;
        selected.Range = selected.Sheet.UsedRange;
        selected.ScaleMode = SpreadsheetScaleMode.FitToPages;
        selected.FitWidth = selected.Sheet.UsedColumns > 60 ? 2 : 1;
        selected.FitHeight = 0;
        if (selected.Sheet.UsedColumns > 20)
            selected.Orientation = SpreadsheetPageOrientation.Landscape;
        if (selected.Paper == SpreadsheetPaper.Source)
            selected.Paper = selected.Sheet.PageSetup.Paper == SpreadsheetPaper.Source
                ? SpreadsheetPaper.A4 : selected.Sheet.PageSetup.Paper;
        selected.KeepManualBreaks = false;
    }

    [RelayCommand]
    private void AddRegion()
    {
        if (SelectedSheet is not { } selected)
            return;
        var number = Sheets.Count(item => item.Sheet.Index == selected.Sheet.Index) + 1;
        var duplicate = new SpreadsheetSheetPlan(selected.Sheet, _loc, SchedulePreview, selected.ToRegion(),
            _loc.Format("sheet_studio.region_name", selected.Sheet.Name, number));
        var index = Sheets.IndexOf(selected) + 1;
        Sheets.Insert(index, duplicate);
        SelectedSheet = duplicate;
    }

    [RelayCommand]
    private void RemoveRegion()
    {
        if (SelectedSheet is not { } selected || Sheets.Count == 1)
            return;
        var index = Sheets.IndexOf(selected);
        Sheets.Remove(selected);
        SelectedSheet = Sheets[Math.Min(index, Sheets.Count - 1)];
    }

    [RelayCommand]
    private void MoveRegionUp() => MoveRegion(-1);

    [RelayCommand]
    private void MoveRegionDown() => MoveRegion(1);

    private void MoveRegion(int direction)
    {
        if (SelectedSheet is not { } selected)
            return;
        var index = Sheets.IndexOf(selected);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= Sheets.Count)
            return;
        Sheets.Move(index, target);
        SelectedSheet = selected;
    }

    public async Task ExportAsync(string outputPath)
    {
        if (IsExporting)
            return;
        var regions = Sheets.Where(sheet => sheet.Included).Select(sheet => sheet.ToRegion()).ToArray();
        if (regions.Any(region => !SpreadsheetPrintInspector.IsValidRange(region.Range)))
        {
            Status = _loc["sheet_studio.invalid_range"];
            return;
        }
        IsExporting = true;
        ExportProgress = 0;
        Status = _loc["sheet_studio.exporting"];
        try
        {
            var progress = new Progress<double>(value => ExportProgress = value * 100);
            await _service.ExportAsync(_workbook, regions, outputPath, progress);
            Status = _loc.Format("sheet_studio.exported", Path.GetFileName(outputPath));
            try
            {
                _store.AddHistory(new HistoryEntry
                {
                    JobId = Guid.NewGuid().ToString("N")[..12],
                    FinishedAt = DateTime.Now,
                    PresetId = "tool-sheet-studio",
                    PresetName = _loc["preset.sheet_studio"],
                    State = JobState.Completed,
                    Sources = [_workbook.Path],
                    Outputs = [outputPath],
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The PDF is already safely in place; a history write must not turn that success into an error.
                _log?.LogWarning(ex, "Could not record spreadsheet studio export in history");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log?.LogError(ex, "Could not export spreadsheet print plan for {File}", _workbook.Path);
            Status = ex.Message;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void SchedulePreview()
    {
        OnPropertyChanged(nameof(CanExport));
        if (_closed || SelectedSheet is null)
            return;
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        IsBusy = false;
        _previewCancellation = new CancellationTokenSource();
        _ = PreviewAfterDelayAsync(SelectedSheet, _previewCancellation.Token);
    }

    private async Task PreviewAfterDelayAsync(SpreadsheetSheetPlan sheet, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            if (!SpreadsheetPrintInspector.IsValidRange(sheet.Range))
            {
                Status = _loc["sheet_studio.invalid_range"];
                return;
            }
            IsBusy = true;
            Status = _loc["sheet_studio.rendering"];
            var rendered = await _service.RenderAsync(_workbook, sheet.ToRegion(), cancellationToken);
            if (_closed || cancellationToken.IsCancellationRequested)
            {
                SpreadsheetPrintService.Release(rendered);
                return;
            }
            ClearPages();
            SpreadsheetPrintService.Release(_rendered);
            _rendered = rendered;
            foreach (var page in rendered.Pages)
            {
                using var stream = new MemoryStream(page.Png, writable: false);
                Pages.Add(new SpreadsheetPreviewPage(page.PageNumber, new Bitmap(stream)));
            }
            SelectedPage = Pages.FirstOrDefault();
            Status = _loc.Format("sheet_studio.page_count", Pages.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log?.LogWarning(ex, "Could not render spreadsheet preview for {File}", _workbook.Path);
            Status = ex.Message;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                IsBusy = false;
        }
    }

    private void ClearPages()
    {
        foreach (var page in Pages) page.Dispose();
        Pages.Clear();
    }

    public void Dispose()
    {
        _closed = true;
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        ClearPages();
        SpreadsheetPrintService.Release(_rendered);
    }
}
