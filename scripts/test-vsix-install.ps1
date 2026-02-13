# Script de test d'installation du VSIX Dataverse MCP ToolBox (Windows)
# Teste l'installation complète end-to-end avec téléchargement NuGet

$ErrorActionPreference = "Stop"

$ExtensionDir = Join-Path (Split-Path $PSScriptRoot -Parent) "Extension"
$VsixFile = Get-ChildItem -Path $ExtensionDir -Filter "*.vsix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $VsixFile) {
    Write-Host "❌ Aucun fichier VSIX trouvé dans $ExtensionDir" -ForegroundColor Red
    Write-Host "💡 Exécutez d'abord: cd Extension; npm run package" -ForegroundColor Yellow
    exit 1
}

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🧪 Test d'installation Dataverse MCP ToolBox VSIX" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📦 VSIX: $($VsixFile.Name)" -ForegroundColor White
Write-Host "📐 Taille: $([math]::Round($VsixFile.Length/1KB, 2)) KB" -ForegroundColor White
Write-Host "🖥️  Plateforme: Windows x64" -ForegroundColor White
Write-Host ""

$PlatformId = "win-x64"
$StoragePath = "$env:APPDATA\Code\User\globalStorage\tchinnin.dataverse-mcp-toolbox"
$McpConfig = "$env:APPDATA\Code\User\mcp.json"

Write-Host "🎯 Plateforme cible: $PlatformId" -ForegroundColor Green
Write-Host ""

# Backup du storage existant
if (Test-Path $StoragePath) {
    $BackupPath = "${StoragePath}.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "💾 Backup du storage existant..." -ForegroundColor Yellow
    Write-Host "   $StoragePath" -ForegroundColor Gray
    Write-Host "   → $BackupPath" -ForegroundColor Gray
    Move-Item -Path $StoragePath -Destination $BackupPath
    Write-Host "   ✅ Backup créé" -ForegroundColor Green
    Write-Host ""
}

# Backup de la config MCP existante
if (Test-Path $McpConfig) {
    $McpBackup = "${McpConfig}.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "💾 Backup de la config MCP..." -ForegroundColor Yellow
    Write-Host "   $McpConfig" -ForegroundColor Gray
    Write-Host "   → $McpBackup" -ForegroundColor Gray
    Copy-Item -Path $McpConfig -Destination $McpBackup
    Write-Host "   ✅ Backup créé" -ForegroundColor Green
    Write-Host ""
}

# Désinstallation de la version existante
Write-Host "🗑️  Désinstallation de la version existante..." -ForegroundColor Yellow
try {
    & code --uninstall-extension tchinnin.dataverse-mcp-toolbox 2>$null
} catch {
    # Ignore si pas installé
}
Write-Host "   ✅ Désinstallé (si existait)" -ForegroundColor Green
Write-Host ""

# Installation du VSIX
Write-Host "📥 Installation du VSIX..." -ForegroundColor Yellow
Write-Host "   $($VsixFile.FullName)" -ForegroundColor Gray
& code --install-extension $VsixFile.FullName
Write-Host "   ✅ Extension installée" -ForegroundColor Green
Write-Host ""

# Attente pour VS Code
Write-Host "⏳ Attente de 3 secondes pour VS Code..." -ForegroundColor Yellow
Start-Sleep -Seconds 3
Write-Host ""

