# Publiczne kafelki kursów — plan implementacji

## Overview

Wycinek S-03 z roadmapy: odwiedzający bez konta wchodzi z menu głównego do „Kursy” i widzi kafelki opublikowanych kursów (tytuł, krótki opis, tagi, grafika). Administrator publikuje kurs poza aplikacją: wstawia wiersz SQL-em, wgrywa plik `.md` z frontmatterem do `Courses:ContentPath` i grafikę do `wwwroot/media/courses/<slug>/`. Wymagania PRD: FR-001, FR-010, FR-013, FR-014.

## Current State Analysis

- Moduł `App01.Modules.Courses` istnieje i jest wpięty w host oraz testy (F-01). Ma tylko techniczny `Features/ModuleHello` (`src/server/App01/App01.Modules.Courses/ModuleDI.cs:20-25`).
- Nie ma encji, konfiguracji EF, `DbSet` ani migracji Kursów. Ostatnia migracja to `20260927063754_LottoDicts`. `AppDbContext` ma sekcje `// Portal` i `// Lotto` (`src/server/App01/App01.Shared.Infrastructure/Repositories/AppDbContext.cs:15-29`).
- W solucji nie ma parsera YAML ani frontmattera. Pakiety NuGet są przypięte lock-file'ami.
- Konfigurację czyta się przez `IConfiguration` (np. `OpenRouterService.cs:30-32`). W `appsettings.Example.json` nie ma sekcji `Courses`.
- W repo nie ma `wwwroot`. Deploy kopiuje `dist` → `publish/wwwroot` i nie kasuje plików istniejących tylko na serwerze, w tym „media w wwwroot” i „treść kursów” (`.github/workflows/deploy.yml:105-107`, `:145-147`). `UseStaticFiles` jest wpięte w `Program.cs:79-87`.
- Klient nie ma trasy `/courses` (catch-all przekierowuje na `/`, `src/client/app01/src/main.tsx:65`) ani pozycji w menu (`menuItems`, `src/client/app01/src/components/Layout.tsx:261-267`). Layout ma już wyłącznik tła dla `/courses` (`Layout.tsx:578-580`).
- `apiFetch` przy niepustym Bearer i nieważnej sesji wylogowuje i przekierowuje do logowania przed wysłaniem żądania (`src/client/app01/src/services/api-fetch.ts:48-52`). Publiczne wywołanie nie może więc wysyłać `Authorization`.
- Lokalny prototyp `src/client/app01/public/data/courses.json` (42 wpisy) i katalogi `public/data/courses/*` są w `.gitignore` (`src/client/app01/.gitignore:26`). Ta zmiana ich nie używa ani nie usuwa.

## Desired End State

- `GET api/courses/course-tiles` z nagłówkiem `X-TOKEN`, bez JWT, zwraca 200 i listę kafelków.
  - Na liście są tylko kursy z `PublishDate <= UtcNow` i poprawnym plikiem.
  - Kolejność: `PublishDate` rosnąco, potem `Title`, potem `Id`.
  - Kafelek ma wyłącznie pola publiczne: `Slug`, `Title`, `ShortDescription`, `Tags`, `ImageUrl`.
- Bez `X-TOKEN` endpoint zwraca 403.
- Kurs z brakującym plikiem, błędnym frontmatterem albo niedozwolonym slugiem jest logowany ostrzeżeniem i pomijany. Lista nigdy nie zwraca 500 z tego powodu.
- Strona `/courses` jest publiczna i dostępna z pozycji „Kursy” w menu (desktop i mobile). Pokazuje nieklikalne kafelki oraz stany ładowania, błędu i pustej listy.
- Istniejące pozycje menu, Apki, Gry, logowanie oraz ekrany Users i Rejestracja działają bez zmian.
- Pełny zestaw kroków CI przechodzi lokalnie.

### Key Discoveries:

