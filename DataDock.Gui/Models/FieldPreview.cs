using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DataDock.Core.Models;
using DataDock.Core.Services;

namespace DataDock.Gui.Models;

public sealed class FieldPreview : INotifyPropertyChanged
{
    private string _displayName;
    private FieldType _fieldType;
    private bool _hasCustomName;
    private bool _suppressCustomTracking;
    private bool _allowsNull = true;
    private int? _maxLength;
    private bool _isSelected = true;

    public FieldPreview(string sourceName, FieldType fieldType, bool allowsNull = true, int? maxLength = null)
    {
        SourceName = sourceName;
        _fieldType = fieldType;
        _displayName = sourceName;
        _allowsNull = allowsNull;
        _maxLength = fieldType == FieldType.String ? maxLength : null;
        EnsureStringLengthDefault();
    }

    public string SourceName { get; }
    public FieldType FieldType
    {
        get => _fieldType;
        set
        {
            if (SetField(ref _fieldType, value))
            {
                OnPropertyChanged(nameof(FieldTypeDisplay));
                OnPropertyChanged(nameof(RequiresLength));

                if (value != FieldType.String)
                {
                    MaxLength = null;
                }
                else if (!_maxLength.HasValue)
                {
                    MaxLength = StringLengthBucketizer.GetSuggestedLength(0);
                }
            }
        }
    }

    public string FieldTypeDisplay => FieldType.ToString();

    public bool RequiresLength => FieldType == FieldType.String;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetField(ref _displayName, value) && !_suppressCustomTracking)
            {
                HasCustomName = true;
            }
        }
    }

    public bool HasCustomName
    {
        get => _hasCustomName;
        private set => SetField(ref _hasCustomName, value);
    }

    public bool AllowsNull
    {
        get => _allowsNull;
        set => SetField(ref _allowsNull, value);
    }

    public int? MaxLength
    {
        get => _maxLength;
        set => SetField(ref _maxLength, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyStyle(ColumnNameStyle style, bool force = false)
    {
        if (!force && HasCustomName)
        {
            return;
        }

        var generated = ColumnNameGenerator.ToColumnName(SourceName, style);
        _suppressCustomTracking = true;
        try
        {
            DisplayName = generated;
            HasCustomName = false;
        }
        finally
        {
            _suppressCustomTracking = false;
        }
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void EnsureStringLengthDefault()
    {
        if (FieldType == FieldType.String && !_maxLength.HasValue)
        {
            _maxLength = StringLengthBucketizer.GetSuggestedLength(0);
        }
    }
}
