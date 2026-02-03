---
applyTo: "Extension/**"
---

# Instructions Extension VS Code TypeScript - Dataverse MCP Toolbox

## Architecture de l'extension

### Vue d'ensemble
L'extension VS Code agit comme interface utilisateur et spawne un **Core Server** (.NET) par instance. Elle communique avec ce serveur via un protocole inter-processus.

### Architecture Sidecar
- **Extension** : Spawn et gère le cycle de vie du Core Server
- **Core Server** : Un processus par instance VS Code, gère l'état et les opérations Dataverse
- **MCP Bridge** : Processus séparé spawné par Copilot, se connecte au même Core Server
- **Isolation** : Chaque instance VS Code a son propre Core Server isolé
- **Communication** : Protocole inter-processus (abstrait de l'implémentation spécifique)

### Principe de séparation
- L'Extension **ne connaît pas** les détails du protocole de communication
- Utilise une couche d'abstraction (client RPC générique)
- Peut être adapté à différents transports sans changer la logique métier

### Structure des dossiers
```
Extension/src/
  ├── extension.ts              # Point d'entrée, activation
  ├── commands/                 # Commandes VS Code
  │   └── connectionCommands.ts
  ├── models/                   # Interfaces TypeScript
  │   ├── DataverseConnection.ts
  │   └── RpcModels.ts
  ├── panels/                   # WebView panels
  │   └── WhoAmIPanel.ts
  ├── providers/                # Tree view providers
  │   └── ConnectionsTreeDataProvider.ts
  └── services/                 # Services
      ├── DataverseMCPToolBoxRpcClient.ts  # Client RPC
      ├── ConnectionStorageService.ts
      └── TokenStorageService.ts
```
```

### Méthodes RPC

#### Convention de nommage
- Méthodes en **camelCase** (ex: `createConnection`)
- Correspondent aux méthodes C# en PascalCase (ex: `CreateConnectionAsync`)
- Préfixe/suffixe "Async" omis côté TypeScript (géré par async/await)

#### Appel RPC
```typescript
async createConnection(request: ConnectionRequest): Promise<ConnectionResult> {
    if (!this.connection) {
        throw new Error('RPC client not connected');
    }
    
    try {
        const result = await this.connection.sendRequest<ConnectionResult>(
            'CreateConnectionAsync', 
            request
        );
        return result;
    } catch (error) {
        console.error('RPC call failed:', error);
        throw error;
    }
}
```

#### Gestion des timeouts
```typescript
const timeoutMs = 30000; // 30 secondes
const timeoutPromise = new Promise<never>((_, reject) => 
    setTimeout(() => reject(new Error('RPC call timeout')), timeoutMs)
);

const result = await Promise.race([
    this.connection.sendRequest<T>('MethodName', params),
    timeoutPromise
]);
```

## Modèles de données (RpcModels.ts)

### Synchronisation avec C#
Les interfaces TypeScript doivent correspondre exactement aux classes C# :

```typescript
// C# (PascalCase)
public class ConnectionRequest {
    public string EnvironmentUrl { get; set; }
    public string ClientId { get; set; }
    public string TenantId { get; set; }
}

// TypeScript (camelCase) - conversion automatique
export interface ConnectionRequest {
    environmentUrl: string;
    clientId: string;
    tenantId: string;
}
```

### Propriétés optionnelles
```typescript
export interface ConnectionResult {
    success: boolean;
    connectionId?: string;  // Nullable côté C#
    error?: string;         // Nullable côté C#
}
```

## Extension VS Code

### Activation (extension.ts)

#### Point d'entrée
```typescript
export function activate(context: vscode.ExtensionContext) {
    console.error('[Extension] Activating Dataverse MCP Toolbox...');
    
    // 1. Initialiser les services
    const rpcClient = new DataverseMCPToolBoxRpcClient();
    const storageService = new ConnectionStorageService(context);
    
    // 2. Connecter au serveur RPC
    await rpcClient.connect(context.extensionPath);
    
    // 3. Enregistrer les providers
    const treeProvider = new ConnectionsTreeDataProvider(storageService, rpcClient);
    vscode.window.registerTreeDataProvider('dataverseConnectionsList', treeProvider);
    
    // 4. Enregistrer les commandes
    registerCommands(context, rpcClient, storageService, treeProvider);
    
    // 5. Cleanup au deactivate
    context.subscriptions.push({
        dispose: () => rpcClient.disconnect()
    });
}

export function deactivate() {
    console.error('[Extension] Deactivating...');
    // Le dispose automatique via subscriptions suffit
}
```

### Enregistrement des commandes

#### Convention de nommage
Format : `extensionId.commandName`
- Préfixe : `dataversemcptoolbox.`
- Nom : camelCase descriptif
- Exemples :
  - `dataversemcptoolbox.addConnection`
  - `dataversemcptoolbox.removeConnection`
  - `dataversemcptoolbox.setActiveConnection`
  - `dataversemcptoolbox.showWhoAmI`

#### Enregistrement
```typescript
function registerCommands(
    context: vscode.ExtensionContext,
    rpcClient: DataverseMCPToolBoxRpcClient,
    storage: ConnectionStorageService,
    treeProvider: ConnectionsTreeDataProvider
) {
    // Commande simple
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.addConnection', async () => {
            await handleAddConnection(rpcClient, storage, treeProvider);
        })
    );
    
    // Commande avec argument
    context.subscriptions.push(
        vscode.commands.registerCommand(
            'dataversemcptoolbox.removeConnection', 
            async (item: ConnectionTreeItem) => {
                await handleRemoveConnection(item, rpcClient, storage, treeProvider);
            }
        )
    );
}
```

### Gestion des erreurs

#### Try/catch systématique
```typescript
async function handleAddConnection(...) {
    try {
        // 1. Collecter les inputs utilisateur
        const environmentUrl = await vscode.window.showInputBox({
            prompt: 'Environment URL',
            placeHolder: 'https://org.crm.dynamics.com'
        });
        
        if (!environmentUrl) return; // User cancelled
        
        // 2. Appeler le RPC
        const result = await rpcClient.createConnection({
            environmentUrl,
            clientId,
            tenantId
        });
        
        // 3. Vérifier le résultat
        if (!result.success) {
            throw new Error(result.error || 'Unknown error');
        }
        
        // 4. Sauvegarder et notifier
        await storage.addConnection({ id: result.connectionId, ... });
        vscode.window.showInformationMessage('Connection created successfully');
        treeProvider.refresh();
        
    } catch (error) {
        console.error('Failed to add connection:', error);
        vscode.window.showErrorMessage(
            `Failed to add connection: ${error instanceof Error ? error.message : 'Unknown error'}`
        );
    }
}
```

#### Messages utilisateur
- **Information** : `vscode.window.showInformationMessage()`
- **Warning** : `vscode.window.showWarningMessage()`
- **Error** : `vscode.window.showErrorMessage()`
- **Progress** : `vscode.window.withProgress()`

```typescript
await vscode.window.withProgress({
    location: vscode.ProgressLocation.Notification,
    title: "Connecting to Dataverse...",
    cancellable: false
}, async (progress) => {
    progress.report({ increment: 50 });
    const result = await rpcClient.createConnection(request);
    progress.report({ increment: 100 });
    return result;
});
```

## Storage Services

### ConnectionStorageService
Gère la persistance des connexions dans le workspace state :

```typescript
export class ConnectionStorageService {
    private static readonly CONNECTIONS_KEY = 'dataverse.connections';
    