- Wzorzec publicznego endpointu: `App01.Modules.Portal/Features/MailFromClientAdd/Endpoint.cs:14-23` (tylko `XTokenFilter`). Jego test: `tests/server/App01/App01.Api.Tests/Features/Portal/AddMailFromClient/EndpointTests.cs:26-124`.
- Wzorzec testu z seedowaniem InMemory i stałą nazwą bazy na fabrykę: `tests/server/App01/App01.Api.Tests/Features/Portal/UserList/EndpointTests.cs:36-92`. Wspólne `TestWebApplicationFactory` tworzą `Guid.NewGuid()` wewnątrz lambdy opcji, czyli nową bazę na scope. Nie używać ich do testów z seedem.
- `XTokenService` rzuca `ForbiddenException` (→ 403), gdy `Tokens:X-TOKEN` nie jest skonfigurowany. Testy muszą ustawić ten klucz.
- Wzorzec encji i konfiguracji: `Entities/Portal/Mail.cs`, `Configurations/Portal/MailConfiguration.cs` (`ToTable(..., "<Schemat>")`, jawne typy kolumn). Namespace konfiguracji to `App01.Shared.Application.Repositories.Configurations.<Moduł>`, mimo że folder leży w Infrastructure. Trzymać się istniejącej konwencji.
- Wzorzec kafelka z obrazem w UI: `src/client/app01/src/pages/games/GamesPage.tsx:65-93`. Wzorzec ładowania listy (isLoading/error): `src/client/app01/src/pages/portal/user/UserPage.tsx:12-67`, `:289-302`.
- Wzorzec testu komponentu: `src/client/app01/src/components/ConfirmModal.test.tsx`. Żaden istniejący test nie mockuje serwisów. Test strony użyje `vi.mock` serwisu i `MemoryRouter`.

## What We're NOT Doing

- Strony szczegółowej kursu i renderowania treści Markdown (S-04). Kafelek nie jest klikalny.
- Flag, zadań i weryfikacji odpowiedzi (S-01).
- Seedowania kursów migracją. Moduł startuje pusty (PRD: brak migracji danych), kursy wstawia administrator SQL-em.
- Endpointu serwującego grafiki. Grafiki serwuje istniejące `UseStaticFiles` z `wwwroot/media/courses/`.
- Zapowiedzi kursów z przyszłą datą („Wkrótce”). Przyszłe kursy są niewidoczne.
- Cache metadanych. Pliki są czytane przy każdym żądaniu, bo skala jest mała.
- Pola `SortOrder` i sortowania konfigurowanego przez admina.
- Migracji lub usuwania lokalnego prototypu `public/data/courses*` (ignorowany przez git).
- Zmian w `ExceptionHandlingMiddleware`, `apiFetch`, istniejących trasach i pozycjach menu.

## Implementation Approach

Dane kafelka łączą dwa źródła:
- **Wiersz w bazie** decyduje o istnieniu kursu, slugu i dacie publikacji (UTC, z godziną).
- **Frontmatter YAML pliku `<ContentPath>/<Slug>/<Slug>.md`** daje tytuł, krótki opis, tagi i nazwę pliku grafiki.

Handler:
1. Pobiera z bazy kursy z `PublishDate <= TimeProvider.GetUtcNow()`.
2. Dla każdego kursu woła czytnik frontmattera, który waliduje slug, buduje ścieżkę wyłącznie z wartości z bazy i nie rzuca wyjątków.
3. Pomija kursy z błędem, sortuje i mapuje na publiczne DTO.

YamlDotNet (dodany w `Shared.Abstractions`) parsuje sam blok frontmattera. Czytnik jest serwisem modułu, poza katalogiem `Features`, bo S-04 użyje go do odcięcia frontmattera od treści.

Klient wywołuje endpoint bez `Authorization`, żeby wygasła sesja nie wyrzucała gościa do logowania. Do `ImageUrl` dokleja `VITE_API_URL`.

## Critical Implementation Details

