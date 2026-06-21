namespace SystemOptimizer.Models;

public enum StartupSource
{
    RegistryCurrentUser,
    RegistryLocalMachine,
    StartupFolderUser,
    StartupFolderCommon,
}

/// <summary>What the analyzer suggests doing with a startup entry.</summary>
public enum StartupRecommendation
{
    Keep,          // Leave it: driver, security, input, etc.
    Delay,         // Useful but not needed at boot - run a bit after logon.
    Disable,       // Updater / optional helper - safe to turn off.
    Unknown,       // Couldn't classify - leave decision to the user.
}

/// <summary>Rough guess at how much this entry slows boot.</summary>
public enum BootImpact { Low, Medium, High, Unknown }

/// <summary>A program configured to run at logon/boot.</summary>
public sealed class StartupEntry : SelectableItem
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupSource Source { get; init; }

    /// <summary>Registry key path or startup folder path the entry lives in.</summary>
    public required string Location { get; init; }

    public bool IsEnabled { get; set; } = true;

    public StartupRecommendation Recommendation { get; set; } = StartupRecommendation.Unknown;
    public BootImpact Impact { get; set; } = BootImpact.Unknown;

    /// <summary>Human-readable reason for the recommendation, shown in the UI.</summary>
    public string Reason { get; set; } = "";

    public string SourceDisplay => Source switch
    {
        StartupSource.RegistryCurrentUser => "Registry (current user)",
        StartupSource.RegistryLocalMachine => "Registry (all users)",
        StartupSource.StartupFolderUser => "Startup folder (you)",
        StartupSource.StartupFolderCommon => "Startup folder (all users)",
        _ => Source.ToString(),
    };

    public string RecommendationDisplay => Recommendation switch
    {
        StartupRecommendation.Keep => "Keep",
        StartupRecommendation.Delay => "Delay after boot",
        StartupRecommendation.Disable => "Safe to disable",
        _ => "Your call",
    };
}
