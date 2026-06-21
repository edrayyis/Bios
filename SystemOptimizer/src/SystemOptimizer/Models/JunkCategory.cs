namespace SystemOptimizer.Models;

/// <summary>A category of junk files (e.g. user temp, Windows Update cache).</summary>
public sealed class JunkCategory : SelectableItem
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Folders this category cleans. Multiple may roll up into one row.</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    /// <summary>Number of files found during the scan.</summary>
    public int FileCount { get; set; }

    /// <summary>True if cleaning this needs admin rights (e.g. system temp).</summary>
    public bool RequiresAdmin { get; init; }
}
