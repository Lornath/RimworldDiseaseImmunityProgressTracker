# Deploy script for DiseaseImmunityProgressTracker
# Copies mod files to RimWorld Mods directory
#
# Usage:
#   .\deploy.ps1              - Deploy all mod files to Steam
#   .\deploy.ps1 --dir About  - Deploy only the specified directory (e.g. About, Languages)
#   .\deploy.ps1 --verify     - Dry run: compare repo vs deployed, report any differences

param(
    [switch]$Verify,
    [string]$Dir
)

$modName = "DiseaseImmunityProgressTracker"
$sourceDir = $PSScriptRoot
$destDir = "E:\Steam\steamapps\common\RimWorld\Mods\$modName"
$dllPath = Join-Path $sourceDir "Assemblies\$modName.dll"

# Directories that are always deployed
$requiredDirs = @("About", "Assemblies")
# Directories deployed only if present in source
$optionalDirs = @("Defs", "Textures", "Sounds", "Languages")

# --- SINGLE-DIR MODE ---
if ($Dir) {
    $allDirs = $requiredDirs + $optionalDirs
    if ($Dir -notin $allDirs) {
        Write-Host "ERROR: '$Dir' is not a known deployable directory." -ForegroundColor Red
        Write-Host "Known directories: $($allDirs -join ', ')" -ForegroundColor Yellow
        exit 1
    }

    $srcPath = Join-Path $sourceDir $Dir
    $dstPath = Join-Path $destDir $Dir

    if (-not (Test-Path $srcPath)) {
        Write-Host "ERROR: Source directory not found: $srcPath" -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $destDir)) {
        Write-Host "ERROR: Destination mod folder does not exist: $destDir" -ForegroundColor Red
        Write-Host "Has the mod been deployed at least once?" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "=== Deploying $Dir/ only ===" -ForegroundColor Cyan
    Write-Host "Source: $srcPath"
    Write-Host "Dest:   $dstPath"

    if (Test-Path $dstPath) { Remove-Item $dstPath -Recurse -Force }
    Copy-Item $srcPath -Destination $dstPath -Recurse -Force

    Write-Host ""
    Write-Host "=== Done: $Dir/ deployed ===" -ForegroundColor Green
    exit 0
}

# --- VERIFY MODE ---
if ($Verify) {
    Write-Host "=== Verifying deployment: $modName ===" -ForegroundColor Cyan
    Write-Host "Source: $sourceDir"
    Write-Host "Dest:   $destDir"
    Write-Host ""

    if (-not (Test-Path $destDir)) {
        Write-Host "ERROR: Destination directory does not exist. Has the mod been deployed yet?" -ForegroundColor Red
        exit 1
    }

    $allMatch = $true

    # Determine which directories to check (required + optional that exist in source)
    $dirsToCheck = $requiredDirs
    foreach ($dir in $optionalDirs) {
        if (Test-Path (Join-Path $sourceDir $dir)) {
            $dirsToCheck += $dir
        }
    }

    foreach ($dir in $dirsToCheck) {
        $srcPath = Join-Path $sourceDir $dir
        $dstPath = Join-Path $destDir $dir

        Write-Host "Checking $dir/ ..." -ForegroundColor Cyan

        if (-not (Test-Path $srcPath)) {
            Write-Host "  SKIP: $dir not present in source" -ForegroundColor DarkGray
            continue
        }
        if (-not (Test-Path $dstPath)) {
            Write-Host "  MISSING: $dir not present in destination" -ForegroundColor Red
            $allMatch = $false
            continue
        }

        # Collect all source files (relative paths)
        $srcFiles = Get-ChildItem -Path $srcPath -Recurse -File |
            ForEach-Object { $_.FullName.Substring($srcPath.Length + 1) }

        # Collect all destination files (relative paths)
        $dstFiles = Get-ChildItem -Path $dstPath -Recurse -File |
            ForEach-Object { $_.FullName.Substring($dstPath.Length + 1) }

        $srcSet = [System.Collections.Generic.HashSet[string]]($srcFiles)
        $dstSet = [System.Collections.Generic.HashSet[string]]($dstFiles)

        # Files in source but missing from destination
        foreach ($rel in $srcFiles) {
            if (-not $dstSet.Contains($rel)) {
                Write-Host "  MISSING from deployed: $dir\$rel" -ForegroundColor Red
                $allMatch = $false
            }
        }

        # Files in destination but not in source (stale)
        foreach ($rel in $dstFiles) {
            if (-not $srcSet.Contains($rel)) {
                Write-Host "  EXTRA in deployed (stale?): $dir\$rel" -ForegroundColor Yellow
                $allMatch = $false
            }
        }

        # Compare content of files present in both
        foreach ($rel in $srcFiles) {
            if (-not $dstSet.Contains($rel)) { continue }  # already reported above

            $srcFile = Join-Path $srcPath $rel
            $dstFile = Join-Path $dstPath $rel

            $srcHash = (Get-FileHash $srcFile -Algorithm MD5).Hash
            $dstHash = (Get-FileHash $dstFile -Algorithm MD5).Hash

            if ($srcHash -ne $dstHash) {
                Write-Host "  MISMATCH: $dir\$rel" -ForegroundColor Red
                $allMatch = $false
            }
        }

        if ($allMatch) {
            Write-Host "  OK" -ForegroundColor Green
        }
    }

    # Check for extra top-level dirs in destination that aren't in source
    $deployedTopDirs = Get-ChildItem -Path $destDir -Directory | Select-Object -ExpandProperty Name
    foreach ($d in $deployedTopDirs) {
        if ($d -notin ($requiredDirs + $optionalDirs)) {
            Write-Host "EXTRA top-level dir in deployed (unknown): $d\" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    if ($allMatch) {
        Write-Host "=== All files match. Ready to release. ===" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "=== VERIFICATION FAILED: deployed files differ from source ===" -ForegroundColor Red
        Write-Host "Run .\deploy.ps1 to redeploy." -ForegroundColor Yellow
        exit 1
    }
}

# --- DEPLOY MODE ---
Write-Host "=== Deploying $modName ===" -ForegroundColor Cyan

# Check if DLL exists
if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: DLL not found at $dllPath" -ForegroundColor Red
    Write-Host "Did you forget to build? Run: dotnet build Source/$modName/$modName.csproj" -ForegroundColor Yellow
    exit 1
}

