# Instructions Globales - Dataverse MCP Toolbox

## Vue d'ensemble du projet

Le **Dataverse MCP Toolbox** est un projet composé de deux parties principales :
1. **Core** : Un serveur .NET qui communique avec Dataverse via JSON-RPC
2. **Extension** : Une extension VS Code en TypeScript qui permet la gestion des connexions Dataverse

## Architecture JSON-RPC

### Principe fondamental
- Le serveur .NET écoute sur **stdin/stdout** pour les messages JSON-RPC
- **stderr** est réservé exclusivement aux logs et traces de débogage
- **JAMAIS** écrire sur stdout sauf pour les messages JSON-RPC formatés
- L'extension TypeScript communique via `child_process` avec le serveur .NET

### Format des messages
- Messages délimités par des en-têtes `Content-Length` (format standard JSON-RPC)
- Encodage **UTF-8** obligatoire
- Sérialisation JSON avec **camelCase** pour la compatibilité TypeScript/C#

### Règle critique pour stdout/stderr
⚠️ **RÈGLE ABSOLUE** : Ne jamais mélanger logs et JSON-RPC sur stdout
- ✅ `Console.Error.WriteLine()` pour tous les logs en C#
- ✅ `console.error()` pour tous les logs en TypeScript
- ✅ `Trace.Listeners.Add(new TextWriterTraceListener(Console.Error))` en C#
- ❌ `Console.WriteLine()` n'est utilisé QUE par StreamJsonRpc
- ❌ `console.log()` sur stdout côté TypeScript peut corrompre JSON-RPC

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
- **Serveur .NET** : pas de version explicite (lié à la version de l'extension)
- **Extension VS Code** : `package.json` > `version` (format semver : `x.y.z`)
- Maintenir la cohérence entre Core et Extension

### Publication du serveur .NET
1. Utiliser les scripts de build dans `Core/scripts/`
   - Linux/macOS : `./build-publish.sh`
   - Windows : `.\build-publish.ps1`
2. Générer des binaires self-contained pour toutes les plateformes :
   - `osx-arm64` (Apple Silicon)
   - `osx-x64` (Intel Mac)
   - `win-x64` (Windows 64-bit)
   - `linux-x64` (Linux 64-bit)
3. Output dans `Core/publish/<platform>/`

### Publication de l'extension VS Code
1. Inclure les binaires .NET dans le package VSIX
2. Mise à jour du `version` dans `package.json`
3. Build : `npm run compile`
4. Package : `vsce package`
5. Publication : `vsce publish` ou marketplace manuelle

### Checklist avant release
- [ ] Tester toutes les commandes JSON-RPC
- [ ] Vérifier que stderr ne contient que des logs
- [ ] Tester sur les 4 plateformes
- [ ] Mettre à jour la version dans package.json
- [ ] Rebuild tous les binaires .NET
- [ ] Tester l'extension complète end-to-end
- [ ] Documenter les breaking changes

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

## Bonnes pratiques JSON-RPC

### Côté Serveur (.NET)
```csharp
// ✅ BON - Logs sur stderr
Console.Error.WriteLine("Message de log");
Trace.WriteLine("Debug info"); // Redirigé vers stderr

// ❌ MAUVAIS - Pollue stdout
Console.WriteLine("Log message"); // Corrompt JSON-RPC!
```

### Côté Client (TypeScript)
```typescript
// ✅ BON - Logs sur stderr/console
console.error('[Extension] Log message');

// ❌ MAUVAIS - stdout est réservé
console.log('Message'); // Si redirigé vers process.stdout
```

### Méthodes RPC
- Toutes les méthodes doivent être async (`Task` ou `Task<T>`)
- Noms cohérents entre interface et implémentation
- Paramètres et retours sérialisables en JSON
- Utiliser des DTOs (Data Transfer Objects) pour les structures complexes

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
- **"Connection closed"** : Le serveur a crashé → vérifier stderr
- **"Request timed out"** : Le serveur ne répond pas → vérifier qu'il écoute
- **"Parse error"** : stdout pollué → chercher Console.WriteLine illégaux
- **"Method not found"** : Nom de méthode ne correspond pas entre client/serveur

## Bugs connus à ne pas reproduire

### 🐛 Pollution stdout avec des logs
**Problème** : Utilisation de `Console.WriteLine()` pour logger → corrompt JSON-RPC
**Solution** : Toujours utiliser `Console.Error.WriteLine()`

### 🐛 Encodage des caractères
**Problème** : Caractères spéciaux mal encodés entre .NET et TypeScript
**Solution** : Spécifier UTF-8 explicitement dans StreamMessageReader/Writer

### 🐛 Méthodes synchrones qui bloquent
**Problème** : Méthodes RPC synchrones qui causent des deadlocks
**Solution** : Toutes les méthodes RPC doivent être async (Task)

### 🐛 Exceptions non catchées
**Problème** : Exception dans le serveur RPC → crash silencieux
**Solution** : Try/catch au niveau le plus haut + log stderr

### 🐛 Process zombie après fermeture extension
**Problème** : Le processus .NET continue de tourner après fermeture VS Code
**Solution** : Implémenter `dispose()` et `deactivate()` correctement

## Structure des répertoires

```
/Core                    → Serveur .NET
  /JsonRpc              → Services RPC
  /Models               → DTOs partagés
  /Services             → Logique métier Dataverse
  /scripts              → Scripts de build
  /publish              → Binaires générés
/Extension              → Extension VS Code
  /src
    /commands           → Commandes VS Code
    /models             → Interfaces TypeScript
    /panels             → Panels WebView
    /providers          → Data providers
    /services           → Services (RPC client, storage)
```

## Ressources

- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [StreamJsonRpc Documentation](https://github.com/microsoft/vs-streamjsonrpc)
- [VS Code Extension API](https://code.visualstudio.com/api)
- [Dataverse SDK Documentation](https://learn.microsoft.com/power-apps/developer/data-platform/)
