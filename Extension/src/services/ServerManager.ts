import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as https from 'https';
import AdmZip = require('adm-zip');
import { UpdateCheckResult, ServerVersionInfo } from '../models/ServerVersionInfo';
import type { DataverseMCPToolBoxRpcClient } from './DataverseMCPToolBoxRpcClient';

/**
 * Manages MCP server binary lifecycle: download, installation, and updates
 * Note: This class does NOT start the server - that's handled by GitHub Copilot via MCP configuration
 */
export class ServerManager {
    private static readonly PACKAGE_ID = 'DataverseMCPToolBox.Runtime';
    private readonly context: vscode.ExtensionContext;
    private readonly outputChannel: vscode.OutputChannel;
    private rpcClient: DataverseMCPToolBoxRpcClient | null = null;

    constructor(context: vscode.ExtensionContext) {
        this.context = context;
        this.outputChannel = vscode.window.createOutputChannel('Dataverse MCP Server');
    }

    /**
     * Set the RPC client for querying running server information
     */
    setRpcClient(client: DataverseMCPToolBoxRpcClient): void {
        this.rpcClient = client;
    }

    /**
     * Check if running in local development mode with bundled binaries
     * Local dev binaries are in Extension/server/binaries/ (copied by install-local scripts)
     */
    private checkLocalDevBinaries(): string | null {
        try {
            const platform = this.getPlatform();
            const executableName = platform.startsWith('win') ? 'DataverseMCPToolBox.exe' : 'DataverseMCPToolBox';
            
            // Check Extension/server/binaries/runtimes/<platform>/native/
            const localBinaryPath = path.join(
                this.context.extensionPath,
                'server',
                'binaries',
                'runtimes',
                platform,
                'native',
                executableName
            );
            
            if (fs.existsSync(localBinaryPath)) {
                this.outputChannel.appendLine(`[Local Dev] Found local binary: ${localBinaryPath}`);
                
                // Verify Bridge is also present
                const bridgeName = platform.startsWith('win') 
                    ? 'DataverseMCPToolBox.Bridge.exe' 
                    : 'DataverseMCPToolBox.Bridge';
                const bridgePath = path.join(path.dirname(localBinaryPath), bridgeName);
                
                if (fs.existsSync(bridgePath)) {
                    this.outputChannel.appendLine(`[Local Dev] Found Bridge: ${bridgePath}`);
                    return localBinaryPath;
                } else {
                    this.outputChannel.appendLine(`[Local Dev] Warning: Core found but Bridge missing at ${bridgePath}`);
                    this.outputChannel.appendLine(`[Local Dev] Run ./scripts/install-local.sh to install both binaries`);
                }
            }
        } catch (error) {
            // Silently fail - just means we're not in local dev mode
            this.outputChannel.appendLine(`[Local Dev] Check skipped: ${error}`);
        }
        
        return null;
    }

    /**
     * Ensure server is installed, download if necessary
     */
    async ensureServerInstalled(): Promise<string> {
        // Check for local development binaries FIRST (F5 debugging)
        const localBinary = this.checkLocalDevBinaries();
        if (localBinary) {
            this.outputChannel.appendLine('[Local Dev Mode] Using local binaries from Extension/server/binaries/');
            return localBinary;
        }

        // Production mode: download from NuGet
        const config = vscode.workspace.getConfiguration('dataverse.server');
        const enforcedVersion = config.get<string>('enforcedVersion') || '';

        let requiredVersion: string;

        if (enforcedVersion) {
            // User has enforced a specific version
            requiredVersion = enforcedVersion;
            this.outputChannel.appendLine(`Using enforced server version: v${requiredVersion}`);
        } else {
            // Get the latest version from NuGet based on channel
            this.outputChannel.appendLine(`Fetching latest server version from NuGet...`);
            requiredVersion = await this.getLatestVersionFromNuGet();
            this.outputChannel.appendLine(`Latest version available: v${requiredVersion}`);
        }

        this.outputChannel.appendLine(`Checking for server installation (required: v${requiredVersion})...`);

        // Check if already installed
        const installedVersion = this.getInstalledVersion();
        if (installedVersion === requiredVersion) {
            const serverPath = this.getServerExecutablePath(requiredVersion);
            if (fs.existsSync(serverPath)) {
                this.outputChannel.appendLine(`Server v${installedVersion} already installed`);
                return serverPath;
            }
        }

        // Download and install
        this.outputChannel.appendLine(`Downloading server v${requiredVersion} from NuGet...`);
        await this.downloadAndInstallServer(requiredVersion);

        return this.getServerExecutablePath(requiredVersion);
    }

