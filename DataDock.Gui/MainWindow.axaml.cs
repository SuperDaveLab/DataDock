using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DataDock.Gui.ViewModels;
using DataDock.Gui.Views;

namespace DataDock.Gui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnWindowDragEnter, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(DragDrop.DropEvent, OnWindowDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnBrowseFileClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null || StorageProvider is null || !StorageProvider.CanOpen)
        {
            return;
        }

        var pickerOptions = new FilePickerOpenOptions
        {
            Title = "Choose source file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("CSV files") { Patterns = new[] { "*.csv" } },
                new("Excel workbooks") { Patterns = new[] { "*.xlsx", "*.xls" } },
                new("All files") { Patterns = new[] { "*" } }
            }
        };

        var result = await StorageProvider.OpenFilePickerAsync(pickerOptions);
        var file = result.FirstOrDefault();
        var path = file?.Path?.LocalPath ?? file?.Path?.ToString();

        if (!string.IsNullOrWhiteSpace(path))
        {
            await ViewModel.HandleFileAsync(path);
        }
    }

    private void OnDropZoneDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDragState(e, entering: true);
    }

    private void OnDropZoneDragLeave(object? sender, DragEventArgs e)
    {
        SetDropZoneHighlight(false);
        e.Handled = true;
    }

    private void OnDropZoneDragOver(object? sender, DragEventArgs e)
    {
        UpdateDragState(e, entering: false);
    }

    private async void OnDropZoneDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        SetDropZoneHighlight(false);

        if (ViewModel is null)
        {
            return;
        }

        var path = ExtractFirstPath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await ViewModel.HandleFileAsync(path);
        }
    }

    private void UpdateDragState(DragEventArgs e, bool entering)
    {
        if (HasSupportedFile(e))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (entering)
            {
                SetDropZoneHighlight(true);
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            if (entering)
            {
                SetDropZoneHighlight(false);
            }
        }

        e.Handled = true;
    }

    #pragma warning disable CS0618
    private bool HasSupportedFile(DragEventArgs e)
    {
        return TryGetDroppedFilePath(e, out var path) && IsSupportedExtension(path);
    }

    private static string? ExtractFirstPath(DragEventArgs e)
    {
        return TryGetDroppedFilePath(e, out var path) ? path : null;
    }

    private static bool TryGetDroppedFilePath(DragEventArgs e, out string? path)
    {
        path = null;

        if (e.Data.Contains(DataFormats.Files))
        {
            var file = e.Data.GetFiles()?.FirstOrDefault();
            var localPath = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                localPath = file?.Path?.LocalPath ?? file?.Path?.ToString();
            }

            if (TryNormalizePath(localPath, out path))
            {
                return true;
            }
        }

        if (e.Data.Contains(DataFormats.FileNames))
        {
            var fallbackPath = e.Data.GetFileNames()?.FirstOrDefault();
            if (TryNormalizePath(fallbackPath, out path))
            {
                return true;
            }
        }

        if (e.Data.Contains(DataFormats.Text))
        {
            if (TryNormalizePath(e.Data.GetText(), out path))
            {
                return true;
            }
        }

        return false;
    }
    #pragma warning restore CS0618

    private static bool IsSupportedExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".csv" or ".xlsx" or ".xls";
    }

    private static bool TryNormalizePath(string? candidate, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        var newlineIndex = trimmed.IndexOfAny(new[] { '\r', '\n' });
        if (newlineIndex >= 0)
        {
            trimmed = trimmed[..newlineIndex];
        }

        trimmed = trimmed.Trim('"');

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            normalized = uri.LocalPath;
            return true;
        }

        normalized = trimmed;
        return !string.IsNullOrWhiteSpace(normalized);
    }

    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        if (DropZoneBorder?.IsPointerOver == true)
        {
            OnDropZoneDragEnter(DropZoneBorder, e);
        }
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (DropZoneBorder?.IsPointerOver == true)
        {
            OnDropZoneDragOver(DropZoneBorder, e);
        }
    }

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        if (!DropZoneBorder?.IsPointerOver ?? true)
        {
            OnDropZoneDragLeave(DropZoneBorder, e);
        }
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (DropZoneBorder?.IsPointerOver == true)
        {
            OnDropZoneDrop(DropZoneBorder, e);
        }
    }

    private void SetDropZoneHighlight(bool active)
    {
        if (DropZoneBorder is null)
        {
            return;
        }

        if (active)
        {
            DropZoneBorder.Classes.Add("drag-over");
        }
        else
        {
            DropZoneBorder.Classes.Remove("drag-over");
        }
    }

    private async void OnPreviewCreateTableClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            var sql = await ViewModel.GenerateCreateTableSqlAsync();
            await ShowTextPreviewAsync("CREATE TABLE preview", sql);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus($"CREATE TABLE preview failed: {ex.Message}");
        }
    }

    private async void OnExportCreateTableClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            var suggested = string.IsNullOrWhiteSpace(ViewModel.TableName) ? "table.sql" : ViewModel.TableName + ".sql";
            var path = await PickSavePathAsync("Export CREATE TABLE", suggested, new[] { ".sql" });
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await ViewModel.ExportCreateTableSqlAsync(path);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus($"CREATE TABLE export failed: {ex.Message}");
        }
    }

    private async void OnPreviewJsonClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            var json = await ViewModel.GenerateJsonPreviewAsync();
            await ShowTextPreviewAsync("JSON preview", json);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus($"JSON preview failed: {ex.Message}");
        }
    }

    private async void OnExportJsonClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            var suggested = string.IsNullOrWhiteSpace(ViewModel.SelectedFileName)
                ? "data.json"
                : Path.ChangeExtension(ViewModel.SelectedFileName, ".json");
            var path = await PickSavePathAsync("Export JSON", suggested, new[] { ".json" });
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await ViewModel.ExportJsonAsync(path);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus($"JSON export failed: {ex.Message}");
        }
    }

    private async void OnCreateTableClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            await ViewModel.EnsureTableInDatabaseAsync();
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus($"Create table failed: {ex.Message}");
        }
    }

    private async void OnImportDataClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            await ViewModel.ImportDataIntoDatabaseAsync();
        }
        catch (Exception ex)
        {
            ViewModel.SetStatus($"Import failed: {ex.Message}");
        }
    }

    private async Task ShowTextPreviewAsync(string title, string content)
    {
        var previewWindow = new TextPreviewWindow
        {
            DataContext = new TextPreviewViewModel(title, content)
        };

        await previewWindow.ShowDialog(this);
    }

    private async Task<string?> PickSavePathAsync(string title, string suggestedName, IEnumerable<string> extensions)
    {
        if (StorageProvider is null || !StorageProvider.CanSave)
        {
            return null;
        }

        var patterns = extensions.Select(ext => ext.StartsWith('.') ? "*" + ext : "*." + ext.TrimStart('.')).ToArray();
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(title) { Patterns = patterns }
            }
        };

        if (extensions.FirstOrDefault() is { } extValue)
        {
            options.DefaultExtension = extValue.TrimStart('.');
        }

        var file = await StorageProvider.SaveFilePickerAsync(options);
        return file?.Path?.LocalPath ?? file?.Path?.ToString();
    }
}