using System;

namespace DataDock.Cli.DataSources;

/// <summary>
/// Abstraction for reading tabular data from various sources (CSV, Excel, etc.)
/// </summary>
public interface IDataSourceReader : IDisposable
{
    /// <summary>
    /// Gets the header names from the data source
    /// </summary>
    string[] GetHeaders();

    /// <summary>
    /// Reads the next row of data
    /// </summary>
    /// <returns>True if a row was read, false if end of data</returns>
    bool Read();

    /// <summary>
    /// Gets the value at the specified column index for the current row
    /// </summary>
    string? GetField(int index);
}
