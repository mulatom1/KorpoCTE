import { clearAuth, isAuthenticated, redirectToLogin } from "../utils/auth";

export interface ApiFetchOptions {
  /**
   * Wyłącza automatyczne przekierowanie na /login przy odpowiedzi 401.
   * Używane przez endpointy autoryzacji (logowanie, rejestracja, zmiana hasła), które
   * zwracają 401 przy błędnych danych – tam 401 to komunikat, nie wygaśnięcie sesji.
   */
  skipAuthRedirect?: boolean;
}

/** Czy request niesie realny (niepusty) token użytkownika. */
function hasBearerToken(init?: RequestInit): boolean {
  const headers = init?.headers;
  if (!headers) return false;

  let value: string | null = null;

  if (headers instanceof Headers) {
    value = headers.get("Authorization");
  } else if (Array.isArray(headers)) {
    value =
      headers.find(([key]) => key.toLowerCase() === "authorization")?.[1] ??
      null;
  } else {
    const record = headers as Record<string, string>;
    value = record["Authorization"] ?? record["authorization"] ?? null;
  }

  return !!value && value.trim().length > "Bearer ".length;
}

/**
 * Opakowanie na fetch z centralną obsługą braku autoryzacji.
 *
 * 1. Zanim wyślemy zapytanie z tokenem – sprawdzamy, czy sesja jest nadal ważna.
 *    Dzięki temu wygasły token nie trafia do API i przeglądarka nie ma szansy
 *    pokazać natywnego okna logowania.
 * 2. Jeśli API mimo wszystko odpowie 401 – czyścimy sesję i przekierowujemy na /login.
 */
export async function apiFetch(
  input: RequestInfo | URL,
  init?: RequestInit,
  options?: ApiFetchOptions,
): Promise<Response> {
  const handleAuth = !options?.skipAuthRedirect;

  if (handleAuth && hasBearerToken(init) && !isAuthenticated()) {
    clearAuth();
    redirectToLogin();
    throw new Error("Sesja wygasła – przekierowanie na stronę logowania");
  }

  const response = await fetch(input, init);

  if (handleAuth && response.status === 401) {
    clearAuth();
    redirectToLogin();
    throw new Error("Brak autoryzacji – przekierowanie na stronę logowania");
  }

  return response;
}
