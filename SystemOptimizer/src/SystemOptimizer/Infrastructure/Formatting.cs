namespace SystemOptimizer.Infrastructure;

public static class Formatting
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    /// <summary>Formats a byte count as a human-readable size, e.g. "1.04 GB".</summary>
    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {Units[unit]}";
    }
}
