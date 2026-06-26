using System.Collections.ObjectModel;
using System.Windows;
using SystemOptimizer.Infrastructure;
using SystemOptimizer.Models;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

public sealed class JunkCleanupViewModel : ViewModelBase
{
    private readonly JunkCleanupService _service = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<JunkCategory> Categories { get; } = new();

    private string _summary = "";
    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    private bool _permanentDelete;
    public bool PermanentDelete
    {
        get => _permanentDelete;
        set => SetProperty(ref _permanentDelete, value);
    }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand CleanCommand { get; }

    public JunkCleanupViewModel()
    {
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        CleanCommand = new AsyncRelayCommand(CleanAsync,
            () => !IsBusy && Categories.Any(c => c.IsSelected && c.SizeBytes > 0));
    }

    private async Task ScanAsync()
    {
        try
        {
            IsBusy = true;
            Categories.Clear();
            Summary = "";
            _cts = new CancellationTokenSource();

            var cats = _service.BuildCategories();
            foreach (var c in cats) Categories.Add(c);

            await _service.ScanAsync(cats, CreateProgress(), _cts.Token);

            long total = cats.Sum(c => c.SizeBytes);
            Summary = $"{Formatting.HumanSize(total)} of junk found across " +
                      $"{cats.Count(c => c.SizeBytes > 0)} categories.";
            Status = "Scan complete. Review and clean selected categories.";
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; _cts?.Dispose(); _cts = null; }
    }

    private async Task CleanAsync()
    {
        var selected = Categories.Where(c => c.IsSelected && c.SizeBytes > 0).ToList();
        if (selected.Count == 0) return;

        long total = selected.Sum(c => c.SizeBytes);
        string mode = PermanentDelete ? "permanently delete" : "send to the Recycle Bin";
        var confirm = MessageBox.Show(
            $"This will {mode} {Formatting.HumanSize(total)} across " +
            $"{selected.Count} categories. Continue?",
            "Confirm cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();
            long freed = await _service.CleanAsync(selected, PermanentDelete, CreateProgress(), _cts.Token);
            Status = $"Freed {Formatting.HumanSize(freed)}.";
            // Re-scan to refresh remaining sizes.
            await ScanAsync();
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; _cts?.Dispose(); _cts = null; }
    }
}
