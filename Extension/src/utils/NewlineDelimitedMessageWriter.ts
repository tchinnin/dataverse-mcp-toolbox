import { MessageWriter, Message } from 'vscode-jsonrpc';

/**
 * MessageWriter implementation that writes newline-delimited JSON-RPC messages
 * Compatible with .NET's NewLineDelimitedMessageHandler
 */
export class NewlineDelimitedMessageWriter implements MessageWriter {
    private errorListeners: Set<(e: [Error, Message | undefined, number | undefined]) => any> = new Set();
    private closeListeners: Set<() => any> = new Set();

    constructor(private output: NodeJS.WritableStream) {
        this.output.on('error', (error: Error) => {
            for (const listener of this.errorListeners) {
                listener([error, undefined, undefined]);
            }
        });

        this.output.on('close', () => {
            for (const listener of this.closeListeners) {
                listener();
            }
        });
    }

    async write(message: Message): Promise<void> {
        return new Promise((resolve, reject) => {
            try {
                // Serialize message to JSON and append newline
                const json = JSON.stringify(message);
                const line = json + '\n';

                this.output.write(line, 'utf-8', (error) => {
                    if (error) {
                        for (const listener of this.errorListeners) {
                            listener([error, message, undefined]);
                        }
                        reject(error);
                    } else {
                        resolve();
                    }
                });
            } catch (error) {
                const err = error instanceof Error ? error : new Error(String(error));
                for (const listener of this.errorListeners) {
                    listener([err, message, undefined]);
                }
                reject(err);
            }
        });
    }

    readonly onError = (listener: (e: [Error, Message | undefined, number | undefined]) => any): { dispose: () => void } => {
        this.errorListeners.add(listener);
        return {
            dispose: () => {
                this.errorListeners.delete(listener);
            }
        };
    };

    readonly onClose = (listener: () => any): { dispose: () => void } => {
        this.closeListeners.add(listener);
        return {
            dispose: () => {
                this.closeListeners.delete(listener);
            }
        };
    };

    dispose(): void {
        this.errorListeners.clear();
        this.closeListeners.clear();
    }

    end(): void {
        // Optionally end the output stream
        // Usually not needed as we don't want to close stdin of the server
    }
}
