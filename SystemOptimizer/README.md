# System Optimizer

A Windows desktop utility (C# / .NET 8 / WPF) that bundles safe, report-first
PC maintenance tools into one dashboard. Built around a consistent philosophy:
**scan → review → apply**, with reversible actions wherever possible.

> ⚠️ **Windows-only.** WPF and the system APIs used here (registry, WMI,
> Recycle Bin, scheduled tasks) only build and run on Windows. It will not
> compile on Linux/macOS.

## Features

| Module | What it does | Safety |
|---|---|---|
| **Duplicate Finder** | Finds byte-identical files (SHA-256), keeps one copy per set | Quarantines before delete; skips system folders |
| **Junk & Temp Cleanup** | Temp folders, Windows Update cache, browser/thumbnail caches, crash dumps | Sends to Recycle Bin by default |
| **Startup & Bloatware** | Lists logon startup items with keep/delay/disable recommendations; lists installed programs to uninstall | Disable is reversible (backed up); delay uses a scheduled task |
| **Disk Health & Space** | SSD/HDD detection, health status, volume usage, large-file finder, correct optimize (TRIM/defrag) | Read-only reporting; optimize uses built-in Windows tooling |

## Design principles

- **Report first.** Every module scans and shows you exactly what it found
  before changing anything.
- **Reversible by default.** Deletes go to the Recycle Bin; quarantined
  duplicates are restorable; disabled startup items are backed up.
- **No snake oil.** Deliberately *no* "registry cleaner" — registry cleaning
  offers negligible real performance benefit and carries real risk. The
  genuine wins (startup management, junk removal, disk optimization) are here
  instead.
- **Hand off, don't hack.** Uninstalls launch each app's own uninstaller;
  disk optimization calls Windows' `defrag /O` (which TRIMs SSDs correctly).

## Build & run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
cd SystemOptimizer
dotnet restore
dotnet build -c Release
dotnet run --project src/SystemOptimizer/SystemOptimizer.csproj
```

The app manifest requests Administrator rights (needed for system temp,
HKLM startup entries, and disk optimization), so Windows will show a UAC
prompt at launch.

### Produce a standalone .exe

```powershell
dotnet publish src/SystemOptimizer/SystemOptimizer.csproj -c Release `
  -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The single-file executable lands in
`src/SystemOptimizer/bin/Release/net8.0-windows/win-x64/publish/`.

## Logs

Every destructive action is logged to:
`%LOCALAPPDATA%\SystemOptimizer\logs\actions_YYYYMMDD.log`

## Status / roadmap

This is an initial version. Natural next steps:
- One-click **Restore** UI for quarantined duplicates and disabled startup items
- A **System Restore point** created automatically before risky operations
- A **dashboard "Scan all"** summary across modules
- Perceptual (near-duplicate) image matching, in addition to exact matches
