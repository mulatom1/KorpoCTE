import { getToken, isAuthenticated } from './auth';

export function getIsAdminFromToken(): boolean {
  // Wygasła sesja = brak uprawnień, inaczej UI próbowałby wołać API adminowe
  // i dostawał 401 zamiast trafić na stronę logowania.
  if (!isAuthenticated()) return false;

  const token = getToken();
  if (!token) return false;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload.isAdmin === true || payload.isAdmin === 'true';
  } catch {
    return false;
  }
}
