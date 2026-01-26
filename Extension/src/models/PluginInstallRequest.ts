import { PluginInfo } from './PluginInfo';

/**
 * Request to install a plugin from NuGet
 */
export interface PluginInstallRequest {
    /**
     * NuGet package ID to install
     */
    packageId: string;

    /**
     * Specific version to install (null for latest)
     */
    version?: string;
}

/**
 * Result of a plugin installation operation
 */
export interface PluginInstallResult {
    /**
     * Whether the installation was successful
     */
    success: boolean;

    /**
     * Error message (if failed)
     */
    errorMessage?: string;

    /**
     * Installed plugin information (if successful)
     */
    pluginInfo?: PluginInfo;
}