- **Kolejność filtrowania i sortowania:** filtr daty działa w zapytaniu EF. Sortowanie po `Title` musi być w pamięci, po odczycie plików, bo tytuł pochodzi z frontmattera, a nie z bazy. Remis `PublishDate` i `Title` rozstrzyga `Id`.
- **Bezpieczeństwo ścieżek:** slug z bazy musi pasować do `^[a-z0-9][a-z0-9_-]*$`. Prototyp ma slug `korpo-cte-3_1`, stąd dopuszczony `_`. Nazwa grafiki z frontmattera nie może zawierać `/`, `\` ani `..`, w przeciwnym razie `ImageUrl = null` i ostrzeżenie w logu. Ścieżka pliku po `Path.GetFullPath` musi leżeć wewnątrz `ContentPath`.
- **Publiczny klient:** metoda serwisu dla kafelków wysyła tylko `Content-Type` i `X-TOKEN`, bez nagłówka `Authorization`, nawet gdy w `localStorage` jest token.

## Phase 1: Model danych Kursów

### Overview

Encja `Course` z konfiguracją EF, `DbSet` i migracją `CoursesInitial`. Najpierw sprawdzamy, że schemat powstaje czysto, a dopiero potem dokładamy logikę.

### Changes Required:

#### 1. Encja

**File**: `src/server/App01/App01.Shared.Application/Entities/Courses/Course.cs`

**Intent**: Rekord kursu opublikowanego przez administratora: identyfikator dla plików i data publikacji. Metadane prezentacyjne żyją w pliku.

**Contract**: `namespace App01.Shared.Application.Entities.Courses; public class Course { int Id; string Slug; DateTime PublishDate /* UTC */ }` (wzorzec `required` jak w `Mail.cs`).

#### 2. Konfiguracja EF

**File**: `src/server/App01/App01.Shared.Infrastructure/Repositories/Configurations/Courses/CourseConfiguration.cs`

**Intent**: Tabela w schemacie modułu, unikalny slug (jeden kurs na katalog).

**Contract**:
- `ToTable("Courses", "Courses")`.
- `Id` jako `int` identity.
- `Slug`: `varchar(100)`, required, unikalny indeks.
- `PublishDate`: `datetime2`, required.
- Namespace zgodny z istniejącą konwencją: `App01.Shared.Application.Repositories.Configurations.Courses`.

#### 3. DbSet

**File**: `src/server/App01/App01.Shared.Infrastructure/Repositories/AppDbContext.cs`

**Intent**: Nowa sekcja `// Courses` z `DbSet<Course> Courses { get; set; } = null!;`.

**Contract**: `AppDbContext.Courses`.

#### 4. Migracja

**File**: `src/server/App01/App01.Shared.Infrastructure/Migrations/<timestamp>_CoursesInitial.cs` (+ snapshot)

**Intent**: Wygenerowana komendą z CLAUDE.md, bez ręcznych edycji i bez danych seed.

**Contract**: `dotnet ef migrations add CoursesInitial --project src/server/App01/App01.Shared.Infrastructure --startup-project src/server/App01/App01.Bootstrapper.Api`. Tworzy tylko schemat `Courses` i tabelę `Courses.Courses` z indeksem unikalnym.

### Success Criteria:

#### Automated Verification:

- Migracja `CoursesInitial` wygenerowana i zawiera wyłącznie tabelę `Courses.Courses` z unikalnym indeksem na `Slug`
- `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete`
- `dotnet test APPS.sln` przechodzi (brak regresji)
- `dotnet format APPS.sln --verify-no-changes` przechodzi

#### Manual Verification:

- `dotnet ef database update` na lokalnej bazie tworzy tabelę `Courses.Courses`, a host startuje i istniejące moduły działają

**Implementation Note**: Po automatycznej weryfikacji zatrzymaj się na ręczne potwierdzenie przed fazą 2.

---

## Phase 2: Publiczny endpoint kafelków

### Overview

Czytnik frontmattera, wycinek `CourseTiles`, klucze konfiguracji i testy endpointu.

### Changes Required:

#### 1. Pakiet YamlDotNet

