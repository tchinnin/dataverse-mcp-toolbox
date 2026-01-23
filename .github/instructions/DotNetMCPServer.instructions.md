---
applyTo: "Core/**"
---

# Instructions Serveur .NET Core - Dataverse MCP Toolbox

## Principes architecturaux

### Communication JSON-RPC via stdio
Le serveur .NET est un processus autonome qui communique exclusivement via **stdin/stdout** avec le client TypeScript.

#### Règles critiques pour stdio
⚠️ **ABSOLUMENT IMPÉRATIF** ⚠️
- **stdout** est réservé EXCLUSIVEMENT aux messages JSON-RPC (HeaderDelimitedMessageHandler)
- **stderr** est le SEUL canal autorisé pour les logs, traces, et messages de débogage
- **stdin** reçoit les requêtes JSON-RPC du client

```csharp
// ✅ CORRECT - Logging
Console.Error.WriteLine("Server starting...");
Trace.WriteLine("Debug info"); // Si redirigé vers stderr

// ❌ INTERDIT - Corrompt le flux JSON-RPC
Console.WriteLine("Log message"); // NE JAMAIS FAIRE ÇA
Console.Out.WriteLine("Info"); // NE JAMAIS FAIRE ÇA
```

### Configuration du Trace Listener
Au démarrage dans `Program.cs`, toujours rediriger les traces vers stderr :

```csharp
Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
Trace.AutoFlush = true;
```

## Structure du serveur JSON-RPC

### Program.cs - Point d'entrée
Le `Main` doit :
1. Rediriger tous les logs vers stderr
2. Créer le service RPC
3. Configurer le formatter JSON avec CamelCase
4. Créer le HeaderDelimitedMessageHandler sur stdin/stdout
5. Démarrer l'écoute JSON-RPC
6. Attendre la fin de la connexion
7. Gérer les exceptions fatales avec exit code 1

```csharp
var formatter = new JsonMessageFormatter
{
    JsonSerializer = 
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    }
};

var messageHandler = new HeaderDelimitedMessageHandler(
    Console.OpenStandardOutput(), 
    Console.OpenStandardInput(), 
    formatter
);
```

### Interface RPC (IDataverseMCPToolBoxRpcService)
- Définit le contrat RPC partagé entre serveur et client
- **Toutes les méthodes doivent retourner Task ou Task<T>**
- Noms de méthodes en PascalCase avec suffixe `Async`
- Paramètres sérialisables en JSON uniquement

```csharp
public interface IDataverseMCPToolBoxRpcService
{
    Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request);
    Task<bool> TestConnectionAsync(string connectionId);
    Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId);
    Task<WhoAmIResult> GetWhoAmIAsync(string connectionId);
    Task CloseConnectionAsync(string connectionId);
    Task CloseAllConnectionsAsync();
}
```

### Implémentation RPC (DataverseMCPToolBoxRpcService)
- Implémente l'interface RPC
- Délègue la logique métier aux services (`DataverseConnectionService`, etc.)
- Gère la conversion des exceptions en résultats
- **NE PAS logger sur stdout**, utiliser stderr si besoin

```csharp
public class DataverseMCPToolBoxRpcService : IDataverseMCPToolBoxRpcService
{
    private readonly DataverseConnectionService _connectionService;
    
    // Injection de dépendances ou instanciation des services
    public DataverseMCPToolBoxRpcService()
    {
        _connectionService = new DataverseConnectionService();
    }
    
    // Implémentation des méthodes...
}
```

## Modèles de données (DTOs)

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

### Projet .csproj
- Target framework : `net8.0` ou supérieur
- RuntimeIdentifiers pour cross-platform :
  - `osx-arm64` (Apple Silicon)
  - `osx-x64` (Intel Mac)
  - `win-x64` (Windows)
  - `linux-x64` (Linux)
- PublishSingleFile : true (exécutable unique)
- SelfContained : true (inclut le runtime .NET)

### Scripts de build
Utiliser les scripts dans `Core/scripts/` :

```bash
# macOS/Linux
./build-publish.sh

# Windows
.\build-publish.ps1
```

Ces scripts créent des binaires self-contained pour toutes les plateformes dans `Core/publish/<runtime>/`

### Commande manuelle
```bash
dotnet publish -c Release -r <runtime> -o ./publish/<runtime> --self-contained
```

### Checklist avant publication
- [ ] Compiler sans warnings
- [ ] Tester chaque méthode RPC individuellement
- [ ] Vérifier qu'aucun `Console.WriteLine` ne pollue stdout
- [ ] Vérifier les logs stderr sont clairs et utiles
- [ ] Tester l'authentification Dataverse
- [ ] Vérifier la gestion des erreurs
- [ ] Tester sur les 4 plateformes cibles
- [ ] Vérifier la taille des binaires (optimisation si nécessaire)

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
