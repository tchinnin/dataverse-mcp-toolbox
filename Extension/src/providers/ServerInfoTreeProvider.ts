import * as vscode from 'vscode';
import { ServerManager } from '../services/ServerManager';
import { UpdateCheckResult } from '../models/ServerVersionInfo';

/**
 * Tree item types for server info panel
 */
type ServerInfoItem = VersionItem | ActionItem | SettingItem;

/**
 * Version information tree item
 */
class VersionItem extends vscode.TreeItem {
    constructor(
        public readonly label: string,
        public readonly version: string,
        public readonly itemType: 'installed' | 'latest'
    ) {
        super(label, vscode.TreeItemCollapsibleState.None);
        this.description = version;
        this.tooltip = `${label}: ${version}`;
        this.contextValue = itemType === 'installed' ? 'installedVersion' : 'latestVersion';
        
        if (itemType === 'installed') {
            this.iconPath = new vscode.ThemeIcon('check');
        } else {
            this.iconPath = new vscode.ThemeIcon('cloud-download');
        }
    }
}

/**
 * Action button tree item
 */
class ActionItem extends vscode.TreeItem {
    constructor(
        public readonly label: string,
        public readonly command: vscode.Command,
        public readonly icon: string,
        public readonly description?: string
    ) {
        super(label, vscode.TreeItemCollapsibleState.None);
        this.command = command;
        this.tooltip = description || label;
        this.description = description;
        this.contextValue = 'action';
        this.iconPath = new vscode.ThemeIcon(icon);
    }
}

/**
 * Settings display tree item
 */
class SettingItem extends vscode.TreeItem {
    constructor(
        public readonly label: string,
        public readonly value: string,
        public readonly settingKey: string,
        public readonly icon: string = 'settings-gear',
        command?: vscode.Command
    ) {
        super(label, vscode.TreeItemCollapsibleState.None);
        this.description = value;
        this.tooltip = command 
            ? `${label}: ${value} (click to change)` 
            : `${label}: ${value}`;
        this.contextValue = 'setting';
        this.iconPath = new vscode.ThemeIcon(icon);
        if (command) {
            this.command = command;
        }
    }
}

/**
 * TreeDataProvider for displaying MCP Server information
 */
export class ServerInfoTreeProvider implements vscode.TreeDataProvider<ServerInfoItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ServerInfoItem | undefined | null | void> = new vscode.EventEmitter<ServerInfoItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ServerInfoItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private versionInfo: UpdateCheckResult | null = null;
    private isCheckingUpdate: boolean = false;

    constructor(private serverManager: ServerManager) {}

    /**
     * Refresh the tree view
     */
    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    /**
     * Update version info and refresh
     */
    async updateVersionInfo(): Promise<void> {
        if (this.isCheckingUpdate) {
            return;
        }

        this.isCheckingUpdate = true;
        try {
            this.versionInfo = await this.serverManager.checkForUpdates();
            this.refresh();
        } catch (error) {
            console.error('Failed to check for updates:', error);
            vscode.window.showErrorMessage(`Failed to check for updates: ${error}`);
        } finally {
            this.isCheckingUpdate = false;
        }
    }

    /**
     * Get tree item representation
     */
    getTreeItem(element: ServerInfoItem): vscode.TreeItem {
        return element;
    }

    /**
     * Get children of an element (or root if element is undefined)
     */
    async getChildren(element?: ServerInfoItem): Promise<ServerInfoItem[]> {
        if (element) {
            // No nested items
            return [];
        }

        const items: ServerInfoItem[] = [];

        // Load version info if not already loaded
        if (!this.versionInfo && !this.isCheckingUpdate) {
            this.isCheckingUpdate = true;
            try {
                this.versionInfo = await this.serverManager.checkForUpdates();
            } catch (error) {
                console.error('Failed to load version info:', error);
            } finally {
                this.isCheckingUpdate = false;
            }
        }

        // Version information
        if (this.versionInfo) {
            items.push(new VersionItem(
                'Installed Version',
                this.versionInfo.currentVersion || 'Not installed',
                'installed'
            ));

            items.push(new VersionItem(
                'Latest Version',
                this.versionInfo.latestVersion,
                'latest'
            ));

            // Show upgrade button if update available
            if (this.versionInfo.updateAvailable) {
                items.push(new ActionItem(
                    'Upgrade Available',
                    {
                        command: 'dataversemcptoolbox.upgradeServer',
                        title: 'Upgrade Server',
                        arguments: []
                    },
                    'arrow-up',
                    `Click to upgrade to v${this.versionInfo.latestVersion}`
                ));
            }
        }

        // Manual check for updates action
        items.push(new ActionItem(
            'Check for Updates',
            {
                command: 'dataversemcptoolbox.checkServerUpdates',
                title: 'Check for Updates',
                arguments: []
            },
            'refresh',
            'Manually check for server updates'
        ));

        // Settings section (inline-actionable)
        const config = vscode.workspace.getConfiguration('dataverse.server');
        const channel = config.get<string>('channel') || 'prerelease';
        const enforcedVersion = config.get<string>('enforcedVersion') || '';

        items.push(new SettingItem(
            'Update Channel',
            channel,
            'channel',
            'arrow-swap',
            {
                command: 'dataversemcptoolbox.toggleServerChannel',
                title: 'Toggle Server Channel',
                arguments: []
            }
        ));

        items.push(new SettingItem(
            'Enforced Version',
            enforcedVersion || 'Auto (latest)',
            'enforcedVersion',
            'tag',
            {
                command: 'dataversemcptoolbox.toggleEnforcedVersion',
                title: 'Set Enforced Version',
                arguments: []
            }
        ));

        return items;
    }
}