**File**: `src/server/App01/App01.Shared.Abstractions/App01.Shared.Abstractions.csproj` (+ wszystkie zmienione `packages.lock.json`)

**Intent**: Parser YAML dla frontmattera. Pakiety NuGet dodajemy w `Shared.Abstractions`.

**Contract**: `PackageReference Include="YamlDotNet"` w aktualnej stabilnej wersji (przypiętej). Commit obejmuje zaktualizowane lock-file'y, bo CI robi `restore --locked-mode`.

#### 2. Konfiguracja

**File**: `src/server/App01/App01.Bootstrapper.Api/appsettings.Example.json`

**Intent**: Nowa sekcja z przykładowymi wartościami.

**Contract**: `"Courses": { "ContentPath": "<ścieżka poza wwwroot, np. ../courses-content>", "MediaUrlBase": "/media/courses" }`.
- Ścieżka względna `ContentPath` jest liczona względem `AppContext.BaseDirectory`.
- Brak klucza `ContentPath` → ostrzeżenie w logu i pusta lista (nie 500).

#### 3. Czytnik frontmattera

**File**: `src/server/App01/App01.Modules.Courses/Content/CourseFrontmatterReader.cs` (+ rejestracja w `ModuleDI.cs`)

**Intent**: Dla sluga z bazy bezpiecznie odczytuje `<ContentPath>/<Slug>/<Slug>.md`, parsuje blok `---…---` z początku pliku (z tolerancją BOM i CRLF) i zwraca metadane albo `null` z ostrzeżeniem w logu. Nigdy nie rzuca wyjątku do handlera.

**Contract**:
- Interfejs: `ICourseFrontmatterReader.ReadAsync(string slug, CancellationToken) → Task<CourseFrontmatter?>`.
- `CourseFrontmatter(string Title, string Description, IReadOnlyList<string> Tags, string? Image)`.
- Schemat YAML: `title` (wymagany, niepusty), `description` (wymagany, niepusty), `tags` (lista, opcjonalna → pusta), `image` (nazwa pliku, opcjonalna). Nieznane klucze ignorowane (`IgnoreUnmatchedProperties`), żeby S-04 mógł dokładać pola.
- `null`, gdy: slug nie pasuje do `^[a-z0-9][a-z0-9_-]*$`, ścieżka wychodzi poza `ContentPath`, brak pliku, brak bloku frontmattera, błąd YAML albo brak wymaganych pól.
- Rejestracja: `AddSingleton<ICourseFrontmatterReader, CourseFrontmatterReader>()` w `AddModuleCoursesServices` (serwis bezstanowy) oraz `services.TryAddSingleton(TimeProvider.System)`.

#### 4. Wycinek CourseTiles

**File**: `src/server/App01/App01.Modules.Courses/Features/CourseTiles/{Contracts,Validator,Handler,Endpoint}.cs`

**Intent**: Publiczna lista kafelków opublikowanych kursów, zgodnie ze wzorcem czterech plików.

**Contract**:
- `Contracts`:
  - `Request() : IRequest<Response>`
  - `Response(IReadOnlyList<CourseTileDto> Courses)`
  - `CourseTileDto(string Slug, string Title, string ShortDescription, IReadOnlyList<string> Tags, string? ImageUrl)`
- `Validator`: bez reguł (żądanie nie ma pól), z komentarzem jak w `ModuleHello`.
- `CourseTilesHandler`:
  1. Walidacja.
  2. EF z `cancellationToken`: kursy z `PublishDate <= now` (UTC z wstrzykniętego `TimeProvider`).
  3. Czytnik dla każdego kursu; `null` → pominięcie.
  4. Sortowanie: `PublishDate` ↑, `Title` ↑ (`StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true)` — niezależnie od kultury serwera), `Id` ↑.
  5. `ImageUrl = $"{MediaUrlBase}/{slug}/{image}"` albo `null`.
  Handler nie zwraca `PublishDate` ani niczego z treści.