    constructor(private context: vscode.ExtensionContext) {}
    
    async getConnections(): Promise<DataverseConnection[]> {
        return this.context.workspaceState.get<DataverseConnection[]>(
            ConnectionStorageService.CONNECTIONS_KEY, 
            []
        );
    }
    
    async saveConnections(connections: DataverseConnection[]): Promise<void> {
        await this.context.workspaceState.update(
            ConnectionStorageService.CONNECTIONS_KEY, 
            connections
        );
    }
}
```

### TokenStorageService
Gère le stockage sécurisé des tokens via `SecretStorage` :

```typescript
export class TokenStorageService {
    constructor(private secrets: vscode.SecretStorage) {}
    
    async storeToken(connectionId: string, token: string): Promise<void> {
        await this.secrets.store(`token_${connectionId}`, token);
    }
    
    async getToken(connectionId: string): Promise<string | undefined> {
        return await this.secrets.get(`token_${connectionId}`);
    }
    
    async deleteToken(connectionId: string): Promise<void> {
        await this.secrets.delete(`token_${connectionId}`);
    }
}
```

## Tree View Provider

### ConnectionsTreeDataProvider
Implémente `vscode.TreeDataProvider<T>` pour afficher les connexions :

```typescript
export class ConnectionsTreeDataProvider implements vscode.TreeDataProvider<ConnectionTreeItem> {
    private _onDidChangeTreeData = new vscode.EventEmitter<ConnectionTreeItem | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    
    constructor(
        private storage: ConnectionStorageService,
        private rpcClient: DataverseMCPToolBoxRpcClient
    ) {}
    
