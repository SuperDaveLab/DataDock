using System;
using System.Linq;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public class DbFieldBinder
{
    public void BindFieldsToDbColumns(ImportProfile profile, DbTableInfo dbTable)
    {
        // Map DB columns by lower name for lookup
        var columnsByName = dbTable.Columns
            .ToDictionary(c => c.Name.ToLowerInvariant(), c => c);

        foreach (var field in profile.TargetFields)
        {
            // 1. if profile explicitly set DbColumnName, try that first
            if (!string.IsNullOrWhiteSpace(field.DbColumnName))
            {
                var key = field.DbColumnName.ToLowerInvariant();
                if (columnsByName.ContainsKey(key))
                    continue; // binding is fine

                // if explicit name doesn’t exist, we can log/collect a warning later
            }

            // 2. otherwise derive expected name from ColumnNameStyle
            var expected = ColumnNameGenerator.ToColumnName(field.Name, profile.ColumnNameStyle);
            var expectedKey = expected.ToLowerInvariant();

            if (columnsByName.TryGetValue(expectedKey, out var matched))
            {
                field.DbColumnName = matched.Name; // set the canonical DB name
                continue;
            }

            // 3. (optional) try aliases — useful if DB col names are weird
            var aliases = profile.Aliases
                .Where(a => string.Equals(a.TargetFieldName, field.Name, StringComparison.OrdinalIgnoreCase))
                .Select(a => ColumnNameGenerator.ToColumnName(a.Alias, profile.ColumnNameStyle))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var aliasName in aliases)
            {
                var aliasKey = aliasName.ToLowerInvariant();
                if (columnsByName.TryGetValue(aliasKey, out var aliasMatch))
                {
                    field.DbColumnName = aliasMatch.Name;
                    break;
                }
            }

            // If still not bound, DbColumnName stays null, and you can report it later.
        }
    }
}