- `Endpoint`:
  - `MapGet("api/courses/course-tiles")`
  - `.WithName("CoursesCourseTiles")`, `.WithTags("Courses")`
  - `.Produces<Contracts.Response>(200)`, `.Produces(403)`
  - `.AddEndpointFilter<XTokenFilter>()`, **bez** `RequireAuthorization()`
- Rejestracja: `Features.CourseTiles.Endpoint.AddEndpoint(app);` w `UseModuleCoursesEndpoints`.

#### 5. Testy endpointu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseTiles/EndpointTests.cs`

**Intent**: Pokrycie kontraktu i reguł pominięcia.

**Contract**:
- Fabryka jak w `UserList/EndpointTests.cs`: InMemory ze stałą nazwą bazy, seed `Course`. Konfiguracja: `Tokens:X-TOKEN`, `Courses:ContentPath` = katalog tymczasowy z plikami fixture tworzonymi w teście i sprzątanymi w `Dispose`, `Courses:MediaUrlBase`.
- `TimeProvider` podmieniony na stałą chwilę: `s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(fake)`, z prostą klasą pochodną `TimeProvider` w teście.
- Przypadki:
  - Sukces: 200 z pełnym kafelkiem (tytuł, opis, tagi, `ImageUrl` = `/media/courses/<slug>/<plik>`). Asercja treści JSON, nie samego statusu, bo fallback SPA też daje 200.
  - Kurs z `PublishDate` 1 minutę po „teraz” jest niewidoczny, kurs z `PublishDate == teraz` jest widoczny.
  - Kolejność: dwa kursy z tą samą datą sortują się po tytule, wcześniejsza data idzie pierwsza.
  - Pominięcia bez 500, pozostałe kursy dalej na liście: brak pliku, plik bez frontmattera, niepoprawny YAML, brak `title`, slug `../etc` / z separatorem.
  - Frontmatter bez `image` → `ImageUrl == null`. `image: ../x.png` → `ImageUrl == null`.
  - Pusta tabela → 200 i pusta lista.
  - Bez JWT → 200 (endpoint publiczny). Z niepoprawnym JWT → 200.
  - Bez `X-TOKEN` → 403. Z błędnym `X-TOKEN` → 403.
  - 400 nie dotyczy (żądanie bez pól), 401 nie dotyczy (endpoint publiczny). W teście komentarz, dlaczego tych przypadków brak.

### Success Criteria:

#### Automated Verification:

- `dotnet restore --locked-mode` przechodzi z zacommitowanymi `packages.lock.json`
- `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete`
- `dotnet test APPS.sln` przechodzi, w tym nowe `Features/Courses/CourseTiles/EndpointTests.cs`
- `dotnet format APPS.sln --verify-no-changes` przechodzi
- `appsettings.Example.json` zawiera sekcję `Courses` z `ContentPath` i `MediaUrlBase`

#### Manual Verification:

- Na lokalnej bazie po `INSERT INTO Courses.Courses (Slug, PublishDate) VALUES ('korpo-cte-300', '2026-08-28T04:00:00')` i wgraniu pliku z frontmatterem `GET api/courses/course-tiles` z `X-TOKEN` (Swagger/curl, bez JWT) zwraca kafelek
- Grafika z `wwwroot/media/courses/korpo-cte-300/` otwiera się pod zwróconym `ImageUrl`
- Kurs z literówką w nazwie katalogu znika z listy z ostrzeżeniem w logu, a reszta listy działa

**Implementation Note**: Po automatycznej weryfikacji zatrzymaj się na ręczne potwierdzenie przed fazą 3.

---

## Phase 3: Strona Kursy w kliencie

### Overview

Kontrakt TS, serwis API, strona z kafelkami, publiczna trasa i pozycja menu.

### Changes Required:

#### 1. Kontrakty TS

**File**: `src/client/app01/src/services/contracts/courses-course-tiles-response.ts`

**Intent**: Interfejsy 1:1 z rekordami C#.

