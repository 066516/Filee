using Avalonia.Controls;
using Avalonia.Interactivity;
using Filee.App.Services;
using Filee.App.ViewModels;

namespace Filee.App.Views;

/// <summary>Modal group editor. Returns true from ShowDialog when the user saved or deleted the group.</summary>
public partial class GroupEditorWindow : Window
{
    public GroupEditorWindow()
    {
        InitializeComponent();
        Motion.Track(this);
        Opened += (_, _) => NameBox.Focus();
    }

    /// <summary>Asks before deleting (set by WindowService; without it the group is deleted at once).</summary>
    public Func<string, Task<bool>>? Confirm { get; init; }

    /// <summary>True after the user saved or deleted (also available when the window was not shown as a dialog).</summary>
    public bool Changed { get; private set; }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GroupEditorViewModel vm || !vm.Apply())
            return;
        Changed = true;
        Close(true);
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GroupEditorViewModel vm)
            return;
        if (Confirm is not null && !await Confirm(vm.DeleteConfirmation))
            return;
        vm.Delete();
        Changed = true;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
