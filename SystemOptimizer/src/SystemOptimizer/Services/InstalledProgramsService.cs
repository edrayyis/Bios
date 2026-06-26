using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

/// <summary>
/// Lists installed applications (from the standard uninstall registry keys) and
/// launches their official uninstallers. We never delete program files
/// ourselves - we hand off to the app's own uninstaller, which is the safe and
/// supported way to remove software.
/// </summary>
public sealed class InstalledProgramsService
{
    private static readonly (RegistryKey Hive, string Path)[] UninstallKeys =
    {
        (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    public IReadOnlyList<InstalledProgram> GetInstalledPrograms()
    {
        var byName = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, path) in UninstallKeys)
        {
            using var root = hive.OpenSubKey(path);
            if (root is null) continue;

            foreach (var subName in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(subName);
                if (sub is null) continue;

                string name = sub.GetValue("DisplayName")?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Skip system components and updates.
                if ((sub.GetValue("SystemComponent") as int?) == 1) continue;
                if (!string.IsNullOrEmpty(sub.GetValue("ParentKeyName")?.ToString())) continue;

                long size = 0;
                if (sub.GetValue("EstimatedSize") is int kb) size = (long)kb * 1024;

                var program = new InstalledProgram
                {
                    Name = name,
                    Publisher = sub.GetValue("Publisher")?.ToString() ?? "",
                    Version = sub.GetValue("DisplayVersion")?.ToString() ?? "",
                    UninstallString = sub.GetValue("QuietUninstallString")?.ToString()
                                      ?? sub.GetValue("UninstallString")?.ToString() ?? "",
                    InstallLocation = sub.GetValue("InstallLocation")?.ToString() ?? "",
                    InstallDate = ParseInstallDate(sub.GetValue("InstallDate")?.ToString()),
                    SizeBytes = size,
                };

                // De-dupe across hives, preferring the entry that has a size.
                if (!byName.TryGetValue(name, out var existing) || existing.SizeBytes == 0)
                    byName[name] = program;
            }
        }

        return byName.Values
            .OrderByDescending(p => p.SizeBytes)
            .ThenBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Launches the program's own uninstaller. Returns false if there's no
    /// uninstall command. This is interactive - the app's uninstaller UI shows.
    /// </summary>
    public bool Uninstall(InstalledProgram program)
    {
        if (!program.CanUninstall) return false;
        try
        {
            // UninstallString is a full command line; run it via cmd so quoted
            // paths and arguments are handled correctly.
            var psi = new ProcessStartInfo("cmd.exe", $"/c {program.UninstallString}")
            {
                UseShellExecute = true,
            };
            Process.Start(psi);
            Logger.Action($"Launched uninstaller for '{program.Name}'.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to start uninstaller for '{program.Name}': {ex.Message}");
            return false;
        }
    }

    private static DateTime? ParseInstallDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt) ? dt : null;
    }
}
