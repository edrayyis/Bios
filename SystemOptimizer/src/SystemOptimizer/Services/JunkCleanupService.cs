using System.IO;
using SystemOptimizer.Models;
using static SystemOptimizer.Services.FileSystemHelpers;

namespace SystemOptimizer.Services;

/// <summary>
/// Identifies and clears common safe-to-remove junk: temp folders, caches, and
/// update leftovers. Deletes go to the Recycle Bin by default so they're
/// recoverable. Only well-known, safe locations are ever offered.
/// </summary>
public sealed class JunkCleanupService
{
    /// <summary>
    /// Builds the list of junk categories with their resolved paths for this
    /// machine/user. Paths that don't exist are dropped.
    /// </summary>
    public IReadOnlyList<JunkCategory> BuildCategories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string userTemp = Path.GetTempPath();

        var categories = new List<JunkCategory>
        {
            new()
            {
                Name = "User temporary files",
                Description = "Files in your %TEMP% folder left behind by apps.",
                Paths = Existing(userTemp),
            },
            new()
            {
                Name = "Windows temporary files",
                Description = "System temp folder (C:\\Windows\\Temp).",
                Paths = Existing(Path.Combine(windir, "Temp")),
                RequiresAdmin = true,
            },
            new()
            {
                Name = "Windows Update cache",
                Description = "Downloaded update files in SoftwareDistribution\\Download.",
                Paths = Existing(Path.Combine(windir, "SoftwareDistribution", "Download")),
                RequiresAdmin = true,
            },
            new()
            {
                Name = "Thumbnail cache",
                Description = "Explorer thumbnail cache (rebuilds automatically).",
                Paths = Existing(Path.Combine(local, "Microsoft", "Windows", "Explorer")),
            },
            new()
            {
                Name = "Internet Explorer / Edge legacy cache",
                Description = "Old WebCache / INetCache temporary internet files.",
                Paths = Existing(
                    Path.Combine(local, "Microsoft", "Windows", "INetCache")),
            },
            new()
            {
                Name = "Chrome cache",
                Description = "Google Chrome's on-disk cache (rebuilds automatically).",
                Paths = Existing(
                    Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache")),
            },
            new()
            {
                Name = "Crash dumps",
                Description = "Windows error-reporting and minidump files.",
                Paths = Existing(
                    Path.Combine(local, "CrashDumps"),
                    Path.Combine(windir, "Minidump")),
                RequiresAdmin = true,
            },
        };

        // Drop categories with no existing paths on this machine.
        return categories.Where(c => c.Paths.Count > 0).ToList();
    }

    /// <summary>Computes size + file count for each category (no changes made).</summary>
    public async Task ScanAsync(
        IReadOnlyList<JunkCategory> categories,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        await Task.Run(() =>
        {
            int i = 0;
            foreach (var cat in categories)
            {
                ct.ThrowIfCancellationRequested();
                i++;
                progress?.Report(new ScanProgress($"Measuring: {cat.Name}", i, categories.Count));

                long bytes = 0;
                int count = 0;
                foreach (var p in cat.Paths)
                {
                    foreach (var f in SafeEnumerateFiles(p))
                    {
                        ct.ThrowIfCancellationRequested();
                        try { bytes += new FileInfo(f).Length; count++; }
                        catch { /* skip locked/unreadable */ }
                    }
                }

                // SizeBytes is init-only on the base; expose via reflection-free copy.
                cat.FileCount = count;
                cat.SizeBytes = bytes;
                // Pre-select non-admin categories that actually have content.
                cat.IsSelected = count > 0;
            }
        }, ct);
    }

    /// <summary>
    /// Deletes the contents of the selected categories. Sends files to the
    /// Recycle Bin unless <paramref name="permanent"/> is true. Returns bytes freed.
    /// </summary>
    public async Task<long> CleanAsync(
        IEnumerable<JunkCategory> categories,
        bool permanent,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            long freed = 0;
            var list = categories.ToList();
            int i = 0;
            foreach (var cat in list)
            {
                ct.ThrowIfCancellationRequested();
                i++;
                progress?.Report(new ScanProgress($"Cleaning: {cat.Name}", i, list.Count));
                foreach (var p in cat.Paths)
                {
                    foreach (var f in SafeEnumerateFiles(p))
                    {
                        ct.ThrowIfCancellationRequested();
                        freed += DeleteFile(f, permanent);
                    }
                }
                Logger.Action($"Cleaned junk category '{cat.Name}'.");
            }
            return freed;
        }, ct);
    }

    private static IReadOnlyList<string> Existing(params string[] paths)
        => paths.Where(Directory.Exists).ToList();
}
