---
applyTo: "Core/**"
---

# Instructions Serveur .NET Core - Dataverse MCP Toolbox

## Principes architecturaux

### Architecture Sidecar
Le serveur .NET suit une **architecture sidecar** composée de deux applications :
1. **Core Server** : Serveur principal gérant l'état, les connexions Dataverse, et l'exécution des outils
2. **MCP Bridge** : Adaptateur protocol pour GitHub Copilot (communication MCP)

### Séparation des préoccupations

#### Couche Communication (Transport Layer)
- **Responsabilité** : Transport des messages entre processus
- **Abstraction** : Utilise des interfaces génériques (`IRpcServer`, `IRpcClient`)
- **Implémentation** : Peut être remplacée sans impacter les services métier
- **Isolation** : Les services ne doivent JAMAIS dépendre du protocole de transport

#### Couche Service (Business Logic)
- **Responsabilité** : Logique métier Dataverse et gestion des outils
- **Indépendance** : Aucune dépendance sur le transport inter-processus
- **Réutilisabilité** : Services utilisables dans n'importe quelle architecture de communication
- **Testabilité** : Services testables sans infrastructure de transport

#### Couche Données (Models/DTOs)
- **Responsabilité** : Structures de données partagées entre client et serveur
- **Sérialisation** : Facilement sérialisables en JSON
- **Validation** : Validation des données à l'entrée des services

### Règles critiques pour les logs
⚠️ **ABSOLUMENT IMPÉRATIF** ⚠️

#### Core Server
- **stderr** est le SEUL canal autorisé pour les logs, traces, et messages de débogage
- **stdout** ne doit JAMAIS être utilisé (réservé pour données structurées si nécessaire)
- Les canaux de communication (pipes, sockets, etc.) transportent des données, pas des logs

#### MCP Bridge
- **stderr** pour tous les logs
- **stdout** uniquement pour la communication avec Copilot (protocole MCP)
- **stdin** uniquement pour recevoir les requêtes Copilot

```csharp
// ✅ CORRECT - Logging Core Server
Console.Error.WriteLine("Server starting...");
Trace.WriteLine("Debug info"); // Si redirigé vers stderr

// ✅ CORRECT - Logging MCP Bridge
Console.Error.WriteLine("[Bridge] Forwarding request...");

// ❌ INTERDIT - Corrompt les flux de données
Console.WriteLine("Log message"); // NE JAMAIS FAIRE ÇA dans Core Server!
Console.Out.WriteLine("Info");     // NE JAMAIS FAIRE ÇA!
```

### Configuration du Trace Listener
Au démarrage dans `Program.cs`, toujours rediriger les traces vers stderr :

```csharp
Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
Trace.AutoFlush = true;
```

## Structure du serveur

### Program.cs - Point d'entrée

#### Core Server
Le `Main` du Core Server doit :
1. Rediriger tous les logs vers stderr
2. Lire les variables d'environnement de configuration
3. Initialiser les services métier (singleton partagé entre clients)
4. Démarrer le serveur de communication (via interface `IRpcServer`)
5. Accepter les connexions clientes et créer des sessions
6. Gérer le cycle de vie et l'arrêt gracieux

```csharp
// Structure générique du Core Server
static async Task Main(string[] args)
{
    // 1. Configuration des logs
    Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
    Trace.AutoFlush = true;
    
    Console.Error.WriteLine("Core Server starting...");
    
    // 2. Lecture configuration depuis env vars
    string pipeName = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PIPE_NAME") 
                      ?? "default-pipe";
    string pluginDir = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PLUGIN_DIR")
                      ?? Path.Combine(Environment.GetFolderPath(
                          Environment.SpecialFolder.UserProfile), 
                          ".dataverse-mcp-toolbox", "plugins");
    
    // 3. Initialisation des services (singleton)
    var managementService = new DataverseMCPToolBoxRpcService(pluginDir);
    
    // 4. Démarrage du serveur (abstraction via IRpcServer)
    var rpcServer = new CommunicationServer(pipeName); // Implémentation spécifique
    rpcServer.RegisterService(managementService);
    
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    
    await rpcServer.StartAsync(cts.Token);
    
    Console.Error.WriteLine("Core Server stopped");
}
```

#### MCP Bridge
Le `Main` du MCP Bridge doit :
1. Rediriger tous les logs vers stderr
2. Lire les variables d'environnement (pipe name, etc.)
3. Se connecter au Core Server (via interface `IRpcClient`)
4. Gérer la communication bidirectionnelle stdin/stdout avec Copilot
5. Traduire les requêtes MCP vers le format Core et inversement

