using System.Runtime.Versioning;
using System.Security.Principal;

namespace SystemOptimizer.ViewModels;

[SupportedOSPlatform("windows")]
public sealed class MainViewModel
{
    public DuplicateFinderViewModel DuplicateFinder { get; } = new();
    public JunkCleanupViewModel JunkCleanup { get; } = new();
    public StartupManagerViewModel StartupManager { get; } = new();
    public DiskHealthViewModel DiskHealth { get; } = new();

    public string Title => "System Optimizer" + (IsElevated ? " (Administrator)" : "");

    public static bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }
}
