# Instructions Globales - Dataverse MCP Toolbox

## Vue d'ensemble du projet

Le **Dataverse MCP Toolbox** suit une **architecture sidecar** composée de trois parties principales :
1. **Core Server** : Serveur .NET gérant les opérations Dataverse et l'exécution des outils
2. **MCP Bridge** : Serveur .NET adaptateur pour GitHub Copilot (protocole MCP)
3. **Extension VS Code** : Interface utilisateur TypeScript pour la gestion visuelle

## Architecture Sidecar

### Principe fondamental
- **Séparation des responsabilités** : Chaque composant a un rôle distinct
- **Core Server** : Gère l'état, les connexions Dataverse, et l'exécution des outils
- **MCP Bridge** : Adaptateur protocol MCP pour GitHub Copilot
- **Extension VS Code** : Spawn et manage le Core Server, interface utilisateur
- **Isolation par instance** : Chaque instance VS Code a son propre Core Server

### Architecture en couches
```
┌─────────────────────────────────────────────────────┐
│ VS Code Instance                                    │
│  ┌──────────────┐     ┌─────────────────────────┐  │
│  │  Extension   │────▶│    Core Server          │  │
│  │  (UI Layer)  │     │  • Connection Mgmt      │  │
│  └──────────────┘     │  • Tool Execution       │  │
│                       │  • State Management     │  │
│                       └────────▲────────────────┘  │
│                                │                    │
│  ┌──────────────┐             │                    │
│  │  MCP Bridge  │─────────────┘                    │
│  │  (Copilot)   │                                   │
│  └──────────────┘                                   │
└─────────────────────────────────────────────────────┘
```

### Principes de communication
- **Transport agnostique** : Les services ne dépendent pas d'un protocole spécifique
- **Contrat clair** : Interfaces définies pour toute communication inter-processus
- **Sérialisation standard** : JSON avec camelCase pour compatibilité C#/TypeScript
- **Isolation des logs** : stderr exclusivement pour logs, jamais sur les canaux de données

### Variables d'environnement
- Utilisées pour la configuration et l'identification des instances
- Permettent l'isolation entre multiples instances VS Code
- Exemple : `DATAVERSE_MCP_PIPE_NAME`, `DATAVERSE_MCP_PLUGIN_DIR`

### Règle critique pour les logs
⚠️ **RÈGLE ABSOLUE** : Séparer logs et données
- ✅ `Console.Error.WriteLine()` pour tous les logs en C#
- ✅ `console.error()` pour tous les logs en TypeScript
- ✅ `Trace.Listeners` redirigé vers stderr
- ❌ Ne jamais écrire sur stdout en Core Server
- ❌ stdout réservé pour MCP Bridge (communication Copilot)

## Séparation des Préoccupations (Separation of Concerns)

### Principes architecturaux

#### 1. Couche de Communication
- **Responsabilité** : Transport des messages entre processus
- **Isolation** : Les services métier ne connaissent PAS le protocole de transport
- **Abstraction** : Interfaces génériques (`IRpcServer`, `IRpcClient`)
- **Implémentation** : Peut être remplacée sans impacter les services

#### 2. Couche Service (Business Logic)
- **Responsabilité** : Logique métier Dataverse et gestion des outils
- **Indépendance** : Aucune dépendance sur la communication inter-processus
- **Testabilité** : Peut être testé sans transport (mocking)
- **Réutilisabilité** : Services utilisables dans n'importe quelle architecture

#### 3. Couche Données (Models/DTOs)
- **Responsabilité** : Structures de données partagées
- **Sérialisation** : Facilement sérialisables en JSON
- **Synchronisation** : C# (PascalCase) ↔ TypeScript (camelCase)
- **Validation** : Validation des données à l'entrée des services

### Services Core (réutilisables)

#### ConnectionStateService
- Thread-safe in-memory state management
- CRUD operations pour les connexions
- Pas de dépendance externe

#### DataverseConnectionService
- Gestion du cycle de vie des connexions Dataverse SDK
- Pool de connexions actives
- Test de connectivité

#### DataverseAuthService
- Acquisition et refresh de tokens OAuth
- Utilise MSAL (Microsoft Identity Client)
- Pas de logique de transport

#### ToolExecutionService
- Orchestration de l'exécution des outils
- Gestion du contexte Dataverse
- Propagation des erreurs

#### PluginManager
- Chargement dynamique des plugins depuis filesystem
- Validation des manifests
- Isolation et lifecycle des plugins

### Avantages de cette architecture
- **Maintenabilité** : Changement de protocole sans toucher aux services
- **Testabilité** : Services testables indépendamment
- **Évolutivité** : Ajout de nouveaux transports facile
- **Clarté** : Responsabilités clairement définies

