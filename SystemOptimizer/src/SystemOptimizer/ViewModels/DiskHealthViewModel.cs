using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Windows;
using SystemOptimizer.Infrastructure;
using SystemOptimizer.Models;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

[SupportedOSPlatform("windows")]
public sealed class DiskHealthViewModel : ViewModelBase
{
    private readonly DiskHealthService _service = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<PhysicalDiskInfo> Disks { get; } = new();
    public ObservableCollection<VolumeInfo> Volumes { get; } = new();
    public ObservableCollection<LargeFileItem> LargeFiles { get; } = new();

    private string _largeFileRoots = @"C:\Users";
    public string LargeFileRoots
    {
        get => _largeFileRoots;
        set => SetProperty(ref _largeFileRoots, value);
    }

    private int _minSizeMB = 100;
    public int MinSizeMB
    {
        get => _minSizeMB;
        set => SetProperty(ref _minSizeMB, value);
    }

    private VolumeInfo? _selectedVolume;
    public VolumeInfo? SelectedVolume
    {
        get => _selectedVolume;
        set => SetProperty(ref _selectedVolume, value);
    }

    public AsyncRelayCommand RefreshHealthCommand { get; }
    public AsyncRelayCommand FindLargeFilesCommand { get; }
    public AsyncRelayCommand OptimizeCommand { get; }

    public DiskHealthViewModel()
    {
        RefreshHealthCommand = new AsyncRelayCommand(RefreshHealthAsync, () => !IsBusy);
        FindLargeFilesCommand = new AsyncRelayCommand(FindLargeFilesAsync, () => !IsBusy);
        OptimizeCommand = new AsyncRelayCommand(OptimizeAsync,
            () => !IsBusy && SelectedVolume is not null);
    }

    private async Task RefreshHealthAsync()
    {
        try
        {
            IsBusy = true;
            Status = "Reading disk health...";
            Disks.Clear();
            Volumes.Clear();

            var (disks, volumes) = await Task.Run(()
                => (_service.GetPhysicalDisks(), _service.GetVolumes()));
            foreach (var d in disks) Disks.Add(d);
            foreach (var v in volumes) Volumes.Add(v);

            Status = $"{disks.Count} physical disk(s), {volumes.Count} volume(s). " +
                     "Select a volume to optimize (TRIM for SSD, defrag for HDD).";
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task FindLargeFilesAsync()
    {
        try
        {
            IsBusy = true;
            LargeFiles.Clear();
            _cts = new CancellationTokenSource();
            var roots = LargeFileRoots.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            long minBytes = (long)MinSizeMB * 1024 * 1024;
            var files = await _service.FindLargeFilesAsync(roots, minBytes, CreateProgress(), _cts.Token);
            foreach (var f in files) LargeFiles.Add(f);
            Status = $"Top {files.Count} files ≥ {MinSizeMB} MB. Review in Explorer before deleting.";
        }
        catch (OperationCanceledException) { Status = "Search cancelled."; }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; _cts?.Dispose(); _cts = null; }
    }

    private async Task OptimizeAsync()
    {
        if (SelectedVolume is null) return;
        var confirm = MessageBox.Show(
            $"Optimize volume {SelectedVolume.Drive}?\n\n" +
            "Windows will TRIM it if it's an SSD, or defragment it if it's an HDD. " +
            "This is safe and may take a while.",
            "Confirm optimize", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();
            Status = $"Optimizing {SelectedVolume.Drive}... this can take several minutes.";
            bool ok = await _service.OptimizeVolumeAsync(SelectedVolume.Drive, _cts.Token);
            Status = ok
                ? $"Optimized {SelectedVolume.Drive}."
                : $"Optimization of {SelectedVolume.Drive} did not complete cleanly.";
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; _cts?.Dispose(); _cts = null; }
    }
}
