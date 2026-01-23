# Build Scripts

Scripts pour construire et publier le serveur .NET Dataverse MCP ToolBox.

## Scripts disponibles

### `build-publish.sh` (macOS/Linux)
```bash
./build-publish.sh
```
Construit des exécutables self-contained pour toutes les plateformes supportées.

### `build-publish.ps1` (Windows)
```powershell
.\build-publish.ps1
```
Version PowerShell du script de build pour Windows.

## Plateformes supportées

- **macOS ARM64** (Apple Silicon) - `osx-arm64`
- **macOS x64** (Intel) - `osx-x64`
- **Windows x64** - `win-x64`
- **Linux x64** - `linux-x64`

## Output

Les exécutables sont générés dans `../publish/<platform>/DataverseMCPToolBox[.exe]`
