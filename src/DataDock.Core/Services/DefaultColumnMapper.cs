using System;
using System.Collections.Generic;
using System.Linq;
using DataDock.Core.Interfaces;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public class DefaultColumnMapper : IColumnMapper
{
    public List<ColumnMapping> GenerateMappings(
        ImportProfile profile,
        IReadOnlyList<SourceColumn> sourceColumns)
    {
        var mappings = new List<ColumnMapping>();

        foreach (var field in profile.TargetFields)
        {
            // 1. Exact match on header
            var exact = sourceColumns
                .FirstOrDefault(c =>
                    string.Equals(c.HeaderName, field.Name,
                        StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                mappings.Add(new ColumnMapping
                {
                    TargetField = field,
                    SourceColumn = exact,
                    IsAutoMapped = true
                });

                continue;
            }

            // 2. Alias match
            var aliases = profile.Aliases
                .Where(a => string.Equals(a.TargetFieldName, field.Name,
                    StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Alias)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var aliasMatch = sourceColumns.FirstOrDefault(c => aliases.Contains(c.HeaderName));

            mappings.Add(new ColumnMapping
            {
                TargetField = field,
                SourceColumn = aliasMatch,
                IsAutoMapped = aliasMatch != null
            });
        }

        return mappings;
    }
}
