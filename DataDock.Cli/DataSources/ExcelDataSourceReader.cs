using System;
using System.Collections.Generic;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;

namespace DataDock.Cli.DataSources;

/// <summary>
/// Excel file reader that implements IDataSourceReader using NPOI
/// Supports both .xls (HSSF) and .xlsx (XSSF) formats
/// </summary>
public class ExcelDataSourceReader : IDataSourceReader
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

        // Determine file type by extension and create appropriate workbook
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        _workbook = extension switch
        {
            ".xlsx" => new XSSFWorkbook(_fileStream),
            ".xls" => new HSSFWorkbook(_fileStream),
            _ => throw new NotSupportedException($"Excel file format '{extension}' not supported. Use .xlsx or .xls")
        };

        if (_workbook.NumberOfSheets == 0)
            throw new InvalidOperationException("Excel file contains no worksheets.");

        _sheet = worksheetIndex < _workbook.NumberOfSheets
            ? _workbook.GetSheetAt(worksheetIndex)
            : _workbook.GetSheetAt(0);

        _lastRowNum = _sheet.LastRowNum;
        
        if (_lastRowNum < 0)
            throw new InvalidOperationException("Worksheet is empty.");

        // Start at 0 (header row). First Read() will move to row 1 (first data row)
        _currentRowIndex = 0;
    }

    public string[] GetHeaders()
    {
        if (_headers.Length == 0)
        {
            var headerRow = _sheet.GetRow(0);
            if (headerRow == null)
                throw new InvalidOperationException("Header row is missing.");

            var headerList = new List<string>();
            for (int col = 0; col < headerRow.LastCellNum; col++)
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
        // Ensure headers are read first
        if (_headers.Length == 0)
        {
            GetHeaders();
        }

        // Move to next row (first Read() will move from 0 to 1, which is the first data row)
        _currentRowIndex++;

        // Check if we've reached the end
        return _currentRowIndex <= _lastRowNum;
    }

    public string? GetField(int index)
    {
        if (_currentRowIndex < 0 || _currentRowIndex > _lastRowNum)
            throw new InvalidOperationException("No current row. Call Read() first.");

        var row = _sheet.GetRow(_currentRowIndex);
        if (row == null)
            return null;

        var cell = row.GetCell(index);
        return GetCellValueAsString(cell);
    }

    private string? GetCellValueAsString(ICell? cell)
    {
        if (cell == null)
            return null;

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

    private string? GetFormulaCellValueAsString(ICell cell)
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
        _workbook?.Close();
        _fileStream?.Dispose();
    }
}
