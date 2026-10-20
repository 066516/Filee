using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Filee.Setup.ViewModels;

namespace Filee.Setup.Views;

public partial class SetupWindow : Window
{
    private readonly SetupViewModel _model;
    private bool _closing;

    /// <summary>For the XAML previewer.</summary>
    public SetupWindow() : this(null!, false)
    {
    }

    /// <param name="silent">/SILENT: install at once, show only the progress and close when done.</param>
    public SetupWindow(SetupViewModel model, bool silent)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        if (model is null)
            return;
        model.PickFolder = PickFolderAsync;
        model.CloseRequested += (_, _) =>
        {
            _closing = true;
            Close();
        };
        // Installing can't be stopped halfway: the window stays until the files are in place (or back).
        Closing += (_, e) =>
        {
            if (_closing)
                return;
            if (model.IsProgress)
                e.Cancel = true;
            else
                model.WindowClosing();
        };
        Opened += async (_, _) =>
        {
            if (silent)
            {
                if (model.IsUninstall)
                    await model.UninstallAsync();
                else
                    await model.InstallAsync();
                SilentFinished?.Invoke(this, EventArgs.Empty);
                _closing = true;
                Close();
                return;
            }
            (model.IsUninstall ? (Control)NextButton : LanguageBox).Focus();
        };
    }

    /// <summary>A /SILENT run ended (before the window closes): Program shows an error message if there was one.</summary>
    public event EventHandler? SilentFinished;

    private async Task<string?> PickFolderAsync(string current)
    {
        IStorageFolder? start = null;
        try
        {
            var existing = current;
            while (!string.IsNullOrEmpty(existing) && !Directory.Exists(existing))
                existing = Path.GetDirectoryName(existing);
            if (!string.IsNullOrEmpty(existing))
                start = await StorageProvider.TryGetFolderFromPathAsync(existing);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
        }
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = start,
            Title = Title,
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
