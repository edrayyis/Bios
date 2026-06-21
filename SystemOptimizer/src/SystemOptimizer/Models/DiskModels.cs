using SystemOptimizer.Infrastructure;

namespace SystemOptimizer.Models;

/// <summary>A physical drive with health and media-type info from WMI.</summary>
public sealed class PhysicalDiskInfo
{
    public required string Model { get; init; }
    public required string MediaType { get; init; }   // "SSD", "HDD", "Unknown"
    public long SizeBytes { get; init; }
    public required string HealthStatus { get; init; } // "Healthy", "Warning", "Unhealthy"
    public bool IsSsd => MediaType == "SSD";
    public string SizeDisplay => Formatting.HumanSize(SizeBytes);
}

/// <summary>A mounted volume (drive letter) with space usage.</summary>
public sealed class VolumeInfo
{
    public required string Drive { get; init; }
    public string Label { get; init; } = "";
    public string FileSystem { get; init; } = "";
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }

    public long UsedBytes => TotalBytes - FreeBytes;
    public double UsedPercent => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes * 100.0;
    public string TotalDisplay => Formatting.HumanSize(TotalBytes);
    public string FreeDisplay => Formatting.HumanSize(FreeBytes);
    public string UsedDisplay => Formatting.HumanSize(UsedBytes);
}

/// <summary>A large file surfaced by the space finder.</summary>
public sealed class LargeFileItem : SelectableItem
{
    public required string FullName { get; init; }
    public DateTime LastModified { get; init; }
    public string LastModifiedDisplay => LastModified.ToString("yyyy-MM-dd");
}