    refresh(): void {
        this._onDidChangeTreeData.fire(undefined);
    }
    
    getTreeItem(element: ConnectionTreeItem): vscode.TreeItem {
        return element;
    }
    
    async getChildren(element?: ConnectionTreeItem): Promise<ConnectionTreeItem[]> {
        if (!element) {
            // Root level - afficher toutes les connexions
            const connections = await this.storage.getConnections();
            return connections.map(conn => new ConnectionTreeItem(conn));
        }
        return [];
    }
}
```

### TreeItem personnalisé
```typescript
export class ConnectionTreeItem extends vscode.TreeItem {
    constructor(
        public readonly connection: DataverseConnection
    ) {
        super(connection.name, vscode.TreeItemCollapsibleState.None);
        
        this.description = connection.environmentUrl;
        this.tooltip = `${connection.name}\n${connection.environmentUrl}`;
        this.contextValue = connection.isActive ? 'activeConnection' : 'connection';
        this.iconPath = connection.isActive 
            ? new vscode.ThemeIcon('star-full') 
            : new vscode.ThemeIcon('database');
    }
}
```

## WebView Panels

### WhoAmIPanel
Affiche les informations utilisateur dans un panel WebView :

```typescript
export class WhoAmIPanel {
    public static currentPanel: WhoAmIPanel | undefined;
    private readonly _panel: vscode.WebviewPanel;
    
    public static createOrShow(extensionUri: vscode.Uri, data: WhoAmIResult) {
        // Singleton pattern pour le panel
        if (WhoAmIPanel.currentPanel) {
            WhoAmIPanel.currentPanel._panel.reveal(vscode.ViewColumn.One);
            WhoAmIPanel.currentPanel.update(data);
            return;
        }
        
        const panel = vscode.window.createWebviewPanel(
            'whoAmI',
            'WhoAmI Info',
            vscode.ViewColumn.One,
            { enableScripts: true }
        );
        
        WhoAmIPanel.currentPanel = new WhoAmIPanel(panel, extensionUri, data);
    }
    
    private constructor(
        panel: vscode.WebviewPanel,
        extensionUri: vscode.Uri,
        data: WhoAmIResult
    ) {
        this._panel = panel;
        this._update(data);
        
        this._panel.onDidDispose(() => {
            WhoAmIPanel.currentPanel = undefined;
        });
    }
    
    private _update(data: WhoAmIResult) {
        this._panel.webview.html = this._getHtmlContent(data);
    }
    
    private _getHtmlContent(data: WhoAmIResult): string {
        return `<!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>WhoAmI</title>
        </head>
        <body>
            <h1>User Information</h1>
            <p>User ID: ${data.userId}</p>
            <p>Business Unit ID: ${data.businessUnitId}</p>
            <p>Organization ID: ${data.organizationId}</p>
        </body>
        </html>`;
    }
}
```

## Package.json

### Métadonnées essentielles
```json
{
  "name": "dataverse-mcp-toolbox",
  "displayName": "Dataverse MCP ToolBox",
  "version": "0.0.1",
  "engines": {
    "vscode": "^1.85.0"
  },
  "activationEvents": ["onStartupFinished"],
  "main": "./out/extension.js"
}
```

### Contributions

#### View Container
```json
"viewsContainers": {
  "activitybar": [
    {
      "id": "dataverse-connections",
      "title": "Dataverse Connections",
      "icon": "$(database)"
    }
  ]
}
```

#### Views
```json
"views": {
  "dataverse-connections": [
    {
      "id": "dataverseConnectionsList",
      "name": "Connections"
    }
  ]
}
```

#### Commands
```json
"commands": [
  {
    "command": "dataversemcptoolbox.addConnection",
    "title": "Add Connection",
    "icon": "$(add)"
  }
]
```

#### Menus
```json
"menus": {
  "view/title": [
    {
      "command": "dataversemcptoolbox.addConnection",
      "when": "view == dataverseConnectionsList",
      "group": "navigation"
    }
  ],
  "view/item/context": [
    {
      "command": "dataversemcptoolbox.removeConnection",
      "when": "view == dataverseConnectionsList && viewItem == connection",
      "group": "inline"
    }
  ]
}
```

## Build et publication

### Dépendances des binaires .NET

L'extension référence le **package NuGet Runtime** plutôt que d'embarquer directement les binaires.

#### Structure attendue
```
Extension/
  server/
    binaries/           # Binaires locaux pour debug (gitignored)
      osx-arm64/
      osx-x64/
      win-x64/
      linux-x64/
