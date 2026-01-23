import { ToolInfo } from './ToolInfo';

/**
 * Information about an installed plugin
 */
export interface PluginInfo {
    /**
     * Plugin name
     */
    name: string;

    /**
     * Plugin version
     */
    version: string;

    /**
     * Plugin author
     */
    author: string;

    /**
     * Plugin description
     */
    description: string;

    /**
     * NuGet package ID (if installed from NuGet)
     */
    packageId?: string;

    /**
     * List of tools exposed by this plugin
     */
    tools: ToolInfo[];
}
