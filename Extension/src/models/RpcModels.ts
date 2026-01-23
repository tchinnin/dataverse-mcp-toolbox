export interface ConnectionRequest {
    environmentUrl: string;
    connectionName?: string;
    accessToken?: string;
    refreshToken?: string;
}

export interface ConnectionResult {
    success: boolean;
    errorMessage?: string;
    connectionId?: string;
    organizationUrl?: string;
    userId?: string;
    userName?: string;
    accessToken?: string;
    refreshToken?: string;
    expiresOn?: string;
}

export interface OrganizationDetail {
    organizationId: string;
    friendlyName: string;
    uniqueName: string;
    version: string;
}

export interface WhoAmIResult {
    success: boolean;
    errorMessage?: string;
    environmentUrl?: string;
    userId?: string;
    userName?: string;
    businessUnitId?: string;
    businessUnitName?: string;
    organizationId?: string;
}
