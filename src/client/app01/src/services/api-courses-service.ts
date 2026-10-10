import type { CoursesCourseTilesResponse } from "./contracts/courses-course-tiles-response";
import type { CoursesCourseContentRequest } from "./contracts/courses-course-content-request";
import type { CoursesCourseContentResponse } from "./contracts/courses-course-content-response";
import type { CoursesHangarFlagsRequest } from "./contracts/courses-hangar-flags-request";
import type { CoursesHangarFlagsResponse } from "./contracts/courses-hangar-flags-response";
import type { CoursesHangarTasksResponse } from "./contracts/courses-hangar-tasks-response";
import type { CoursesVerifyAnswerRequest } from "./contracts/courses-verify-answer-request";
import type { CoursesVerifyAnswerResponse } from "./contracts/courses-verify-answer-response";
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

  // Lista zadań hangaru – wymaga zalogowania (Bearer).
  public async getHangarTasks(): Promise<CoursesHangarTasksResponse> {
    const response = await apiFetch(`${this.apiUrl}/api/courses/hangar-tasks`, {
      method: "GET",
      headers: this.getHeaders(),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        getProblemMessage(errorData) ||
          `Błąd pobierania listy zadań: ${response.statusText}`,
      );
    }

    return response.json();
  }

  // Strona listy flag hangaru ze statusem zdobycia (filtr + paginacja) –
  // wymaga zalogowania (Bearer).
  public async getHangarFlags(
    request: CoursesHangarFlagsRequest,
  ): Promise<CoursesHangarFlagsResponse> {
    const params = new URLSearchParams();
    params.append("filter", request.filter);
    params.append("page", request.page.toString());
    params.append("pageSize", request.pageSize.toString());

    const response = await apiFetch(
      `${this.apiUrl}/api/courses/hangar-flags?${params.toString()}`,
      {
        method: "GET",
        headers: this.getHeaders(),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        getProblemMessage(errorData) ||
          `Błąd pobierania listy flag: ${response.statusText}`,
      );
    }

    return response.json();
  }

  // Weryfikacja odpowiedzi – wymaga zalogowania (Bearer). Awaria modelu wraca
  // jako 200 ze statusem Unavailable; wyjątek oznacza błąd HTTP (400/404/inne).
  public async verifyAnswer(
    request: CoursesVerifyAnswerRequest,
  ): Promise<CoursesVerifyAnswerResponse> {
    const response = await apiFetch(
      `${this.apiUrl}/api/courses/verify-answer`,
      {
        method: "POST",
        headers: this.getHeaders(),
        body: JSON.stringify(request),
      },
    );

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(
        getProblemMessage(errorData) ||
          `Błąd weryfikacji odpowiedzi: ${response.statusText}`,
      );
    }

    return response.json();
  }
}

// Komunikat z ProblemDetails: detail (403/404/500), pierwszy błąd walidacji
// z errors (400) albo message.
function getProblemMessage(errorData: unknown): string | null {
  if (!errorData || typeof errorData !== "object") return null;
  const data = errorData as {
    detail?: string;
    message?: string;
    errors?: Record<string, string[]>;
  };
  if (data.detail) return data.detail;
  if (data.errors) {
    const first = Object.values(data.errors).flat()[0];
    if (first) return first;
  }
  return data.message || null;
}
