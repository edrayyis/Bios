using System.Collections.ObjectModel;
using System.Windows;
using SystemOptimizer.Infrastructure;
using SystemOptimizer.Models;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

/// <summary>
/// Drives the Startup &amp; Bloatware tab: lists logon startup items with
/// keep/delay/disable recommendations, and lists installed programs for
/// uninstalling.
/// </summary>
public sealed class StartupManagerViewModel : ViewModelBase
{
    private readonly StartupManagerService _startup = new();
    private readonly InstalledProgramsService _programs = new();

    public ObservableCollection<StartupEntry> StartupEntries { get; } = new();
    public ObservableCollection<InstalledProgram> Programs { get; } = new();

    private string _startupSummary = "";
    public string StartupSummary
    {
        get => _startupSummary;
        set => SetProperty(ref _startupSummary, value);
    }

    private string _programsSummary = "";
    public string ProgramsSummary
    {
        get => _programsSummary;
        set => SetProperty(ref _programsSummary, value);
    }

    private int _delaySeconds = 60;
    public int DelaySeconds
    {
        get => _delaySeconds;
        set => SetProperty(ref _delaySeconds, value);
    }

    public AsyncRelayCommand ScanStartupCommand { get; }
    public AsyncRelayCommand DisableSelectedCommand { get; }
    public AsyncRelayCommand DelaySelectedCommand { get; }
    public AsyncRelayCommand ScanProgramsCommand { get; }
    public RelayCommand<InstalledProgram> UninstallCommand { get; }

    public StartupManagerViewModel()
    {
        ScanStartupCommand = new AsyncRelayCommand(ScanStartupAsync, () => !IsBusy);
        DisableSelectedCommand = new AsyncRelayCommand(DisableSelectedAsync,
            () => !IsBusy && StartupEntries.Any(e => e.IsSelected));
        DelaySelectedCommand = new AsyncRelayCommand(DelaySelectedAsync,
            () => !IsBusy && StartupEntries.Any(e => e.IsSelected));
        ScanProgramsCommand = new AsyncRelayCommand(ScanProgramsAsync, () => !IsBusy);
        UninstallCommand = new RelayCommand<InstalledProgram>(Uninstall, p => p?.CanUninstall == true);
    }

    private async Task ScanStartupAsync()
    {
        try
        {
            IsBusy = true;
            Status = "Reading startup entries...";
            StartupEntries.Clear();
            var entries = await Task.Run(_startup.GetStartupEntries);
            foreach (var e in entries) StartupEntries.Add(e);

            int disable = entries.Count(e => e.Recommendation == StartupRecommendation.Disable);
            int delay = entries.Count(e => e.Recommendation == StartupRecommendation.Delay);
            StartupSummary = $"{entries.Count} startup items · {disable} safe to disable · " +
                             $"{delay} worth delaying";
            Status = "Review recommendations, then disable or delay selected items.";
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DisableSelectedAsync()
    {
        var selected = StartupEntries.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0) return;
        var confirm = MessageBox.Show(
            $"Disable {selected.Count} startup item(s)? They're backed up and can be " +
            "re-enabled later. The apps still work when launched manually.",
            "Confirm disable", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            int done = await Task.Run(() => selected.Count(_startup.Disable));
            foreach (var e in selected.Where(e => !e.IsEnabled).ToList())
                StartupEntries.Remove(e);
            Status = $"Disabled {done} startup item(s).";
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DelaySelectedAsync()
    {
        var selected = StartupEntries.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0) return;
        var confirm = MessageBox.Show(
            $"Delay {selected.Count} startup item(s) to {DelaySeconds}s after logon?\n\n" +
            "Each becomes a scheduled task and is removed from immediate startup. " +
            "This keeps the app available shortly after boot while speeding up logon.",
            "Confirm delay", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var failures = new List<string>();
            int done = await Task.Run(() =>
            {
                int ok = 0;
                foreach (var e in selected)
                {
                    string? err = _startup.DelayAfterBoot(e, DelaySeconds);
                    if (err is null) ok++;
                    else failures.Add($"{e.Name}: {err}");
                }
                return ok;
            });
            foreach (var e in selected.Where(e => !e.IsEnabled).ToList())
                StartupEntries.Remove(e);

            Status = failures.Count == 0
                ? $"Delayed {done} startup item(s) to {DelaySeconds}s after logon."
                : $"Delayed {done}; {failures.Count} failed (see log). First: {failures[0]}";
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ScanProgramsAsync()
    {
        try
        {
            IsBusy = true;
            Status = "Reading installed programs...";
            Programs.Clear();
            var list = await Task.Run(_programs.GetInstalledPrograms);
            foreach (var p in list) Programs.Add(p);
            long total = list.Sum(p => p.SizeBytes);
            ProgramsSummary = $"{list.Count} programs · {Formatting.HumanSize(total)} total";
            Status = "Select a program and click Uninstall to remove it.";
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void Uninstall(InstalledProgram? program)
    {
        if (program is null) return;
        var confirm = MessageBox.Show(
            $"Launch the uninstaller for \"{program.Name}\"?",
            "Confirm uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        if (_programs.Uninstall(program))
            Status = $"Launched uninstaller for {program.Name}.";
        else
            Status = $"Could not start uninstaller for {program.Name}.";
    }
}
