import type { LottoDrawsGetListRequest } from "./contracts/lotto-draws-get-list-request";
import type { LottoDrawsGetListResponse } from "./contracts/lotto-draws-get-list-response";
import type { LottoDrawsExportRequest } from "./contracts/lotto-draws-export-request";
import type { LottoDrawsExportResponse } from "./contracts/lotto-draws-export-response";
import type { LottoDrawsImportRequest } from "./contracts/lotto-draws-import-request";
import type { LottoDrawsImportResponse } from "./contracts/lotto-draws-import-response";
import type { LottoDrawsAddRequest } from "./contracts/lotto-draws-add-request";
import type { LottoDrawsAddResponse } from "./contracts/lotto-draws-add-response";
import type { LottoDrawsUpdateRequest } from "./contracts/lotto-draws-update-request";
import type { LottoDrawsUpdateResponse } from "./contracts/lotto-draws-update-response";
import type { LottoDrawsDeleteRequest } from "./contracts/lotto-draws-delete-request";
import type { LottoDrawsDeleteResponse } from "./contracts/lotto-draws-delete-response";
import type { LottoDrawsGetPrizesListRequest } from "./contracts/lotto-draws-get-prizes-list-request";
import type { LottoDrawsGetPrizesListResponse } from "./contracts/lotto-draws-get-prizes-list-response";
import type { LottoDrawsNumbersStatsListRequest } from "./contracts/lotto-draws-numbers-stats-list-request";
import type { LottoDrawsNumbersStatsListResponse } from "./contracts/lotto-draws-numbers-stats-list-response";
import type { LottoTicketsGetListRequest } from "./contracts/lotto-tickets-get-list-request";
import type { LottoTicketsGetListResponse } from "./contracts/lotto-tickets-get-list-response";
import type { LottoTicketsAddRequest } from "./contracts/lotto-tickets-add-request";
import type { LottoTicketsAddResponse } from "./contracts/lotto-tickets-add-response";
import type { LottoTicketsDeleteRequest } from "./contracts/lotto-tickets-delete-request";
import type { LottoTicketsDeleteResponse } from "./contracts/lotto-tickets-delete-response";
import type { LottoTicketsExportRequest } from "./contracts/lotto-tickets-export-request";
import type { LottoTicketsExportResponse } from "./contracts/lotto-tickets-export-response";
import type { LottoTicketsImportRequest } from "./contracts/lotto-tickets-import-request";
import type { LottoTicketsImportResponse } from "./contracts/lotto-tickets-import-response";
import type { LottoWinningTicketsRequest } from "./contracts/lotto-winning-tickets-request";
import type { LottoWinningTicketsResponse } from "./contracts/lotto-winning-tickets-response";
import { apiFetch } from "./api-fetch";

