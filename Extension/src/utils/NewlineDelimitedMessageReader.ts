import * as readline from 'readline';
import { MessageReader, DataCallback, Disposable } from 'vscode-jsonrpc';

/**
 * MessageReader implementation that reads newline-delimited JSON-RPC messages
 * Compatible with .NET's NewLineDelimitedMessageHandler
 */
export class NewlineDelimitedMessageReader implements MessageReader {
    private readline: readline.Interface;
    private dataCallback: DataCallback | undefined;
    private errorListeners: Set<(e: Error) => any> = new Set();
    private closeListeners: Set<() => any> = new Set();
    private partialMessageListeners: Set<(messageChunk: any) => any> = new Set();

    constructor(private input: NodeJS.ReadableStream) {
        this.readline = readline.createInterface({
            input: input,
            terminal: false,
            crlfDelay: Infinity // Treat \r\n as single line break
        });

        this.readline.on('line', (line: string) => {
            this.handleLine(line);
        });

        this.readline.on('close', () => {
            for (const listener of this.closeListeners) {
                listener();
            }
        });

        this.input.on('error', (error: Error) => {
            for (const listener of this.errorListeners) {
                listener(error);
            }
        });
    }

    private handleLine(line: string): void {
        // Skip empty lines
        if (!line.trim()) {
            return;
        }

        try {
            const message = JSON.parse(line);
            
            if (this.dataCallback) {
                this.dataCallback(message);
            }
        } catch (error) {
            const err = new Error(`Failed to parse JSON-RPC message: ${error}. Line: ${line}`);
            for (const listener of this.errorListeners) {
                listener(err);
            }
        }
    }

    listen(callback: DataCallback): Disposable {
        this.dataCallback = callback;
        return {
            dispose: () => {
                this.dataCallback = undefined;
            }
        };
    }

    readonly onError = (listener: (e: Error) => any): Disposable => {
        this.errorListeners.add(listener);
        return {
            dispose: () => {
                this.errorListeners.delete(listener);
            }
        };
    };

    readonly onClose = (listener: () => any): Disposable => {
        this.closeListeners.add(listener);
        return {
            dispose: () => {
                this.closeListeners.delete(listener);
            }
        };
    };

    readonly onPartialMessage = (listener: (messageChunk: any) => any): Disposable => {
        this.partialMessageListeners.add(listener);
        return {
            dispose: () => {
                this.partialMessageListeners.delete(listener);
            }
        };
    };

    dispose(): void {
        this.dataCallback = undefined;
        this.errorListeners.clear();
        this.closeListeners.clear();
        this.partialMessageListeners.clear();
        this.readline.close();
    }
}
