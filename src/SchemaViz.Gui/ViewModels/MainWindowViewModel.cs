using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using DataDock.Core.Models;
using DataDock.Core.Services;
using DataDock.Services;
using Microsoft.Data.SqlClient;
using SchemaViz.Gui.Commands;
using SchemaViz.Gui.Models;
using SchemaViz.Gui.Services;
using SchemaViz.Gui.ViewModels.Diagram;

namespace SchemaViz.Gui.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int DiagramTableLimit = 40;
    private readonly SchemaMetadataService _metadataService = new();
    private readonly ConnectionSettings _defaultSettings;
    private readonly ConnectionProfileStore _profileStore = new();
    private readonly RelayCommand _removeConnectionCommand;
    private string? _activeConnectionString;

    private string _databaseHost = "localhost";
    private string _databasePort = "1433";
    private string _databaseName = "master";
    private string _databaseSchema = "dbo";
    private bool _useIntegratedSecurity = true;
    private bool _trustServerCertificate = true;
    private string _databaseUser = string.Empty;
    private string _databasePassword = string.Empty;
    private bool _isBusy;
    private int _busyCounter;
    private string _statusMessage = "Click Test Connection to load tables.";
    private TableListItem? _selectedTable;
    private int _tableLoadVersion;
    private int _selectionSyncSuppressionCount;
    private ConnectionProfile? _selectedConnection;

    public MainWindowViewModel()
    {
        Tables = new ObservableCollection<TableListItem>();
        Columns = new ObservableCollection<ColumnDisplay>();
        SavedConnections = new ObservableCollection<ConnectionProfile>();
        Diagram = new SchemaDiagramViewModel();
        Diagram.PropertyChanged += OnDiagramPropertyChanged;

        Tables.CollectionChanged += OnTablesChanged;
        Columns.CollectionChanged += OnColumnsChanged;
        SavedConnections.CollectionChanged += OnSavedConnectionsChanged;

        _removeConnectionCommand = new RelayCommand(_ => RemoveSelectedConnection(), _ => SelectedConnection is not null);
        RemoveSelectedConnectionCommand = _removeConnectionCommand;

        var appConfig = AppConfigLoader.Load();
        _defaultSettings = ConnectionSettingsResolver.Resolve(new ImportOptions(), new ImportProfile(), appConfig);
        ApplyDefaults(_defaultSettings);
        LoadSavedConnections();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TableListItem> Tables { get; }

    public ObservableCollection<ColumnDisplay> Columns { get; }

    public ObservableCollection<ConnectionProfile> SavedConnections { get; }

    public SchemaDiagramViewModel Diagram { get; }

    public ConnectionProfile? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (!SetProperty(ref _selectedConnection, value))
            {
                return;
            }

            _removeConnectionCommand.RaiseCanExecuteChanged();

            if (value is not null)
            {
                ApplyProfile(value);
            }
        }
    }

    public ICommand RemoveSelectedConnectionCommand { get; }

    public bool HasSavedConnections => SavedConnections.Count > 0;

    public string DatabaseHost
    {
        get => _databaseHost;
        set => SetProperty(ref _databaseHost, value);
    }

    public string DatabasePort
    {
        get => _databasePort;
        set => SetProperty(ref _databasePort, value);
    }

    public string DatabaseName
    {
        get => _databaseName;
        set => SetProperty(ref _databaseName, value);
    }

    public string DatabaseSchema
    {
        get => _databaseSchema;
        set => SetProperty(ref _databaseSchema, value);
    }

    public bool UseIntegratedSecurity
    {
        get => _useIntegratedSecurity;
        set
        {
            if (SetProperty(ref _useIntegratedSecurity, value))
            {
                OnPropertyChanged(nameof(IsSqlAuthEnabled));
            }
        }
    }

    public bool TrustServerCertificate
    {
        get => _trustServerCertificate;
        set => SetProperty(ref _trustServerCertificate, value);
    }

    public string DatabaseUser
    {
        get => _databaseUser;
        set => SetProperty(ref _databaseUser, value);
    }

    public string DatabasePassword
    {
        get => _databasePassword;
        set => SetProperty(ref _databasePassword, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanTestConnection));
                OnPropertyChanged(nameof(TableHintMessage));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSqlAuthEnabled => !UseIntegratedSecurity;

    public bool CanTestConnection => !IsBusy;

    public TableListItem? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (!SetProperty(ref _selectedTable, value))
            {
                return;
            }

            if (!IsSelectionSyncSuppressed)
            {
                WithSelectionSyncSuppressed(() => SyncDiagramSelectionFromList(value));
            }

            _ = LoadSelectedTableAsync(value);
            OnPropertyChanged(nameof(SelectedTableDisplay));
            OnPropertyChanged(nameof(SelectedTableDetails));
            OnPropertyChanged(nameof(TableHintMessage));
        }
    }

    public string SelectedTableDisplay => SelectedTable?.DisplayName ?? "No table selected";

    public string SelectedTableDetails
    {
        get
        {
            if (SelectedTable is null)
            {
                return "Select a table from the list to view its columns.";
            }

            var rowText = SelectedTable.RowCount.HasValue
                ? $"{SelectedTable.RowCount:N0} rows"
                : "Row count unavailable";

            var columnCount = Columns.Count;
            return $"{rowText} • {columnCount} column{(columnCount == 1 ? string.Empty : "s")}";
        }
    }

    public string TableHintMessage
    {
        get
        {
            if (IsBusy)
            {
                return "Working...";
            }

            if (Tables.Count == 0)
            {
                return "No tables loaded yet. Test the connection to fetch schemas.";
            }

            if (SelectedTable is null)
            {
                return "Select a table to view its metadata.";
            }

            if (Columns.Count == 0)
            {
                return "Loading column metadata...";
            }

            return string.Empty;
        }
    }

    public async Task TestConnectionAsync()
    {
        string connectionString;
        try
        {
            connectionString = BuildConnectionString();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        BeginWork("Testing connection...");
        try
        {
            await _metadataService.TestConnectionAsync(connectionString);
            StatusMessage = "Connection successful. Loading tables...";

            var tables = await _metadataService.GetTablesAsync(connectionString, NormalizeSchemaFilter());
            UpdateTables(tables);
            _activeConnectionString = connectionString;
            await BuildDiagramAsync(connectionString, tables);

            var status = tables.Count switch
            {
                0 => "Connection succeeded, but no tables were found for the selected schema.",
                1 => "Loaded 1 table.",
                _ => $"Loaded {tables.Count} tables."
            };

            if (tables.Count > DiagramTableLimit)
            {
                status += $" Diagram view limited to first {DiagramTableLimit} tables.";
            }

            StatusMessage = status;
            SaveCurrentConnectionProfile();
        }
        catch (Exception ex)
        {
            Tables.Clear();
            Columns.Clear();
            _activeConnectionString = null;
            Diagram.LoadDiagram(Array.Empty<TableNodeViewModel>(), Array.Empty<RelationshipViewModel>());
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            EndWork();
        }
    }

    private void UpdateTables(IReadOnlyCollection<TableListItem> tables)
    {
        Tables.Clear();
        foreach (var table in tables)
        {
            Tables.Add(table);
        }

        OnPropertyChanged(nameof(TableHintMessage));

        SelectedTable = Tables.FirstOrDefault();
    }

    private async Task LoadSelectedTableAsync(TableListItem? table)
    {
        if (table is null)
        {
            Columns.Clear();
            return;
        }

        if (string.IsNullOrWhiteSpace(_activeConnectionString))
        {
            StatusMessage = "Test the connection before loading table schemas.";
            return;
        }

        var localVersion = ++_tableLoadVersion;
        BeginWork($"Loading {table.DisplayName}...");
        try
        {
            var tableInfo = await _metadataService.GetTableSchemaAsync(
                _activeConnectionString,
                table.Schema,
                table.Name);

            if (localVersion != _tableLoadVersion)
            {
                return;
            }

            Columns.Clear();
            foreach (var column in tableInfo.Columns)
            {
                Columns.Add(new ColumnDisplay(
                    column.Name,
                    BuildDataType(column),
                    BuildLengthDisplay(column),
                    column.IsNullable));
            }
            StatusMessage = $"Loaded {Columns.Count} column{(Columns.Count == 1 ? string.Empty : "s")} for {table.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load schema: {ex.Message}";
        }
        finally
        {
            EndWork();
        }

        OnPropertyChanged(nameof(SelectedTableDetails));
        OnPropertyChanged(nameof(TableHintMessage));
    }

    private static string BuildDataType(DbColumnInfo column)
    {
        if (column.NumericPrecision.HasValue)
        {
            if (column.NumericScale.HasValue)
            {
                return $"{column.DataType} ({column.NumericPrecision},{column.NumericScale})";
            }

            return $"{column.DataType} ({column.NumericPrecision})";
        }

        if (column.MaxLength.HasValue && column.MaxLength > 0)
        {
            return $"{column.DataType} ({column.MaxLength})";
        }

        return column.DataType;
    }

    private static string BuildLengthDisplay(DbColumnInfo column)
    {
        if (column.MaxLength.HasValue)
        {
            return column.MaxLength.Value == -1 ? "max" : column.MaxLength.Value.ToString();
        }

        if (column.NumericPrecision.HasValue)
        {
            return column.NumericScale.HasValue
                ? $"{column.NumericPrecision},{column.NumericScale}"
                : column.NumericPrecision.Value.ToString();
        }

        return "—";
    }

    private void ApplyDefaults(ConnectionSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Schema))
        {
            DatabaseSchema = settings.Schema;
        }

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            StatusMessage = "No default connection string found. Enter connection details to continue.";
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(settings.ConnectionString);

            if (!string.IsNullOrWhiteSpace(builder.DataSource))
            {
                var dataSource = builder.DataSource.Trim();
                var host = dataSource;
                var port = string.Empty;

                var commaIndex = dataSource.IndexOf(',', StringComparison.Ordinal);
                if (commaIndex >= 0)
                {
                    host = dataSource[..commaIndex];
                    port = dataSource[(commaIndex + 1)..];
                }

                DatabaseHost = host;
                if (!string.IsNullOrWhiteSpace(port))
                {
                    DatabasePort = port.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                DatabaseName = builder.InitialCatalog;
            }

            UseIntegratedSecurity = builder.IntegratedSecurity;
            TrustServerCertificate = builder.TrustServerCertificate;

            if (!UseIntegratedSecurity)
            {
                if (!string.IsNullOrWhiteSpace(builder.UserID))
                {
                    DatabaseUser = builder.UserID;
                }

                if (!string.IsNullOrEmpty(builder.Password))
                {
                    DatabasePassword = builder.Password;
                }
            }
        }
        catch
        {
            StatusMessage = "Unable to parse the default connection string. Please update datadock.config.json.";
        }
    }

    private string? NormalizeSchemaFilter()
    {
        var schema = DatabaseSchema?.Trim();
        return string.IsNullOrWhiteSpace(schema) ? _defaultSettings.Schema : schema;
    }

    private async Task BuildDiagramAsync(string connectionString, IReadOnlyCollection<TableListItem> tables)
    {
        if (tables.Count == 0)
        {
            Diagram.LoadDiagram(Array.Empty<TableNodeViewModel>(), Array.Empty<RelationshipViewModel>());
            return;
        }

        var limitedTables = tables.Take(DiagramTableLimit).ToList();
        var nodes = new List<TableNodeViewModel>();

        foreach (var table in limitedTables)
        {
            try
            {
                var tableInfo = await _metadataService.GetTableSchemaAsync(connectionString, table.Schema, table.Name);
                var columns = tableInfo.Columns
                    .Select(column => new TableColumnViewModel(column.Name, BuildDataType(column)));
                nodes.Add(new TableNodeViewModel(table.Schema, table.Name, columns));
            }
            catch
            {
                // Ignore tables that fail to load for diagram view.
            }
        }

        var diagramSchemaFilter = NormalizeSchemaFilter();
        var relationships = new List<RelationshipViewModel>();
        var lookup = nodes.ToDictionary(n => BuildTableKey(n.Schema, n.Name), StringComparer.OrdinalIgnoreCase);

        try
        {
            var foreignKeys = await _metadataService.GetForeignKeysAsync(connectionString, diagramSchemaFilter);
            foreach (var fk in foreignKeys)
            {
                if (lookup.TryGetValue(BuildTableKey(fk.ToSchema, fk.ToTable), out var parentNode) &&
                    lookup.TryGetValue(BuildTableKey(fk.FromSchema, fk.FromTable), out var childNode))
                {
                    relationships.Add(new RelationshipViewModel(
                        parentNode,
                        childNode,
                        fk.ConstraintName,
                        fk.ColumnLinks.ToList()));
                }
            }
        }
        catch
        {
            // Relationships are optional; ignore failures for now.
        }

        ApplyRelationshipColumnMetadata(relationships);

        WithSelectionSyncSuppressed(() =>
        {
            Diagram.LoadDiagram(nodes, relationships);
            SyncDiagramSelectionFromList(SelectedTable);
        });
    }

    private static void ApplyRelationshipColumnMetadata(IEnumerable<RelationshipViewModel> relationships)
    {
        foreach (var relationship in relationships)
        {
            foreach (var link in relationship.ColumnLinks)
            {
                var parentColumn = relationship.From.FindColumn(link.FromColumn);
                if (parentColumn is not null)
                {
                    parentColumn.IsPrimaryKey = true;
                }

                var childColumn = relationship.To.FindColumn(link.ToColumn);
                if (childColumn is not null)
                {
                    childColumn.IsForeignKey = true;
                }
            }
        }
    }

    private bool IsSelectionSyncSuppressed => _selectionSyncSuppressionCount > 0;

    private void WithSelectionSyncSuppressed(Action action)
    {
        _selectionSyncSuppressionCount++;
        try
        {
            action();
        }
        finally
        {
            _selectionSyncSuppressionCount--;
        }
    }

    private void SyncDiagramSelectionFromList(TableListItem? table)
    {
        var node = table is null ? null : FindNodeForTable(table);
        Diagram.SelectTable(node);
    }

    private TableNodeViewModel? FindNodeForTable(TableListItem table)
    {
        return Diagram.Tables.FirstOrDefault(node =>
            string.Equals(node.Schema, table.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Name, table.Name, StringComparison.OrdinalIgnoreCase));
    }

    private TableListItem? FindTableForNode(TableNodeViewModel node)
    {
        return Tables.FirstOrDefault(table =>
            string.Equals(table.Schema, node.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(table.Name, node.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SchemaDiagramViewModel.SelectedTable), StringComparison.Ordinal) ||
            sender is not SchemaDiagramViewModel diagram)
        {
            return;
        }

        if (IsSelectionSyncSuppressed)
        {
            return;
        }

        WithSelectionSyncSuppressed(() =>
        {
            var node = diagram.SelectedTable;
            if (node is null)
            {
                SelectedTable = null;
                return;
            }

            var matching = FindTableForNode(node);
            if (matching is null)
            {
                SelectedTable = null;
                return;
            }

            if (!ReferenceEquals(matching, SelectedTable))
            {
                SelectedTable = matching;
            }
        });
    }

    private void LoadSavedConnections()
    {
        var profiles = _profileStore.LoadProfiles();
        if (profiles.Count == 0)
        {
            return;
        }

        foreach (var profile in profiles)
        {
            SavedConnections.Add(profile);
        }

        OnPropertyChanged(nameof(HasSavedConnections));
    }

    private void OnSavedConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSavedConnections));
    }

    private void ApplyProfile(ConnectionProfile profile)
    {
        DatabaseSchema = profile.Schema;
        DatabaseHost = profile.Host;
        DatabasePort = profile.Port ?? string.Empty;
        DatabaseName = profile.Database;
        UseIntegratedSecurity = profile.UseIntegratedSecurity;
        TrustServerCertificate = profile.TrustServerCertificate;
    }

    private void SaveCurrentConnectionProfile()
    {
        var profile = BuildCurrentProfile();
        if (profile is null)
        {
            return;
        }

        var index = FindProfileIndex(profile);
        if (index >= 0)
        {
            SavedConnections[index] = profile;
        }
        else
        {
            SavedConnections.Add(profile);
        }

        PersistSavedConnections();
        SelectedConnection = profile;
    }

    private int FindProfileIndex(ConnectionProfile profile)
    {
        for (var i = 0; i < SavedConnections.Count; i++)
        {
            if (SavedConnections[i].TargetsSameDatabase(profile))
            {
                return i;
            }
        }

        return -1;
    }

    private ConnectionProfile? BuildCurrentProfile()
    {
        var host = DatabaseHost?.Trim();
        var database = DatabaseName?.Trim();
        var schema = DatabaseSchema?.Trim();

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(schema))
        {
            schema = "dbo";
        }

        var port = DatabasePort?.Trim();
        if (string.IsNullOrWhiteSpace(port))
        {
            port = null;
        }

        return new ConnectionProfile
        {
            Schema = schema!,
            Host = host!,
            Port = port,
            Database = database!,
            UseIntegratedSecurity = UseIntegratedSecurity,
            TrustServerCertificate = TrustServerCertificate
        };
    }

    private void PersistSavedConnections()
    {
        _profileStore.SaveProfiles(SavedConnections.ToList());
    }

    private void RemoveSelectedConnection()
    {
        if (SelectedConnection is null)
        {
            return;
        }

        var profile = SelectedConnection;
        SelectedConnection = null;
        SavedConnections.Remove(profile);
        PersistSavedConnections();
    }

    private static string BuildTableKey(string schema, string table)
        => $"{schema}.{table}";

    private string BuildConnectionString()
    {
        var host = DatabaseHost?.Trim();
        var database = DatabaseName?.Trim();

        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(database))
        {
            var dataSource = host!;
            var port = DatabasePort?.Trim();
            if (!string.IsNullOrWhiteSpace(port))
            {
                dataSource = $"{host},{port}";
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = database!,
                TrustServerCertificate = TrustServerCertificate
            };

            if (UseIntegratedSecurity)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(DatabaseUser) || string.IsNullOrWhiteSpace(DatabasePassword))
                {
                    throw new InvalidOperationException("Username and password are required for SQL authentication.");
                }

                builder.IntegratedSecurity = false;
                builder.UserID = DatabaseUser.Trim();
                builder.Password = DatabasePassword;
            }

            return builder.ConnectionString;
        }

        if (!string.IsNullOrWhiteSpace(_defaultSettings.ConnectionString))
        {
            return _defaultSettings.ConnectionString!;
        }

        throw new InvalidOperationException("Server host and database name are required.");
    }

    private void BeginWork(string? message = null)
    {
        _busyCounter++;
        if (_busyCounter == 1)
        {
            IsBusy = true;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusMessage = message;
        }
    }

    private void EndWork()
    {
        if (_busyCounter == 0)
        {
            return;
        }

        _busyCounter--;
        if (_busyCounter == 0)
        {
            IsBusy = false;
        }
    }

    private void OnTablesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TableHintMessage));
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedTableDetails));
        OnPropertyChanged(nameof(TableHintMessage));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
