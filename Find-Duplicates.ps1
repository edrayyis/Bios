<#
.SYNOPSIS
    Finds (and optionally quarantines/deletes) duplicate files by content hash.

.DESCRIPTION
    Safe, three-stage workflow for cleaning up duplicate files:

      Stage 1 (default) : REPORT ONLY. Scans the given drives/paths, identifies
                          true duplicates by SHA-256 content hash, and writes a
                          CSV + console summary. Nothing is changed on disk.

      Stage 2 (-Move)   : Moves the duplicate copies into a quarantine folder,
                          preserving their original folder structure, so you can
                          review before anything is lost. The original (the copy
                          it keeps) is left in place.

      Stage 3 (-Delete) : Permanently deletes the quarantined duplicates. Only
                          touches files inside the quarantine folder.

    "Duplicate" = identical file *content* (same SHA-256). The first file found
    in each group (by default, the one with the shortest path, then oldest) is
    treated as the KEEPER; all other identical files are the duplicates.

.PARAMETER Paths
    One or more drives/folders to scan. Examples: 'D:\', 'G:\', 'D:\Photos'.

.PARAMETER ReportPath
    Where to write the CSV report. Defaults to the current folder.

.PARAMETER QuarantinePath
    Folder where duplicates are moved when -Move is used.

.PARAMETER Move
    Move duplicates to the quarantine folder instead of only reporting.

.PARAMETER Delete
    Permanently delete files already sitting in the quarantine folder.
    Run this only AFTER you've reviewed the quarantine contents.

.PARAMETER MinSizeKB
    Ignore files smaller than this (default 1 KB). Skips empty/tiny files.

.PARAMETER IncludeExtensions
    Optional filter, e.g. -IncludeExtensions '.jpg','.png','.heic' to target
    photos only.

.PARAMETER ExcludePaths
    Extra folder names/paths to skip, on top of the built-in system list.
    Matched as a path substring, e.g. -ExcludePaths 'Dropbox','OneDrive'.

.PARAMETER IncludeSystemFolders
    Override the safety exclusions and scan protected system folders
    (Windows, WindowsApps, Program Files, etc.). NOT recommended - these
    contain OS/app files, not your personal duplicates.

.EXAMPLE
    # Stage 1 - just see what's duplicated (no changes)
    .\Find-Duplicates.ps1 -Paths 'D:\','G:\'

.EXAMPLE
    # Photos only, across both drives
    .\Find-Duplicates.ps1 -Paths 'D:\','G:\' -IncludeExtensions '.jpg','.jpeg','.png','.heic','.gif','.bmp','.tiff'

.EXAMPLE
    # Stage 2 - move dupes to a review folder
    .\Find-Duplicates.ps1 -Paths 'D:\','G:\' -Move -QuarantinePath 'D:\_DuplicateReview'

.EXAMPLE
    # Stage 3 - after reviewing, permanently delete the quarantined files
    .\Find-Duplicates.ps1 -Delete -QuarantinePath 'D:\_DuplicateReview'

.NOTES
    - Run PowerShell as the user who owns the files (admin not usually needed).
    - If blocked by execution policy, start the session with:
        powershell -ExecutionPolicy Bypass -File .\Find-Duplicates.ps1 ...
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string[]] $Paths,

    [string]   $ReportPath = (Join-Path (Get-Location) ("duplicates_report_{0:yyyyMMdd_HHmmss}.csv" -f (Get-Date))),

    [string]   $QuarantinePath = (Join-Path (Get-Location) "_DuplicateReview"),

    [switch]   $Move,

    [switch]   $Delete,

    [int]      $MinSizeKB = 1,

    [string[]] $IncludeExtensions,

    # Extra paths to skip, on top of the built-in system/protected list below.
    [string[]] $ExcludePaths,

    # Set this to deliberately scan protected system folders (NOT recommended).
    [switch]   $IncludeSystemFolders
)

