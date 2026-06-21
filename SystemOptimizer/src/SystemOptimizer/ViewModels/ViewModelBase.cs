using SystemOptimizer.Infrastructure;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

/// <summary>
/// Shared state for module view models: a status line, a busy flag, and a
/// 0-100 progress value the views bind their progress bars to.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private string _status = "Ready.";
    private bool _isBusy;
    private double _progress;

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Progress 0-100 for the module's progress bar.</summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    /// <summary>Builds an IProgress that marshals updates onto the UI thread.</summary>
    protected IProgress<ScanProgress> CreateProgress()
        => new Progress<ScanProgress>(p =>
        {
            Status = p.Status;
            Progress = p.Percent;
        });
}
