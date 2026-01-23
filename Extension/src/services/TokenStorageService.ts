import * as vscode from 'vscode';

export interface StoredTokens {
    accessToken: string;
    refreshToken?: string;
    expiresOn?: string;
}

/**
 * Service pour gérer le stockage sécurisé des tokens OAuth dans VS Code Secrets
 */
export class TokenStorageService {
    private static readonly TOKEN_KEY_PREFIX = 'dataverse.token.';

    constructor(private context: vscode.ExtensionContext) {}

    /**
     * Stocke les tokens pour une connexion de manière sécurisée
     */
    async storeTokens(connectionId: string, tokens: StoredTokens): Promise<void> {
        const key = this.getTokenKey(connectionId);
        const value = JSON.stringify(tokens);
        await this.context.secrets.store(key, value);
    }

    /**
     * Récupère les tokens stockés pour une connexion
     */
    async getTokens(connectionId: string): Promise<StoredTokens | null> {
        const key = this.getTokenKey(connectionId);
        const value = await this.context.secrets.get(key);
        
        if (!value) {
            return null;
        }

        try {
            return JSON.parse(value) as StoredTokens;
        } catch {
            return null;
        }
    }

    /**
     * Supprime les tokens stockés pour une connexion
     */
    async deleteTokens(connectionId: string): Promise<void> {
        const key = this.getTokenKey(connectionId);
        await this.context.secrets.delete(key);
    }

    /**
     * Vérifie si un token est expiré
     */
    isTokenExpired(tokens: StoredTokens): boolean {
        if (!tokens.expiresOn) {
            // Si pas de date d'expiration, considérer comme potentiellement valide
            return false;
        }

        const expiryDate = new Date(tokens.expiresOn);
        const now = new Date();
        
        // Ajouter une marge de 5 minutes
        const bufferMs = 5 * 60 * 1000;
        return expiryDate.getTime() - bufferMs < now.getTime();
    }

    /**
     * Supprime tous les tokens (pour cleanup)
     */
    async clearAllTokens(): Promise<void> {
        // Note: VS Code Secrets n'a pas d'API pour lister toutes les clés
        // On devra supprimer les tokens individuellement quand on supprime une connexion
    }

    private getTokenKey(connectionId: string): string {
        return `${TokenStorageService.TOKEN_KEY_PREFIX}${connectionId}`;
    }
}
