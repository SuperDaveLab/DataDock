using System.Collections.Generic;

namespace SchemaViz.Gui.ViewModels.Diagram;

public sealed class TableColumnViewModel : ViewModelBase
{
    private bool _isPrimaryKey;
    private bool _isForeignKey;

    public TableColumnViewModel(string name, string dataType, bool isPrimaryKey = false, bool isForeignKey = false)
    {
        Name = name;
        DataType = dataType;
        _isPrimaryKey = isPrimaryKey;
        _isForeignKey = isForeignKey;
    }

    public string Name { get; }
    public string DataType { get; }

    public bool IsPrimaryKey
    {
        get => _isPrimaryKey;
        set
        {
            if (SetProperty(ref _isPrimaryKey, value))
            {
                OnPropertyChanged(nameof(DiagramLabel));
                OnPropertyChanged(nameof(BadgeText));
            }
        }
    }

    public bool IsForeignKey
    {
        get => _isForeignKey;
        set
        {
            if (SetProperty(ref _isForeignKey, value))
            {
                OnPropertyChanged(nameof(DiagramLabel));
                OnPropertyChanged(nameof(BadgeText));
            }
        }
    }

    public string BadgeText
    {
        get
        {
            var badges = new List<string>();
            if (IsPrimaryKey)
            {
                badges.Add("[PK]");
            }

            if (IsForeignKey)
            {
                badges.Add("[FK]");
            }

            return badges.Count == 0 ? string.Empty : string.Join(' ', badges);
        }
    }

    public string DiagramLabel
    {
        get
        {
            var badgeSuffix = string.IsNullOrWhiteSpace(BadgeText) ? string.Empty : $"  {BadgeText}";
            return $"{Name} : {DataType}{badgeSuffix}";
        }
    }
}