export class ApiLottoService {
  private apiUrl: string = "";
  private appToken: string = "";
  private usrToken: string = "";

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
      "Content-Type": "application/json",
      "X-TOKEN": this.appToken,
      Authorization: `Bearer ${this.usrToken}`,
    };
  }

  private getApiUrl(): string {
    return this.apiUrl;
  }

  public async lottoDrawsGetList(
    request: LottoDrawsGetListRequest,
  ): Promise<LottoDrawsGetListResponse> {
    const params = new URLSearchParams();
    if (request.drawDateFrom)
      params.append("drawDateFrom", request.drawDateFrom);
    if (request.drawDateTo) params.append("drawDateTo", request.drawDateTo);
    if (request.drawTypeId !== undefined)
      params.append("drawTypeId", request.drawTypeId.toString());
    params.append("page", (request.page || 1).toString());
    params.append("pageSize", (request.pageSize || 100).toString());
    if (request.sortOrder) params.append("sortOrder", request.sortOrder);

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-get-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching lotto draws: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsExport(
    request: LottoDrawsExportRequest,
  ): Promise<LottoDrawsExportResponse> {
    const params = new URLSearchParams();
    if (request.drawDateFrom)
      params.append("drawDateFrom", request.drawDateFrom);
    if (request.drawDateTo) params.append("drawDateTo", request.drawDateTo);
    if (request.drawTypeId !== undefined)
      params.append("drawTypeId", request.drawTypeId.toString());

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-export?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error exporting lotto draws: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsImport(
    request: LottoDrawsImportRequest,
  ): Promise<LottoDrawsImportResponse> {
    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-import`,
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
          `Error importing lotto draws: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsAdd(
    request: LottoDrawsAddRequest,
  ): Promise<LottoDrawsAddResponse> {
    const response = await apiFetch(`${this.getApiUrl()}/api/lotto/draws-add`, {
      method: "POST",
      headers: this.getHeaders(),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.detail ||
          errorData?.message ||
          `Error adding lotto draw: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsUpdate(
    request: LottoDrawsUpdateRequest,
  ): Promise<LottoDrawsUpdateResponse> {
    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-update`,
      {
        method: "PUT",
        headers: this.getHeaders(),
        body: JSON.stringify(request),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.detail ||
          errorData?.message ||
          `Error updating lotto draw: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsDelete(
    request: LottoDrawsDeleteRequest,
  ): Promise<LottoDrawsDeleteResponse> {
    const params = new URLSearchParams();
    params.append("drawId", request.drawId.toString());

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-delete?${params.toString()}`,
      {
        method: "DELETE",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.detail ||
          errorData?.message ||
          `Error deleting lotto draw: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsGetPrizesList(
    request: LottoDrawsGetPrizesListRequest,
  ): Promise<LottoDrawsGetPrizesListResponse> {
    const params = new URLSearchParams();
    params.append("drawTypeId", request.drawTypeId.toString());
    params.append("drawSystemId", request.drawSystemId.toString());

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-get-prizes-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching prizes list: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoDrawsNumbersStatsList(
    request: LottoDrawsNumbersStatsListRequest,
  ): Promise<LottoDrawsNumbersStatsListResponse> {
    const params = new URLSearchParams();
    params.append("drawTypeId", request.drawTypeId.toString());
    params.append("numbersGroup", request.numbersGroup.toString());
    if (request.specialsGroup !== undefined)
      params.append("specialsGroup", request.specialsGroup.toString());
    if (request.sortOrder) params.append("sortOrder", request.sortOrder);
    if (request.drawDateFrom)
      params.append("drawDateFrom", request.drawDateFrom);
    if (request.drawDateTo) params.append("drawDateTo", request.drawDateTo);

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/draws-numbers-stats-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching numbers stats: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoTicketsGetList(
    request: LottoTicketsGetListRequest,
  ): Promise<LottoTicketsGetListResponse> {
    const params = new URLSearchParams();
    if (request.groupName) params.append("groupName", request.groupName);
    if (request.drawTypeId !== undefined)
      params.append("drawTypeId", request.drawTypeId.toString());
    params.append("page", (request.page || 1).toString());
    params.append("pageSize", (request.pageSize || 100).toString());

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/tickets-get-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching lotto tickets: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoTicketsAdd(
    request: LottoTicketsAddRequest,
  ): Promise<LottoTicketsAddResponse> {
    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/tickets-add`,
      {
        method: "POST",
        headers: this.getHeaders(),
        body: JSON.stringify(request),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.detail ||
          errorData?.message ||
          `Error adding lotto ticket: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoTicketsDelete(
    request: LottoTicketsDeleteRequest,
  ): Promise<LottoTicketsDeleteResponse> {
    const params = new URLSearchParams();
    params.append("ticketId", request.ticketId.toString());

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/tickets-delete?${params.toString()}`,
      {
        method: "DELETE",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error deleting lotto ticket: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoTicketsExport(
    request: LottoTicketsExportRequest,
  ): Promise<LottoTicketsExportResponse> {
    const params = new URLSearchParams();
    if (request.groupName) params.append("groupName", request.groupName);
    if (request.drawTypeId !== undefined)
      params.append("drawTypeId", request.drawTypeId.toString());

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/tickets-export?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error exporting lotto tickets: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoTicketsImport(
    request: LottoTicketsImportRequest,
  ): Promise<LottoTicketsImportResponse> {
    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/tickets-import`,
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
          `Error importing lotto tickets: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }

  public async lottoWinningTicketsGetList(
    request: LottoWinningTicketsRequest,
  ): Promise<LottoWinningTicketsResponse> {
    const params = new URLSearchParams();
    if (request.drawDateFrom)
      params.append("drawDateFrom", request.drawDateFrom);
    if (request.drawDateTo) params.append("drawDateTo", request.drawDateTo);
    if (request.drawTypeId !== undefined)
      params.append("drawTypeId", request.drawTypeId.toString());
    if (request.groupName) params.append("groupName", request.groupName);
    if (request.page !== undefined)
      params.append("page", request.page.toString());
    if (request.pageSize !== undefined)
      params.append("pageSize", request.pageSize.toString());
    if (request.winTier1 !== undefined)
      params.append("winTier1", request.winTier1.toString());
    if (request.winTier2 !== undefined)
      params.append("winTier2", request.winTier2.toString());
    if (request.winTier3 !== undefined)
      params.append("winTier3", request.winTier3.toString());
    if (request.winTier4 !== undefined)
      params.append("winTier4", request.winTier4.toString());
    if (request.winTier5 !== undefined)
      params.append("winTier5", request.winTier5.toString());
    if (request.winTier6 !== undefined)
      params.append("winTier6", request.winTier6.toString());
    if (request.winTier7 !== undefined)
      params.append("winTier7", request.winTier7.toString());
    if (request.winTier8 !== undefined)
      params.append("winTier8", request.winTier8.toString());
    if (request.winTier9 !== undefined)
      params.append("winTier9", request.winTier9.toString());
    if (request.winTier10 !== undefined)
      params.append("winTier10", request.winTier10.toString());
    if (request.winTier11 !== undefined)
      params.append("winTier11", request.winTier11.toString());
    if (request.winTier12 !== undefined)
      params.append("winTier12", request.winTier12.toString());
    if (request.hideDrawsWithoutMatches !== undefined)
      params.append(
        "hideDrawsWithoutMatches",
        request.hideDrawsWithoutMatches.toString(),
      );

    const response = await apiFetch(
      `${this.getApiUrl()}/api/lotto/winning-tickets-list?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Error fetching winning tickets: ${response.statusText}`,
      );
    }

    const result = await response.json();
    return result;
  }
}
