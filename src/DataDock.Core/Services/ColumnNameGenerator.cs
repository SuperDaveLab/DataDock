using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public static class ColumnNameGenerator
{
    // Roughly: words like "Job", "Number", "2024", handles underscored/space-separated inputs too
    private static readonly Regex WordSplitter =
        new(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", RegexOptions.Compiled);

    public static string ToColumnName(string fieldName, ColumnNameStyle style)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return fieldName;

        if (style == ColumnNameStyle.AsIs)
            return fieldName;

        var normalized = NormalizeRawName(fieldName);

        // If normalization kills everything, fall back to original
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = fieldName;

        var words = NormalizeToWords(normalized);
        if (words.Count == 0)
            words = NormalizeToWords(fieldName); // last-resort fallback

        return style switch
        {
            ColumnNameStyle.CamelCase       => ToCamelCase(words),
            ColumnNameStyle.PascalCase      => ToPascalCase(words),
            ColumnNameStyle.SnakeCase       => ToSnakeCase(words),
            ColumnNameStyle.KebabCase       => ToKebabCase(words),
            ColumnNameStyle.TitleWithSpaces => ToTitleWithSpaces(words),
            _                               => fieldName
        };
    }

    /// <summary>
    /// Inspired by legacy formatColName: strip junk, handle # → num, % → pct,
    /// collapse spaces/underscores/dashes, etc.
    /// </summary>
    private static string NormalizeRawName(string name)
    {
        var result = name.Trim();

        // Characters we want to *remove entirely*
        var removeChars = new[]
        {
            '$', '/', '\\', '&', '@', '.', ':', ';', '?', ',', 
            '(', ')', '[', ']', '{', '}', '\'', '"'
        };

        foreach (var c in removeChars)
        {
            result = result.Replace(c.ToString(), "");
        }

        // Special replacements
        result = result
            .Replace("#", " num")   // "Ticket #" → "ticket num"
            .Replace("%", " pct");  // "Success %" → "success pct"

        // Normalize separators to space
        result = result
            .Replace("_", " ")
            .Replace("-", " ");

        // Collapse multiple spaces
        while (result.Contains("  "))
            result = result.Replace("  ", " ");

        return result.Trim();
    }

    private static List<string> NormalizeToWords(string input)
    {
        // First split on spaces (after NormalizeRawName) to get chunks,
        // then run regex on each chunk to handle CamelCase/mixed forms.
        var words = new List<string>();

        foreach (var token in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var matches = WordSplitter.Matches(token);
            foreach (Match match in matches)
            {
                var w = match.Value.Trim();
                if (!string.IsNullOrEmpty(w))
                    words.Add(w.ToLowerInvariant());
            }
        }

        return words;
    }

    private static string ToCamelCase(List<string> words)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            var w = words[i];
            if (i == 0)
                sb.Append(w);
            else
                sb.Append(char.ToUpperInvariant(w[0]))
                  .Append(w.AsSpan(1));
        }
        return sb.ToString();
    }

    private static string ToPascalCase(List<string> words)
    {
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            sb.Append(char.ToUpperInvariant(w[0]))
              .Append(w.AsSpan(1));
        }
        return sb.ToString();
    }

    private static string ToSnakeCase(List<string> words)
        => string.Join("_", words);

    private static string ToKebabCase(List<string> words)
        => string.Join("-", words);

    private static string ToTitleWithSpaces(List<string> words)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            var w = words[i];
            if (i > 0)
                sb.Append(' ');

            sb.Append(char.ToUpperInvariant(w[0]))
              .Append(w.AsSpan(1));
        }
        return sb.ToString();
    }
}
