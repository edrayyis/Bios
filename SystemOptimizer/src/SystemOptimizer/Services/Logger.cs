namespace SystemOptimizer.Services;

/// <summary>
/// Append-only action log. Every destructive operation records what it did and
/// where, so there is always an audit trail under %LOCALAPPDATA%.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SystemOptimizer", "logs");

    public static string LogFile { get; } = Path.Combine(
        LogDirectory, $"actions_{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Action(string message) => Write("ACTION", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
