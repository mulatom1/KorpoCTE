import type { UserRegisterRequest } from "./contracts/user-register-request";
import type { UserRegisterResponse } from "./contracts/user-register-response";
import type { UserLoginRequest } from "./contracts/user-login-request";
import type { UserLoginResponse } from "./contracts/user-login-response";
import type { UserPassChangeRequest } from "./contracts/user-pass-change-request";
import type { UserPassChangeResponse } from "./contracts/user-pass-change-response";
import { apiFetch } from "./api-fetch";

export class AuthService {
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

    public getHeaders(): Record<string, string> {
      return {
        'Content-Type': 'application/json',
        'X-TOKEN': this.appToken,
        'Authorization': `Bearer ${this.usrToken}`,
      };
    }

    public async userRegister(request: UserRegisterRequest): Promise<UserRegisterResponse> {
      const response = await apiFetch(`${this.apiUrl}/api/portal/user-register`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(request)
      }, { skipAuthRedirect: true });

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.message || `Error registering user: ${response.statusText}`);
      }

      const result = await response.json();
      return result;
    }

    public async userLogin(request: UserLoginRequest): Promise<UserLoginResponse> {
      const response = await apiFetch(`${this.apiUrl}/api/portal/user-login`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(request)
      }, { skipAuthRedirect: true });

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.message || `Error logging in: ${response.statusText}`);
      }

      const result = await response.json();
      this.setUsrToken(result.token);
      return result;
    }

    public async userPassChange(request: UserPassChangeRequest): Promise<UserPassChangeResponse> {
      const response = await apiFetch(`${this.apiUrl}/api/portal/user-pass-change`, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(request)
      }, { skipAuthRedirect: true });

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.message || `Error changing password: ${response.statusText}`);
      }

      const result = await response.json();
      return result;
    }
}
