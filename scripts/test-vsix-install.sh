#!/bin/bash

# Script de test d'installation du VSIX Dataverse MCP ToolBox
# Teste l'installation complète end-to-end avec téléchargement NuGet

set -e

EXTENSION_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Extension"
VSIX_FILE=$(ls -t "$EXTENSION_DIR"/*.vsix 2>/dev/null | head -1)

if [ -z "$VSIX_FILE" ]; then
    echo "❌ Aucun fichier VSIX trouvé dans $EXTENSION_DIR"
    echo "💡 Exécutez d'abord: cd Extension && npm run package"
    exit 1
fi

echo "═══════════════════════════════════════════════════════════════"
echo "🧪 Test d'installation Dataverse MCP ToolBox VSIX"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "📦 VSIX: $(basename "$VSIX_FILE")"
echo "📐 Taille: $(du -h "$VSIX_FILE" | cut -f1)"
echo "🖥️  Plateforme: $(uname -s) $(uname -m)"
echo ""

# Détection de la plateforme
PLATFORM=$(uname -s)
ARCH=$(uname -m)

if [ "$PLATFORM" == "Darwin" ]; then
    if [ "$ARCH" == "arm64" ]; then
        PLATFORM_ID="osx-arm64"
    else
        PLATFORM_ID="osx-x64"
    fi
    STORAGE_PATH="$HOME/Library/Application Support/Code/User/globalStorage/tchinnin.dataverse-mcp-toolbox"
    MCP_CONFIG="$HOME/Library/Application Support/Code/User/mcp.json"
elif [ "$PLATFORM" == "Linux" ]; then
    PLATFORM_ID="linux-x64"
    STORAGE_PATH="$HOME/.config/Code/User/globalStorage/tchinnin.dataverse-mcp-toolbox"
    MCP_CONFIG="$HOME/.config/Code/User/mcp.json"
else
    echo "❌ Plateforme non supportée: $PLATFORM"
    exit 1
fi

echo "🎯 Plateforme cible: $PLATFORM_ID"
echo ""

# Backup du storage existant
if [ -d "$STORAGE_PATH" ]; then
    BACKUP_PATH="${STORAGE_PATH}.backup.$(date +%Y%m%d_%H%M%S)"
    echo "💾 Backup du storage existant..."
    echo "   $STORAGE_PATH"
    echo "   → $BACKUP_PATH"
    mv "$STORAGE_PATH" "$BACKUP_PATH"
    echo "   ✅ Backup créé"
    echo ""
fi

# Backup de la config MCP existante
if [ -f "$MCP_CONFIG" ]; then
    MCP_BACKUP="${MCP_CONFIG}.backup.$(date +%Y%m%d_%H%M%S)"
    echo "💾 Backup de la config MCP..."
    echo "   $MCP_CONFIG"
    echo "   → $MCP_BACKUP"
    cp "$MCP_CONFIG" "$MCP_BACKUP"
    echo "   ✅ Backup créé"
    echo ""
fi

# Désinstallation de la version existante
echo "🗑️  Désinstallation de la version existante..."
code --uninstall-extension tchinnin.dataverse-mcp-toolbox 2>/dev/null || true
echo "   ✅ Désinstallé (si existait)"
echo ""

# Installation du VSIX
echo "📥 Installation du VSIX..."
echo "   $VSIX_FILE"
code --install-extension "$VSIX_FILE"
echo "   ✅ Extension installée"
echo ""

# Attente pour VS Code
echo "⏳ Attente de 3 secondes pour VS Code..."
sleep 3
echo ""

# Vérification de l'installation
echo "═══════════════════════════════════════════════════════════════"
echo "🔍 Vérification de l'installation"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Vérifier l'extension
echo "1️⃣  Extension VS Code..."
if code --list-extensions | grep -q "tchinnin.dataverse-mcp-toolbox"; then
    VERSION=$(code --list-extensions --show-versions | grep "tchinnin.dataverse-mcp-toolbox" | cut -d'@' -f2)
    echo "   ✅ Extension installée: v$VERSION"
else
    echo "   ❌ Extension non trouvée"
    exit 1
fi
echo ""

# Instructions pour le test manuel
echo "═══════════════════════════════════════════════════════════════"
echo "📋 Étapes de test manuel"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "1️⃣  LANCER VS CODE"
echo "   code"
echo ""
echo "2️⃣  VÉRIFIER L'ACTIVITY BAR"
echo "   ✓ Icône Dataverse MCP ToolBox visible dans la barre latérale gauche"
echo "   ✓ Logo personnalisé affiché (pas l'icône database générique)"
echo ""
echo "3️⃣  OUVRIR LA VUE DATAVERSE CONNECTIONS"
echo "   ✓ Cliquer sur l'icône dans l'Activity Bar"
echo "   ✓ Vue 'MCP Server Info' affichée"
echo "   ✓ Vérifier le téléchargement du serveur depuis NuGet"
echo ""
echo "4️⃣  VÉRIFIER LE TÉLÉCHARGEMENT DES BINAIRES"
echo "   ✓ Ouvrir Output → 'Dataverse MCP ToolBox'"
echo "   ✓ Chercher: 'Checking for server updates...'"
echo "   ✓ Chercher: 'Downloading server binaries from NuGet'"
echo "   ✓ Chercher: 'Server binaries downloaded successfully'"
echo "   ✓ Plateforme détectée: $PLATFORM_ID"
echo ""
echo "5️⃣  VÉRIFIER LES FICHIERS TÉLÉCHARGÉS"
echo "   ls -lh \"$STORAGE_PATH/server/\""
echo "   ✓ Dossiers: runtimes/$PLATFORM_ID/native/"
echo "   ✓ Binaires: DataverseMCPToolBox, DataverseMCPToolBox.Bridge"
echo ""
echo "6️⃣  VÉRIFIER LE DÉMARRAGE DU SERVEUR"
echo "   ✓ Dans Output: 'MCP Server started successfully'"
echo "   ✓ Vue 'MCP Server Info' affiche: Connected"
echo "   ✓ Version du serveur affichée: 0.2.260204"
echo ""
echo "7️⃣  TESTER UNE CONNEXION DATAVERSE"
echo "   ✓ Cliquer '+' dans la vue Connections"
echo "   ✓ Entrer les détails de connexion"
echo "   ✓ Compléter l'authentification OAuth"
echo "   ✓ Connexion apparaît dans la liste"
echo "   ✓ Définir comme connexion active (étoile)"
echo ""
echo "8️⃣  VÉRIFIER LA CONFIG MCP"
echo "   cat \"$MCP_CONFIG\""
echo "   ✓ Entrée 'dataverseMcpToolbox' présente"
echo "   ✓ Chemin vers DataverseMCPToolBox.Bridge correct"
echo ""
echo "9️⃣  TESTER AVEC GITHUB COPILOT"
echo "   ✓ Ouvrir un fichier et activer Copilot Chat"
echo "   ✓ Taper: @github who am I in Dataverse?"
echo "   ✓ Vérifier la réponse avec les infos utilisateur"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "💡 CHEMINS UTILES"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Storage Extension:"
echo "  $STORAGE_PATH"
echo ""
echo "Config MCP:"
echo "  $MCP_CONFIG"
echo ""
echo "Logs Output:"
echo "  VS Code → Output → 'Dataverse MCP ToolBox'"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "✅ Installation terminée - Suivre les étapes de test manuel ci-dessus"
echo "═══════════════════════════════════════════════════════════════"
