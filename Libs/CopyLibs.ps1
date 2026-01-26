# CopyLibs.ps1 - Copies required DLLs for RimWorld mod development
# Run this once after cloning the repo to populate Libs/

# =============================================================================
# CONFIGURATION - Update these paths for your system
# =============================================================================

$steamBase = "E:\Steam"

# RimWorld's Steam App ID
$rimworldAppId = "294100"

# Harmony mod's Steam Workshop ID
$harmonyWorkshopId = "2009463077"

# Additional mod dependencies (Workshop ID -> DLL name)
# Uncomment and add entries as needed:
# $modDependencies = @{
#     "2009463077" = "0Harmony.dll"           # Harmony (already handled below)
#     "1234567890" = "SomeMod.dll"            # Example: Some other mod
# }

# =============================================================================
# PATHS (derived from config above)
# =============================================================================

$rimworldManaged = "$steamBase\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
$workshopBase = "$steamBase\steamapps\workshop\content\$rimworldAppId"
$harmonyPath = "$workshopBase\$harmonyWorkshopId\Current\Assemblies\0Harmony.dll"
$destDir = $PSScriptRoot

# =============================================================================
# REQUIRED DLLS
# =============================================================================

$rimworldDlls = @(
    "Assembly-CSharp.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.TextRenderingModule.dll"
)

# =============================================================================
# COPY LOGIC
# =============================================================================

Write-Host "=== Copying RimWorld Mod Dependencies ===" -ForegroundColor Cyan
Write-Host ""

# Check RimWorld installation
if (-not (Test-Path $rimworldManaged)) {
    Write-Host "ERROR: RimWorld Managed folder not found at:" -ForegroundColor Red
    Write-Host "  $rimworldManaged" -ForegroundColor Red
    Write-Host ""
    Write-Host "Update `$steamBase at the top of this script." -ForegroundColor Yellow
    exit 1
}

# Copy RimWorld DLLs
Write-Host "Copying from RimWorld installation..." -ForegroundColor White
foreach ($dll in $rimworldDlls) {
    $src = Join-Path $rimworldManaged $dll
    $dest = Join-Path $destDir $dll

    if (Test-Path $src) {
        Copy-Item $src -Destination $dest -Force
        Write-Host "  [OK] $dll" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $dll - not found at $src" -ForegroundColor Red
    }
}

# Copy Harmony
Write-Host ""
Write-Host "Copying Harmony from Steam Workshop..." -ForegroundColor White

# Try "Current" subfolder first (newer Harmony versions), then root
$harmonyPaths = @(
    "$workshopBase\$harmonyWorkshopId\Current\Assemblies\0Harmony.dll",
    "$workshopBase\$harmonyWorkshopId\Assemblies\0Harmony.dll",
    "$workshopBase\$harmonyWorkshopId\1.6\Assemblies\0Harmony.dll"
)

$harmonyCopied = $false
foreach ($hp in $harmonyPaths) {
    if (Test-Path $hp) {
        Copy-Item $hp -Destination (Join-Path $destDir "0Harmony.dll") -Force
        Write-Host "  [OK] 0Harmony.dll (from $hp)" -ForegroundColor Green
        $harmonyCopied = $true
        break
    }
}

if (-not $harmonyCopied) {
    Write-Host "  [MISSING] 0Harmony.dll - Harmony mod not found in Workshop" -ForegroundColor Red
    Write-Host "  Searched:" -ForegroundColor Yellow
    foreach ($hp in $harmonyPaths) {
        Write-Host "    $hp" -ForegroundColor Yellow
    }
    Write-Host "  Make sure you're subscribed to Harmony on Steam Workshop." -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan
Write-Host "DLLs copied to: $destDir" -ForegroundColor White

# List what we have
Write-Host ""
Write-Host "Libs/ contents:" -ForegroundColor White
Get-ChildItem $destDir -Filter "*.dll" | ForEach-Object {
    Write-Host "  $($_.Name)" -ForegroundColor Gray
}
