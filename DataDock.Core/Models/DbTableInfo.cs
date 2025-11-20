namespace DataDock.Core.Models;

public class DbTableInfo
{
    public string Schema { get; set; } = "dbo";
    public string Name { get; set; } = string.Empty;
    public List<DbColumnInfo> Columns { get; set; } = new();
}
