# Build and Packaging Scripts

Scripts to build and publish the .NET Dataverse MCP ToolBox server for NuGet distribution.

## Available Scripts

### Build Scripts

#### `build-publish.sh` (macOS/Linux)
```bash
./build-publish.sh
```
Builds self-contained executables for all supported platforms. Creates optimized single-file executables in `../publish/<platform>/` directories.

**Output Structure:**
```
publish/
├── osx-arm64/DataverseMCPToolBox
├── osx-x64/DataverseMCPToolBox
├── win-x64/DataverseMCPToolBox.exe
└── linux-x64/DataverseMCPToolBox
```

#### `build-publish.ps1` (Windows)
```powershell
.\build-publish.ps1
```
PowerShell version of the build script for Windows environments.

### NuGet Packaging Scripts

#### `pack-nuget.sh` (macOS/Linux)
```bash
./pack-nuget.sh
```
Complete packaging workflow:
1. Builds all platform binaries using `build-publish.sh`
2. Creates NuGet package with correct `runtimes/<platform>/native/` structure
3. Outputs `.nupkg` file to `../nupkg/` directory
4. Verifies package structure

#### `pack-nuget.ps1` (Windows)
```powershell
.\pack-nuget.ps1
```
PowerShell version of the NuGet packaging script.

## Supported Platforms

- **macOS ARM64** (Apple Silicon) - `osx-arm64`
- **macOS x64** (Intel) - `osx-x64`
- **Windows x64** - `win-x64`
- **Linux x64** - `linux-x64`

## NuGet Package Structure

The generated `.nupkg` file contains:
```
DataverseMCPToolBox.Server.<version>.nupkg
├── README.md
├── runtimes/
│   ├── osx-arm64/native/DataverseMCPToolBox
│   ├── osx-x64/native/DataverseMCPToolBox
│   ├── win-x64/native/DataverseMCPToolBox.exe
│   └── linux-x64/native/DataverseMCPToolBox
└── [metadata files]
```

This structure is required by the VS Code extension's `ServerManager` which expects executables at `runtimes/<platform>/native/` paths.

## Publishing to NuGet.org

After running `pack-nuget.sh` or `pack-nuget.ps1`:

```bash
dotnet nuget push nupkg/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Local Testing

To test the NuGet package locally with the VS Code extension:

1. **Add local NuGet source:**
   ```bash
   dotnet nuget add source /path/to/Core/nupkg --name LocalDataverseMCP
   ```

2. **Configure extension settings** in VS Code:
   ```json
   {
     "dataverse.server.channel": "prerelease",
     "dataverse.server.enforcedVersion": "0.1.0-alpha"
   }
   ```

3. **Launch VS Code extension** - it will download from your local NuGet source

4. **Verify installation** at:
   - macOS: `~/Library/Application Support/Code/User/globalStorage/<extension-id>/server/`
   - Windows: `%APPDATA%\Code\User\globalStorage\<extension-id>\server\`
   - Linux: `~/.config/Code/User/globalStorage/<extension-id>/server/`

## Version Management

Update the version in [DataverseMCPToolBox.csproj](../DataverseMCPToolBox.csproj):
```xml
<Version>0.1.0-alpha</Version>
```

The extension automatically:
- Downloads the latest version from NuGet on first install
- Checks for updates daily
- Supports both `stable` and `prerelease` channels via settings
