using Avalonia.Controls;
using Avalonia.Interactivity;
using SchemaViz.Gui.ViewModels;

namespace SchemaViz.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnTestConnectionClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.TestConnectionAsync();
    }
}
