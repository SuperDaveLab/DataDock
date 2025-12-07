using System;
using System.Globalization;
using System.IO;
using CsvHelper;

namespace DataDock.Services.DataSources;

/// <summary>
/// Wrapper for CsvHelper that implements IDataSourceReader.
/// </summary>
public sealed class CsvDataSourceReader : IDataSourceReader
{
    private readonly StreamReader _streamReader;
    private readonly CsvReader _csvReader;
    private string[] _headers = Array.Empty<string>();
    private bool _headersRead;

    public CsvDataSourceReader(string filePath)
    {
        _streamReader = new StreamReader(filePath);
        _csvReader = new CsvReader(_streamReader, CultureInfo.InvariantCulture);
    }

    public string[] GetHeaders()
    {
        if (!_headersRead)
        {
            if (!_csvReader.Read())
            {
                throw new InvalidOperationException("CSV file appears to be empty.");
            }

            _csvReader.ReadHeader();
            _headers = _csvReader.HeaderRecord ?? Array.Empty<string>();
            _headersRead = true;
        }

        return _headers;
    }

    public bool Read()
    {
        if (!_headersRead)
        {
            GetHeaders();
        }

        return _csvReader.Read();
    }

    public string? GetField(int index) => _csvReader.GetField(index);

    public void Dispose()
    {
        _csvReader.Dispose();
        _streamReader.Dispose();
    }
}