    /**
     * Check for available server updates
     * Compares installed binary version with NuGet and running server version
     */
    async checkForUpdates(): Promise<UpdateCheckResult> {
        // Throttle update checks to once per day
        const lastCheck = this.context.globalState.get<number>('lastUpdateCheck', 0);
        const oneDayMs = 24 * 60 * 60 * 1000;
        const now = Date.now();

        const installedVersion = this.getInstalledVersion();

        if (now - lastCheck < oneDayMs) {
            this.outputChannel.appendLine('Skipping update check (checked recently)');
            return {
                currentVersion: installedVersion,
                latestVersion: installedVersion || 'unknown',
                updateAvailable: false
            };
        }

        this.outputChannel.appendLine('Checking for server updates...');

        try {
            const latestVersion = await this.getLatestVersionFromNuGet();
            await this.context.globalState.update('lastUpdateCheck', now);

            // Get running server version if connected
            let runningServerVersion: string | undefined;
            if (this.rpcClient) {
                try {
                    const serverInfo = await this.rpcClient.getServerVersion();
                    runningServerVersion = serverInfo.version;
                    this.outputChannel.appendLine(`Running server version: v${runningServerVersion}`);
                } catch (error) {
                    this.outputChannel.appendLine('Could not query running server version (server may not be running)');
                }
            }

            const updateAvailable = installedVersion !== null && this.compareVersions(latestVersion, installedVersion) > 0;

            this.outputChannel.appendLine(`Installed: v${installedVersion || 'none'}, Latest: v${latestVersion}, Update available: ${updateAvailable}`);

            return {
                currentVersion: installedVersion,
                latestVersion,
                updateAvailable,
                runningServerVersion
            };
        } catch (error) {
            console.error('Failed to check for updates:', error);
            this.outputChannel.appendLine(`Update check failed: ${error}`);
            return {
                currentVersion: installedVersion,
                latestVersion: installedVersion || 'unknown',
                updateAvailable: false
            };
        }
    }

    /**
     * Upgrade server to latest version
     * Attempts graceful shutdown of running server if RPC client is available
     */
    async upgradeServer(): Promise<string> {
        this.outputChannel.appendLine('Upgrading server...');

        // Attempt graceful shutdown of running server
        if (this.rpcClient) {
            try {
                this.outputChannel.appendLine('Requesting graceful server shutdown...');
                await this.rpcClient.shutdownServer();
                this.outputChannel.appendLine('Server shutdown initiated');
                
                // Wait a moment for shutdown to complete
                await new Promise(resolve => setTimeout(resolve, 1000));
            } catch (error) {
                this.outputChannel.appendLine(`Note: Could not shutdown running server: ${error}`);
                this.outputChannel.appendLine('Proceeding with upgrade (server may need manual restart)');
            }
        }

        const config = vscode.workspace.getConfiguration('dataverse.server');
        const enforcedVersion = config.get<string>('enforcedVersion') || '';

        let targetVersion: string;

        if (enforcedVersion) {
            // User has enforced a specific version
            targetVersion = enforcedVersion;
            this.outputChannel.appendLine(`Upgrading to enforced version: v${targetVersion}`);
        } else {
            // Get the latest version from NuGet
            targetVersion = await this.getLatestVersionFromNuGet();
            this.outputChannel.appendLine(`Upgrading to latest version: v${targetVersion}`);
        }

        await this.downloadAndInstallServer(targetVersion);

        this.outputChannel.appendLine(`Successfully upgraded to v${targetVersion}`);

        return this.getServerExecutablePath(targetVersion);
    }

