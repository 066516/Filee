// The files in a merged PDF follow the order reviewed here. Moving a row changes only this copy of the selection.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Filee.App.ViewModels;

public sealed class MergeSource(string path)
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
}

public sealed partial class MergeOrderViewModel : ObservableObject
{
    public MergeOrderViewModel(IReadOnlyList<string> files)
    {
        Files = new(files.Select(file => new MergeSource(file)));
        SelectedFile = Files.FirstOrDefault();
    }

    public ObservableCollection<MergeSource> Files { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private MergeSource? _selectedFile;

    private bool CanMoveUp() => SelectedFile is not null && Files.IndexOf(SelectedFile) > 0;
    private bool CanMoveDown() => SelectedFile is not null && Files.IndexOf(SelectedFile) is var index && index >= 0 && index < Files.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private void Move(int direction)
    {
        if (SelectedFile is not { } selected) return;
        var index = Files.IndexOf(selected);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= Files.Count) return;
        Files.Move(index, target);
        // ListBox can clear its selection while processing a collection move.
        SelectedFile = selected;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<string> OrderedFiles => Files.Select(file => file.Path).ToArray();
}