**Contract**: `export interface CourseTileDto { slug: string; title: string; shortDescription: string; tags: string[]; imageUrl: string | null }`, `export interface CoursesCourseTilesResponse { courses: CourseTileDto[] }`. Request bez pól, więc bez pliku `-request.ts`.

#### 2. Serwis API

**File**: `src/client/app01/src/services/api-courses-service.ts`

**Intent**: `ApiCoursesService` na wzór `api-portal-service.ts`, z wywołaniami przez `apiFetch`. Metoda kafelków nie wysyła `Authorization`, żeby wygasła sesja nie przekierowywała gościa do logowania.

**Contract**: `class ApiCoursesService { constructor(apiUrl, appToken, usrToken?); setUsrToken(token); getCourseTiles(): Promise<CoursesCourseTilesResponse> }`. Prywatne `getPublicHeaders()` zwraca `Content-Type` i `X-TOKEN`. Pozostałe metody (S-04+) użyją `getHeaders()` z Bearer. Przy `!response.ok` rzuca `errorData?.message || "..."`.

#### 3. Komponent kafelka i strona

**File**: `src/client/app01/src/pages/courses/CoursesPage.tsx`, `src/client/app01/src/components/CourseTile.tsx`

**Intent**: Strona w stylu `GamesPage` z siatką nieklikalnych kafelków. Stany ładowania, błędu i pustej listy jak w `UserPage`. `document.title` i animacja `isVisible` jak na pozostałych stronach. Teksty po polsku.

**Contract**:
- `CourseTile({ tile, apiUrl, index, isVisible })` renderuje:
  - grafikę `apiUrl + imageUrl` albo neutralny placeholder, gdy `imageUrl === null` lub obraz się nie wczyta;
  - tytuł, opis i tagi jako etykiety.
- `CourseTile` nie ma linku ani `onClick`. Bazuje na istniejącym `Card`/`CardListItem`.
- `CoursesPage` (default export) woła `ApiCoursesService.getCourseTiles()` z `VITE_API_URL`/`VITE_APP_TOKEN`.
- Komunikaty: „Ładowanie...”, „Brak opublikowanych kursów”, komunikat błędu.

#### 4. Trasa i menu

**File**: `src/client/app01/src/main.tsx`, `src/client/app01/src/components/Layout.tsx`

**Intent**: Publiczna trasa i jedna nowa pozycja menu. Bez przestawiania istniejących pozycji.

**Contract**:
- `<Route path="courses" element={<CoursesPage />} />` wśród tras publicznych, poza `<RequireAuth>`.
- W `menuItems` wstawiony `{ label: "Kursy", to: "/courses" }` bezpośrednio po „Gry”. Względna kolejność pozostałych pozycji bez zmian.
- Desktop i mobile korzystają z tej samej tablicy. Pozycja admina „Users” nadal doklejana na końcu.

#### 5. Testy frontendu

**File**: `src/client/app01/src/components/CourseTile.test.tsx`, `src/client/app01/src/pages/courses/CoursesPage.test.tsx`

**Intent**: Zachowanie komponentów, bez sprawdzania klas Tailwind.

**Contract**: importy z `"vitest"`.
- `CourseTile`: renderuje tytuł, opis i każdy tag; przy `imageUrl: null` brak `<img>` z tym URL i obecny placeholder; brak roli `link`.
- `CoursesPage` (`vi.mock` serwisu, `MemoryRouter`):
  - lista dwóch kursów renderuje dwa tytuły w kolejności z odpowiedzi;
  - pusta lista daje „Brak opublikowanych kursów”;
  - odrzucony promise daje komunikat błędu.

### Success Criteria:

#### Automated Verification:

- `npx prettier --check "src/**/*.{ts,tsx,css}"` przechodzi (w `src/client/app01`)
- `npm run lint` przechodzi
- `npm test` przechodzi, w tym `CourseTile.test.tsx` i `CoursesPage.test.tsx`
- `npm run build` przechodzi (`tsc -b` + Vite)
- `npm audit --audit-level=high` bez nowych problemów
- `dotnet build APPS.sln` i `dotnet test APPS.sln` nadal przechodzą

