using SystemOptimizer.Infrastructure;

namespace SystemOptimizer.Models;

/// <summary>
/// Base for any row shown in a results grid with a checkbox. Selection state
/// and size are observable so "select all" toggles and post-scan size updates
/// refresh totals live.
/// </summary>
public abstract class SelectableItem : ObservableObject
{
    private bool _isSelected;
    private long _sizeBytes;

    /// <summary>Whether this item is included in the next apply action.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Bytes this item represents (for "space recoverable" totals).</summary>
    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
                OnPropertyChanged(nameof(SizeDisplay));
        }
    }

    public string SizeDisplay => Formatting.HumanSize(SizeBytes);
}
