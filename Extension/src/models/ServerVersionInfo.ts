/**
 * Server version information from running server
 */
export interface ServerVersionInfo {
    version: string;
    buildDate: string;
    platform: string;
}

/**
 * Update check result
 */
export interface UpdateCheckResult {
    currentVersion: string | null;
    latestVersion: string;
    updateAvailable: boolean;
    runningServerVersion?: string;
}
