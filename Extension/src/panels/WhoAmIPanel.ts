import * as vscode from 'vscode';
import { WhoAmIResult } from '../models/RpcModels';

/**
 * Panel pour afficher les informations WhoAmI d'une connexion Dataverse
 */
export class WhoAmIPanel {
    public static currentPanel: WhoAmIPanel | undefined;
    private readonly _panel: vscode.WebviewPanel;
    private _disposables: vscode.Disposable[] = [];

    private constructor(panel: vscode.WebviewPanel, private whoAmIInfo: WhoAmIResult) {
        this._panel = panel;
        this._panel.onDidDispose(() => this.dispose(), null, this._disposables);
        this._update();
    }

    public static show(whoAmIInfo: WhoAmIResult) {
        const column = vscode.window.activeTextEditor
            ? vscode.window.activeTextEditor.viewColumn
            : undefined;

        // If we already have a panel, show it
        if (WhoAmIPanel.currentPanel) {
            WhoAmIPanel.currentPanel._panel.reveal(column);
            WhoAmIPanel.currentPanel.whoAmIInfo = whoAmIInfo;
            WhoAmIPanel.currentPanel._update();
            return;
        }

        // Otherwise, create a new panel
        const panel = vscode.window.createWebviewPanel(
            'dataverseWhoAmI',
            'Dataverse Connection Info',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true
            }
        );

        WhoAmIPanel.currentPanel = new WhoAmIPanel(panel, whoAmIInfo);
    }

    private _update() {
        const webview = this._panel.webview;
        this._panel.title = 'Dataverse Connection Info';
        this._panel.webview.html = this._getHtmlForWebview(webview, this.whoAmIInfo);
    }

    private _getHtmlForWebview(webview: vscode.Webview, info: WhoAmIResult): string {
        if (!info.success) {
            return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Dataverse Connection Info</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            padding: 20px;
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
        }
        .error {
            color: var(--vscode-errorForeground);
            background-color: var(--vscode-inputValidation-errorBackground);
            border: 1px solid var(--vscode-inputValidation-errorBorder);
            padding: 15px;
            border-radius: 4px;
        }
    </style>
</head>
<body>
    <h1>❌ Error</h1>
    <div class="error">
        <p>${info.errorMessage || 'Unknown error'}</p>
    </div>
</body>
</html>`;
        }

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Dataverse Connection Info</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            padding: 20px;
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
        }
        h1 {
            color: var(--vscode-foreground);
            border-bottom: 1px solid var(--vscode-panel-border);
            padding-bottom: 10px;
        }
        .info-section {
            margin: 20px 0;
            padding: 15px;
            background-color: var(--vscode-editor-inactiveSelectionBackground);
            border-radius: 4px;
        }
        .info-item {
            margin: 10px 0;
            padding: 8px 0;
            border-bottom: 1px solid var(--vscode-panel-border);
        }
        .info-item:last-child {
            border-bottom: none;
        }
        .info-label {
            font-weight: bold;
            color: var(--vscode-symbolIcon-variableForeground);
            display: inline-block;
            width: 150px;
        }
        .info-value {
            color: var(--vscode-foreground);
            word-break: break-all;
        }
        .success-icon {
            color: var(--vscode-testing-iconPassed);
            font-size: 24px;
        }
        .header {
            display: flex;
            align-items: center;
            gap: 10px;
        }
    </style>
</head>
<body>
    <div class="header">
        <span class="success-icon">✓</span>
        <h1>Dataverse Connection Information</h1>
    </div>
    
    <div class="info-section">
        <h2>Environment</h2>
        <div class="info-item">
            <span class="info-label">URL:</span>
            <span class="info-value">${info.environmentUrl || 'N/A'}</span>
        </div>
        <div class="info-item">
            <span class="info-label">Organization ID:</span>
            <span class="info-value">${info.organizationId || 'N/A'}</span>
        </div>
    </div>

    <div class="info-section">
        <h2>User Information</h2>
        <div class="info-item">
            <span class="info-label">Name:</span>
            <span class="info-value">${info.userName || 'N/A'}</span>
        </div>
        <div class="info-item">
            <span class="info-label">User ID:</span>
            <span class="info-value">${info.userId || 'N/A'}</span>
        </div>
    </div>

    <div class="info-section">
        <h2>Business Unit</h2>
        <div class="info-item">
            <span class="info-label">Name:</span>
            <span class="info-value">${info.businessUnitName || 'N/A'}</span>
        </div>
        <div class="info-item">
            <span class="info-label">Business Unit ID:</span>
            <span class="info-value">${info.businessUnitId || 'N/A'}</span>
        </div>
    </div>
</body>
</html>`;
    }

    public dispose() {
        WhoAmIPanel.currentPanel = undefined;

        this._panel.dispose();

        while (this._disposables.length) {
            const disposable = this._disposables.pop();
            if (disposable) {
                disposable.dispose();
            }
        }
    }
}
