# Czytanie treści kursu — Plan Brief

> Full plan: `context/changes/course-content-reading/plan.md`

## What & Why

Wycinek S-04 z roadmapy. Zalogowany użytkownik klika kafelek kursu i czyta jego sformatowaną treść pod `/courses/:slug`. To drugi krok ścieżki „Primary” z PRD: otwórz kurs, potem wykonaj zadanie. Granica prywatności jest twarda: treść kursu nie może trafić do niezalogowanych, także przez publiczne pliki statyczne.

## Starting Point

- S-03 dał kafelki, encję `Course` i czytnik frontmattera. Kafelki są nieklikalne.
- Lokalna konfiguracja trzyma pliki `.md` w `wwwroot/media/courses`, więc `/media/courses/<slug>/<slug>.md` jest dziś publicznie dostępny (200).
- Obrazki w treści mają ścieżki z prototypu, które nie działają.
- Logowanie z powrotem (`returnUrl`) już działa.

## Desired End State

- `GET api/courses/course-content?slug=…` (JWT + `X-TOKEN`) zwraca tytuł, tagi, treść bez frontmattera i `mediaBaseUrl`.
  - Zły slug daje 400.
  - Kurs nieopublikowany, nieistniejący albo zepsuty daje 404.
- Czytnik odmawia pracy, gdy treść leży w `wwwroot`.
- `/courses/:slug` za `RequireAuth` renderuje Markdown z GFM i podświetlaniem kodu, bez surowego HTML. Obrazki względne prowadzą do mediów kursu.
- Kafelek jest linkiem. Gość loguje się i wraca na kurs.

## Key Decisions Made

| Decision                    | Choice                                                                                             | Why (1 sentence)                                                                                  |
| --------------------------- | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Ochrona treści przed wwwroot | Bezpiecznik w czytniku (`ContentPath` w `WebRootPath` → błąd w logu, kursy ukryte) + `.md` poza `wwwroot` | Błędna konfiguracja nie wystawi po cichu treści; zachowanie pokryte testem                       |
| Obrazki w treści            | Ścieżki względne do katalogu kursu, przepisywane w kliencie (`urlTransform`)                       | Przenośne pliki, ta sama konwencja co `image:` we frontmatterze, działa w dev i na produkcji      |
| Surowy HTML                 | Nie: Markdown + GFM + highlight, bez `rehype-raw`                                                   | Brak XSS w sesji z JWT w `localStorage`; obecna treść nie używa HTML                              |
| Zakres strony               | Tytuł, tagi, „Powrót do kursów”, treść; 404 → „Kurs nie istnieje lub nie jest jeszcze opublikowany” | Spójne z kafelkiem, prosta nawigacja                                                              |
| Kody błędów                 | Każdy powód niedostępności = ten sam 404                                                             | Nie zdradza istnienia nieopublikowanych kursów (FR-003)                                           |
| Ścieżka pliku               | Slug z encji w bazie, nie z requestu; walidacja sluga → 400                                         | Reguła CLAUDE.md, ochrona przed path traversal                                                    |
| Typografia                  | Mapowanie elementów na klasy Tailwind + motyw `highlight.js/styles/github-dark.css`                | Bez nowych pakietów (brak pluginu typography)                                                      |

## Scope

**In scope:**
- Czytnik z treścią (`CourseDocument`) i bezpiecznikiem `wwwroot`.
- Wycinek `CourseContent` z testami.
- Klient: kontrakty, `getCourseContent`, helper `resolveCourseMediaUrl`, `CourseMarkdown`, `CourseDetailsPage`, trasa chroniona, kafelek jako link, testy Vitest.

**Out of scope:**
- `rehype-raw`/`rehype-sanitize`; zmiany w `Program.cs`/`UseStaticFiles`.
- Ochrona obrazków (media publiczne z założenia).
- Spis treści, postęp czytania, zadania (S-01); cache.
- Automatyczna migracja istniejących plików treści (krok ręczny administratora).

## Architecture / Approach

Handler szuka opublikowanego kursu po slugu, buduje ścieżkę ze sluga z encji i woła wspólny czytnik. Czytnik waliduje ścieżkę, sprawdza bezpiecznik `wwwroot`, parsuje frontmatter i zwraca treść. Klient pobiera treść z Bearer. `CourseMarkdown` renderuje ją przez `react-markdown`, a każdy URL przechodzi przez `resolveCourseMediaUrl` i `defaultUrlTransform`.

## Phases at a Glance

| Phase                                   | What it delivers                                               | Key risk                                                        |
| --------------------------------------- | -------------------------------------------------------------- | --------------------------------------------------------------- |
| 1. Serwer — treść kursu i bezpiecznik   | `CourseDocument`, bezpiecznik, `CourseContent`, testy          | Bezpiecznik ukryje kursy przy obecnej lokalnej konfiguracji (celowo) |
| 2. Klient — strona kursu                | Strona `/courses/:slug`, Markdown, link z kafelka, testy       | XSS przez URL-e w treści; mylące 404 zamiast logowania          |

**Prerequisites:** S-03 zrobiony; do weryfikacji ręcznej przeniesienie `.md` poza `wwwroot` i poprawa ścieżek obrazków w pliku kursu.
**Estimated effort:** ~2 sesje, 2 fazy.

## Open Risks & Assumptions

- Po wdrożeniu serwer musi mieć `ContentPath` poza `wwwroot`. Inaczej bezpiecznik ukryje wszystkie kursy, co jest zamierzone, ale widoczne dla użytkowników.
- Zakładamy, że administrator poprawi ścieżki obrazków w istniejących plikach (`../../../data/...` → `images/...`).
- Obrazki kursów pozostają publiczne. Prywatna jest tylko treść tekstowa.

## Success Criteria (Summary)

- Zalogowany użytkownik czyta sformatowaną treść kursu z obrazkami. Gość po zalogowaniu wraca na kurs.
- Plik `.md` kursu nie jest dostępny publicznie, a błędna konfiguracja nie może go wystawić.
- CI przechodzi, a logowanie, menu i pozostałe moduły działają bez zmian.