    /**
     * Get installed server version
     */
    private getInstalledVersion(): string | null {
        const serverDir = path.join(this.context.globalStoragePath, 'server');
        if (!fs.existsSync(serverDir)) {
            return null;
        }

        const versions = fs.readdirSync(serverDir).filter(name => {
            const versionPath = path.join(serverDir, name);
            return fs.statSync(versionPath).isDirectory();
        });

        // Return the most recent version
        if (versions.length === 0) {
            return null;
        }

        versions.sort((a, b) => this.compareVersions(b, a));
        return versions[0];
    }

    /**
     * Get path to server executable for installed version
     * Returns the path to the Core Server executable
     */
    getServerExecutablePath(version?: string): string {
        const actualVersion = version || this.getInstalledVersion();
        if (!actualVersion) {
            throw new Error('No server version installed');
        }
        
        const platform = this.getPlatform();
        const executableName = platform.startsWith('win') ? 'DataverseMCPToolBox.exe' : 'DataverseMCPToolBox';
        return path.join(this.context.globalStoragePath, 'server', actualVersion, platform, executableName);
    }

    /**
     * Download and install server from NuGet
     */
    private async downloadAndInstallServer(version: string): Promise<void> {
        // Download .nupkg file
        const nupkgBuffer = await this.downloadNuGetPackage(version);

        // Extract to temp directory
        const tempDir = path.join(this.context.globalStoragePath, 'temp', `server-${version}`);
        if (fs.existsSync(tempDir)) {
            fs.rmSync(tempDir, { recursive: true });
        }
        fs.mkdirSync(tempDir, { recursive: true });

        this.outputChannel.appendLine(`Extracting package to ${tempDir}...`);

        const zip = new AdmZip(nupkgBuffer);
        zip.extractAllTo(tempDir, true);

        // Copy ALL platform-specific binaries to final location (Core + Bridge)
        const platform = this.getPlatform();
        const sourceDir = path.join(tempDir, 'runtimes', platform, 'native');
        const targetDir = path.join(this.context.globalStoragePath, 'server', version, platform);

        if (!fs.existsSync(sourceDir)) {
            throw new Error(`Platform binaries not found in package: ${sourceDir}`);
        }

        fs.mkdirSync(targetDir, { recursive: true });

        // Copy ALL files from native directory (Core Server + Bridge)
        const files = fs.readdirSync(sourceDir);
        for (const file of files) {
            const sourcePath = path.join(sourceDir, file);
            const targetPath = path.join(targetDir, file);
            fs.copyFileSync(sourcePath, targetPath);
            this.outputChannel.appendLine(`  Copied: ${file}`);
        }

        // Set executable permissions on Unix for ALL binaries
        if (!platform.startsWith('win')) {
            for (const file of files) {
                const targetPath = path.join(targetDir, file);
                if (fs.statSync(targetPath).isFile()) {
                    fs.chmodSync(targetPath, 0o755);
                }
            }
        }

        // Clean up temp directory
        fs.rmSync(tempDir, { recursive: true });

        // Clean up old versions
        await this.cleanupOldVersions(version);

        this.outputChannel.appendLine(`Server v${version} installed successfully`);
    }

    /**
     * Download NuGet package
     */
    private async downloadNuGetPackage(version: string): Promise<Buffer> {
        const url = `https://api.nuget.org/v3-flatcontainer/${ServerManager.PACKAGE_ID.toLowerCase()}/${version}/${ServerManager.PACKAGE_ID.toLowerCase()}.${version}.nupkg`;
        this.outputChannel.appendLine(`Downloading from ${url}...`);

        return new Promise<Buffer>((resolve, reject) => {
            https.get(url, (response) => {
                if (response.statusCode === 302 || response.statusCode === 301) {
                    // Handle redirect
                    if (response.headers.location) {
                        https.get(response.headers.location, (redirectResponse) => {
                            const chunks: Buffer[] = [];
                            redirectResponse.on('data', (chunk) => chunks.push(chunk));
                            redirectResponse.on('end', () => resolve(Buffer.concat(chunks)));
                            redirectResponse.on('error', reject);
                        }).on('error', reject);
                    } else {
                        reject(new Error('Redirect without location header'));
                    }
                } else if (response.statusCode === 200) {
                    const chunks: Buffer[] = [];
                    response.on('data', (chunk) => chunks.push(chunk));
                    response.on('end', () => resolve(Buffer.concat(chunks)));
                    response.on('error', reject);
                } else {
                    reject(new Error(`Failed to download package: HTTP ${response.statusCode}`));
                }
            }).on('error', reject);
        });
    }

