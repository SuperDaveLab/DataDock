using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDock.Gui.ViewModels;

namespace DataDock.Gui.Views;

public partial class TextPreviewWindow : Window
{
    public TextPreviewWindow()
    {
        InitializeComponent();
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TextPreviewViewModel vm)
        {
            return;
        }

        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(vm.Content);
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