```

#### Auto-installation Runtime
L'extension peut :
1. Vérifier si les binaires sont présents localement (dev mode)
2. Sinon, télécharger le package NuGet Runtime
3. Extraire les binaires pour la plateforme courante
4. Rendre exécutables (Unix: chmod +x)

### Build TypeScript
```bash
npm run compile
```

Produit les fichiers JavaScript dans `out/`

### Watch mode (développement)
```bash
npm run watch
```

### Développement local avec binaires .NET

#### Workflow de développement
1. Modifier code dans Core ou Bridge
2. Lancer `./scripts/install-local.sh` (depuis racine)
3. Les binaires sont copiés dans `Extension/server/binaries/`
4. F5 dans VS Code pour debug

#### Script install-local.sh
```bash
#!/bin/bash
echo "Building and copying to local Extension for debugging..."

# Build Core + Bridge
./scripts/build-all.sh

# Copier vers Extension
mkdir -p Extension/server/binaries
cp -r Core/publish/* Extension/server/binaries/
cp -r Bridge/publish/* Extension/server/binaries/

# Rendre exécutables sur Unix
if [ "$(uname)" != "Windows_NT" ]; then
    chmod +x Extension/server/binaries/*/DataverseMCPToolBox*
fi

echo "✅ Local binaries ready for debugging"
```

### Package VSIX
```bash
# Installer vsce si pas déjà fait
npm install -g @vscode/vsce

# Créer le package
vsce package
```

### Publication sur marketplace
```bash
# Se connecter avec Personal Access Token
vsce login <publisher>

# Publier
vsce publish
```

### Support multi-plateforme

#### Détection de plateforme
```typescript
function getPlatformIdentifier(): string {
    const platform = process.platform;
    const arch = process.arch;
    
    if (platform === 'darwin') {
        return arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
    } else if (platform === 'win32') {
        return 'win-x64';
    } else if (platform === 'linux') {
        return 'linux-x64';
    }
    throw new Error(`Unsupported platform: ${platform}`);
}
```

#### Sélection et permissions binaires
```typescript
import { chmod } from 'fs/promises';
import * as path from 'path';

async function prepareBinary(extensionPath: string): Promise<string> {
    const platformId = getPlatformIdentifier();
    const binaryName = process.platform === 'win32' 
        ? 'DataverseMCPToolBox.exe'
        : 'DataverseMCPToolBox';
    
    const binaryPath = path.join(
        extensionPath,
        'server',
        'binaries',
        platformId,
        binaryName
    );
    
    // Rendre exécutable sur Unix
    if (process.platform !== 'win32') {
        await chmod(binaryPath, 0o755);
    }
    
    return binaryPath;
}
```

### Checklist avant publication
- [ ] Version mise à jour dans package.json
- [ ] Référence au bon package NuGet Runtime
- [ ] Testé sur Windows et macOS (au minimum)
- [ ] README.md à jour
- [ ] CHANGELOG.md à jour
- [ ] Pas d'erreurs TypeScript
- [ ] Pas de console.log() inutiles (utiliser console.error())
- [ ] Tests fonctionnels passés
- [ ] Vérifier taille du VSIX (< 10MB idéalement)

## Débogage

### Launch configuration (.vscode/launch.json)
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Run Extension",
      "type": "extensionHost",
      "request": "launch",
      "args": [
        "--extensionDevelopmentPath=${workspaceFolder}/Extension"
      ],
      "outFiles": [
        "${workspaceFolder}/Extension/out/**/*.js"
      ],
      "preLaunchTask": "${defaultBuildTask}"
    }
  ]
}
```