# Check if source DLL is newer than destination
$destDllPath = Join-Path $destDir "Assemblies\$modName.dll"
$sourceDllTime = (Get-Item $dllPath).LastWriteTime

if (Test-Path $destDllPath) {
    $destDllTime = (Get-Item $destDllPath).LastWriteTime

    if ($sourceDllTime -le $destDllTime) {
        Write-Host ""
        Write-Host "WARNING: Source DLL has not been modified since last deploy" -ForegroundColor Yellow
        Write-Host "  Source:      $sourceDllTime" -ForegroundColor Yellow
        Write-Host "  Destination: $destDllTime" -ForegroundColor Yellow
        Write-Host "Did you forget to rebuild?" -ForegroundColor Yellow
        Write-Host "  rebuild with: dotnet build Source/$modName/$modName.csproj" -ForegroundColor Yellow
        Write-Host ""
        $response = Read-Host "Continue anyway? (y/n)"
        if ($response -ne 'y') {
            Write-Host "Deployment cancelled." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "Source DLL is newer than deployed version" -ForegroundColor Green
    }
} else {
    Write-Host "First deployment (no existing DLL found)" -ForegroundColor Green
}

# Create destination directory
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

# Copy required directories
Write-Host "Copying About/ directory..."
$aboutDest = Join-Path $destDir "About"
if (Test-Path $aboutDest) { Remove-Item $aboutDest -Recurse -Force }
Copy-Item (Join-Path $sourceDir "About") -Destination $aboutDest -Recurse -Force

Write-Host "Copying Assemblies/ directory..."
$assembliesDest = Join-Path $destDir "Assemblies"
if (Test-Path $assembliesDest) { Remove-Item $assembliesDest -Recurse -Force }
Copy-Item (Join-Path $sourceDir "Assemblies") -Destination $assembliesDest -Recurse -Force

# Copy optional directories if they exist
foreach ($dir in $optionalDirs) {
    $srcPath = Join-Path $sourceDir $dir
    if (Test-Path $srcPath) {
        Write-Host "Copying $dir/ directory..."
        $destPath = Join-Path $destDir $dir
        if (Test-Path $destPath) { Remove-Item $destPath -Recurse -Force }
        Copy-Item $srcPath -Destination $destPath -Recurse -Force
    }
}

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Green
Write-Host "Mod deployed to: $destDir" -ForegroundColor Green
