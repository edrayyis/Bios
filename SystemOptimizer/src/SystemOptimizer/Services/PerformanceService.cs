using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SystemOptimizer.Models;

namespace SystemOptimizer.Services;

/// <summary>
/// Memory monitoring + a few genuine performance levers: process management,
/// an honest standby-cache flush (the real RAMMap operation, not a fake "RAM
/// booster"), power-plan switching, and Windows visual-effects mode.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PerformanceService
{
    // ---- Memory -----------------------------------------------------------

    public MemoryStatus GetMemory()
    {
        var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref s);
        return new MemoryStatus
        {
            TotalBytes = (long)s.ullTotalPhys,
            AvailableBytes = (long)s.ullAvailPhys,
            UsedPercent = (int)s.dwMemoryLoad,
            CommitTotalBytes = (long)s.ullTotalPageFile,
            CommitAvailableBytes = (long)s.ullAvailPageFile,
        };
    }

    public IReadOnlyList<ProcessItem> GetTopProcesses(int count = 25)
    {
        var list = new List<ProcessItem>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                list.Add(new ProcessItem
                {
                    Pid = p.Id,
                    Name = p.ProcessName,
                    WorkingSetBytes = p.WorkingSet64,
                });
            }
            catch { /* process may have exited */ }
            finally { p.Dispose(); }
        }
        return list.OrderByDescending(p => p.WorkingSetBytes).Take(count).ToList();
    }

    public bool KillProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            Logger.Action($"Killed process {p.ProcessName} (pid {pid}).");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not kill pid {pid}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Flushes the system standby (cached) memory list - the same operation as
    /// RAMMap's "Empty Standby List". Returns bytes that became available.
    /// Requires elevation + SeProfileSingleProcessPrivilege.
    /// </summary>
    public (bool Ok, long Freed) ClearStandbyCache()
    {
        long before = GetMemory().AvailableBytes;
        try
        {
            if (!EnablePrivilege("SeProfileSingleProcessPrivilege"))
                Logger.Warn("Could not enable SeProfileSingleProcessPrivilege.");

            int command = MemoryPurgeStandbyList;
            int status = NtSetSystemInformation(
                SystemMemoryListInformation, ref command, sizeof(int));
            if (status != 0)
            {
                Logger.Warn($"NtSetSystemInformation returned status {status}.");
                return (false, 0);
            }
            long after = GetMemory().AvailableBytes;
            long freed = Math.Max(0, after - before);
            Logger.Action($"Flushed standby cache (~{Formatting.HumanSize(freed)} freed).");
            return (true, freed);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Standby flush failed: {ex.Message}");
            return (false, 0);
        }
    }

    // ---- Power plans ------------------------------------------------------

    private static readonly Dictionary<PowerPlanKind, string> PlanGuids = new()
    {
        [PowerPlanKind.Balanced] = "381b4222-f694-41f0-9685-ff5bb260df2e",
        [PowerPlanKind.HighPerformance] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
        [PowerPlanKind.PowerSaver] = "a1841308-3541-4fab-bc81-f71556f20b4a",
        [PowerPlanKind.Ultimate] = "e9a42b02-d5df-448d-aa00-03f14749eb61",
    };

    public string GetActivePowerPlanName()
    {
        try
        {
            string output = RunCapture("powercfg", "/getactivescheme");
            // "Power Scheme GUID: <guid>  (Name)"
            int open = output.IndexOf('(');
            int close = output.LastIndexOf(')');
            return open >= 0 && close > open ? output[(open + 1)..close] : "Unknown";
        }
        catch { return "Unknown"; }
    }

    public bool SetPowerPlan(PowerPlanKind kind)
    {
        string guid = PlanGuids[kind];
        try
        {
            // Ultimate Performance isn't present by default - create it first.
            if (kind == PowerPlanKind.Ultimate)
                Run("powercfg", $"-duplicatescheme {guid}");

            int code = Run("powercfg", $"/setactive {guid}");
            Logger.Action($"Set power plan to {kind} (exit {code}).");
            return code == 0;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Set power plan {kind} failed: {ex.Message}");
            return false;
        }
    }

    // ---- Visual effects ---------------------------------------------------

    public bool SetVisualEffects(VisualEffectsMode mode)
    {
        try
        {
            int fx = mode switch
            {
                VisualEffectsMode.BestPerformance => 2,
                VisualEffectsMode.BestAppearance => 1,
                _ => 0,
            };
            using (var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                key?.SetValue("VisualFXSetting", fx, RegistryValueKind.DWord);

            // Window/menu animation toggle (applies after sign-out for most).
            string anim = mode == VisualEffectsMode.BestPerformance ? "0" : "1";
            using (var wm = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                wm?.SetValue("MinAnimate", anim, RegistryValueKind.String);

            Logger.Action($"Set visual effects to {mode}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Set visual effects failed: {ex.Message}");
            return false;
        }
    }

    // ---- Process helpers --------------------------------------------------

    private static int Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return p.ExitCode;
    }

    private static string RunCapture(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output;
    }

    // ---- P/Invoke ---------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private const int SystemMemoryListInformation = 0x50;
    private const int MemoryPurgeStandbyList = 4;

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }

    private const int SE_PRIVILEGE_ENABLED = 0x2;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x20;
    private const uint TOKEN_QUERY = 0x8;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? host, string name, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState,
        int len, IntPtr prev, IntPtr relen);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    private static bool EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(),
                TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr token))
            return false;
        try
        {
            if (!LookupPrivilegeValue(null, name, out long luid)) return false;
            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED,
            };
            return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally { CloseHandle(token); }
    }
}
