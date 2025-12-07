using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DataDock.Core.Models;
using DataDock.Gui.Models;
using DataDock.Services;
using DataDock.Core.Dialects;
using DataDock.Core.Services;
using Microsoft.Data.SqlClient;

namespace DataDock.Gui.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ImportWorkflowService _workflowService = new();

    public ObservableCollection<FieldPreview> FieldPreviews { get; }
    public ObservableCollection<string> ImportIssues { get; }
    public IReadOnlyList<ColumnStyleOption> ColumnStyles { get; }
    public IReadOnlyList<FieldType> FieldTypeOptions { get; }
    public IReadOnlyList<WriteMode> WriteModeOptions { get; }

    private ColumnStyleOption _selectedColumnStyle;
    private string? _selectedFilePath;
    private string _statusMessage = "Drop a CSV or Excel file to get started.";
    private string _previewSummary = "No preview generated yet.";
    private bool _isBusy;
    private string _tableName = string.Empty;
    private string _databaseSchema = "dbo";
    private string _databaseHost = "localhost";
    private string _databasePort = "1433";
    private string _databaseName = string.Empty;
    private string _databaseUser = string.Empty;
    private string _databasePassword = string.Empty;
    private bool _useIntegratedSecurity = true;
    private bool _trustServerCertificate = true;
    private WriteMode _selectedWriteMode = WriteMode.Insert;
    private bool _ensureTableBeforeImport = true;
    private string? _importProgressMessage;

    public MainWindowViewModel()
    {
        FieldPreviews = new ObservableCollection<FieldPreview>();
        FieldPreviews.CollectionChanged += OnFieldPreviewsChanged;
        ImportIssues = new ObservableCollection<string>();
        FieldTypeOptions = Enum.GetValues<FieldType>();
        WriteModeOptions = Enum.GetValues<WriteMode>();

        ColumnStyles = new[]
        {
            new ColumnStyleOption(ColumnNameStyle.SnakeCase, "snake_case", "ticket_number"),
            new ColumnStyleOption(ColumnNameStyle.PascalCase, "PascalCase", "TicketNumber"),
            new ColumnStyleOption(ColumnNameStyle.CamelCase, "camelCase", "ticketNumber"),
            new ColumnStyleOption(ColumnNameStyle.KebabCase, "kebab-case", "ticket-number"),
            new ColumnStyleOption(ColumnNameStyle.AsIs, "Original", "Ticket #")
        };

        _selectedColumnStyle = ColumnStyles[0];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        private set
        {
            if (SetField(ref _selectedFilePath, value))
            {
                OnPropertyChanged(nameof(SelectedFileName));
                OnPropertyChanged(nameof(SelectedFileDisplay));
                OnPropertyChanged(nameof(CanGenerateOutputs));
                OnPropertyChanged(nameof(CanRunDatabaseActions));
            }
        }
    }

    public string SelectedFileName => string.IsNullOrWhiteSpace(SelectedFilePath)
        ? "No file selected"
        : Path.GetFileName(SelectedFilePath);

    public string SelectedFileDisplay => string.IsNullOrWhiteSpace(SelectedFilePath)
        ? "No file selected"
        : SelectedFilePath!;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetField(ref _previewSummary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public ColumnStyleOption SelectedColumnStyle
    {
        get => _selectedColumnStyle;
        set
        {
            if (SetField(ref _selectedColumnStyle, value))
            {
                ApplyNamingStyle();
            }
        }
    }

    public bool HasPreview => FieldPreviews.Count > 0;
    public bool CanGenerateOutputs => HasPreview && !string.IsNullOrWhiteSpace(SelectedFilePath);
    public bool CanRunDatabaseActions => CanGenerateOutputs &&
        !string.IsNullOrWhiteSpace(TableName) &&
        TryBuildConnectionString(out _, out _);

    public string TableName
    {
        get => _tableName;
        set
        {
            if (SetField(ref _tableName, value))
            {
                OnPropertyChanged(nameof(CanRunDatabaseActions));
            }
        }
    }

    public string DatabaseSchema
    {
        get => _databaseSchema;
        set => SetField(ref _databaseSchema, value);
    }

    public string DatabaseHost
    {
        get => _databaseHost;
        set
        {
            if (SetField(ref _databaseHost, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public string DatabasePort
    {
        get => _databasePort;
        set
        {
            if (SetField(ref _databasePort, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public string DatabaseName
    {
        get => _databaseName;
        set
        {
            if (SetField(ref _databaseName, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public string DatabaseUser
    {
        get => _databaseUser;
        set
        {
            if (SetField(ref _databaseUser, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public string DatabasePassword
    {
        get => _databasePassword;
        set
        {
            if (SetField(ref _databasePassword, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public bool UseIntegratedSecurity
    {
        get => _useIntegratedSecurity;
        set
        {
            if (SetField(ref _useIntegratedSecurity, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public bool TrustServerCertificate
    {
        get => _trustServerCertificate;
        set
        {
            if (SetField(ref _trustServerCertificate, value))
            {
                OnConnectionInfoChanged();
            }
        }
    }

    public WriteMode SelectedWriteMode
    {
        get => _selectedWriteMode;
        set => SetField(ref _selectedWriteMode, value);
    }

    public bool EnsureTableBeforeImport
    {
        get => _ensureTableBeforeImport;
        set => SetField(ref _ensureTableBeforeImport, value);
    }

    public string? ImportProgressMessage
    {
        get => _importProgressMessage;
        private set
        {
            if (SetField(ref _importProgressMessage, value))
            {
                OnPropertyChanged(nameof(HasImportProgress));
            }
        }
    }

    public bool HasImportProgress => !string.IsNullOrWhiteSpace(_importProgressMessage);
    public bool HasImportIssues => ImportIssues.Count > 0;

    public async Task HandleFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusMessage = "No file selected.";
            return;
        }

        if (!File.Exists(filePath))
        {
            StatusMessage = $"File not found: {filePath}";
            return;
        }

        SelectedFilePath = filePath;
        TableName = Path.GetFileNameWithoutExtension(filePath) ?? "ImportedTable";
        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            DatabaseName = TableName;
        }
        await LoadPreviewAsync(filePath);
    }

    private async Task LoadPreviewAsync(string filePath)
    {
        SetBusy(true, "Analyzing file...");
        try
        {
            var preview = await Task.Run(() => _workflowService.GeneratePreview(filePath));
            PreviewSummary = $"Detected {preview.InferredFields.Count} columns • Sampled {preview.SampleRows.Count} rows";
            ApplyPreview(preview);
            StatusMessage = "Preview updated.";
        }
        catch (Exception ex)
        {
            FieldPreviews.Clear();
            OnPropertyChanged(nameof(HasPreview));
            StatusMessage = $"Preview failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyPreview(FilePreviewResult preview)
    {
        FieldPreviews.Clear();
        foreach (var field in preview.InferredFields)
        {
            FieldPreviews.Add(new FieldPreview(
                field.Name,
                field.FieldType,
                allowsNull: !field.IsRequired,
                maxLength: field.MaxLength));
        }

        ApplyNamingStyle();
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(CanGenerateOutputs));
        OnPropertyChanged(nameof(CanRunDatabaseActions));
    }

    private void ApplyNamingStyle()
    {
        if (FieldPreviews.Count == 0)
        {
            return;
        }

        var style = SelectedColumnStyle?.Style ?? ColumnNameStyle.AsIs;
        foreach (var field in FieldPreviews)
        {
            field.ApplyStyle(style);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        IsBusy = busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusMessage = message;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
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

    public void SetStatus(string message)
    {
        StatusMessage = message;
    }

    public async Task<string> GenerateCreateTableSqlAsync()
    {
        EnsurePreviewReady();
        var profile = BuildProfileFromPreviews();
        var schema = TableSchemaBuilder.FromProfile(profile);
        var dialect = new SqlServerDialect();
        var sql = dialect.GenerateCreateTable(schema);
        StatusMessage = "Generated CREATE TABLE statement.";
        return await Task.FromResult(sql);
    }

    public async Task ExportCreateTableSqlAsync(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path is required", nameof(destinationPath));
        }

        var sql = await GenerateCreateTableSqlAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        await File.WriteAllTextAsync(destinationPath, sql);
        StatusMessage = $"CREATE TABLE script saved to {destinationPath}";
    }

    public async Task<string> GenerateJsonPreviewAsync()
    {
        EnsurePreviewReady();
        EnsureInputPathAvailable();

        var tempPath = Path.Combine(Path.GetTempPath(), $"datadock-preview-{Guid.NewGuid():N}.json");
        var options = BuildImportOptions(tempPath);
        options.WriteToDatabase = false;
        options.EnsureTable = false;

        var profile = BuildProfileFromPreviews();

        SetBusy(true, "Generating JSON preview...");
        try
        {
            await Task.Run(() => _workflowService.RunImport(options, profile));
            var json = await File.ReadAllTextAsync(tempPath);
            StatusMessage = "JSON preview ready.";
            return json;
        }
        finally
        {
            SetBusy(false);
            TryDelete(tempPath);
        }
    }

    public async Task ExportJsonAsync(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path is required", nameof(destinationPath));
        }

        EnsurePreviewReady();
        EnsureInputPathAvailable();

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        var options = BuildImportOptions(destinationPath);
        options.WriteToDatabase = false;
        options.EnsureTable = false;

        var profile = BuildProfileFromPreviews();

        SetBusy(true, "Exporting JSON data...");
        try
        {
            await Task.Run(() => _workflowService.RunImport(options, profile));
            StatusMessage = $"JSON exported to {destinationPath}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task EnsureTableInDatabaseAsync()
    {
        EnsureDatabaseInputsReady();
        var profile = BuildProfileFromPreviews();
        profile.TableSchema = ResolveSchemaName();
        var connectionString = BuildConnectionStringOrThrow();

        SetBusy(true, $"Ensuring [{profile.TableSchema}].[{profile.TableName}] exists...");
        try
        {
            await Task.Run(() => EnsureTableExists(profile, connectionString));
            StatusMessage = $"Table [{profile.TableSchema}].[{profile.TableName}] is ready.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task<ImportExecutionResult> ImportDataIntoDatabaseAsync()
    {
        EnsureDatabaseInputsReady();
        EnsureInputPathAvailable();

        var tempPath = Path.Combine(Path.GetTempPath(), $"datadock-import-{Guid.NewGuid():N}.json");
        var connectionString = BuildConnectionStringOrThrow();
        var options = BuildImportOptions(tempPath, connectionString);
        options.WriteToDatabase = true;
        options.EnsureTable = EnsureTableBeforeImport;

        var profile = BuildProfileFromPreviews();
        profile.TableSchema = ResolveSchemaName();
        ImportProgressMessage = string.Empty;
        ClearImportIssues();
        var progress = new Progress<ImportLogEvent>(log =>
        {
            if (log != null)
            {
                ImportProgressMessage = log.Message;
            }
        });

        SetBusy(true, "Importing data into database...");
        try
        {
            var result = await Task.Run(() => _workflowService.RunImport(options, profile, progress));
            StatusMessage = $"Imported {result.ValidRows}/{result.TotalRows} rows.";
            PublishImportIssues(result);
            return result;
        }
        finally
        {
            TryDelete(tempPath);
            ImportProgressMessage = string.Empty;
            SetBusy(false);
        }
    }

    private void PublishImportIssues(ImportExecutionResult result)
    {
        if (result.InvalidRows <= 0)
        {
            ClearImportIssues();
            return;
        }

        ImportIssues.Clear();
        ImportIssues.Add($"{result.InvalidRows} row(s) skipped. Showing sample errors below.");

        foreach (var sample in result.SampleErrors)
        {
            if (sample == null)
            {
                continue;
            }

            var firstError = sample.Errors.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstError))
            {
                continue;
            }

            ImportIssues.Add($"Row {sample.RowNumber}: {firstError}");
        }

        OnPropertyChanged(nameof(HasImportIssues));
    }

    private void ClearImportIssues()
    {
        if (ImportIssues.Count == 0)
        {
            return;
        }

        ImportIssues.Clear();
        OnPropertyChanged(nameof(HasImportIssues));
    }

    private ImportProfile BuildProfileFromPreviews()
    {
        EnsurePreviewReady();
        if (string.IsNullOrWhiteSpace(TableName))
        {
            throw new InvalidOperationException("Table name is required.");
        }

        var selectedFields = FieldPreviews.Where(f => f.IsSelected).ToList();
        if (selectedFields.Count == 0)
        {
            throw new InvalidOperationException("Select at least one column to import.");
        }

        var profile = new ImportProfile
        {
            TableName = TableName.Trim(),
            TableSchema = string.IsNullOrWhiteSpace(DatabaseSchema) ? null : DatabaseSchema.Trim(),
            ColumnNameStyle = ColumnNameStyle.AsIs
        };

        foreach (var preview in selectedFields)
        {
            var targetField = new TargetField
            {
                Name = preview.DisplayName,
                FieldType = preview.FieldType,
                IsRequired = !preview.AllowsNull,
                MaxLength = preview.FieldType == FieldType.String
                    ? preview.MaxLength ?? StringLengthBucketizer.GetSuggestedLength(0)
                    : null
            };

            profile.TargetFields.Add(targetField);

            if (!string.Equals(preview.DisplayName, preview.SourceName, StringComparison.OrdinalIgnoreCase))
            {
                profile.Aliases.Add(new ColumnAlias
                {
                    TargetFieldName = targetField.Name,
                    Alias = preview.SourceName
                });
            }
        }

        return profile;
    }

    private ImportOptions BuildImportOptions(string outputPath, string? connectionStringOverride = null)
    {
        EnsureInputPathAvailable();

        string? resolvedConnectionString = connectionStringOverride;
        if (resolvedConnectionString is null && TryBuildConnectionString(out var autoConnection, out _))
        {
            resolvedConnectionString = autoConnection;
        }

        return new ImportOptions
        {
            InputPath = SelectedFilePath!,
            OutputPath = outputPath,
            ConnectionString = resolvedConnectionString,
            DatabaseSchema = string.IsNullOrWhiteSpace(DatabaseSchema) ? null : DatabaseSchema,
            TableName = string.IsNullOrWhiteSpace(TableName) ? null : TableName,
            ColumnNameStyleOverride = ColumnNameStyle.AsIs,
            WriteMode = SelectedWriteMode
        };
    }

    private void EnsurePreviewReady()
    {
        if (!HasPreview)
        {
            throw new InvalidOperationException("Generate a preview first.");
        }
    }

    private void EnsureInputPathAvailable()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            throw new InvalidOperationException("No source file selected.");
        }
    }

    private void EnsureDatabaseInputsReady()
    {
        EnsurePreviewReady();
        if (string.IsNullOrWhiteSpace(TableName))
        {
            throw new InvalidOperationException("Table name is required for database actions.");
        }

        _ = BuildConnectionStringOrThrow();
    }

    private string ResolveSchemaName()
    {
        return string.IsNullOrWhiteSpace(DatabaseSchema) ? "dbo" : DatabaseSchema.Trim();
    }

    private void EnsureTableExists(ImportProfile profile, string connectionString)
    {
        var schemaName = profile.TableSchema ?? ResolveSchemaName();

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        const string existsSql = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;";

        using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = existsSql;
            existsCommand.Parameters.AddWithValue("@schema", schemaName);
            existsCommand.Parameters.AddWithValue("@table", profile.TableName);

            var exists = Convert.ToInt32(existsCommand.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            if (exists)
            {
                return;
            }
        }

        var tableSchema = TableSchemaBuilder.FromProfile(profile);
        tableSchema.SchemaName = schemaName;

        var dialect = new SqlServerDialect();
        var ddl = dialect.GenerateCreateTable(tableSchema);

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = ddl;
        createCommand.ExecuteNonQuery();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored on purpose
        }
    }

    private void OnFieldPreviewsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(CanGenerateOutputs));
        OnPropertyChanged(nameof(CanRunDatabaseActions));
    }

    private string BuildConnectionStringOrThrow()
    {
        if (TryBuildConnectionString(out var connectionString, out var error))
        {
            return connectionString;
        }

        throw new InvalidOperationException(error ?? "Connection settings are incomplete.");
    }

    private bool TryBuildConnectionString(out string connectionString, out string? error)
    {
        connectionString = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(DatabaseHost))
        {
            error = "Server host is required.";
            return false;
        }

        var host = DatabaseHost.Trim();
        var portText = string.IsNullOrWhiteSpace(DatabasePort) ? "1433" : DatabasePort.Trim();
        if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port <= 0 || port > 65535)
        {
            error = "Enter a valid port number.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            error = "Database name is required.";
            return false;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = port == 1433 ? host : $"{host},{port}",
            InitialCatalog = DatabaseName.Trim(),
            TrustServerCertificate = TrustServerCertificate,
            Encrypt = true
        };

        if (UseIntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(DatabaseUser) || string.IsNullOrWhiteSpace(DatabasePassword))
            {
                error = "Username and password are required unless using integrated security.";
                return false;
            }

            builder.IntegratedSecurity = false;
            builder.UserID = DatabaseUser.Trim();
            builder.Password = DatabasePassword;
        }

        connectionString = builder.ConnectionString;
        return true;
    }

    private void OnConnectionInfoChanged()
    {
        OnPropertyChanged(nameof(CanRunDatabaseActions));
    }
}
