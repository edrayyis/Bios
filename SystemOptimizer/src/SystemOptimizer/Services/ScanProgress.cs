namespace SystemOptimizer.Services;

/// <summary>Progress payload reported from background scans to the UI.</summary>
public sealed record ScanProgress(string Status, int Current, int Total)
{
    public double Percent => Total <= 0 ? 0 : Math.Min(100.0, (double)Current / Total * 100.0);
}
