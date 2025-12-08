using System.Collections.Generic;

namespace SchemaViz.Gui.ViewModels.Diagram;

public sealed class TableNodeViewModel : ViewModelBase
{
    private double _x;
    private double _y;
    private bool _isPrimaryTable;
    private bool _isSelected;
    private double _width = 220;
    private double _height = 140;

    public TableNodeViewModel(string schema, string name, IEnumerable<string> columns)
    {
        Schema = schema;
        Name = name;
        Columns = new List<string>(columns);
    }

    public string Schema { get; }

    public string Name { get; }

    public string DisplayName => $"[{Schema}].[{Name}]";

    public IReadOnlyList<string> Columns { get; }

    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
            {
                OnPropertyChanged(nameof(CenterX));
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (SetProperty(ref _y, value))
            {
                OnPropertyChanged(nameof(CenterY));
            }
        }
    }

    public bool IsPrimaryTable
    {
        get => _isPrimaryTable;
        set => SetProperty(ref _isPrimaryTable, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public double Width
    {
        get => _width;
        set
        {
            if (SetProperty(ref _width, value))
            {
                OnPropertyChanged(nameof(CenterX));
            }
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (SetProperty(ref _height, value))
            {
                OnPropertyChanged(nameof(CenterY));
            }
        }
    }

    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}
