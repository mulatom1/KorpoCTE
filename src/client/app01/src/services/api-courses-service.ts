import type { CoursesCourseTilesResponse } from "./contracts/courses-course-tiles-response";
import { apiFetch } from "./api-fetch";

export class ApiCoursesService {
  private apiUrl: string = "";
  private appToken: string = "";
  private usrToken: string = "";

  constructor(apiUrl: string, appToken: string, usrToken?: string) {
    this.apiUrl = apiUrl;
    this.appToken = appToken;
    if (usrToken) this.setUsrToken(usrToken);
  }

  public setUsrToken(usrToken: string) {
    this.usrToken = usrToken;
  }

  // Nagłówki dla endpointów wymagających zalogowania (kolejne funkcje modułu).
  public getHeaders(): Record<string, string> {
    return {
      "Content-Type": "application/json",
      "X-TOKEN": this.appToken,
      Authorization: `Bearer ${this.usrToken}`,
    };
  }

  // Nagłówki dla endpointów publicznych – celowo bez Authorization, żeby
  // wygasła sesja nie przekierowywała gościa na stronę logowania (apiFetch).
  private getPublicHeaders(): Record<string, string> {
    return {
      "Content-Type": "application/json",
      "X-TOKEN": this.appToken,
    };
  }

  public async getCourseTiles(): Promise<CoursesCourseTilesResponse> {
    const response = await apiFetch(`${this.apiUrl}/api/courses/course-tiles`, {
      method: "GET",
      headers: this.getPublicHeaders(),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.message ||
          `Błąd pobierania listy kursów: ${response.statusText}`,
      );
    }

    return response.json();
  }
}
