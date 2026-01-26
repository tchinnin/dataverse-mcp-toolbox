# Script PowerShell to package the .NET MCP server as a NuGet package
# This script builds all platform binaries and creates a .nupkg file

Write-Host "🚀 Building and packaging DataverseMCPToolBox.Server..." -ForegroundColor Green

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir

Set-Location $ProjectDir

Write-Host ""
Write-Host "📦 Step 1: Building platform-specific binaries..." -ForegroundColor Cyan
& "$ScriptDir\build-publish.ps1"

Write-Host "📦 Step 2: Creating NuGet package..." -ForegroundColor Cyan
Write-Host ""

# Create nupkg output directory
New-Item -ItemType Directory -Force -Path nupkg | Out-Null

# Pack the project (includes binaries from publish folders)
# The .csproj file maps publish/* folders to runtimes/<platform>/native/ structure
dotnet pack DataverseMCPToolBox.csproj `
    -c Release `
    -o nupkg

Write-Host ""
Write-Host "✅ NuGet package created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Package details:" -ForegroundColor Cyan
Write-Host "   Location: $ProjectDir\nupkg\"
Get-ChildItem "$ProjectDir\nupkg\*.nupkg" | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "   $($_.Name) - $sizeMB MB"
}
Write-Host ""

# Verify package structure (requires 7-Zip or similar)
$latestPkg = Get-ChildItem "$ProjectDir\nupkg\*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (Get-Command 7z -ErrorAction SilentlyContinue) {
    Write-Host "📦 Package structure verification:" -ForegroundColor Cyan
    $content = & 7z l $latestPkg.FullName
    $runtimeFiles = $content | Select-String "runtimes/.*/native/DataverseMCPToolBox"
    if ($runtimeFiles) {
        $runtimeFiles | ForEach-Object { Write-Host "   ✓ $_" -ForegroundColor Green }
    } else {
        Write-Host "   ⚠️  Warning: No binaries found in expected runtimes/<platform>/native/ structure" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "📤 To publish to NuGet.org:" -ForegroundColor Cyan
Write-Host "   dotnet nuget push nupkg\*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json"
Write-Host ""
Write-Host "🧪 To test locally:" -ForegroundColor Cyan
Write-Host "   1. Create a local NuGet source:"
Write-Host "      dotnet nuget add source $ProjectDir\nupkg --name LocalDataverseMCP"
Write-Host "   2. Install in VS Code extension (it will download from your local source)"
Write-Host "   3. Check downloaded structure in:"
Write-Host "      %APPDATA%\Code\User\globalStorage\<extension-id>\server\"