## Conventions de nommage

### C# (.NET Core)
- **Classes** : `PascalCase` (ex: `DataverseConnectionService`)
- **Méthodes** : `PascalCase` avec suffixe `Async` si asynchrone (ex: `CreateConnectionAsync`)
- **Propriétés** : `PascalCase` (ex: `ConnectionId`)
- **Variables locales** : `camelCase` (ex: `connectionService`)
- **Paramètres** : `camelCase` (ex: `connectionId`)
- **Interfaces** : Préfixe `I` + `PascalCase` (ex: `IDataverseMCPToolBoxRpcService`)
- **Fichiers** : Nom de la classe principale (ex: `DataverseConnectionService.cs`)

### TypeScript (Extension)
- **Classes** : `PascalCase` (ex: `DataverseMCPToolBoxRpcClient`)
- **Méthodes** : `camelCase` (ex: `createConnection`)
- **Propriétés** : `camelCase` (ex: `connectionId`)
- **Interfaces/Types** : `PascalCase` (ex: `ConnectionRequest`)
- **Constantes** : `UPPER_SNAKE_CASE` (ex: `DEFAULT_TIMEOUT`)
- **Fichiers** : `camelCase` ou `PascalCase` selon le contenu
  - Services : `PascalCase` (ex: `DataverseMCPToolBoxRpcClient.ts`)
  - Utilitaires : `camelCase` (ex: `connectionCommands.ts`)

### Commandes VS Code
- Format : `extensionId.commandName` en camelCase
- Préfixe obligatoire : `dataversemcptoolbox.` 
- Exemple : `dataversemcptoolbox.addConnection`

## Versionning et déploiement

### Versions
- **Core + Bridge Runtime** : Version dans NuGet package (`DataverseMCPToolBox.Runtime`)
- **Extensibility SDK** : Version dans NuGet package (`DataverseMCPToolBox.Extensibility`)
- **Extension VS Code** : `package.json` > `version` (format semver : `x.y.z`)
- **Synchronisation** : Maintenir cohérence entre Runtime et Extension

### Packages de déploiement

#### 1. NuGet Package Runtime (`DataverseMCPToolBox.Runtime`)
**Contenu** :
- Core Server (DataverseMCPToolBox) - 4 plateformes
- MCP Bridge (DataverseMCPToolBox.Bridge) - 4 plateformes
- Binaires self-contained (pas de dépendance .NET runtime)

**Structure** :
```
runtimes/
  osx-arm64/native/
    DataverseMCPToolBox
    DataverseMCPToolBox.Bridge
  osx-x64/native/
    DataverseMCPToolBox
    DataverseMCPToolBox.Bridge
  win-x64/native/
    DataverseMCPToolBox.exe
    DataverseMCPToolBox.Bridge.exe
  linux-x64/native/
    DataverseMCPToolBox
    DataverseMCPToolBox.Bridge
```

**Publication** :
```bash
# Build et pack (depuis racine du projet)
./scripts/build-all.sh          # ou build-all.ps1 sur Windows
./Core/scripts/pack-nuget.sh    # Crée le .nupkg

# Publier sur NuGet.org
dotnet nuget push Core/nupkg/DataverseMCPToolBox.Runtime.*.nupkg \
  --api-key <key> \
  --source https://api.nuget.org/v3/index.json
```

#### 2. NuGet Package Extensibility (`DataverseMCPToolBox.Extensibility`)
**Contenu** :
- SDK pour créer des plugins custom
- Interfaces : `IPlugin`, `IMcpTool`, `IDataverseContext`
- Classes de base : `PluginBase`, `McpToolBase`
- Attributs : `[McpPlugin]`, `[McpTool]`
- Helpers : `SchemaGenerator`

**Publication** :
```bash
cd Extensibility
dotnet pack -c Release -o nupkg
dotnet nuget push nupkg/DataverseMCPToolBox.Extensibility.*.nupkg \
  --api-key <key> \
  --source https://api.nuget.org/v3/index.json
```