#### Manual Verification:

- Niezalogowany odwiedzający widzi „Kursy” w menu (desktop i mobile), a `/courses` pokazuje kafelki z grafiką, tytułem, opisem i tagami; kafelek nie reaguje na kliknięcie
- Użytkownik z wygasłą sesją wchodzi na `/courses` i nie jest przekierowany do logowania
- Pozostałe pozycje menu (Home, Apki, Gry, O mnie, Kontakt) są w tej samej kolejności i działają; Flashcard, Lotto, AgentPLN, Invaders działają jak przed zmianą (FR-013)
- Zalogowany admin nadal widzi i otwiera Users oraz Rejestrację, a zwykły użytkownik ich nie widzi (FR-014); logowanie i wylogowanie bez zmian (FR-012)
- Strona `/courses` nie ma animowanego tła (istniejący wyłącznik), a kafelki są czytelne na mobile

**Implementation Note**: Po automatycznej weryfikacji zatrzymaj się na ręczne potwierdzenie przed zamknięciem zmiany.

---

## Testing Strategy

### Unit Tests:

- Frontend: `CourseTile` (dane, placeholder, brak linku) i `CoursesPage` (lista, pusta lista, błąd) z mockiem serwisu.

### Integration Tests:

- `CourseTiles/EndpointTests.cs` przez `WebApplicationFactory` + EF InMemory + fixture plików w katalogu tymczasowym + podmieniony `TimeProvider`. Pokrywa filtr daty (granica równości), kolejność z remisem, wszystkie powody pominięcia, publiczny dostęp i `X-TOKEN`.

### Manual Testing Steps:

1. Wstaw kurs SQL-em, wgraj `<ContentPath>/<slug>/<slug>.md` z frontmatterem i grafikę do `wwwroot/media/courses/<slug>/`.
2. Otwórz `/courses` w oknie prywatnym (bez logowania): kafelek widoczny.
3. Zmień `PublishDate` na jutro: kafelek znika. Zepsuj nazwę katalogu: kafelek znika, w logu jest ostrzeżenie, inne kursy zostają.
4. Przejdź po wszystkich pozycjach menu oraz Apki i Gry. Jako admin otwórz Users i Rejestrację.

## Performance Considerations

Przy skali „small” (kilkadziesiąt kursów) odczyt kilkudziesięciu małych plików na żądanie jest akceptowalny. Pełny odczyt pliku tylko po to, żeby wyciąć frontmatter, też jest akceptowalny. Gdyby lista urosła, cache po `LastWriteTime` jest do dodania później (poza zakresem). Grafiki prototypu mają 2–3 MB. Optymalizacja obrazów to zadanie administratora przy publikacji, nie kodu.

## Migration Notes

- Nowa migracja `CoursesInitial` tylko dodaje schemat i tabelę, bez danych. Wycofanie to `dotnet ef database update LottoDicts`.
- S-01 może równolegle dodawać migrację `Courses*`. Przy konflikcie snapshotu modelu ta zmiana, która trafi do `main` druga, regeneruje swoją migrację zamiast ręcznie łączyć snapshot.

## References

- Roadmapa: `context/foundation/roadmap.md` (S-03), PRD: `context/foundation/prd.md` (FR-001, FR-010, FR-013, FR-014, OQ-2)
- Poprzednia zmiana: `context/changes/courses-module-skeleton/plan.md`
- Publiczny endpoint: `src/server/App01/App01.Modules.Portal/Features/MailFromClientAdd/Endpoint.cs:14-23`
- Wzorzec testów: `tests/server/App01/App01.Api.Tests/Features/Portal/UserList/EndpointTests.cs:36-92`, `tests/server/App01/App01.Api.Tests/Features/Portal/AddMailFromClient/EndpointTests.cs:26-124`
- Kafelek UI: `src/client/app01/src/pages/games/GamesPage.tsx:65-93`; menu: `src/client/app01/src/components/Layout.tsx:261-267`
- `apiFetch` i pre-check sesji: `src/client/app01/src/services/api-fetch.ts:41-63`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Model danych Kursów

