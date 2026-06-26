namespace SystemOptimizer.Models;

/// <summary>One file within a duplicate set.</summary>
public sealed class DuplicateItem : SelectableItem
{
    public required string FullName { get; init; }
    public required string Hash { get; init; }

    /// <summary>"KEEP" for the retained copy, "DUPLICATE" for redundant copies.</summary>
    public required string Role { get; init; }

    /// <summary>Full path of the copy being kept for this set.</summary>
    public required string KeeperPath { get; init; }

    public DateTime LastModified { get; init; }

    public bool IsDuplicate => Role == "DUPLICATE";
}
