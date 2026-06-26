using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using SystemOptimizer.Infrastructure;
using SystemOptimizer.Models;
using SystemOptimizer.Services;

namespace SystemOptimizer.ViewModels;

[SupportedOSPlatform("windows")]
public sealed class PerformanceViewModel : ViewModelBase
{
    private readonly PerformanceService _service = new();
    private readonly DispatcherTimer _timer;

    public ObservableCollection<ProcessItem> Processes { get; } = new();

    private MemoryStatus _memory = new();
    public MemoryStatus Memory
    {
        get => _memory;
        private set => SetProperty(ref _memory, value);
    }

    private string _activePowerPlan = "—";
    public string ActivePowerPlan
    {
        get => _activePowerPlan;
        private set => SetProperty(ref _activePowerPlan, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ClearStandbyCommand { get; }
    public RelayCommand<ProcessItem> KillProcessCommand { get; }
    public RelayCommand<string> SetPowerPlanCommand { get; }
    public RelayCommand<string> SetVisualEffectsCommand { get; }

    public PerformanceViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        ClearStandbyCommand = new RelayCommand(ClearStandby);
        KillProcessCommand = new RelayCommand<ProcessItem>(Kill, p => p is not null);
        SetPowerPlanCommand = new RelayCommand<string>(SetPowerPlan);
        SetVisualEffectsCommand = new RelayCommand<string>(SetVisualEffects);

        Refresh();

        // Live-refresh memory + processes every 3s.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        try
        {
            Memory = _service.GetMemory();
            ActivePowerPlan = _service.GetActivePowerPlanName();

            var top = _service.GetTopProcesses(25);
            Processes.Clear();
            foreach (var p in top) Processes.Add(p);
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    private void ClearStandby()
    {
        var (ok, freed) = _service.ClearStandbyCache();
        Status = ok
            ? $"Standby cache flushed — ~{Formatting.HumanSize(freed)} became available."
            : "Standby flush failed (needs Administrator). See log.";
        Refresh();
    }

    private void Kill(ProcessItem? p)
    {
        if (p is null) return;
        var confirm = MessageBox.Show(
            $"End process \"{p.Name}\" (PID {p.Pid})?\n\n" +
            "Unsaved work in that program will be lost.",
            "Confirm end process", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        Status = _service.KillProcess(p.Pid)
            ? $"Ended {p.Name}."
            : $"Could not end {p.Name} (may be protected).";
        Refresh();
    }

    private void SetPowerPlan(string? kind)
    {
        if (!Enum.TryParse<PowerPlanKind>(kind, out var plan)) return;
        Status = _service.SetPowerPlan(plan)
            ? $"Power plan set to {plan}."
            : $"Could not set power plan {plan}.";
        ActivePowerPlan = _service.GetActivePowerPlanName();
    }

    private void SetVisualEffects(string? mode)
    {
        if (!Enum.TryParse<VisualEffectsMode>(mode, out var m)) return;
        bool ok = _service.SetVisualEffects(m);
        Status = ok
            ? $"Visual effects set to {m}. Sign out and back in for full effect."
            : "Could not change visual effects.";
    }
}
