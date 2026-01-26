# Script PowerShell to publish the .NET server for all platforms
# Builds self-contained executables and prepares them for NuGet packaging

Write-Host "🚀 Building self-contained executables for all platforms..." -ForegroundColor Green
Write-Host ""

# Clean old publish directories
if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}

# Array of target platforms
$platforms = @("osx-arm64", "osx-x64", "win-x64", "linux-x64")

# Build for each platform
foreach ($platform in $platforms) {
    Write-Host "📦 Building for $platform..." -ForegroundColor Cyan
    
    # Publish to temporary directory
    $tempDir = "./publish/temp-$platform"
    dotnet publish -c Release -r $platform -o $tempDir --self-contained true /p:PublishSingleFile=true
    
    # Create final directory
    $finalDir = "./publish/$platform"
    New-Item -ItemType Directory -Force -Path $finalDir | Out-Null
    
    # Copy only the main executable (not .pdb or other files)
    if ($platform -like "win-*") {
        Copy-Item "$tempDir/DataverseMCPToolBox.exe" "$finalDir/"
        Write-Host "   ✓ Copied DataverseMCPToolBox.exe" -ForegroundColor Green
    }
    else {
        Copy-Item "$tempDir/DataverseMCPToolBox" "$finalDir/"
        Write-Host "   ✓ Copied DataverseMCPToolBox (executable)" -ForegroundColor Green
    }
    
    # Clean up temporary directory
    Remove-Item -Recurse -Force $tempDir
    Write-Host ""
}

Write-Host "✅ Build complete! Executables are in ./publish/" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Binary sizes:" -ForegroundColor Cyan
Get-ChildItem -Recurse ./publish/*/DataverseMCPToolBox* | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "   $($_.FullName): $sizeMB MB"
}
Write-Host ""
Write-Host "📁 Directory structure for NuGet packaging:" -ForegroundColor Cyan
Get-ChildItem -Recurse ./publish -File | Select-Object FullName
