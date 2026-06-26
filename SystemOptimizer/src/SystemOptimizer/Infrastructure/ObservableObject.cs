using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SystemOptimizer.Infrastructure;

/// <summary>
/// Minimal INotifyPropertyChanged base so view models can raise change
/// notifications without pulling in an external MVVM framework.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Sets a backing field and raises PropertyChanged only when the value changed.
    /// Returns true if the value actually changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