#### 3. VSIX Extension VS Code
**Contenu** :
- Code TypeScript compilé
- **Référence** au package NuGet Runtime (pas d'embed direct)
- Configuration pour auto-téléchargement du Runtime
- UI, commandes, providers, panels

**Build et publication** :
```bash
cd Extension
npm install
npm run compile
vsce package        # Crée le .vsix
vsce publish        # Publie sur marketplace
```

### Workflow de développement local

#### Script de copy locale pour debug
**Objectif** : Tester rapidement sans publier sur NuGet

**Script `scripts/install-local.sh` (ou `.ps1`)** :
```bash
#!/bin/bash
echo "Building and copying to local Extension for debugging..."

# Build Core + Bridge pour la plateforme actuelle
./scripts/build-all.sh

# Créer NuGet package local
cd Core
./scripts/pack-nuget.sh

# Copier vers Extension pour debug
mkdir -p ../Extension/server/binaries
cp -r publish/* ../Extension/server/binaries/

echo "✅ Local binaries installed in Extension/server/binaries"
echo "Press F5 in VS Code to debug"
```

**Usage** :
1. Modifier code dans Core ou Bridge
2. Lancer `./scripts/install-local.sh`
3. F5 dans VS Code pour debug Extension
4. Les binaires locaux sont utilisés automatiquement

### Support multi-plateforme

#### Plateformes supportées
- **macOS** : Intel (x64) et Apple Silicon (arm64)
- **Windows** : 64-bit (x64)
- **Linux** : 64-bit (x64)

#### Détection de plateforme (Extension)
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

#### Sélection du binaire correct
L'Extension doit :
1. Détecter la plateforme courante
2. Sélectionner le binaire approprié depuis le package Runtime
3. Vérifier les permissions d'exécution (Unix: `chmod +x`)
4. Spawner le Core Server avec le bon exécutable

### Checklist avant release

#### Core + Bridge
- [ ] Build sur toutes les plateformes (osx-arm64, osx-x64, win-x64, linux-x64)
- [ ] Tester manuellement sur au moins 2 plateformes
- [ ] Vérifier stderr logs uniquement (pas stdout)
- [ ] Vérifier taille des binaires (< 50MB par plateforme)
- [ ] Version cohérente dans .csproj
- [ ] Générer NuGet package

#### Extensibility SDK
- [ ] Build sans warnings
- [ ] Documentation XML à jour
- [ ] Exemples de plugins testés
- [ ] Version cohérente avec Runtime
- [ ] Générer NuGet package

#### Extension VS Code
- [ ] Compiler TypeScript sans erreurs
- [ ] Référence au bon package NuGet Runtime
- [ ] Tester sur Windows et macOS
- [ ] Version mise à jour dans package.json
- [ ] README et CHANGELOG à jour
- [ ] Tests end-to-end passés
- [ ] Générer VSIX

#### Documentation
- [ ] Documenter breaking changes
- [ ] Mettre à jour ARCHITECTURE_REWORK.md si nécessaire
- [ ] Mettre à jour instructions Copilot
- [ ] Guide de migration si version majeure

## Gestion des erreurs

### Serveur .NET
- Exceptions catchées et loggées sur **stderr**
- Retourner des objets de résultat avec propriétés `success` et `error` si applicable
- Ne jamais laisser une exception non gérée crash le serveur
- Exit code 1 uniquement en cas d'erreur fatale au démarrage

### Extension TypeScript
- Try/catch autour de tous les appels RPC
- Messages d'erreur utilisateur via `vscode.window.showErrorMessage()`
- Logs détaillés via `console.error()`
- Gérer la déconnexion du serveur RPC gracieusement

## Bonnes pratiques

### Logging et debugging

#### Côté Core Server (.NET)
```csharp
// ✅ BON - Logs sur stderr uniquement
Console.Error.WriteLine("[Core] Server starting...");
Trace.WriteLine("Debug info"); // Si redirigé vers stderr

// ❌ INTERDIT - stdout réservé pour les données
Console.WriteLine("Log message"); // NE JAMAIS FAIRE ÇA!
```

#### Côté MCP Bridge (.NET)
```csharp
// ✅ BON - Logs sur stderr
Console.Error.WriteLine("[Bridge] Connecting to Core...");

// ✅ BON - stdout pour communication Copilot uniquement
Console.WriteLine(jsonMessage); // Seulement dans le forward STDIO
```

#### Côté Extension (TypeScript)
```typescript
// ✅ BON - Logs sur stderr/console
console.error('[Extension] Server connected');

// ✅ BON - Utiliser vscode.window pour messages utilisateur
vscode.window.showInformationMessage('Connection successful');
```

### Services réutilisables

#### Principes de design
```csharp
// ✅ BON - Service sans dépendance transport
public class DataverseConnectionService
{
    // Pas de référence à sockets, pipes, ou streams
    public async Task<ConnectionResult> CreateConnectionAsync(
        ConnectionRequest request)
    {
        // Logique métier pure
    }
}

// ❌ MAUVAIS - Service couplé au transport
public class DataverseConnectionService
{
    private readonly NamedPipeServerStream _pipe; // ❌ Couplage!
    
    public async Task<ConnectionResult> CreateConnectionAsync(...)
    {
        // Logique mélangée avec transport
    }
}
```

#### Injection de dépendances
```csharp
// ✅ BON - Dépendances via constructeur
public class ToolExecutionService
{
    private readonly IDataverseContext _context;
    private readonly IPluginManager _pluginManager;
    
    public ToolExecutionService(
        IDataverseContext context,
        IPluginManager pluginManager)
    {
        _context = context;
        _pluginManager = pluginManager;
    }
}
```

### Communication inter-processus

#### Contrats d'interface
```csharp
// Core Server expose des méthodes via interface
public interface IDataverseOperations
{
    Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request);
    Task<WhoAmIResult> GetWhoAmIAsync(string connectionId);
    Task<ToolCallResult> ExecuteToolAsync(ToolCallRequest request);
}
```

#### DTOs et sérialisation
- Toutes les méthodes async (`Task` ou `Task<T>`)
- Paramètres et retours sérialisables en JSON
- Utiliser des DTOs (Data Transfer Objects)
- Validation des données à l'entrée

### Multi-plateforme

#### Détection de plateforme
```csharp
// C# - RuntimeInformation
using System.Runtime.InteropServices;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Code Windows-specific
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    // Code macOS-specific
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    // Code Linux-specific
}
```

```typescript
// TypeScript - process.platform
if (process.platform === 'win32') {
    // Windows
} else if (process.platform === 'darwin') {
    // macOS
} else if (process.platform === 'linux') {
    // Linux
}
```

#### Chemins de fichiers
```csharp
// ✅ BON - Utiliser Path.Combine
var path = Path.Combine(baseDir, "subfolder", "file.txt");

// ❌ MAUVAIS - Hardcoded separators
var path = baseDir + "\\subfolder\\file.txt"; // Échoue sur Unix
```

#### Permissions d'exécution (Unix)
```typescript
// Extension doit rendre les binaires exécutables sur Unix
import { chmod } from 'fs/promises';

if (process.platform !== 'win32') {
    await chmod(serverPath, 0o755);
}
```

## Gestion de l'état

### State Management (In-Memory par instance)
- **Core Server** : Maintient l'état en RAM pour son instance VS Code
- **ConnectionStateService** : In-memory dictionary thread-safe, pas de fichiers
- **Isolation** : Chaque instance VS Code a son propre état isolé
- **Avantages** : 
  - Performance : pas d'I/O disque
  - Simplicité : pas de synchronisation inter-processus
  - Isolation : pas de conflit entre instances VS Code
  - Sécurité : état confiné à l'espace de l'instance
- **Comportement** : L'état est perdu au redémarrage du Core Server (comportement normal)

### Partage d'état entre Extension et Copilot
- Extension et MCP Bridge communiquent avec le **même Core Server**
- État partagé naturellement via le Core Server
- Connexions créées dans l'Extension visibles par Copilot
- Outils exécutés par Copilot utilisent les connexions de l'Extension

## Modèles de données

### Synchronisation C# ↔ TypeScript
- Créer des classes C# dans `Core/Models/`
- Créer des interfaces TypeScript correspondantes dans `Extension/src/models/`
- **Propriétés identiques** avec mapping camelCase automatique
- Exemple :
  ```csharp
  // C# - PascalCase
  public class ConnectionResult {
      public bool Success { get; set; }
      public string ConnectionId { get; set; }
  }
  ```
  ```typescript
  // TypeScript - camelCase (auto-converti par JSON-RPC)
  interface ConnectionResult {
      success: boolean;
      connectionId: string;
  }
  ```

## Dépendances

### Serveur .NET
- `Microsoft.PowerPlatform.Dataverse.Client` : SDK Dataverse officiel
- `StreamJsonRpc` : Implémentation JSON-RPC (version 2.x)
- `Newtonsoft.Json` : Sérialisation JSON avec CamelCase resolver
- `Microsoft.Identity.Client` : Authentification MSAL

### Extension TypeScript
- `vscode` : API VS Code (version ^1.85.0 minimum)
- `vscode-jsonrpc` : Client JSON-RPC pour Node.js (version 8.x)

## Tests et débogage

### Déboguer le serveur .NET
1. Lancer depuis IDE avec arguments configurés
2. Surveiller les logs stderr
3. Tester les appels RPC manuellement si nécessaire

### Déboguer l'extension
1. `F5` dans VS Code (Extension Development Host)
2. Console de débogage pour logs stderr du processus .NET
3. Breakpoints dans le code TypeScript

### Problèmes courants

#### Core Server
- **"Server crashed"** : Vérifier les logs stderr du processus
- **"Connection failed"** : Vérifier que le serveur a bien démarré
- **"Parse error"** : stdout pollué → chercher Console.WriteLine illégaux
- **"Method not found"** : Nom de méthode ne correspond pas entre client/serveur
- **"Dataverse connection failed"** : Vérifier credentials et URL d'environnement

#### MCP Bridge
- **"Cannot connect to Core"** : Core Server pas démarré ou variable d'env incorrecte
- **"Copilot not receiving responses"** : Vérifier format stdout JSON
- **"Protocol mismatch"** : Vérifier traduction MCP ↔ Core

#### Extension
- **"Server process not found"** : Binaire manquant pour la plateforme
- **"Permission denied"** : Fichier pas exécutable (Unix: chmod +x)
- **"Multiple instances conflict"** : Variable d'env pas unique par instance

#### Multi-plateforme
- **"Binary not compatible"** : Mauvaise plateforme détectée ou binaire corrompu
- **"Path not found"** : Chemins hardcodés avec mauvais séparateurs

## Bugs connus à ne pas reproduire

### 🐛 Pollution stdout avec des logs
**Problème** : Utilisation de `Console.WriteLine()` pour logger → corrompt les données
**Solution** : Toujours utiliser `Console.Error.WriteLine()` pour les logs

### 🐛 Encodage des caractères
**Problème** : Caractères spéciaux mal encodés entre .NET et TypeScript
**Solution** : Spécifier UTF-8 explicitement dans tous les streams

### 🐛 Méthodes synchrones qui bloquent
**Problème** : Méthodes synchrones qui causent des deadlocks
**Solution** : Toutes les opérations I/O doivent être async (Task/await)

### 🐛 Exceptions non catchées
**Problème** : Exception dans le serveur → crash silencieux
**Solution** : Try/catch au niveau service + log stderr + retour erreur structuré

### 🐛 Services couplés au transport
**Problème** : Services dépendent d'un protocole spécifique (pipes, TCP, etc.)
**Solution** : Services doivent être transport-agnostic avec interfaces abstraites

### 🐛 Process orphelin après fermeture extension
**Problème** : Le Core Server continue de tourner après fermeture VS Code
**Solution** : Extension doit terminate le processus Core Server au deactivate()

### 🐛 Binaire wrong platform
**Problème** : Extension lance le mauvais binaire pour la plateforme
**Solution** : Détecter `process.platform` et `process.arch` correctement

### 🐛 Permissions exécution Unix
**Problème** : Binaires pas exécutables sur macOS/Linux
**Solution** : Extension doit faire `chmod +x` avant de spawner le processus

## Structure des répertoires

```
/Core                           → Core Server .NET
  /Abstractions                 → Interfaces génériques (IRpcServer, etc.)
  /JsonRpc                      → Services RPC (implémentation)
  /Models                       → DTOs partagés (C#)
  /Services                     → Logique métier Dataverse (réutilisable)
  /scripts                      → Scripts de build et packaging
  /publish                      → Binaires générés par plateforme
  /nupkg                        → Package NuGet généré

/Bridge                         → MCP Bridge .NET
  /Services                     → Services Bridge (MCP protocol, client)
  /publish                      → Binaires générés par plateforme

/Extension                      → Extension VS Code
  /src
    /commands                   → Commandes VS Code
    /models                     → Interfaces TypeScript (sync avec C#)
    /panels                     → Panels WebView
    /providers                  → Tree view providers
    /services                   → Services Extension (client, storage)
  /server                       → Binaires locaux pour debug (gitignored)

/Extensibility                  → SDK pour plugins custom
  /Abstractions                 → Interfaces publiques
  /Attributes                   → Attributs pour plugins
  /Models                       → Modèles pour plugins
  /Helpers                      → Helpers (SchemaGenerator)
  /nupkg                        → Package NuGet Extensibility

/Tests                          → Projet de test .NET
  /Unit                         → Tests unitaires
    /Services                   → Tests des services
    /Communication              → Tests de la couche communication
  /Integration                  → Tests d'intégration
  /Helpers                      → Helpers de test

/scripts                        → Scripts globaux
  build-all.sh                  → Build Core + Bridge
  build-all.ps1                 → Build Core + Bridge (Windows)
  install-local.sh              → Copy local pour debug
  install-local.ps1             → Copy local pour debug (Windows)
```

## Ressources

- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [StreamJsonRpc Documentation](https://github.com/microsoft/vs-streamjsonrpc)
- [VS Code Extension API](https://code.visualstudio.com/api)
- [Dataverse SDK Documentation](https://learn.microsoft.com/power-apps/developer/data-platform/)
