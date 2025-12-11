using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using SchemaViz.Core.SchemaExport;
using SchemaViz.Gui.Commands;

namespace SchemaViz.Gui.ViewModels.Diagram;

public sealed class SchemaDiagramViewModel : ViewModelBase
{
    public const double MinZoom = 0.15;
    public const double MaxZoom = 4.0;
    public const double MinCanvasWidth = 2000;
    public const double MinCanvasHeight = 1500;

    private double _zoom = 1.0;
    private double _offsetX;
    private double _offsetY;
    private TableNodeViewModel? _selectedTable;
    private double _canvasWidth = 4000;
    private double _canvasHeight = 2500;
    private readonly SchemaExportService _exportService = new();
    private readonly RelayCommand _exportSchemaCommand;
    private string? _databaseName;
    private string? _schemaFilter;

    public SchemaDiagramViewModel()
    {
        ResetViewCommand = new RelayCommand(_ => ResetView());
        _exportSchemaCommand = new RelayCommand(_ => ExportSchema(), _ => Tables.Count > 0);
        ExportSchemaCommand = _exportSchemaCommand;
        Relationships.CollectionChanged += OnRelationshipsCollectionChanged;
        Tables.CollectionChanged += OnTablesCollectionChanged;
    }

    public ObservableCollection<TableNodeViewModel> Tables { get; } = new();

    public ObservableCollection<RelationshipViewModel> Relationships { get; } = new();

    public ICommand ResetViewCommand { get; }

    public ICommand ExportSchemaCommand { get; }

    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom));
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
            OnPropertyChanged(nameof(HasSelectedTable));
            OnPropertyChanged(nameof(OutgoingRelationships));
            OnPropertyChanged(nameof(IncomingRelationships));
        }
    }

    public bool HasSelectedTable => SelectedTable is not null;

    public double CanvasWidth
    {
        get => _canvasWidth;
        set => SetProperty(ref _canvasWidth, Math.Max(MinCanvasWidth, value));
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        set => SetProperty(ref _canvasHeight, Math.Max(MinCanvasHeight, value));
    }

    public string? DatabaseName
    {
        get => _databaseName;
        set => SetProperty(ref _databaseName, value);
    }

    public string? SchemaFilter
    {
        get => _schemaFilter;
        set => SetProperty(ref _schemaFilter, value);
    }

    public event EventHandler<SchemaExportRequestedEventArgs>? SchemaExportRequested;

    public void ResetView()
    {
        // Reset zoom first, then center will be handled by zoom change handler
        Zoom = 1.0;
        // Note: Offset will be set by the view's zoom handler to center the canvas
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

    public IEnumerable<RelationshipViewModel> OutgoingRelationships
        => SelectedTable is null
            ? Enumerable.Empty<RelationshipViewModel>()
            : Relationships.Where(r => ReferenceEquals(r.To, SelectedTable));

    public IEnumerable<RelationshipViewModel> IncomingRelationships
        => SelectedTable is null
            ? Enumerable.Empty<RelationshipViewModel>()
            : Relationships.Where(r => ReferenceEquals(r.From, SelectedTable));

    public void ApplyAutoLayout(double horizontalSpacing = 0, double verticalSpacing = 0)
    {
        if (Tables.Count == 0)
        {
            return;
        }

        var maxWidth = Tables.Max(table => table.Width);
        var maxHeight = Tables.Max(table => table.Height);
        if (maxWidth <= 0)
        {
            maxWidth = 220;
        }

        if (maxHeight <= 0)
        {
            maxHeight = 140;
        }

        var spacingX = horizontalSpacing > 0 ? horizontalSpacing : maxWidth + 80;
        var spacingY = verticalSpacing > 0 ? verticalSpacing : maxHeight + 80;
        var columns = (int)Math.Ceiling(Math.Sqrt(Tables.Count));
        var index = 0;

        foreach (var table in Tables)
        {
            var row = index / columns;
            var column = index % columns;
            table.X = column * spacingX;
            table.Y = row * spacingY;
            index++;
        }
    }

    public void SelectTable(TableNodeViewModel? node)
    {
        SelectedTable = node;
    }

    private void OnRelationshipsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(OutgoingRelationships));
        OnPropertyChanged(nameof(IncomingRelationships));
    }

    private void OnTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _exportSchemaCommand.RaiseCanExecuteChanged();
    }

    private void ExportSchema()
    {
        var export = _exportService.CreateExport(DatabaseName, SchemaFilter, Tables, Relationships);
        var json = _exportService.ToJson(export);
        var suggestedFileName = BuildSuggestedFileName();
        SchemaExportRequested?.Invoke(this, new SchemaExportRequestedEventArgs(json, suggestedFileName));
    }

    private string BuildSuggestedFileName()
    {
        var baseName = string.IsNullOrWhiteSpace(DatabaseName)
            ? "schema-export"
            : $"{DatabaseName}-schema";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "schema-export" : sanitized;
    }
}