```csharp
// Structure générique du MCP Bridge
static async Task Main(string[] args)
{
    // 1. Configuration des logs
    Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
    Trace.AutoFlush = true;
    
    Console.Error.WriteLine("MCP Bridge starting...");
    
    // 2. Lecture configuration
    string pipeName = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PIPE_NAME");
    if (string.IsNullOrEmpty(pipeName))
    {
        Console.Error.WriteLine("ERROR: DATAVERSE_MCP_PIPE_NAME not set");
        Environment.Exit(1);
    }
    
    // 3. Connexion au Core Server
    var coreClient = new CommunicationClient(pipeName); // Implémentation spécifique
    await coreClient.ConnectAsync(CancellationToken.None);
    
    // 4. Forwarding bidirectionnel stdin/stdout ↔ Core
    await ForwardMessagesAsync(Console.OpenStandardInput(), 
                               Console.OpenStandardOutput(),
                               coreClient);
}
```

### Interfaces de communication

#### IRpcServer (Core Server)
Interface abstraite pour le serveur de communication :

```csharp
public interface IRpcServer
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    void RegisterService(object service);
}
```

#### IRpcClient (MCP Bridge)
Interface abstraite pour le client de communication :

```csharp
public interface IRpcClient
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task<TResult> InvokeAsync<TResult>(string method, object? args);
}
```

### Interface RPC Service (Contrat métier)
Définit le contrat métier entre serveur et clients :

```csharp
public interface IDataverseMCPToolBoxRpcService
{
    Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request);
    Task<bool> TestConnectionAsync(string connectionId);
    Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId);
    Task<WhoAmIResult> GetWhoAmIAsync(string connectionId);
    Task<ToolCallResult> ExecuteToolAsync(ToolCallRequest request);
    Task CloseConnectionAsync(string connectionId);
    Task CloseAllConnectionsAsync();
}
```

Caractéristiques :
- Toutes les méthodes retournent `Task` ou `Task<T>`
- Noms en PascalCase avec suffixe `Async`
- Paramètres sérialisables en JSON uniquement
- Indépendant du protocole de transport

### Implémentation RPC Service (DataverseMCPToolBoxRpcService)
- Implémente l'interface RPC métier
- Délègue la logique aux services spécialisés
- Gestion de la conversion des exceptions en résultats
- **Transport-agnostic** : ne dépend d'aucun protocole de communication

```csharp
public class DataverseMCPToolBoxRpcService : IDataverseMCPToolBoxRpcService
{
    private readonly ConnectionStateService _connectionState;
    private readonly DataverseConnectionService _connectionService;
    private readonly ToolExecutionService _toolExecutionService;
    
    public DataverseMCPToolBoxRpcService(string pluginDirectory)
    {
        _connectionState = new ConnectionStateService();
        _connectionService = new DataverseConnectionService(_connectionState);
        
        var pluginManager = new PluginManager(pluginDirectory);
        _toolExecutionService = new ToolExecutionService(
            _connectionState, 
            pluginManager
        );
    }
    
    public async Task<ConnectionResult> CreateConnectionAsync(
        ConnectionRequest request)
    {
        // Délégation au service spécialisé
        return await _connectionService.CreateConnectionAsync(request);
    }
    
    // Autres méthodes...
}
```

## Services métier (Business Logic)

### Principes de conception

⚠️ **CRITIQUE** : Les services métier doivent être **totalement indépendants** du protocole de communication

#### Ce qu'un service NE DOIT PAS faire :
- ❌ Référencer des types de transport (NamedPipe, TcpClient, Socket, Stream)
- ❌ Dépendre de StreamJsonRpc ou d'autres librairies RPC
- ❌ Accéder à Console.In ou Console.Out (seulement Console.Error pour logs)
- ❌ Connaître l'existence de clients ou de sessions

