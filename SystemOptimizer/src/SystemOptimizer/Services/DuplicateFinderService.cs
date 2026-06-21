using SystemOptimizer.Models;
using static SystemOptimizer.Services.FileSystemHelpers;

namespace SystemOptimizer.Services;

/// <summary>
/// Finds byte-identical duplicate files using the same safe strategy as the
/// original PowerShell script: group by size first, only hash size-collisions,
/// then group by SHA-256. Keeps one copy per set; quarantines before deleting.
/// </summary>
public sealed class DuplicateFinderService
{
    /// <summary>
    /// Path fragments that are never scanned by default. These hold OS and
    /// installed-app files; identical files here are not user junk, and many
    /// (e.g. WindowsApps) are access-denied even to administrators.
    /// </summary>
    public static readonly string[] DefaultExcludedFragments =
    {
        @"\WindowsApps\", @"\Windows\", @"\Program Files\", @"\Program Files (x86)\",
        @"\ProgramData\", @"\$Recycle.Bin\", @"\System Volume Information\",
        @"\AppData\Local\Packages\", @"\AppData\Local\Microsoft\WindowsApps\",
    };

    private static bool IsExcluded(string fullPath, IReadOnlyCollection<string> fragments)
    {
        string normalized = "\\" + fullPath.TrimStart('\\');
        foreach (var frag in fragments)
            if (normalized.Contains(frag, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Scans the given roots and returns every file that belongs to a duplicate
    /// set (both the kept copy and the redundant ones), so the UI can show the
    /// full picture. Honors cancellation and reports progress.
    /// </summary>
    public async Task<IReadOnlyList<DuplicateItem>> ScanAsync(
        IEnumerable<string> roots,
        ISet<string>? includeExtensions,
        long minBytes,
        IProgress<ScanProgress>? progress,
        CancellationToken ct,
        IReadOnlyCollection<string>? extraExcludedFragments = null)
    {
        // Built-in system exclusions plus any caller-supplied ones.
        var excludes = new List<string>(DefaultExcludedFragments);
        if (extraExcludedFragments is { Count: > 0 })
            excludes.AddRange(extraExcludedFragments);

        return await Task.Run(() =>
        {
            var files = new List<FileInfo>();
            progress?.Report(new ScanProgress("Scanning for files...", 0, 0));

            foreach (var root in roots.Where(Directory.Exists))
            {
                ct.ThrowIfCancellationRequested();
                foreach (var path in SafeEnumerateFiles(root))
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsExcluded(path, excludes)) continue;

                    FileInfo fi;
                    try { fi = new FileInfo(path); }
                    catch { continue; }

                    if (fi.Length < minBytes) continue;
                    if (includeExtensions is { Count: > 0 } &&
                        !includeExtensions.Contains(fi.Extension.ToLowerInvariant()))
                        continue;

                    files.Add(fi);
                }
            }

            // Only files sharing an exact size can possibly be identical.
            var sizeCollisions = files
                .GroupBy(f => f.Length)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            var hashed = new List<(string Hash, FileInfo Info)>();
            int i = 0;
            foreach (var fi in sizeCollisions)
            {
                ct.ThrowIfCancellationRequested();
                i++;
                progress?.Report(new ScanProgress(
                    $"Hashing: {fi.Name}", i, sizeCollisions.Count));
                try
                {
                    hashed.Add((ComputeSha256(fi.FullName), fi));
                }
                catch
                {
                    // Locked / no access — skip, mirroring the PowerShell behavior.
                    Logger.Warn($"Could not hash (in use/no access): {fi.FullName}");
                }
            }

            var results = new List<DuplicateItem>();
            foreach (var group in hashed.GroupBy(h => h.Hash).Where(g => g.Count() > 1))
            {
                // Keeper = shortest path, then oldest.
                var ordered = group
                    .OrderBy(x => x.Info.FullName.Length)
                    .ThenBy(x => x.Info.LastWriteTime)
                    .ToList();
                var keeper = ordered[0].Info;

                foreach (var (hash, info) in ordered)
                {
                    bool isKeeper = string.Equals(info.FullName, keeper.FullName,
                        StringComparison.OrdinalIgnoreCase);
                    results.Add(new DuplicateItem
                    {
                        Hash = hash,
                        Role = isKeeper ? "KEEP" : "DUPLICATE",
                        FullName = info.FullName,
                        SizeBytes = info.Length,
                        LastModified = info.LastWriteTime,
                        KeeperPath = keeper.FullName,
                        // Pre-select redundant copies; never the keeper.
                        IsSelected = !isKeeper,
                    });
                }
            }

            progress?.Report(new ScanProgress("Scan complete.", sizeCollisions.Count,
                sizeCollisions.Count));
            return (IReadOnlyList<DuplicateItem>)results;
        }, ct);
    }

    /// <summary>
    /// Moves the selected duplicates into a quarantine folder, preserving their
    /// original drive + path so they can be reviewed or restored. Returns the
    /// number successfully moved.
    /// </summary>
    public async Task<int> QuarantineAsync(
        IEnumerable<DuplicateItem> items, string quarantineRoot, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(quarantineRoot);
            int moved = 0;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(item.FullName)) continue;
                try
                {
                    string driveLetter = item.FullName[..1];
                    string relative = item.FullName.Length > 3 ? item.FullName[3..] : item.FullName;
                    string dest = Path.Combine(quarantineRoot, driveLetter, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(item.FullName, dest, overwrite: true);
                    Logger.Action($"Quarantined duplicate: {item.FullName} -> {dest}");
                    moved++;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Could not quarantine {item.FullName}: {ex.Message}");
                }
            }
            return moved;
        }, ct);
    }
}
