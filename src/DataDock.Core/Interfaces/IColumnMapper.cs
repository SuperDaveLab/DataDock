using System.Collections.Generic;
using DataDock.Core.Models;

namespace DataDock.Core.Interfaces;

public interface IColumnMapper
{
    List<ColumnMapping> GenerateMappings(
        ImportProfile profile,
        IReadOnlyList<SourceColumn> sourceColumns);
}