#### Automated

- [x] 1.1 Migracja `CoursesInitial` wygenerowana i zawiera wyłącznie tabelę `Courses.Courses` z unikalnym indeksem na `Slug` — 6f09e2a
- [x] 1.2 `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete` — 6f09e2a
- [x] 1.3 `dotnet test APPS.sln` przechodzi (brak regresji) — 6f09e2a
- [x] 1.4 `dotnet format APPS.sln --verify-no-changes` przechodzi — 6f09e2a

#### Manual

- [x] 1.5 `dotnet ef database update` na lokalnej bazie tworzy tabelę `Courses.Courses`, a host startuje i istniejące moduły działają — 6f09e2a

### Phase 2: Publiczny endpoint kafelków

#### Automated

- [x] 2.1 `dotnet restore --locked-mode` przechodzi z zacommitowanymi `packages.lock.json` — 519a8dd
- [x] 2.2 `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete` — 519a8dd
- [x] 2.3 `dotnet test APPS.sln` przechodzi, w tym nowe `Features/Courses/CourseTiles/EndpointTests.cs` — 519a8dd
- [x] 2.4 `dotnet format APPS.sln --verify-no-changes` przechodzi — 519a8dd
- [x] 2.5 `appsettings.Example.json` zawiera sekcję `Courses` z `ContentPath` i `MediaUrlBase` — 519a8dd

#### Manual

- [x] 2.6 Na lokalnej bazie po `INSERT INTO Courses.Courses (Slug, PublishDate) VALUES ('korpo-cte-300', '2026-08-28T04:00:00')` i wgraniu pliku z frontmatterem `GET api/courses/course-tiles` z `X-TOKEN` (Swagger/curl, bez JWT) zwraca kafelek — 519a8dd
- [x] 2.7 Grafika z `wwwroot/media/courses/korpo-cte-300/` otwiera się pod zwróconym `ImageUrl` — 519a8dd
- [x] 2.8 Kurs z literówką w nazwie katalogu znika z listy z ostrzeżeniem w logu, a reszta listy działa — 519a8dd

### Phase 3: Strona Kursy w kliencie

#### Automated

- [x] 3.1 `npx prettier --check "src/**/*.{ts,tsx,css}"` przechodzi (w `src/client/app01`)
- [x] 3.2 `npm run lint` przechodzi
- [x] 3.3 `npm test` przechodzi, w tym `CourseTile.test.tsx` i `CoursesPage.test.tsx`
- [x] 3.4 `npm run build` przechodzi (`tsc -b` + Vite)
- [x] 3.5 `npm audit --audit-level=high` bez nowych problemów
- [x] 3.6 `dotnet build APPS.sln` i `dotnet test APPS.sln` nadal przechodzą

#### Manual

- [x] 3.7 Niezalogowany odwiedzający widzi „Kursy” w menu (desktop i mobile), a `/courses` pokazuje kafelki z grafiką, tytułem, opisem i tagami; kafelek nie reaguje na kliknięcie
- [x] 3.8 Użytkownik z wygasłą sesją wchodzi na `/courses` i nie jest przekierowany do logowania
- [x] 3.9 Pozostałe pozycje menu (Home, Apki, Gry, O mnie, Kontakt) są w tej samej kolejności i działają; Flashcard, Lotto, AgentPLN, Invaders działają jak przed zmianą (FR-013)
- [x] 3.10 Zalogowany admin nadal widzi i otwiera Users oraz Rejestrację, a zwykły użytkownik ich nie widzi (FR-014); logowanie i wylogowanie bez zmian (FR-012)
- [x] 3.11 Strona `/courses` nie ma animowanego tła (istniejący wyłącznik), a kafelki są czytelne na mobile
