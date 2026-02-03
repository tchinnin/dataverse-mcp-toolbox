# Build script for Dataverse MCP ToolBox - Sidecar Architecture
# Builds both the main server and the bridge for all platforms

Write-Host "========================================"
Write-Host "Dataverse MCP ToolBox - Build All"
Write-Host "Sidecar Architecture (Server + Bridge)"
Write-Host "========================================"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$CoreDir = Split-Path -Parent $ScriptDir
$BridgeDir = Join-Path (Split-Path -Parent $CoreDir) "Bridge"
$RootDir = Split-Path -Parent $CoreDir

$Platforms = @("osx-arm64", "osx-x64", "win-x64", "linux-x64")

Write-Host ""
Write-Host "📦 Building Main Server (DataverseMCPToolBox)..."
Write-Host ""

# Build Core/Server
foreach ($Platform in $Platforms) {
    Write-Host "Building Core for $Platform..."
    
    $OutputDir = Join-Path $CoreDir "publish\$Platform"
    if (Test-Path $OutputDir) {
        Remove-Item -Recurse -Force $OutputDir
    }
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    
    dotnet publish (Join-Path $CoreDir "DataverseMCPToolBox.csproj") `
        -c Release `
        -r $Platform `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -o $OutputDir
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Core built for $Platform" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to build Core for $Platform" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "🌉 Building Bridge (DataverseMCPToolBox.Bridge)..."
Write-Host ""

# Build Bridge
foreach ($Platform in $Platforms) {
    Write-Host "Building Bridge for $Platform..."
    
    $OutputDir = Join-Path $BridgeDir "publish\$Platform"
    if (Test-Path $OutputDir) {
        Remove-Item -Recurse -Force $OutputDir
    }
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    
    dotnet publish (Join-Path $BridgeDir "DataverseMCPToolBox.Bridge.csproj") `
        -c Release `
        -r $Platform `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -o $OutputDir
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Bridge built for $Platform" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to build Bridge for $Platform" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "========================================"
Write-Host "✓ Build completed successfully!" -ForegroundColor Green
Write-Host "========================================"
Write-Host ""
Write-Host "Outputs:"
Write-Host "  Server: $CoreDir\publish\<platform>\"
Write-Host "  Bridge: $BridgeDir\publish\<platform>\"
Write-Host ""
