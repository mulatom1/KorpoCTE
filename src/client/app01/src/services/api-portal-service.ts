import type { MailFromClientAddRequest } from "./contracts/mail-from-client-add-request";
import type { MailFromClientAddResponse } from "./contracts/mail-from-client-add-response";
import type { UserListRequest } from "./contracts/user-list-request";
import type { UserListResponse } from "./contracts/user-list-response";
import type { UserSetRequest } from "./contracts/user-set-request";
import type { UserSetResponse } from "./contracts/user-set-response";
import type { UserDeleteRequest } from "./contracts/user-delete-request";
import type { UserDeleteResponse } from "./contracts/user-delete-response";
import type { UserPassResetRequest } from "./contracts/user-pass-reset-request";
import type { UserPassResetResponse } from "./contracts/user-pass-reset-response";
import type { MailFromClientListRequest } from "./contracts/mail-from-client-list-request";
import type { MailFromClientListResponse } from "./contracts/mail-from-client-list-response";
import type { MailFromClientGetRequest } from "./contracts/mail-from-client-get-request";
import type { MailFromClientGetResponse } from "./contracts/mail-from-client-get-response";
import type { MailFromClientDeleteRequest } from "./contracts/mail-from-client-delete-request";
import type { MailFromClientDeleteResponse } from "./contracts/mail-from-client-delete-response";
import { apiFetch } from "./api-fetch";

export class ApiPortalService {
  private apiUrl: string = "";
  private appToken: string = "";
  private usrToken: string = "";

  constructor(apiUrl: string, appToken: string, usrToken?: string) {
    this.setApiUrl(apiUrl);
    this.setAppToken(appToken);
    if (usrToken) this.setUsrToken(usrToken);
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
      "Content-Type": "application/json",
      "X-TOKEN": this.appToken,
      Authorization: `Bearer ${this.usrToken}`,
    };
  }

  public getApiUrl(): string {
    return this.apiUrl;
  }

  public async mailFromClientAdd(
    request: MailFromClientAddRequest,
  ): Promise<MailFromClientAddResponse> {
    const response = await apiFetch(
      `${this.apiUrl}/api/portal/mail-from-client-add`,
      {
        method: "POST",
        headers: this.getHeaders(),
        body: JSON.stringify(request),
      },
    );

    if (!response.ok) {
      throw new Error(`Error adding mail: ${response.statusText}`);
    }

    return response.json();
  }

  public async mailFromClientList(
    request: MailFromClientListRequest,
  ): Promise<MailFromClientListResponse> {
    const params = new URLSearchParams();
    params.append("page", request.page.toString());
    params.append("pageSize", request.pageSize.toString());

    const response = await apiFetch(
      `${this.apiUrl}/api/portal/mail-from-client-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching mail list: ${response.statusText}`,
      );
    }

    return response.json();
  }

  public async mailFromClientGet(
    request: MailFromClientGetRequest,
  ): Promise<MailFromClientGetResponse> {
    const params = new URLSearchParams();
    params.append("id", request.id.toString());

    const response = await apiFetch(
      `${this.apiUrl}/api/portal/mail-from-client-get?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message || `Error fetching mail: ${response.statusText}`,
      );
    }

    return response.json();
  }

  public async mailFromClientDelete(
    request: MailFromClientDeleteRequest,
  ): Promise<MailFromClientDeleteResponse> {
    const params = new URLSearchParams();
    params.append("id", request.id.toString());

    const response = await apiFetch(
      `${this.apiUrl}/api/portal/mail-from-client-delete?${params.toString()}`,
      {
        method: "DELETE",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message || `Error deleting mail: ${response.statusText}`,
      );
    }

    return response.json();
  }

  public async userList(request: UserListRequest): Promise<UserListResponse> {
    const params = new URLSearchParams();
    if (request.email) params.append("email", request.email);
    if (request.isAdmin !== undefined)
      params.append("isAdmin", request.isAdmin.toString());
    params.append("page", request.page.toString());
    params.append("pageSize", request.pageSize.toString());

    const response = await apiFetch(
      `${this.apiUrl}/api/portal/user-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching user list: ${response.statusText}`,
      );
    }

    return response.json();
  }

  public async userSet(request: UserSetRequest): Promise<UserSetResponse> {
    const response = await apiFetch(`${this.apiUrl}/api/portal/user-set`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message || `Error updating user: ${response.statusText}`,
      );
    }

    return response.json();
  }

  public async userDelete(
    request: UserDeleteRequest,
  ): Promise<UserDeleteResponse> {
    const params = new URLSearchParams();
    params.append("email", request.email);

    const response = await apiFetch(
      `${this.apiUrl}/api/portal/user-delete?${params.toString()}`,
      {
        method: "DELETE",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message || `Error deleting user: ${response.statusText}`,
      );
    }

    return response.json();
  }

  public async userPassReset(
    request: UserPassResetRequest,
  ): Promise<UserPassResetResponse> {
    const response = await apiFetch(
      `${this.apiUrl}/api/portal/user-pass-reset`,
      {
        method: "POST",
        headers: this.getHeaders(),
        body: JSON.stringify(request),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error resetting password: ${response.statusText}`,
      );
    }

    return response.json();
  }
}
