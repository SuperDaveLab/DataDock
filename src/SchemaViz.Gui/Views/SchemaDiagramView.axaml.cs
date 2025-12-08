using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SchemaViz.Gui.ViewModels.Diagram;

namespace SchemaViz.Gui.Views;

public partial class SchemaDiagramView : UserControl
{
    private TableNodeViewModel? _draggingNode;
    private Point _dragStartPointer;
    private double _dragStartX;
    private double _dragStartY;

    private bool _isPanning;
    private Point _panStartPointer;
    private double _panStartOffsetX;
    private double _panStartOffsetY;

    public SchemaDiagramView()
    {
        InitializeComponent();
    }

    private Control? DiagramCanvasElement => this.FindControl<Control>("DiagramCanvas");

    private SchemaDiagramViewModel? Diagram => DataContext as SchemaDiagramViewModel;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed)
        {
            return;
        }

        if (Diagram is null || sender is not Control control)
        {
            return;
        }

        if (control.DataContext is not TableNodeViewModel node)
        {
            return;
        }

        Diagram.SelectTable(node);
        _draggingNode = node;
        _dragStartPointer = DiagramCanvasElement is { } canvas
            ? e.GetPosition(canvas)
            : e.GetPosition(this);
        _dragStartX = node.X;
        _dragStartY = node.Y;
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnNodePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingNode is null || Diagram is null)
        {
            return;
        }

        var current = DiagramCanvasElement is { } canvas
            ? e.GetPosition(canvas)
            : e.GetPosition(this);
        var deltaX = (current.X - _dragStartPointer.X) / Diagram.Zoom;
        var deltaY = (current.Y - _dragStartPointer.Y) / Diagram.Zoom;
        _draggingNode.X = _dragStartX + deltaX;
        _draggingNode.Y = _dragStartY + deltaY;
        e.Handled = true;
    }

    private void OnNodePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingNode is null)
        {
            return;
        }

        e.Pointer.Capture(null);
        _draggingNode = null;
        e.Handled = true;
    }

    private void OnNodeSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not TableNodeViewModel node)
        {
            return;
        }

        node.Width = e.NewSize.Width;
        node.Height = e.NewSize.Height;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Diagram is null || DiagramCanvasElement is null)
        {
            return;
        }

        var props = e.GetCurrentPoint(DiagramCanvasElement).Properties;
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStartPointer = e.GetPosition(DiagramCanvasElement);
            _panStartOffsetX = Diagram.OffsetX;
            _panStartOffsetY = Diagram.OffsetY;
            e.Pointer.Capture(DiagramCanvasElement);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            Diagram.SelectTable(null);
        }
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || Diagram is null || DiagramCanvasElement is null)
        {
            return;
        }

        var current = e.GetPosition(DiagramCanvasElement);
        var delta = current - _panStartPointer;
        Diagram.OffsetX = _panStartOffsetX + delta.X;
        Diagram.OffsetY = _panStartOffsetY + delta.Y;
        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            e.Pointer.Capture(null);
            _isPanning = false;
            e.Handled = true;
        }
    }
}
