using DataDock.Core.Models;

namespace DataDock.Core.Interfaces;

public interface ISqlDialect
{
    string GenerateCreateTable(TableSchema schema);
}
