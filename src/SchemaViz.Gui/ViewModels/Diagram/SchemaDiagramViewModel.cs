using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SchemaViz.Gui.Commands;

namespace SchemaViz.Gui.ViewModels.Diagram;

public sealed class SchemaDiagramViewModel : ViewModelBase
{
    private double _zoom = 1.0;
    private double _offsetX;
    private double _offsetY;
    private TableNodeViewModel? _selectedTable;

    public SchemaDiagramViewModel()
    {
        ResetViewCommand = new RelayCommand(_ => ResetView());
    }

    public ObservableCollection<TableNodeViewModel> Tables { get; } = new();

    public ObservableCollection<RelationshipViewModel> Relationships { get; } = new();

    public ICommand ResetViewCommand { get; }

    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, Math.Clamp(value, 0.1, 4.0));
    }

    public double OffsetX
    {
        get => _offsetX;
        set => SetProperty(ref _offsetX, value);
    }

    public double OffsetY
    {
        get => _offsetY;
        set => SetProperty(ref _offsetY, value);
    }

    public TableNodeViewModel? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (_selectedTable == value)
            {
                return;
            }

            if (_selectedTable is not null)
            {
                _selectedTable.IsSelected = false;
            }

            _selectedTable = value;

            if (_selectedTable is not null)
            {
                _selectedTable.IsSelected = true;
            }

            OnPropertyChanged();
        }
    }

    public void ResetView()
    {
        Zoom = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }

    public void LoadDiagram(
        IEnumerable<TableNodeViewModel> tables,
        IEnumerable<RelationshipViewModel> relationships)
    {
        Tables.Clear();
        foreach (var table in tables)
        {
            Tables.Add(table);
        }

        Relationships.Clear();
        foreach (var relationship in relationships)
        {
            Relationships.Add(relationship);
        }

        SelectedTable = Tables.FirstOrDefault();
        ApplyAutoLayout();
    }

    public void ApplyAutoLayout(double horizontalSpacing = 260, double verticalSpacing = 200)
    {
        if (Tables.Count == 0)
        {
            return;
        }

        var columns = (int)Math.Ceiling(Math.Sqrt(Tables.Count));
        var index = 0;

        foreach (var table in Tables)
        {
            var row = index / columns;
            var column = index % columns;
            table.X = column * horizontalSpacing;
            table.Y = row * verticalSpacing;
            index++;
        }
    }

    public void SelectTable(TableNodeViewModel? node)
    {
        SelectedTable = node;
    }
}
