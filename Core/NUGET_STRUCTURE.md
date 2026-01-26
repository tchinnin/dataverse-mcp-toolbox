# NuGet Package Structure Requirements

## Overview

The VS Code extension's `ServerManager.ts` expects the `DataverseMCPToolBox.Server` NuGet package to follow a specific directory structure for platform-specific binaries.

## Required Structure

```
DataverseMCPToolBox.Server.<version>.nupkg
├── README.md                                    (Package documentation)
├── runtimes/
│   ├── osx-arm64/
│   │   └── native/
│   │       └── DataverseMCPToolBox             (macOS Apple Silicon executable)
│   ├── osx-x64/
│   │   └── native/
│   │       └── DataverseMCPToolBox             (macOS Intel executable)
│   ├── win-x64/
│   │   └── native/
│   │       └── DataverseMCPToolBox.exe         (Windows executable)
│   └── linux-x64/
│       └── native/
│           └── DataverseMCPToolBox             (Linux executable)
└── [NuGet metadata files]
```

## Why This Structure?

The VS Code extension's `ServerManager.ts` downloads and extracts the package with this logic:

```typescript
// From Extension/src/services/ServerManager.ts (lines ~176-220)
const sourcePath = path.join(
    tempDir,                  // Extraction directory
    'runtimes',              // Fixed path
    platform,                // 'osx-arm64', 'win-x64', etc.
    'native',                // Fixed path
    executableName           // 'DataverseMCPToolBox' or 'DataverseMCPToolBox.exe'
);
```

If the structure doesn't match, the extension will fail with:
```
Error: Server executable not found at expected location
```

## Implementation

This structure is achieved in `DataverseMCPToolBox.csproj`:

```xml
<!-- Include platform-specific binaries in NuGet package -->
<ItemGroup>
    <None Include="publish/osx-arm64/**" Pack="true" PackagePath="runtimes/osx-arm64/native/" />
    <None Include="publish/osx-x64/**" Pack="true" PackagePath="runtimes/osx-x64/native/" />
    <None Include="publish/win-x64/**" Pack="true" PackagePath="runtimes/win-x64/native/" />
    <None Include="publish/linux-x64/**" Pack="true" PackagePath="runtimes/linux-x64/native/" />
</ItemGroup>
```

The `build-publish.sh` script ensures that `publish/<platform>/` directories contain **only** the main executable (no .pdb, .xml, or other build artifacts).

## Verification

After building the package, verify the structure:

```bash
# Extract and list contents
unzip -l nupkg/DataverseMCPToolBox.Server.*.nupkg | grep runtimes

# Should show:
# runtimes/osx-arm64/native/DataverseMCPToolBox
# runtimes/osx-x64/native/DataverseMCPToolBox
# runtimes/win-x64/native/DataverseMCPToolBox.exe
# runtimes/linux-x64/native/DataverseMCPToolBox
```

## Platform Detection

The extension determines which binary to use based on:

```typescript
private getPlatform(): string {
    const platform = process.platform; // 'darwin', 'win32', 'linux'
    const arch = process.arch;         // 'arm64', 'x64'
    
    if (platform === 'darwin') {
        return arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
    } else if (platform === 'win32') {
        return 'win-x64';
    } else if (platform === 'linux') {
        return 'linux-x64';
    }
    throw new Error(`Unsupported platform: ${platform}-${arch}`);
}
```

## Binary Requirements

Each executable must be:

1. **Self-contained** - Includes all .NET runtime dependencies
2. **Single-file** - PublishSingleFile=true
3. **Executable** - Unix binaries have execute permission (chmod +x)
4. **Optimized** - Release build configuration
5. **Platform-specific** - Built for the target runtime identifier (RID)

## Build Process Summary

1. `build-publish.sh` → Creates platform-specific executables in `publish/<platform>/`
2. `pack-nuget.sh` → Packages executables into `.nupkg` with correct `runtimes/` structure
3. Publishing to NuGet.org makes the package available for extension download
4. Extension downloads package on first run or version change
5. Extension extracts to `globalStorage/server/<version>/<platform>/DataverseMCPToolBox`

## Troubleshooting

**Problem**: Extension fails with "Server executable not found"
- **Solution**: Verify NuGet package structure with `unzip -l` command

**Problem**: Downloaded file is not executable on Unix
- **Solution**: Build script must set execute permissions: `chmod +x`

**Problem**: Binary is too large (>100MB per platform)
- **Solution**: Ensure PublishSingleFile=true and only executable is packaged (no extra DLLs)

**Problem**: Extension downloads but fails to start server
- **Solution**: Check stderr logs in VS Code Output panel (DataverseMCPToolBox channel)
