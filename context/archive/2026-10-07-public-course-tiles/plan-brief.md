# Publiczne kafelki kursów — Plan Brief

> Full plan: `context/changes/public-course-tiles/plan.md`

## What & Why

Wycinek S-03 z roadmapy. Odwiedzający bez konta wchodzi z menu głównego do „Kursy” i widzi kafelki opublikowanych kursów: tytuł, krótki opis, tagi, grafikę. Administrator publikuje kurs poza aplikacją (FR-010). To wizytówka modułu: treść kursów zostaje za logowaniem (S-04). Zmiana menu nie może zepsuć reszty portalu (FR-013, FR-014).

## Starting Point

- Moduł Courses jest wpięty w host i w testy, ale ma tylko techniczny `ModuleHello`.
- Nie ma encji, migracji, kluczy `Courses:*`, parsera YAML, trasy `/courses` ani pozycji w menu.
- Deploy już chroni na serwerze „media w wwwroot” i „treść kursów”.

## Desired End State

- `GET api/courses/course-tiles` (tylko `X-TOKEN`, bez JWT) zwraca kafelki kursów z `PublishDate <= UtcNow`.
- Kolejność: data rosnąco, potem tytuł, potem Id.
- Kurs z brakującym lub błędnym plikiem jest logowany i pomijany, bez 500.
- Strona `/courses` jest publiczna, z nieklikalnymi kafelkami. Pozycja „Kursy” stoi w menu po „Gry”.
- Reszta menu, Apki, Gry, logowanie i ekrany admina bez zmian.

## Key Decisions Made

| Decision                 | Choice                                                                                   | Why (1 sentence)                                                                                   |
| ------------------------ | ---------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Źródło danych kafelka    | Wiersz `Courses.Courses` (Id, Slug, PublishDate) + frontmatter YAML `<ContentPath>/<Slug>/<Slug>.md` | Zgodne z regułą „ścieżka z id z bazy”; metadane leżą obok treści; S-01/S-04 dostają encję        |
| Parser frontmattera      | YamlDotNet w `Shared.Abstractions` (+ lock-file'y)                                       | Popularny i odporny; ręczny parser byłby kruchy przy listach tagów                                 |
| Widoczność               | Tylko `PublishDate <= teraz`, przyszłe kursy niewidoczne                                  | Jedna reguła z FR-003; tytuły nieopublikowanych materiałów nie wyciekają                           |
| Semantyka „dziś”         | `datetime2` UTC z godziną, porównanie z `UtcNow` (przez `TimeProvider`)                  | Decyzja użytkownika: publikacja co do godziny, jak w prototypie (`04:00Z`)                          |
| Grafiki                  | `wwwroot/media/courses/<slug>/<plik>`, URL z serwera, klient dokleja `VITE_API_URL`      | Dozwolone przez CLAUDE.md, deploy już chroni media, brak nowego endpointu                          |
| Kolejność                | `PublishDate` ↑, `Title` ↑ (pl-PL, bez wielkości liter), `Id` ↑                           | Odpowiada przebiegowi bootcampu, deterministyczna w testach                                        |
| Kliknięcie kafelka       | Nieklikalny do S-04                                                                      | Żadnego martwego linku ani przekierowania przez catch-all                                          |
| Nagłówki klienta         | Wywołanie kafelków bez `Authorization`                                                   | `apiFetch` wylogowałby gościa z wygasłą sesją przed wysłaniem żądania                               |
| Seed danych              | Brak; admin wstawia wiersze SQL-em                                                       | PRD: moduł startuje pusty, publikacja poza aplikacją                                                |

## Scope

**In scope:**
- Encja `Course`, konfiguracja EF, `DbSet`, migracja `CoursesInitial`.
- YamlDotNet, `ICourseFrontmatterReader` (walidacja sluga, ochrona przed path traversal, bez wyjątków).
- Wycinek `CourseTiles` i klucze `Courses:ContentPath`/`MediaUrlBase` w `appsettings.Example.json`.
- `EndpointTests.cs` dla `CourseTiles`.
- Klient: kontrakt TS, `ApiCoursesService`, `CourseTile`, `CoursesPage`, trasa, menu, testy Vitest.

**Out of scope:**
- Strona treści kursu i renderowanie Markdown (S-04); flagi i weryfikacja (S-01).
- Seed, endpoint grafik, zapowiedzi „Wkrótce”, cache, `SortOrder`.
- Lokalny prototyp `public/data/courses*` (ignorowany przez git).

## Architecture / Approach

Handler filtruje kursy po dacie w EF. Dla każdego kursu czytnik buduje ścieżkę wyłącznie ze sluga z bazy, czyta i parsuje frontmatter, a przy błędzie zwraca `null` z ostrzeżeniem. Handler pomija takie kursy, sortuje w pamięci (tytuł pochodzi z pliku) i zwraca publiczne DTO `{slug, title, shortDescription, tags, imageUrl}`. Klient pobiera listę bez Bearer i renderuje kafelki w stylu `GamesPage`.

## Phases at a Glance

| Phase                          | What it delivers                                              | Key risk                                                                     |
| ------------------------------ | ------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1. Model danych Kursów         | Encja, konfiguracja, `DbSet`, migracja `CoursesInitial`       | Konflikt snapshotu z równoległą migracją S-01                                 |
| 2. Publiczny endpoint kafelków | YamlDotNet, czytnik frontmattera, `CourseTiles`, testy        | Path traversal i błędne pliki wywalające listę; niezacommitowane lock-file'y |
| 3. Strona Kursy w kliencie     | Serwis, kafelek, strona, trasa, menu, testy Vitest            | Regresja współdzielonego menu i layoutu (FR-013/FR-014)                       |

**Prerequisites:** F-01 (zrobione); lokalna baza i `appsettings.json` do ręcznej weryfikacji; jeden przykładowy kurs (plik `.md` z frontmatterem + grafika).
**Estimated effort:** ~2–3 sesje, 3 fazy.

## Open Risks & Assumptions

- S-01 może równolegle dodać migrację `Courses*`. Zmiana, która trafi do `main` druga, regeneruje migrację.
- Zakładamy, że administrator dopisze frontmatter (`title`, `description`, `tags`, `image`) do plików `.md`. Obecne pliki prototypu go nie mają.
- Grafiki prototypu mają 2–3 MB. Optymalizacja obrazów jest po stronie publikującego.
- Slug dopuszcza `[a-z0-9_-]`. Prototyp ma `korpo-cte-3_1`.

## Success Criteria (Summary)

- Gość widzi „Kursy” w menu i opublikowane kafelki na `/courses`, nawet z wygasłą sesją.
- Literówka w katalogu kursu lub zły frontmatter ukrywa tylko ten kurs, a lista działa dalej.
- Wszystkie kroki CI przechodzą, a istniejące moduły, menu i ekrany admina działają bez zmian.
