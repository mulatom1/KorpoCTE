import { useEffect, useState } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router';
import { AUTH_CHANGED_EVENT, buildLoginUrl, clearAuth, hasStoredSession, isAuthenticated } from '../utils/auth';

/** Co ile sprawdzamy ważność tokenu, gdy użytkownik siedzi na podstronie. */
const RECHECK_INTERVAL_MS = 15000;

/**
 * Bramka dla podstron wymagających logowania.
 *
 * Brak tokenu lub token wygasły => przekierowanie na /login?returnUrl=...
 * Sprawdzanie jest powtarzane cyklicznie, przy powrocie do karty oraz po
 * wylogowaniu w innym miejscu aplikacji – dzięki temu nie zdarzy się sytuacja,
 * w której z nieaktualną sesją strzelamy do API i dostajemy 401.
 */
function RequireAuth() {
    const location = useLocation();
    const [isAuthed, setIsAuthed] = useState(() => isAuthenticated());

    useEffect(() => {
        const check = () => {
            const authed = isAuthenticated();

            // Token wygasł – sprzątamy resztki sesji, żeby menu też się odświeżyło.
            if (!authed && hasStoredSession()) {
                clearAuth();
            }

            setIsAuthed(authed);
        };

        check();

        const timer = setInterval(check, RECHECK_INTERVAL_MS);
        window.addEventListener('focus', check);
        window.addEventListener('storage', check);
        window.addEventListener(AUTH_CHANGED_EVENT, check);
        document.addEventListener('visibilitychange', check);

        return () => {
            clearInterval(timer);
            window.removeEventListener('focus', check);
            window.removeEventListener('storage', check);
            window.removeEventListener(AUTH_CHANGED_EVENT, check);
            document.removeEventListener('visibilitychange', check);
        };
    }, [location.pathname]);

    if (!isAuthed) {
        return <Navigate to={buildLoginUrl(location.pathname + location.search)} replace />;
    }

    return <Outlet />;
}

export default RequireAuth;
