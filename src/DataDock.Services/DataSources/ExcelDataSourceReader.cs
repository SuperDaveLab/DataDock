using System;
using System.Collections.Generic;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DataDock.Services.DataSources;

/// <summary>
/// Excel file reader that implements IDataSourceReader using NPOI.
/// Supports both .xls (HSSF) and .xlsx (XSSF) formats.
/// </summary>
public sealed class ExcelDataSourceReader : IDataSourceReader
{
    private readonly FileStream _fileStream;
    private readonly IWorkbook _workbook;
    private readonly ISheet _sheet;
    private string[] _headers = Array.Empty<string>();
    private int _currentRowIndex;
    private readonly int _lastRowNum;

    public ExcelDataSourceReader(string filePath, int worksheetIndex = 0)
    {
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        _workbook = extension switch
        {
            ".xlsx" => new XSSFWorkbook(_fileStream),
            ".xls" => new HSSFWorkbook(_fileStream),
            _ => throw new NotSupportedException($"Excel file format '{extension}' not supported. Use .xlsx or .xls")
        };

        if (_workbook.NumberOfSheets == 0)
        {
            throw new InvalidOperationException("Excel file contains no worksheets.");
        }

        _sheet = worksheetIndex < _workbook.NumberOfSheets
            ? _workbook.GetSheetAt(worksheetIndex)
            : _workbook.GetSheetAt(0);

        _lastRowNum = _sheet.LastRowNum;

        if (_lastRowNum < 0)
        {
            throw new InvalidOperationException("Worksheet is empty.");
        }

        _currentRowIndex = 0;
    }

    public string[] GetHeaders()
    {
        if (_headers.Length == 0)
        {
            var headerRow = _sheet.GetRow(0) ?? throw new InvalidOperationException("Header row is missing.");
            var headerList = new List<string>();
            for (var col = 0; col < headerRow.LastCellNum; col++)
            {
                var cell = headerRow.GetCell(col);
                var headerName = GetCellValueAsString(cell) ?? $"Column{col + 1}";
                headerList.Add(headerName);
            }

            _headers = headerList.ToArray();
        }

        return _headers;
    }

    public bool Read()
    {
        if (_headers.Length == 0)
        {
            GetHeaders();
        }

        _currentRowIndex++;
        return _currentRowIndex <= _lastRowNum;
    }

    public string? GetField(int index)
    {
        if (_currentRowIndex <= 0 || _currentRowIndex > _lastRowNum)
        {
            throw new InvalidOperationException("No current row. Call Read() first.");
        }

        var row = _sheet.GetRow(_currentRowIndex);
        var cell = row?.GetCell(index);
        return GetCellValueAsString(cell);
    }

    private static string? GetCellValueAsString(ICell? cell)
    {
        if (cell == null)
        {
            return null;
        }

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? $"{cell.DateCellValue:yyyy-MM-dd}"
                : cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => GetFormulaCellValueAsString(cell),
            CellType.Blank => null,
            _ => cell.ToString()
        };
    }

    private static string? GetFormulaCellValueAsString(ICell cell)
    {
        try
        {
            return cell.CachedFormulaResultType switch
            {
                CellType.String => cell.StringCellValue,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                    ? $"{cell.DateCellValue:yyyy-MM-dd}"
                    : cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                _ => cell.ToString()
            };
        }
        catch
        {
            return cell.ToString();
        }
    }

    public void Dispose()
    {
        _workbook.Close();
        _fileStream.Dispose();
    }
}
