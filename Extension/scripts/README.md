# Scripts pour le développement

## copy-local-server.sh

Copie les binaires compilés depuis `Core/publish/` vers le storage de développement de l'extension VS Code.

### Usage

```bash
# Copier avec la version par défaut (0.1.0-alpha)
./scripts/copy-local-server.sh

# Copier avec une version spécifique
./scripts/copy-local-server.sh 0.2.0
```

Ou via npm :

```bash
npm run copy-local-server
```

### Workflow de développement complet

```bash
# 1. Compiler le serveur .NET
cd ../Core
./scripts/build-publish.sh

# 2. Compiler et copier l'extension
cd ../Extension
npm run dev

# 3. Appuyer sur F5 pour déboguer
```

La commande `npm run dev` :
1. Compile le TypeScript
2. Copie les binaires du serveur local
3. L'extension utilisera votre version locale en debug

### Configuration

Le fichier `.vscode/settings.json` force l'utilisation de la version `0.1.0-alpha` pour éviter que l'extension télécharge depuis NuGet pendant le développement.
