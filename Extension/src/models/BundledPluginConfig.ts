/**
 * Configuration for a bundled plugin to be installed automatically
 */
export interface BundledPluginConfig {
    /**
     * NuGet package ID
     */
    packageId: string;
    
    /**
     * Specific version to install, or null/undefined for latest
     */
    version?: string | null;
    
    /**
     * Human-readable description of the plugin
     */
    description?: string;
}

/**
 * Root configuration for bundled plugins
 */
export interface BundledPluginsConfig {
    /**
     * List of plugins to install
     */
    plugins: BundledPluginConfig[];
}
