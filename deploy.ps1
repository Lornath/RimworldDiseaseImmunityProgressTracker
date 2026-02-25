# Deploy script for DiseaseImmunityProgressTracker
# Copies mod files to RimWorld Mods directory

$modName = "DiseaseImmunityProgressTracker"
$sourceDir = $PSScriptRoot
$destDir = "E:\Steam\steamapps\common\RimWorld\Mods\$modName"
$dllPath = Join-Path $sourceDir "Assemblies\$modName.dll"

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
$optionalDirs = @("Defs", "Textures", "Sounds", "Languages")
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