#### Ce qu'un service DOIT faire :
- ✅ Exposer des méthodes async avec paramètres et retours sérialisables
- ✅ Gérer sa propre logique métier de manière autonome
- ✅ Utiliser l'injection de dépendances via constructeur
- ✅ Logger sur stderr uniquement
- ✅ Retourner des résultats structurés (pas d'exceptions non catchées)

### Localisation
Tous les DTOs dans `Core/Models/`

### Conventions
- Classes publiques avec propriétés publiques
- **PascalCase** pour les noms de propriétés (conversion automatique en camelCase pour TypeScript)
- Attributs de sérialisation si nécessaire
- Nullable reference types pour propriétés optionnelles

```csharp
public class ConnectionRequest
{
    public string EnvironmentUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

public class ConnectionResult
{
    public bool Success { get; set; }
    public string? ConnectionId { get; set; }
    public string? Error { get; set; }
}
```

### Synchronisation avec TypeScript
Chaque DTO C# doit avoir une interface TypeScript correspondante dans `Extension/src/models/RpcModels.ts` :
- Propriétés identiques
- Types convertis (string → string, bool → boolean, etc.)
- CamelCase automatique côté TypeScript grâce au resolver

## Services métier

### DataverseConnectionService
Gère les connexions au Dataverse :
- Pool de connexions actives (Dictionary avec connectionId)
- Création de ServiceClient avec MSAL
- Tests de connexion
- Récupération des détails organisation
- WhoAmI
- Fermeture de connexions

#### Principes
- Utiliser `Microsoft.PowerPlatform.Dataverse.Client.ServiceClient`
- Authentification via OAuth avec `Microsoft.Identity.Client` (MSAL)
- Gestion des erreurs explicite avec try/catch
- Logs détaillés sur **stderr uniquement**

```csharp
public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
{
    try
    {
        Console.Error.WriteLine($"Creating connection to {request.EnvironmentUrl}");
        
        // Logique de connexion...
        
        return new ConnectionResult 
        { 
            Success = true, 
            ConnectionId = connectionId 
        };
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Connection failed: {ex.Message}");
        return new ConnectionResult 
        { 
            Success = false, 
            Error = ex.Message 
        };
    }
}
```

### DataverseAuthService
Gère l'authentification :
- Acquisition de tokens OAuth
- Refresh des tokens
- Gestion du cache des tokens
- Support des différents flows (device code, interactive, etc.)

## Gestion des erreurs

### Stratégie
1. **Try/catch au niveau service** : Capturer toutes les exceptions
2. **Retourner des résultats structurés** : Ne pas laisser les exceptions remonter au RPC layer
3. **Logger les erreurs sur stderr** : Pour le débogage
4. **Messages utilisateur clairs** : Dans les propriétés Error des résultats

```csharp
try
{
    // Opération Dataverse
}
catch (DataverseConnectionException ex)
{
    Console.Error.WriteLine($"Dataverse error: {ex.Message}");
    return new Result { Success = false, Error = "Failed to connect to Dataverse" };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex}");
    return new Result { Success = false, Error = "An unexpected error occurred" };
}
```

### Exceptions fatales
Si une erreur empêche le démarrage du serveur :
```csharp
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex}");
    Environment.Exit(1);
}
```

## Build et publication

### Structure des packages

#### 1. NuGet Package Runtime (`DataverseMCPToolBox.Runtime`)
Contient Core Server + MCP Bridge pour toutes les plateformes.

**Configuration .csproj** :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifiers>osx-arm64;osx-x64;win-x64;linux-x64</RuntimeIdentifiers>
  </PropertyGroup>
</Project>
```

**Plateformes supportées** :
- `osx-arm64` (Apple Silicon)
- `osx-x64` (Intel Mac)
- `win-x64` (Windows 64-bit)
- `linux-x64` (Linux 64-bit)

#### 2. NuGet Package Extensibility (`DataverseMCPToolBox.Extensibility`)
SDK pour créer des plugins custom.

**Contenu** :
- Interfaces : `IPlugin`, `IMcpTool`, `IDataverseContext`
- Classes de base : `PluginBase`, `McpToolBase`
- Attributs : `[McpPlugin]`, `[McpTool]`
- Helpers : `SchemaGenerator`

### Scripts de build

#### Build global (depuis racine)
```bash
# macOS/Linux
./scripts/build-all.sh

# Windows PowerShell
.\scripts\build-all.ps1
```

Ces scripts :
1. Buildent Core Server pour les 4 plateformes
2. Buildent MCP Bridge pour les 4 plateformes
3. Génèrent les binaires dans `Core/publish/<platform>/` et `Bridge/publish/<platform>/`

#### Packaging NuGet
```bash
# Depuis Core/
./scripts/pack-nuget.sh      # ou pack-nuget.ps1

# Résultat : Core/nupkg/DataverseMCPToolBox.Runtime.x.y.z.nupkg
```

#### Publication NuGet
```bash
dotnet nuget push Core/nupkg/DataverseMCPToolBox.Runtime.*.nupkg \
  --api-key <key> \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push Extensibility/nupkg/DataverseMCPToolBox.Extensibility.*.nupkg \
  --api-key <key> \
  --source https://api.nuget.org/v3/index.json
```

### Développement local

#### Script de copy locale
Pour tester sans publier sur NuGet :

```bash
# Depuis racine
./scripts/install-local.sh      # ou install-local.ps1

# Ce script :
# 1. Build Core + Bridge
# 2. Crée package NuGet local
# 3. Copie les binaires vers Extension/server/ pour debug
# 4. Rend exécutables (Unix)
```

**Usage développement** :
1. Modifier code dans Core ou Bridge
2. Lancer `./scripts/install-local.sh`
3. F5 dans VS Code Extension Development Host
4. Les binaires locaux sont utilisés automatiquement

### Multi-plateforme

#### Détection de plateforme runtime
```csharp
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

#### Chemins de fichiers cross-platform
```csharp
// ✅ BON - Path.Combine est cross-platform
var path = Path.Combine(baseDir, "subfolder", "file.txt");

// ❌ MAUVAIS - Hardcoded separators
var path = baseDir + "\\subfolder\\file.txt"; // Échoue sur Unix
```

### Checklist avant publication

#### Core + Bridge
- [ ] Build sans warnings sur toutes les plateformes
- [ ] Tester sur au moins 2 plateformes (Windows + macOS ou Linux)
- [ ] Vérifier qu'aucun `Console.WriteLine` ne pollue stdout (Core Server)
- [ ] Logs stderr clairs et structurés
- [ ] Tester authentification Dataverse
- [ ] Vérifier gestion des erreurs
- [ ] Taille binaires < 50MB par plateforme
- [ ] Version cohérente dans .csproj

#### Extensibility SDK
- [ ] Build sans warnings
- [ ] Documentation XML complète
- [ ] Exemples de plugins testés
- [ ] Version cohérente avec Runtime

## Tests

### Tests unitaires
- Créer des tests pour les services métier
- Mocker les dépendances externes (ServiceClient, MSAL)
- Ne pas tester le layer RPC (testé end-to-end)

### Tests d'intégration
- Tester avec un vrai environnement Dataverse (dev)
- Vérifier l'authentification complète
- Tester la reconnexion après expiration du token

## Dépendances NuGet

### Packages requis
```xml
<PackageReference Include="StreamJsonRpc" Version="2.19.27" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Microsoft.PowerPlatform.Dataverse.Client" Version="1.1.32" />
<PackageReference Include="Microsoft.Identity.Client" Version="4.61.3" />
```

### Mises à jour
- Vérifier la compatibilité avant de mettre à jour StreamJsonRpc
- Tester après chaque mise à jour de Dataverse.Client SDK
- Suivre les breaking changes de MSAL

## Patterns et anti-patterns

### ✅ À FAIRE
- Toujours async/await pour les opérations I/O
- Utiliser CancellationToken si opérations longues
- Disposer correctement des ServiceClient
- Logs structurés avec contexte (connectionId, etc.)
- Validation des paramètres d'entrée
- Retourner des objets de résultat structurés

### ❌ À ÉVITER
- Console.WriteLine() ou Console.Out
- Opérations synchrones bloquantes
- Exceptions non catchées qui crashent le serveur
- Logs excessifs (ralentissent le serveur)
- Connexions Dataverse non fermées
- Tokens non rafraîchis
- Dépendances externes non gérées

## Sécurité

### Secrets et tokens
- Ne JAMAIS logger les tokens d'accès
- Ne JAMAIS logger les secrets clients
- Utiliser MSAL pour la gestion sécurisée des tokens
- Le cache MSAL doit être protégé (filesystem avec permissions)

```csharp
// ❌ DANGER
Console.Error.WriteLine($"Access token: {token}");

// ✅ CORRECT
Console.Error.WriteLine("Access token acquired successfully");
```

### Validation des entrées
- Valider les URLs d'environnement
- Valider les GUIDs (clientId, tenantId)
- Sanitizer les messages d'erreur retournés au client

## Performance

### Optimisations
- Pool de connexions pour éviter les reconnexions
- Cache des métadonnées Dataverse si possible
- Opérations asynchrones parallèles quand applicable
- Limiter la taille des réponses JSON

### Monitoring
- Logger les durées des opérations importantes
- Tracker le nombre de connexions actives
- Surveiller l'utilisation mémoire (surtout avec pool de connexions)

## Compatibilité

### Versions .NET
- Target .NET 8.0 minimum
- Compatible avec .NET 10.0

### Versions Dataverse SDK
- Utiliser la dernière version stable du SDK
- Tester avec les API Dataverse v9.x

### OS Support
- Windows 10+ (x64)
- macOS 11+ (x64 et ARM64)
- Linux (x64, distributions récentes)

## Débogage

### Logs stderr
Structure des logs pour faciliter le débogage :
```csharp
Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{context}] {message}");
```

### Diagnostic
- Activer Trace pour diagnostics détaillés
- Utiliser des correlation IDs pour tracer les requêtes
- Logger le début et la fin des opérations longues

### Problèmes courants
1. **Serveur ne démarre pas** → Vérifier les dépendances .NET installées
2. **Connection timeout** → Vérifier l'URL et les credentials Dataverse
3. **JSON parse errors** → Chercher Console.WriteLine() polluant stdout
4. **Memory leaks** → Vérifier que les ServiceClient sont disposés

## Documentation du code

### Commentaires XML
```csharp
/// <summary>
/// Description de la méthode
/// </summary>
/// <param name="request">Description du paramètre</param>
/// <returns>Description du retour</returns>
public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
```

### README par composant
- Documenter chaque service majeur
- Expliquer les flows d'authentification
- Donner des exemples d'utilisation
