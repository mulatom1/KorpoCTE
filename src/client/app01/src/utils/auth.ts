/**
 * Centralna obsługa stanu logowania.
 *
 * Zasada: nigdy nie pozwalamy, żeby przeglądarka sama zareagowała na 401 z API
 * (natywne okno logowania). Brak tokenu / wygasły token = przekierowanie na
 * naszą stronę /login z parametrem returnUrl.
 */

const AUTH_KEYS = ["token", "tokenExpiresAt", "userEmail", "userId"] as const;

/** Zdarzenie emitowane przy każdej zmianie stanu logowania (login / logout / wygaśnięcie). */
export const AUTH_CHANGED_EVENT = "auth-changed";

/** Blokada przed wielokrotnym przekierowaniem, gdy równolegle poleci kilka zapytań. */
let redirectInProgress = false;

export function getToken(): string | null {
  return localStorage.getItem("token");
}

/** Czy w localStorage jest jakakolwiek pozostałość po sesji (nawet nieważnej). */
export function hasStoredSession(): boolean {
  return AUTH_KEYS.some((key) => localStorage.getItem(key) !== null);
}

/** Czy użytkownik jest zalogowany i token nadal ważny. */
export function isAuthenticated(): boolean {
  const token = localStorage.getItem("token");
  const expiresAt = localStorage.getItem("tokenExpiresAt");

  if (!token || !expiresAt) return false;

  const expiry = new Date(expiresAt);
  if (Number.isNaN(expiry.getTime())) return false;

  return expiry > new Date();
}

/** Powiadamia komponenty (menu, guard) o zmianie stanu logowania. */
export function notifyAuthChanged(): void {
  window.dispatchEvent(new Event("storage"));
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

/** Czyści dane sesji i powiadamia UI. */
export function clearAuth(): void {
  AUTH_KEYS.forEach((key) => localStorage.removeItem(key));
  notifyAuthChanged();
}

/** Zapisuje dane sesji po udanym logowaniu i powiadamia UI. */
export function setAuth(data: {
  token: string;
  tokenExpiresAt: string;
  email: string;
  id: number | string;
}): void {
  localStorage.setItem("token", data.token);
  localStorage.setItem("tokenExpiresAt", data.tokenExpiresAt);
  localStorage.setItem("userEmail", data.email);
  localStorage.setItem("userId", String(data.id));
  notifyAuthChanged();
}

/** Ścieżka aplikacji (bez basename) do wykorzystania jako returnUrl. */
export function getCurrentReturnUrl(): string {
  const base = import.meta.env.BASE_URL.replace(/\/$/, "");
  const path = window.location.pathname.startsWith(base)
    ? window.location.pathname.slice(base.length)
    : window.location.pathname;

  return `${path || "/"}${window.location.search}`;
}

/** Adres strony logowania w routerze, np. /login?returnUrl=%2Fcourses%2Fflags */
export function buildLoginUrl(
  returnUrl: string = getCurrentReturnUrl(),
): string {
  if (!returnUrl || returnUrl.startsWith("/login")) return "/login";

  return `/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/**
 * Twarde przekierowanie na stronę logowania – używane poza Reactem (np. z warstwy
 * API), gdzie nie mamy dostępu do routera. Czyści też stan aplikacji w pamięci.
 */
export function redirectToLogin(
  returnUrl: string = getCurrentReturnUrl(),
): void {
  if (redirectInProgress) return;
  redirectInProgress = true;

  const base = import.meta.env.BASE_URL.replace(/\/$/, "");
  window.location.replace(`${base}${buildLoginUrl(returnUrl)}`);
}