# Vérification de l'installation
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔍 Vérification de l'installation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Vérifier l'extension
Write-Host "1️⃣  Extension VS Code..." -ForegroundColor White
$Extensions = & code --list-extensions
if ($Extensions -contains "tchinnin.dataverse-mcp-toolbox") {
    $ExtWithVersion = & code --list-extensions --show-versions | Select-String "tchinnin.dataverse-mcp-toolbox"
    $Version = $ExtWithVersion -replace ".*@", ""
    Write-Host "   ✅ Extension installée: v$Version" -ForegroundColor Green
} else {
    Write-Host "   ❌ Extension non trouvée" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Instructions pour le test manuel
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📋 Étapes de test manuel" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "1️⃣  LANCER VS CODE" -ForegroundColor Yellow
Write-Host "   code" -ForegroundColor Gray
Write-Host ""
Write-Host "2️⃣  VÉRIFIER L'ACTIVITY BAR" -ForegroundColor Yellow
Write-Host "   ✓ Icône Dataverse MCP ToolBox visible dans la barre latérale gauche" -ForegroundColor Gray
Write-Host "   ✓ Logo personnalisé affiché (pas l'icône database générique)" -ForegroundColor Gray
Write-Host ""
Write-Host "3️⃣  OUVRIR LA VUE DATAVERSE CONNECTIONS" -ForegroundColor Yellow
Write-Host "   ✓ Cliquer sur l'icône dans l'Activity Bar" -ForegroundColor Gray
Write-Host "   ✓ Vue 'MCP Server Info' affichée" -ForegroundColor Gray
Write-Host "   ✓ Vérifier le téléchargement du serveur depuis NuGet" -ForegroundColor Gray
Write-Host ""
Write-Host "4️⃣  VÉRIFIER LE TÉLÉCHARGEMENT DES BINAIRES" -ForegroundColor Yellow
Write-Host "   ✓ Ouvrir Output → 'Dataverse MCP ToolBox'" -ForegroundColor Gray
Write-Host "   ✓ Chercher: 'Checking for server updates...'" -ForegroundColor Gray
Write-Host "   ✓ Chercher: 'Downloading server binaries from NuGet'" -ForegroundColor Gray
Write-Host "   ✓ Chercher: 'Server binaries downloaded successfully'" -ForegroundColor Gray
Write-Host "   ✓ Plateforme détectée: $PlatformId" -ForegroundColor Gray
Write-Host ""
Write-Host "5️⃣  VÉRIFIER LES FICHIERS TÉLÉCHARGÉS" -ForegroundColor Yellow
Write-Host "   Get-ChildItem `"$StoragePath\server\`" -Recurse" -ForegroundColor Gray
Write-Host "   ✓ Dossiers: runtimes\$PlatformId\native\" -ForegroundColor Gray
Write-Host "   ✓ Binaires: DataverseMCPToolBox.exe, DataverseMCPToolBox.Bridge.exe" -ForegroundColor Gray
Write-Host ""
Write-Host "6️⃣  VÉRIFIER LE DÉMARRAGE DU SERVEUR" -ForegroundColor Yellow
Write-Host "   ✓ Dans Output: 'MCP Server started successfully'" -ForegroundColor Gray
Write-Host "   ✓ Vue 'MCP Server Info' affiche: Connected" -ForegroundColor Gray
Write-Host "   ✓ Version du serveur affichée: 0.2.260204" -ForegroundColor Gray
Write-Host ""
Write-Host "7️⃣  TESTER UNE CONNEXION DATAVERSE" -ForegroundColor Yellow
Write-Host "   ✓ Cliquer '+' dans la vue Connections" -ForegroundColor Gray
Write-Host "   ✓ Entrer les détails de connexion" -ForegroundColor Gray
Write-Host "   ✓ Compléter l'authentification OAuth" -ForegroundColor Gray
Write-Host "   ✓ Connexion apparaît dans la liste" -ForegroundColor Gray
Write-Host "   ✓ Définir comme connexion active (étoile)" -ForegroundColor Gray
Write-Host ""
Write-Host "8️⃣  VÉRIFIER LA CONFIG MCP" -ForegroundColor Yellow
Write-Host "   Get-Content `"$McpConfig`" | ConvertFrom-Json" -ForegroundColor Gray
Write-Host "   ✓ Entrée 'dataverseMcpToolbox' présente" -ForegroundColor Gray
Write-Host "   ✓ Chemin vers DataverseMCPToolBox.Bridge.exe correct" -ForegroundColor Gray
Write-Host ""
Write-Host "9️⃣  TESTER AVEC GITHUB COPILOT" -ForegroundColor Yellow
Write-Host "   ✓ Ouvrir un fichier et activer Copilot Chat" -ForegroundColor Gray
Write-Host "   ✓ Taper: @github who am I in Dataverse?" -ForegroundColor Gray
Write-Host "   ✓ Vérifier la réponse avec les infos utilisateur" -ForegroundColor Gray
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 CHEMINS UTILES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Storage Extension:" -ForegroundColor Yellow
Write-Host "  $StoragePath" -ForegroundColor Gray
Write-Host ""
Write-Host "Config MCP:" -ForegroundColor Yellow
Write-Host "  $McpConfig" -ForegroundColor Gray
Write-Host ""
Write-Host "Logs Output:" -ForegroundColor Yellow
Write-Host "  VS Code → Output → 'Dataverse MCP ToolBox'" -ForegroundColor Gray
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Installation terminée - Suivre les étapes de test manuel ci-dessus" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
