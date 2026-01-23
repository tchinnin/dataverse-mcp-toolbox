# Script PowerShell pour publier le serveur .NET pour toutes les plateformes

Write-Host "🚀 Building self-contained executables for all platforms..." -ForegroundColor Green

# Nettoyer les anciennes publications
if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}

# macOS ARM64 (Apple Silicon)
Write-Host "📦 Building for macOS ARM64..." -ForegroundColor Cyan
dotnet publish -c Release -r osx-arm64 -o ./publish/osx-arm64 --self-contained

# macOS x64 (Intel)
Write-Host "📦 Building for macOS x64..." -ForegroundColor Cyan
dotnet publish -c Release -r osx-x64 -o ./publish/osx-x64 --self-contained

# Windows x64
Write-Host "📦 Building for Windows x64..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 -o ./publish/win-x64 --self-contained

# Linux x64
Write-Host "📦 Building for Linux x64..." -ForegroundColor Cyan
dotnet publish -c Release -r linux-x64 -o ./publish/linux-x64 --self-contained

Write-Host "✅ Build complete! Executables are in ./publish/" -ForegroundColor Green