### Debug console
- Les logs `console.error()` de l'extension apparaissent dans la Debug Console
- Les logs stderr du serveur .NET apparaissent préfixés `[.NET Server STDERR]`
- Utiliser des breakpoints dans le code TypeScript
- Inspecter les messages RPC avec `connection.trace()`

## Patterns et anti-patterns

### ✅ À FAIRE
- Async/await pour toutes les opérations asynchrones
- Try/catch autour des appels RPC
- Validation des inputs utilisateur
- Messages d'erreur clairs et actionnables
- Dispose des ressources (connections, panels, subscriptions)
- Refresh de la TreeView après modifications
- Gestion des cas utilisateur annule (undefined checks)
- Encodage UTF-8 explicite pour les streams
- Singleton pattern pour les WebView panels

### ❌ À ÉVITER
- console.log() redirigé vers stdout
- Opérations synchrones bloquantes
- Pas de try/catch autour des RPC calls
- Process .NET orphelin après fermeture
- Messages d'erreur techniques exposés à l'utilisateur
- Fuites mémoire (listeners non disposés)
- Timeout infini sur les appels RPC
- Hardcoder les chemins des exécutables
- Ignorer les erreurs silencieusement

## Sécurité

### Tokens et secrets
- Utiliser `vscode.SecretStorage` pour les tokens sensibles
- Ne JAMAIS logger les tokens ou secrets
- Ne JAMAIS inclure de credentials dans le code source
- Valider les URLs avant de les utiliser

```typescript
// ❌ DANGER
console.error(`Token: ${token}`);

// ✅ CORRECT
console.error('Token acquired successfully');
```

### Validation des entrées
```typescript
function validateEnvironmentUrl(url: string): boolean {
    try {
        const parsed = new URL(url);
        return parsed.protocol === 'https:' && 
               parsed.hostname.includes('.dynamics.com');
    } catch {
        return false;
    }
}
```

## Performance

### Optimisations
- Ne pas spawner plusieurs processus .NET (singleton)
- Limiter les refresh de TreeView
- Utiliser le cache pour les données statiques
- Debounce les opérations fréquentes

### Monitoring
- Logger les durées des opérations RPC importantes
- Tracker les timeouts et erreurs
- Surveiller l'utilisation mémoire du process .NET

## Tests

### Structure
```
Extension/src/test/
  ├── suite/
  │   └── extension.test.ts
  └── runTest.ts
```

### Tests unitaires
```typescript
import * as assert from 'assert';
import * as vscode from 'vscode';

suite('Extension Test Suite', () => {
    test('Extension should be present', () => {
        assert.ok(vscode.extensions.getExtension('publisher.dataverse-mcp-toolbox'));
    });
    
    test('Commands should be registered', async () => {
        const commands = await vscode.commands.getCommands();
        assert.ok(commands.includes('dataversemcptoolbox.addConnection'));
    });
});
```

### Tests d'intégration
- Tester avec un mock du RPC client
- Tester les flows utilisateur complets
- Tester la gestion des erreurs

## Documentation

### JSDoc
```typescript
/**
 * Creates a new Dataverse connection
 * @param request Connection parameters (URL, clientId, tenantId)
 * @returns Connection result with success status and connectionId
 * @throws Error if RPC client is not connected
 */
async createConnection(request: ConnectionRequest): Promise<ConnectionResult> {
    // ...
}
```

### README par composant
- Documenter les services complexes
- Expliquer les flows d'authentification
- Donner des exemples d'utilisation des APIs

## Dépendances

### Runtime
```json
"dependencies": {
  "vscode-jsonrpc": "^8.2.0"
}
```

### Development
```json
"devDependencies": {
  "@types/vscode": "^1.85.0",
  "@types/node": "18.x",
  "@typescript-eslint/eslint-plugin": "^6.13.0",
  "@typescript-eslint/parser": "^6.13.0",
  "eslint": "^8.54.0",
  "typescript": "^5.3.0"
}
```

### Mises à jour
- Vérifier la compatibilité avec VS Code Engine
- Tester après mise à jour de vscode-jsonrpc
- Suivre les breaking changes de l'API VS Code
