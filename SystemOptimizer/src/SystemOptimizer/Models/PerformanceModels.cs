using SystemOptimizer.Infrastructure;

namespace SystemOptimizer.Models;

/// <summary>Snapshot of physical + commit memory usage.</summary>
public sealed class MemoryStatus
{
    public long TotalBytes { get; init; }
    public long AvailableBytes { get; init; }
    public int UsedPercent { get; init; }
    public long CommitTotalBytes { get; init; }
    public long CommitAvailableBytes { get; init; }

    public long UsedBytes => TotalBytes - AvailableBytes;
    public string TotalDisplay => Formatting.HumanSize(TotalBytes);
    public string UsedDisplay => Formatting.HumanSize(UsedBytes);
    public string AvailableDisplay => Formatting.HumanSize(AvailableBytes);
}

/// <summary>A running process, surfaced for the process manager.</summary>
public sealed class ProcessItem
{
    public required int Pid { get; init; }
    public required string Name { get; init; }
    public long WorkingSetBytes { get; init; }
    public string WorkingSetDisplay => Formatting.HumanSize(WorkingSetBytes);
}

public enum PowerPlanKind { Balanced, HighPerformance, Ultimate, PowerSaver }

public enum VisualEffectsMode { BestPerformance, BestAppearance, LetWindowsDecide }
