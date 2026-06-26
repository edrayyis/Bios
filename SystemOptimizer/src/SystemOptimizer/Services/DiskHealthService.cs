using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.Versioning;
using SystemOptimizer.Models;
using static SystemOptimizer.Services.FileSystemHelpers;

namespace SystemOptimizer.Services;

/// <summary>
/// Reports disk health (SSD/HDD detection, SMART-style health status), volume
/// space usage, and finds large files. Optimization uses the built-in Windows
/// 'defrag /O', which correctly TRIMs SSDs and defragments HDDs - it never
/// defragments an SSD (which would just cause wear).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DiskHealthService
{
    public IReadOnlyList<VolumeInfo> GetVolumes()
    {
        var list = new List<VolumeInfo>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                list.Add(new VolumeInfo
                {
                    Drive = d.Name,
                    Label = d.VolumeLabel,
                    FileSystem = d.DriveFormat,
                    TotalBytes = d.TotalSize,
                    FreeBytes = d.TotalFreeSpace,
                });
            }
            catch { /* skip drives we can't read */ }
        }
        return list;
    }

    /// <summary>
    /// Reads physical disk model, media type, and health from the Storage WMI
    /// provider. Falls back gracefully if the provider isn't available.
    /// </summary>
    public IReadOnlyList<PhysicalDiskInfo> GetPhysicalDisks()
    {
        var list = new List<PhysicalDiskInfo>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT FriendlyName, MediaType, Size, HealthStatus FROM MSFT_PhysicalDisk"));

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                list.Add(new PhysicalDiskInfo
                {
                    Model = mo["FriendlyName"]?.ToString()?.Trim() ?? "Unknown",
                    MediaType = MediaTypeName(mo["MediaType"]),
                    SizeBytes = ToLong(mo["Size"]),
                    HealthStatus = HealthName(mo["HealthStatus"]),
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not read physical disk info: {ex.Message}");
        }
        return list;
    }

    /// <summary>
    /// Optimizes a volume the correct way for its media (TRIM for SSD, defrag
    /// for HDD) by invoking the built-in defrag tool. Requires elevation.
    /// </summary>
    public async Task<bool> OptimizeVolumeAsync(string driveLetter, CancellationToken ct)
    {
        string drive = driveLetter.TrimEnd('\\', '/');
        try
        {
            var psi = new ProcessStartInfo("defrag.exe", $"{drive} /O")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            await p.WaitForExitAsync(ct);
            Logger.Action($"Optimized volume {drive} (exit {p.ExitCode}).");
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Optimize failed for {drive}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Finds files at or above a size threshold under the given roots.</summary>
    public async Task<IReadOnlyList<LargeFileItem>> FindLargeFilesAsync(
        IEnumerable<string> roots,
        long minBytes,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var results = new List<LargeFileItem>();
            foreach (var root in roots.Where(Directory.Exists))
            {
                progress?.Report(new ScanProgress($"Scanning {root}...", 0, 0));
                foreach (var path in SafeEnumerateFiles(root))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(path);
                        if (fi.Length >= minBytes)
                            results.Add(new LargeFileItem
                            {
                                FullName = fi.FullName,
                                SizeBytes = fi.Length,
                                LastModified = fi.LastWriteTime,
                            });
                    }
                    catch { /* skip */ }
                }
            }
            return (IReadOnlyList<LargeFileItem>)results
                .OrderByDescending(f => f.SizeBytes)
                .Take(500)
                .ToList();
        }, ct);
    }

    // ---- WMI value mapping ------------------------------------------------

    private static string MediaTypeName(object? value) => ToInt(value) switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => "Unknown",
    };

    private static string HealthName(object? value) => ToInt(value) switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        _ => "Unknown",
    };

    private static int ToInt(object? o)
        => o is null ? -1 : (int.TryParse(o.ToString(), out var i) ? i : -1);

    private static long ToLong(object? o)
        => o is null ? 0 : (long.TryParse(o.ToString(), out var l) ? l : 0);
}