# Folders that must never be treated as "junk duplicates": OS, installed apps,
# and program files. Identical files here belong to Windows or your apps and
# moving them can break software. WindowsApps in particular is access-denied.
$script:DefaultExcludes = @(
    'WindowsApps'
    'Windows'
    'Program Files'
    'Program Files (x86)'
    'ProgramData'
    '$Recycle.Bin'
    'System Volume Information'
    'AppData\Local\Packages'
    'AppData\Local\Microsoft\WindowsApps'
) | ForEach-Object { '\' + $_.Trim('\') + '\' }

# Combine built-ins with any user-supplied exclusions (skipped if overridden).
$script:AllExcludes = @()
if (-not $IncludeSystemFolders) { $script:AllExcludes += $script:DefaultExcludes }
if ($ExcludePaths) {
    $script:AllExcludes += ($ExcludePaths | ForEach-Object { '\' + $_.Trim('\') + '\' })
}

function Test-Excluded {
    param([string] $FullPath)
    if (-not $script:AllExcludes) { return $false }
    # Normalize so a substring match like "\Windows\" is reliable.
    $p = '\' + $FullPath.TrimStart('\')
    foreach ($ex in $script:AllExcludes) {
        if ($p -like "*$ex*") { return $true }
    }
    return $false
}

# ---------------------------------------------------------------------------
# Stage 3: Delete quarantined files
# ---------------------------------------------------------------------------
if ($Delete) {
    if (-not (Test-Path -LiteralPath $QuarantinePath)) {
        Write-Error "Quarantine folder '$QuarantinePath' not found. Nothing to delete."
        return
    }

    $toDelete = Get-ChildItem -LiteralPath $QuarantinePath -Recurse -File -ErrorAction SilentlyContinue
    if (-not $toDelete) {
        Write-Host "Quarantine folder is empty. Nothing to delete." -ForegroundColor Yellow
        return
    }

    $count = ($toDelete | Measure-Object).Count
    $bytes = ($toDelete | Measure-Object -Property Length -Sum).Sum

    Write-Host ""
    Write-Host "About to PERMANENTLY DELETE $count file(s) ($([math]::Round($bytes/1MB,2)) MB) from:" -ForegroundColor Red
    Write-Host "  $QuarantinePath" -ForegroundColor Red
    $confirm = Read-Host "Type DELETE to confirm"
    if ($confirm -ne 'DELETE') {
        Write-Host "Aborted. No files were deleted." -ForegroundColor Yellow
        return
    }

    foreach ($f in $toDelete) {
        if ($PSCmdlet.ShouldProcess($f.FullName, "Delete")) {
            Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue
        }
    }
    # Clean up now-empty directories
    Get-ChildItem -LiteralPath $QuarantinePath -Recurse -Directory |
        Sort-Object { $_.FullName.Length } -Descending |
        Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Host "Done. Deleted $count file(s)." -ForegroundColor Green
    return
}

# ---------------------------------------------------------------------------
# Stage 1 / 2: Scan and find duplicates
# ---------------------------------------------------------------------------
if (-not $Paths -or $Paths.Count -eq 0) {
    Write-Error "Specify at least one path to scan, e.g. -Paths 'D:\','G:\'"
    return
}

foreach ($p in $Paths) {
    if (-not (Test-Path -LiteralPath $p)) {
        Write-Warning "Path not found, skipping: $p"
    }
}
$validPaths = $Paths | Where-Object { Test-Path -LiteralPath $_ }
if (-not $validPaths) { Write-Error "No valid paths to scan."; return }

$minBytes = $MinSizeKB * 1KB

Write-Host "Scanning for files..." -ForegroundColor Cyan
$allFiles = foreach ($p in $validPaths) {
    Get-ChildItem -LiteralPath $p -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Length -ge $minBytes -and
            (-not $IncludeExtensions -or $IncludeExtensions -contains $_.Extension.ToLower()) -and
            (-not (Test-Excluded $_.FullName))
        }
}
if (-not $IncludeSystemFolders) {
    Write-Host "Skipping protected system folders (Windows, WindowsApps, Program Files, etc.)" -ForegroundColor DarkGray
}

if (-not $allFiles) { Write-Host "No matching files found." -ForegroundColor Yellow; return }
Write-Host ("Found {0} candidate file(s)." -f $allFiles.Count) -ForegroundColor Cyan

# Optimization: only hash files whose SIZE collides with another file.
# Two files can only be identical if they're the same size, so this avoids
# hashing the (usually large) majority of files that are unique by size.
$bySize = $allFiles | Group-Object Length | Where-Object { $_.Count -gt 1 }
$candidates = $bySize | ForEach-Object { $_.Group }

if (-not $candidates) { Write-Host "No size collisions - no duplicates exist." -ForegroundColor Green; return }
Write-Host ("Hashing {0} file(s) with matching sizes..." -f $candidates.Count) -ForegroundColor Cyan

$hashed = @()
$i = 0
foreach ($f in $candidates) {
    $i++
    Write-Progress -Activity "Hashing files" -Status $f.Name -PercentComplete (($i / $candidates.Count) * 100)
    try {
        $h = Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256 -ErrorAction Stop
        $hashed += [pscustomobject]@{
            Hash         = $h.Hash
            FullName     = $f.FullName
            SizeBytes    = $f.Length
            LastModified = $f.LastWriteTime
            PathLength   = $f.FullName.Length
        }
    } catch {
        Write-Warning "Could not hash (in use / no access): $($f.FullName)"
    }
}
Write-Progress -Activity "Hashing files" -Completed

# Group by hash; any group with >1 member is a true duplicate set.
$dupeGroups = $hashed | Group-Object Hash | Where-Object { $_.Count -gt 1 }

if (-not $dupeGroups) { Write-Host "No duplicate content found." -ForegroundColor Green; return }

# Build the report. Keeper = shortest path, then oldest. Rest = duplicates.
$report = foreach ($g in $dupeGroups) {
    $ordered = $g.Group | Sort-Object PathLength, LastModified
    $keeper  = $ordered[0]
    foreach ($item in $ordered) {
        [pscustomobject]@{
            Hash        = $item.Hash
            Role        = if ($item.FullName -eq $keeper.FullName) { 'KEEP' } else { 'DUPLICATE' }
            FullName    = $item.FullName
            SizeMB      = [math]::Round($item.SizeBytes / 1MB, 3)
            LastModified= $item.LastModified
            KeeperPath  = $keeper.FullName
        }
    }
}

$report | Sort-Object Hash, Role | Export-Csv -LiteralPath $ReportPath -NoTypeInformation -Encoding UTF8

$dupes      = $report | Where-Object Role -eq 'DUPLICATE'
$reclaimMB  = [math]::Round((($dupes | Measure-Object SizeMB -Sum).Sum), 2)

Write-Host ""
Write-Host "==================== SUMMARY ====================" -ForegroundColor Green
Write-Host ("Duplicate sets found : {0}" -f $dupeGroups.Count)
Write-Host ("Redundant copies     : {0}" -f $dupes.Count)
Write-Host ("Space recoverable    : {0} MB" -f $reclaimMB)
Write-Host ("Report written to    : {0}" -f $ReportPath)
Write-Host "=================================================" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Stage 2: Move duplicates to quarantine
# ---------------------------------------------------------------------------
if ($Move) {
    Write-Host ""
    Write-Host "Moving $($dupes.Count) duplicate(s) to quarantine: $QuarantinePath" -ForegroundColor Yellow
    if (-not (Test-Path -LiteralPath $QuarantinePath)) {
        New-Item -ItemType Directory -Path $QuarantinePath -Force | Out-Null
    }

    foreach ($d in $dupes) {
        # Recreate structure as <quarantine>\<drive-letter>\<original path>
        $drive = ($d.FullName.Substring(0,1))
        $rel   = $d.FullName.Substring(3)  # strip "X:\"
        $dest  = Join-Path (Join-Path $QuarantinePath $drive) $rel
        $destDir = Split-Path $dest -Parent

        if ($PSCmdlet.ShouldProcess($d.FullName, "Move to $dest")) {
            if (-not (Test-Path -LiteralPath $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            try {
                Move-Item -LiteralPath $d.FullName -Destination $dest -Force -ErrorAction Stop
            } catch {
                Write-Warning "Could not move: $($d.FullName) - $($_.Exception.Message)"
            }
        }
    }
    Write-Host "Move complete. Review '$QuarantinePath', then re-run with -Delete to remove them." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "REPORT ONLY - nothing was changed." -ForegroundColor Cyan
    Write-Host "Next: review the CSV, then re-run with -Move to quarantine duplicates." -ForegroundColor Cyan
}
