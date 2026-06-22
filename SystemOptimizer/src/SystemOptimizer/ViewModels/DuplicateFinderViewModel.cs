using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using SystemOptimizer.Infrastructure;
using SystemOptimizer.Models;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

public sealed class DuplicateFinderViewModel : ViewModelBase
{
    private readonly DuplicateFinderService _service = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<DuplicateItem> Results { get; } = new();

    /// <summary>Semicolon-separated roots to scan, e.g. "D:\;G:\".</summary>
    private string _scanPaths = @"D:\;G:\";
    public string ScanPaths
    {
        get => _scanPaths;
        set => SetProperty(ref _scanPaths, value);
    }

    private string _extensions = ".jpg;.jpeg;.png;.heic;.gif;.bmp;.tiff";
    public string Extensions
    {
        get => _extensions;
        set => SetProperty(ref _extensions, value);
    }

    private bool _photosOnly = true;
    public bool PhotosOnly
    {
        get => _photosOnly;
        set => SetProperty(ref _photosOnly, value);
    }

    private string _quarantinePath = @"D:\_DuplicateReview";
    public string QuarantinePath
    {
        get => _quarantinePath;
        set => SetProperty(ref _quarantinePath, value);
    }

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
    public AsyncRelayCommand QuarantineCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }

    public DuplicateFinderViewModel()
    {
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        QuarantineCommand = new AsyncRelayCommand(QuarantineAsync,
            () => !IsBusy && HasSelectedDuplicates());
        DeleteCommand = new AsyncRelayCommand(DeleteAsync,
            () => !IsBusy && HasSelectedDuplicates());
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
    }

    private bool HasSelectedDuplicates()
        => Results.Any(r => r is { IsSelected: true, IsDuplicate: true });

    private async Task ScanAsync()
    {
        try
        {
            IsBusy = true;
            Summary = "";
            Results.Clear();
            _cts = new CancellationTokenSource();

            var roots = ScanPaths.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            ISet<string>? exts = PhotosOnly
                ? Extensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                    .ToHashSet()
                : null;

            var items = await _service.ScanAsync(roots, exts, minBytes: 1024,
                CreateProgress(), _cts.Token);

            foreach (var item in items)
                Results.Add(item);

            int sets = items.Where(i => i.IsDuplicate)
                .Select(i => i.Hash).Distinct().Count();
            int dupes = items.Count(i => i.IsDuplicate);
            long recoverable = items.Where(i => i.IsDuplicate).Sum(i => i.SizeBytes);
            Summary = $"{sets} duplicate sets · {dupes} redundant copies · " +
                      $"{Formatting.HumanSize(recoverable)} recoverable";
            Status = "Scan complete. Review, then quarantine selected duplicates.";
        }
        catch (OperationCanceledException)
        {
            Status = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task DeleteAsync()
    {
        var selected = Results.Where(r => r is { IsSelected: true, IsDuplicate: true }).ToList();
        if (selected.Count == 0) return;

        long total = selected.Sum(s => s.SizeBytes);
        string mode = PermanentDelete ? "permanently delete" : "send to the Recycle Bin";
        var confirm = MessageBox.Show(
            $"This will {mode} {selected.Count} duplicate file(s) " +
            $"({Formatting.HumanSize(total)}).\n\n" +
            "The kept copy of each set stays in place. Continue?",
            "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();
            var (count, bytes) = await _service.DeleteAsync(selected, PermanentDelete, _cts.Token);
            foreach (var item in selected)
                Results.Remove(item);
            string where = PermanentDelete ? "permanently" : "to the Recycle Bin";
            Status = $"Deleted {count} file(s) {where}, freed {Formatting.HumanSize(bytes)}.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task QuarantineAsync()
    {
        var selected = Results.Where(r => r is { IsSelected: true, IsDuplicate: true }).ToList();
        if (selected.Count == 0) return;

        var confirm = MessageBox.Show(
            $"Move {selected.Count} duplicate file(s) to:\n{QuarantinePath}\n\n" +
            "The kept copy of each set stays in place. Files are recoverable from " +
            "the quarantine folder. Continue?",
            "Confirm quarantine", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();
            int moved = await _service.QuarantineAsync(selected, QuarantinePath, _cts.Token);
            foreach (var item in selected)
                Results.Remove(item);
            Status = $"Quarantined {moved} file(s) to {QuarantinePath}.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }
}
