namespace SystemOptimizer.Models;

/// <summary>An installed application discovered from the uninstall registry keys.</summary>
public sealed class InstalledProgram : SelectableItem
{
    public required string Name { get; init; }
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string UninstallString { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public DateTime? InstallDate { get; init; }

    public string InstallDateDisplay => InstallDate?.ToString("yyyy-MM-dd") ?? "";
    public bool CanUninstall => !string.IsNullOrWhiteSpace(UninstallString);
}
