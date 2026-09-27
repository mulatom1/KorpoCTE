import type { FlashcardsGenerateFromTextRequest } from "./contracts/flashcards-generate-from-text-request";
import type { FlashcardsGenerateFromTextResponse } from "./contracts/flashcards-generate-from-text-response";
import { apiFetch } from "./api-fetch";

export class ApiFlashcardsService {
    private apiUrl: string = '';
    private appToken: string = '';
    private usrToken: string = '';

    constructor(apiUrl: string, appToken: string) {
        this.setApiUrl(apiUrl);
        this.setAppToken(appToken);
    }

    private setApiUrl(url: string) {
        this.apiUrl = url;
    }

    private setAppToken(appToken: string) {
        this.appToken = appToken;
    }

    public setUsrToken(usrToken: string) {
        this.usrToken = usrToken;
    }

    public getUsrToken(): string {
        return this.usrToken;
    }

    private getHeaders(): Record<string, string> {
        return {
            'Content-Type': 'application/json',
            'X-TOKEN': this.appToken,
            'Authorization': `Bearer ${this.usrToken}`,
        };
    }

    private getApiUrl(): string {
        return this.apiUrl;
    }

    public async flashcardsGenerateFromText(request: FlashcardsGenerateFromTextRequest): Promise<FlashcardsGenerateFromTextResponse> {
        const response = await apiFetch(`${this.getApiUrl()}/api/flashcards/generate-from-text`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify(request)
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            throw new Error(errorData?.message || `Error generating flashcards: ${response.statusText}`);
        }

        const result = await response.json();
        return result;
    }
}
