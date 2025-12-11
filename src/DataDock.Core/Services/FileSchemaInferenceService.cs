using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

/// <summary>
/// Provides basic schema inference for flat files by profiling a sample of rows.
/// </summary>
public static class FileSchemaInferenceService
{
    /// <summary>
    /// Infers a collection of <see cref="TargetField"/> definitions based on the provided headers
    /// and sampled row values. String columns have their suggested <see cref="TargetField.MaxLength"/>
    /// bucketized through <see cref="StringLengthBucketizer"/>.
    /// </summary>
    /// <param name="headers">Source column headers in the order they appear in the sample rows.</param>
    /// <param name="rows">Sampled rows used for inference. Each row must align with the header order.</param>
    /// <param name="sampleRowLimit">Optional cap on the number of rows to evaluate. Use a non-positive value for no cap.</param>
    public static IReadOnlyList<TargetField> InferTargetFields(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        int sampleRowLimit = 1000)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));

        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        var profilers = headers.Select(h => new ColumnProfiler(h)).ToArray();
        var rowCounter = 0;

        foreach (var row in rows)
        {
            if (row == null)
            {
                continue;
            }

            for (var i = 0; i < profilers.Length; i++)
            {
                var value = i < row.Count ? row[i] : null;
                profilers[i].Observe(value);
            }

            rowCounter++;
            if (sampleRowLimit > 0 && rowCounter >= sampleRowLimit)
            {
                break;
            }
        }

        return profilers.Select(p => p.ToTargetField()).ToList();
    }

    private sealed class ColumnProfiler
    {
        private const NumberStyles NumericStyles = NumberStyles.Integer;
        private const NumberStyles DecimalStyles = NumberStyles.Number;
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private bool _sawNonEmpty;
        private bool _sawEmpty;
        private bool _allInts = true;
        private bool _allDecimals = true;
        private bool _allBools = true;
        private bool _allDateTimes = true;
        private int _maxStringLength;

        public ColumnProfiler(string columnName)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        }

        public string ColumnName { get; }

        public void Observe(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                _sawEmpty = true;
                return;
            }

            _sawNonEmpty = true;

            var trimmed = raw.Trim();
            if (trimmed.Length > _maxStringLength)
            {
                _maxStringLength = trimmed.Length;
            }

            if (_allInts && !int.TryParse(trimmed, NumericStyles, Culture, out _))
            {
                _allInts = false;
            }

            if (_allDecimals && !decimal.TryParse(trimmed, DecimalStyles, Culture, out _))
            {
                _allDecimals = false;
            }

            if (_allBools && !TryParseBool(trimmed, out _))
            {
                _allBools = false;
            }

            if (_allDateTimes && !DateTime.TryParse(trimmed, Culture, DateTimeStyles.AssumeLocal, out _))
            {
                _allDateTimes = false;
            }
        }

        public TargetField ToTargetField()
        {
            var fieldType = DetermineFieldType();
            var isRequired = _sawNonEmpty && !_sawEmpty;

            return new TargetField
            {
                Name = ColumnName,
                FieldType = fieldType,
                IsRequired = isRequired,
                MaxLength = fieldType == FieldType.String
                    ? StringLengthBucketizer.GetSuggestedLength(_maxStringLength)
                    : null
            };
        }

        private FieldType DetermineFieldType()
        {
            if (!_sawNonEmpty)
            {
                return FieldType.String;
            }

            if (_allInts)
            {
                return FieldType.Int;
            }

            if (_allDecimals)
            {
                return FieldType.Decimal;
            }

            if (_allBools)
            {
                return FieldType.Bool;
            }

            if (_allDateTimes)
            {
                return FieldType.DateTime;
            }

            return FieldType.String;
        }

        private static bool TryParseBool(string raw, out bool value)
        {
            var s = raw.Trim().ToLowerInvariant();

            switch (s)
            {
                case "true":
                case "t":
                case "yes":
                case "y":
                case "1":
                    value = true;
                    return true;

                case "false":
                case "f":
                case "no":
                case "n":
                case "0":
                    value = false;
                    return true;

                default:
                    value = false;
                    return false;
            }
        }
    }
}
