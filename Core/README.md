# Dataverse MCP ToolBox

Bibliothèque .NET 8 pour gérer les connexions à Microsoft Dataverse via OAuth et PowerPlatform.Dataverse.Client.

## Fonctionnalités

### 1. Authentification OAuth Interactive
- Utilise le client ID de développement Dynamics: `51f81489-12ee-4a9e-aaae-a2591f45987d`
- Flux OAuth interactif avec ouverture automatique du navigateur
- Gestion du cache de tokens pour éviter les authentifications répétées
- Support multi-comptes

### 2. Gestion des connexions Dataverse
- Création de connexions authentifiées à des environnements Dataverse
- Stockage des connexions actives
- Test de validité des connexions
- Récupération des détails d'organisation

## Utilisation

### Créer une connexion

```csharp
using DataverseMCPToolBox.Services;
using DataverseMCPToolBox.Models;

var connectionService = new DataverseConnectionService();

var request = new ConnectionRequest
{
    EnvironmentUrl = "https://org.crm.dynamics.com",
    ConnectionName = "Ma connexion"
};

var result = await connectionService.CreateConnectionAsync(request);

if (result.Success)
{
    Console.WriteLine($"Connecté avec succès! ID: {result.ConnectionId}");
    Console.WriteLine($"Utilisateur: {result.UserName}");
}
else
{
    Console.WriteLine($"Erreur: {result.ErrorMessage}");
}
```

### Tester une connexion

```csharp
bool isValid = connectionService.TestConnection(result.ConnectionId);
```

### Récupérer les détails de l'organisation

```csharp
var orgDetails = await connectionService.GetOrganizationDetailsAsync(result.ConnectionId);
Console.WriteLine($"Organisation: {orgDetails.FriendlyName}");
```

### Fermer une connexion

```csharp
connectionService.CloseConnection(result.ConnectionId);
```

## Dépendances

- .NET 8.0
- Microsoft.PowerPlatform.Dataverse.Client
- Microsoft.Identity.Client (MSAL)

## Architecture

- **Models/**: Modèles de données (ConnectionRequest, ConnectionResult)
- **Services/**: 
  - `DataverseAuthService`: Gestion de l'authentification OAuth
  - `DataverseConnectionService`: Gestion des connexions Dataverse
