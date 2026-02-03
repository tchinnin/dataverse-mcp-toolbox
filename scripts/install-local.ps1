# Local development workflow script (PowerShell)
# Builds Core + Bridge and copies binaries to Extension for immediate testing
# Use this when developing to test changes without publishing to NuGet

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Local Development Install" -ForegroundColor Yellow
Write-Host "Build & Deploy to Extension (Debug)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$ExtensionDir = Join-Path $RootDir "Extension"
$ServerDir = Join-Path $ExtensionDir "server\binaries"

Write-Host ""
Write-Host "📦 Step 1: Building Core + Bridge for all platforms..." -ForegroundColor Cyan
Write-Host ""
& "$ScriptDir\build-all.ps1"

Write-Host ""
Write-Host "📂 Step 2: Preparing Extension server directory..." -ForegroundColor Cyan
if (Test-Path $ServerDir) {
    Remove-Item -Recurse -Force $ServerDir
}
New-Item -ItemType Directory -Force -Path $ServerDir | Out-Null

Write-Host ""
Write-Host "📥 Step 3: Copying binaries to Extension..." -ForegroundColor Cyan
Write-Host ""

# Copy Core binaries
Write-Host "Copying Core Server..." -ForegroundColor Yellow
$Platforms = @("osx-arm64", "osx-x64", "win-x64", "linux-x64")
foreach ($Platform in $Platforms) {
    $SrcDir = Join-Path $RootDir "Core\publish\$Platform"
    $DestDir = Join-Path $ServerDir "runtimes\$Platform\native"
    
    if (Test-Path $SrcDir) {
        New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
        Copy-Item -Path "$SrcDir\*" -Destination $DestDir -Recurse -Force
        Write-Host "  ✓ $Platform" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $Platform not found (skip)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Copying Bridge..." -ForegroundColor Yellow
foreach ($Platform in $Platforms) {
    $SrcDir = Join-Path $RootDir "Bridge\publish\$Platform"
    $DestDir = Join-Path $ServerDir "runtimes\$Platform\native"
    
    if (Test-Path $SrcDir) {
        New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
        Copy-Item -Path "$SrcDir\*" -Destination $DestDir -Recurse -Force
        Write-Host "  ✓ $Platform" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $Platform not found (skip)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Local install completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📂 Binaries installed to:" -ForegroundColor Cyan
Write-Host "   $ServerDir"
Write-Host ""
Write-Host "🚀 Next steps:" -ForegroundColor Yellow
Write-Host "   1. Open VS Code"
Write-Host "   2. Press F5 to debug Extension Development Host"
Write-Host "   3. The Extension will use local binaries"
Write-Host ""
Write-Host "📝 Note: This setup is for development only." -ForegroundColor Gray
Write-Host "   For production, the Extension downloads binaries from NuGet."
Write-Host ""

