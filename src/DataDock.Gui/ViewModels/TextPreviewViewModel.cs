namespace DataDock.Gui.ViewModels;

public sealed class TextPreviewViewModel
{
    public TextPreviewViewModel(string title, string content)
    {
        Title = title;
        Content = content;
    }

    public string Title { get; }
    public string Content { get; }
}
