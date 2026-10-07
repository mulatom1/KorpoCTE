// URL z treści kursu jest względny, gdy nie zaczyna się od "/", "#", "?"
// ani od schematu (np. "https:", "mailto:", "javascript:").
const ABSOLUTE_OR_SPECIAL = /^(?:[/#?]|[a-z][a-z0-9+.-]*:)/i;

/**
 * Zamienia względny URL z treści kursu (np. "images/01.png") na adres mediów
 * kursu: `${apiUrl}${mediaBaseUrl}/${url}`. Pozostałe URL-e zwraca bez zmian.
 * Sanityzację (np. "javascript:") wykonuje dopiero defaultUrlTransform w komponencie.
 */
export function resolveCourseMediaUrl(
  url: string,
  apiUrl: string,
  mediaBaseUrl: string,
): string {
  if (!url || ABSOLUTE_OR_SPECIAL.test(url)) return url;

  const path = url.replace(/^(?:\.\/)+/, "");
  const base = `${apiUrl.replace(/\/+$/, "")}/${mediaBaseUrl.replace(/^\/+|\/+$/g, "")}`;

  return `${base.replace(/\/+$/, "")}/${path}`;
}