    /**
     * Get latest version from NuGet API
     */
    private async getLatestVersionFromNuGet(): Promise<string> {
        const config = vscode.workspace.getConfiguration('dataverse.server');
        const channel = config.get<string>('channel') || 'stable';
        const includePrerelease = channel === 'prerelease';

        const url = `https://api.nuget.org/v3-flatcontainer/${ServerManager.PACKAGE_ID.toLowerCase()}/index.json`;

        this.outputChannel.appendLine(`Fetching versions from ${url}...`);

        return new Promise<string>((resolve, reject) => {
            https.get(url, (response) => {
                let data = '';
                response.on('data', (chunk) => data += chunk);
                response.on('end', () => {
                    try {
                        const json = JSON.parse(data);
                        const versions = json.versions as string[];

                        // Filter out prerelease if needed
                        const filteredVersions = includePrerelease 
                            ? versions 
                            : versions.filter(v => !v.includes('-'));

                        if (filteredVersions.length === 0) {
                            reject(new Error('No versions found'));
                            return;
                        }

                        // Sort and get latest
                        filteredVersions.sort((a, b) => this.compareVersions(b, a));
                        resolve(filteredVersions[0]);
                    } catch (error) {
                        reject(error);
                    }
                });
                response.on('error', reject);
            }).on('error', reject);
        });
    }

    /**
     * Compare semantic versions (returns >0 if a > b, <0 if a < b, 0 if equal)
     */
    private compareVersions(a: string, b: string): number {
        // Remove 'v' prefix if present
        a = a.replace(/^v/, '');
        b = b.replace(/^v/, '');

        const parseVersion = (version: string) => {
            const [mainVersion, prerelease] = version.split('-');
            const parts = mainVersion.split('.').map(x => parseInt(x, 10));
            return { parts, prerelease };
        };

        const vA = parseVersion(a);
        const vB = parseVersion(b);

        // Compare main version parts
        for (let i = 0; i < Math.max(vA.parts.length, vB.parts.length); i++) {
            const partA = vA.parts[i] || 0;
            const partB = vB.parts[i] || 0;
            if (partA !== partB) {
                return partA - partB;
            }
        }

        // If main versions are equal, stable > prerelease
        if (!vA.prerelease && vB.prerelease) {
            return 1;
        }
        if (vA.prerelease && !vB.prerelease) {
            return -1;
        }

        // Both prerelease or both stable
        if (vA.prerelease && vB.prerelease) {
            return vA.prerelease.localeCompare(vB.prerelease);
        }

        return 0;
    }

    /**
     * Get current platform identifier
     */
    private getPlatform(): string {
        const platform = process.platform;
        const arch = process.arch;

        if (platform === 'darwin') {
            return arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
        } else if (platform === 'win32') {
            return 'win-x64';
        } else if (platform === 'linux') {
            return 'linux-x64';
        }

        throw new Error(`Unsupported platform: ${platform}-${arch}`);
    }

    /**
     * Clean up old server versions, keeping only the current one
     */
    private async cleanupOldVersions(currentVersion: string): Promise<void> {
        const serverDir = path.join(this.context.globalStoragePath, 'server');
        if (!fs.existsSync(serverDir)) {
            return;
        }

        const versions = fs.readdirSync(serverDir);
        for (const version of versions) {
            if (version !== currentVersion) {
                const versionPath = path.join(serverDir, version);
                this.outputChannel.appendLine(`Cleaning up old version: ${version}`);
                fs.rmSync(versionPath, { recursive: true, force: true });
            }
        }
    }

    /**
     * Dispose resources
     */
    dispose(): void {
        this.outputChannel.dispose();
    }
}
