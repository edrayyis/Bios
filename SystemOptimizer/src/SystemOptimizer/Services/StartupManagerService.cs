using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

/// <summary>
/// Enumerates everything that runs at logon (registry Run keys + Startup
/// folders), recommends what to keep / delay / disable, and applies those
/// changes reversibly. Disabled entries are backed up so they can be restored.
/// </summary>
public sealed class StartupManagerService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string BackupKey = @"Software\SystemOptimizer\DisabledStartup";

    // ---- Enumeration ------------------------------------------------------

    public IReadOnlyList<StartupEntry> GetStartupEntries()
    {
        var entries = new List<StartupEntry>();

        ReadRunKey(Registry.CurrentUser, StartupSource.RegistryCurrentUser, entries);
        ReadRunKey(Registry.LocalMachine, StartupSource.RegistryLocalMachine, entries);

        ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupSource.StartupFolderUser, entries);
        ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            StartupSource.StartupFolderCommon, entries);

        foreach (var e in entries)
            Classify(e);

        return entries
            .OrderByDescending(e => e.Recommendation == StartupRecommendation.Disable)
            .ThenByDescending(e => e.Impact)
            .ToList();
    }

    private static void ReadRunKey(RegistryKey hive, StartupSource source, List<StartupEntry> into)
    {
        using var key = hive.OpenSubKey(RunSubKey);
        if (key is null) return;
        foreach (var name in key.GetValueNames())
        {
            string cmd = key.GetValue(name)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cmd)) continue;
            into.Add(new StartupEntry
            {
                Name = name,
                Command = cmd,
                Source = source,
                Location = $@"{(source == StartupSource.RegistryCurrentUser ? "HKCU" : "HKLM")}\{RunSubKey}",
            });
        }
    }

    private static void ReadStartupFolder(string folder, StartupSource source, List<StartupEntry> into)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder)
                     .Where(f => !f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)))
        {
            into.Add(new StartupEntry
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Command = file,
                Source = source,
                Location = folder,
            });
        }
    }

    // ---- Recommendation engine -------------------------------------------

    // Keyword heuristics. Not exhaustive, intentionally conservative: when in
    // doubt we leave the entry alone and mark it "Your call".
    private static readonly string[] KeepKeywords =
    {
        "defender", "antivirus", "security", "mcafee", "norton", "bitdefender",
        "kaspersky", "realtek", "nahimic", "audio", "synaptics", "touchpad",
        "elan", "nvidia container", "windows security", "logioptions",
        "intel(r) graphics", "smartaudio",
    };

    private static readonly string[] DelayKeywords =
    {
        "onedrive", "dropbox", "google drive", "googledrivefs", "icloud",
        "slack", "discord", "teams", "skype", "spotify", "zoom", "steam",
        "epic", "telegram", "whatsapp", "backup",
    };

    private static readonly string[] DisableKeywords =
    {
        "update", "updater", "helper", "bonjour", "itunes", "quicktime",
        "java", "jusched", "adobe", "acrobat", "reader", "ccleaner",
        "coupon", "toolbar", "ask.com", "wildtangent", "shopper",
        "epicgameslauncher", "originwebhelper", "rockstar",
    };

    public void Classify(StartupEntry e)
    {
        string hay = (e.Name + " " + e.Command).ToLowerInvariant();

        if (KeepKeywords.Any(hay.Contains))
        {
            e.Recommendation = StartupRecommendation.Keep;
            e.Impact = BootImpact.Low;
            e.Reason = "Driver, audio, input, or security software - best left enabled.";
        }
        else if (DisableKeywords.Any(hay.Contains))
        {
            e.Recommendation = StartupRecommendation.Disable;
            e.Impact = BootImpact.Medium;
            e.Reason = "Auto-updater or optional helper. The app still works if you " +
                       "launch it manually; this just stops it loading at boot.";
            e.IsSelected = true; // pre-select safe-to-disable items
        }
        else if (DelayKeywords.Any(hay.Contains))
        {
            e.Recommendation = StartupRecommendation.Delay;
            e.Impact = BootImpact.High;
            e.Reason = "Useful but not needed instantly at logon. Delaying it speeds " +
                       "up boot while keeping the app available shortly after.";
        }
        else
        {
            e.Recommendation = StartupRecommendation.Unknown;
            e.Impact = BootImpact.Unknown;
            e.Reason = "Unrecognized. Leave enabled unless you know what it is.";
        }
    }

    // ---- Actions (reversible) --------------------------------------------

    /// <summary>
    /// Disables an entry. Registry entries are backed up under
    /// HKCU\Software\SystemOptimizer then removed; startup-folder shortcuts are
    /// moved into a "Disabled" subfolder. Both are restorable.
    /// </summary>
    public bool Disable(StartupEntry e)
    {
        try
        {
            switch (e.Source)
            {
                case StartupSource.RegistryCurrentUser:
                    return DisableRegistry(Registry.CurrentUser, "HKCU", e);
                case StartupSource.RegistryLocalMachine:
                    return DisableRegistry(Registry.LocalMachine, "HKLM", e);
                case StartupSource.StartupFolderUser:
                case StartupSource.StartupFolderCommon:
                    return DisableFolderEntry(e);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to disable startup '{e.Name}': {ex.Message}");
            return false;
        }
    }

    private bool DisableRegistry(RegistryKey hive, string hiveLabel, StartupEntry e)
    {
        using var run = hive.OpenSubKey(RunSubKey, writable: true);
        if (run is null) return false;
        object? value = run.GetValue(e.Name);
        if (value is null) return false;

        // Back up: <BackupKey>\<HKCU|HKLM> : valueName = original command
        using (var backup = Registry.CurrentUser.CreateSubKey($@"{BackupKey}\{hiveLabel}"))
            backup?.SetValue(e.Name, value);

        run.DeleteValue(e.Name, throwOnMissingValue: false);
        Logger.Action($"Disabled startup (registry {hiveLabel}): {e.Name} [backed up].");
        e.IsEnabled = false;
        return true;
    }

    private bool DisableFolderEntry(StartupEntry e)
    {
        string disabledDir = Path.Combine(Path.GetDirectoryName(e.Command)!, "Disabled");
        Directory.CreateDirectory(disabledDir);
        string dest = Path.Combine(disabledDir, Path.GetFileName(e.Command));
        File.Move(e.Command, dest, overwrite: true);
        Logger.Action($"Disabled startup (folder): {e.Command} -> {dest}");
        e.IsEnabled = false;
        return true;
    }

    /// <summary>
    /// Converts a startup entry into a delayed scheduled task that fires a set
    /// number of seconds after logon, then disables the original. Reversible by
    /// deleting the task and restoring the original entry.
    /// </summary>
    public bool DelayAfterBoot(StartupEntry e, int delaySeconds = 60)
    {
        try
        {
            string taskName = $"SystemOptimizer_Delayed_{Sanitize(e.Name)}";
            var delay = TimeSpan.FromSeconds(delaySeconds);
            string delayStr = $"{(int)delay.TotalMinutes:00}:{delay.Seconds:00}";

            // /SC ONLOGON with /DELAY runs the command after logon + delay.
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                Arguments =
                    $"/Create /F /TN \"{taskName}\" /TR \"{e.Command}\" " +
                    $"/SC ONLOGON /DELAY {delayStr}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi);
            p!.WaitForExit();
            if (p.ExitCode != 0)
            {
                Logger.Warn($"schtasks failed for '{e.Name}': {p.StandardError.ReadToEnd()}");
                return false;
            }

            // Remove the original so it doesn't also run immediately.
            Disable(e);
            Logger.Action($"Delayed startup '{e.Name}' to {delaySeconds}s after logon.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to delay startup '{e.Name}': {ex.Message}");
            return false;
        }
    }

    private static string Sanitize(string name)
        => new string(name.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
}
