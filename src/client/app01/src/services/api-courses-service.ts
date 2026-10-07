import type { CoursesCourseTilesResponse } from "./contracts/courses-course-tiles-response";
import type { CoursesCourseContentRequest } from "./contracts/courses-course-content-request";
import type { CoursesCourseContentResponse } from "./contracts/courses-course-content-response";
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

  // Nagłówki dla endpointów wymagających zalogowania.
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

  // Treść kursu – wymaga zalogowania (Bearer); wygasłą sesję obsługuje apiFetch.
  public async getCourseContent(
    request: CoursesCourseContentRequest,
  ): Promise<CoursesCourseContentResponse> {
    const params = new URLSearchParams();
    params.append("slug", request.slug);

    const response = await apiFetch(
      `${this.apiUrl}/api/courses/course-content?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      if (response.status === 404) {
        throw new Error("Kurs nie istnieje lub nie jest jeszcze opublikowany");
      }

      // Błędy serwera mają postać ProblemDetails (detail/title).
      const errorData = await response.json().catch(() => null);
      throw new Error(
        errorData?.detail ||
          errorData?.message ||
          `Błąd pobierania treści kursu: ${response.statusText}`,
      );
    }

    return response.json();
  }
}
