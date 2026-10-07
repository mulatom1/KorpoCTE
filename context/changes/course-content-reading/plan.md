# Czytanie treści kursu — plan implementacji

## Overview

Wycinek S-04 z roadmapy. Zalogowany użytkownik otwiera `/courses/:slug` z kafelka i czyta sformatowaną treść opublikowanego kursu (`PublishDate <= teraz`, UTC). Kurs nieopublikowany albo nieistniejący jest niewidoczny (404). Niezalogowany trafia do logowania i po nim wraca na stronę kursu. Treść kursu nie może wyciec przez publiczne pliki statyczne. Wymagania PRD: FR-002, FR-003, FR-010, FR-012.

## Current State Analysis

- S-03 (zarchiwizowany) dostarczył:
  - encję `Course` (Id, Slug unikalny, PublishDate UTC);
  - czytnik `ICourseFrontmatterReader.ReadAsync(slug)` zwracający `CourseFrontmatter(Title, Description, Tags, Image)` (`src/server/App01/App01.Modules.Courses/Content/CourseFrontmatterReader.cs:13-23`, `:54-108`);
  - publiczny endpoint kafelków (`Features/CourseTiles/Handler.cs:70`);
  - stronę `/courses` z nieklikalnym `CourseTile`, którego test sprawdza „nie jest linkiem” (`src/client/app01/src/components/CourseTile.test.tsx:47`).
- **Wyciek treści:** lokalny `appsettings.Development.json` ma `Courses:ContentPath` = `.../App01.Bootstrapper.Api/wwwroot/media/courses`. `GET /media/courses/korpo-cte-300/korpo-cte-300.md` zwraca 200 `text/markdown` bez logowania. Kod nie broni się przed taką konfiguracją. Łamie to guardrail PRD i regułę CLAUDE.md („treść kursów poza wwwroot”).
- **Ścieżki obrazków** w istniejącej treści pochodzą z prototypu (`../../../data/courses/korpo-cte-300/images/01.png`, `../../../images/przypadek.png`) i po przeniesieniu plików nie działają.
- **Logowanie z powrotem już działa:** `RequireAuth` przekierowuje na `/login?returnUrl=...` (`src/client/app01/src/utils/auth.ts:76-82`), a `UserLoginPage` po zalogowaniu robi `navigate(returnUrl)` (`src/client/app01/src/pages/portal/auth/UserLoginPage.tsx:18,59`).
- **Pakiety do Markdown** są zainstalowane, ale nieużywane: `react-markdown` 10, `remark-gfm`, `rehype-highlight`, `highlight.js` (`src/client/app01/package.json`). Nie ma pluginu typography ani zaimportowanego motywu highlight.js.
- Trasy chronione siedzą w `<Route element={<RequireAuth />}>` (`src/client/app01/src/main.tsx:55`).

## Desired End State

- `GET api/courses/course-content?slug=<slug>` z JWT i `X-TOKEN` zwraca 200: `{ slug, title, tags, content, mediaBaseUrl }`.
  - `content` to Markdown bez frontmattera.
  - `mediaBaseUrl` = `{Courses:MediaUrlBase}/{slug}`.
- Kody błędów:
  - 400 przy pustym lub niepoprawnym slugu;
  - 401 bez JWT;
  - 403 bez `X-TOKEN`;
  - 404, gdy kurs nie istnieje, ma `PublishDate > teraz`, ma brakujący lub błędny plik albo wyłączył go bezpiecznik.
- Bezpiecznik: gdy `Courses:ContentPath` leży wewnątrz `WebRootPath`, czytnik loguje błąd i nie czyta żadnego kursu. Lista kafelków jest wtedy pusta, a treść daje 404.
- `/courses/:slug` (za `RequireAuth`) pokazuje tytuł, tagi, przycisk „Powrót do kursów” i treść Markdown z GFM i podświetlaniem kodu, bez surowego HTML.
- Względne ścieżki obrazków i linków (`images/01.png`) wskazują na `{VITE_API_URL}{mediaBaseUrl}/images/01.png`. Ścieżki absolutne (`/images/...`, `https://...`) zostają bez zmian.
- Kafelek na `/courses` jest linkiem do `/courses/:slug`. Niezalogowany po kliknięciu loguje się i wraca na kurs.
- Logowanie, menu, Apki, Gry i ekrany admina działają bez zmian.

### Key Discoveries:

- Wzorzec endpointu z JWT: `src/server/App01/App01.Modules.Courses/Features/ModuleHello/Endpoint.cs` (`RequireAuthorization` + `XTokenFilter`). Wzorzec testu 200/401/403: `tests/server/App01/App01.Api.Tests/Features/Courses/ModuleHello/EndpointTests.cs`.
- Testy S-03 mają fabrykę z InMemory, katalogiem tymczasowym fixture i podmienionym `TimeProvider`: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseTiles/EndpointTests.cs:31-160`.
- `NotFoundException` → 404 i `ValidationException` → 400 mapuje `ExceptionHandlingMiddleware` (CLAUDE.md).
- `react-markdown` 10 ma prop `urlTransform`. Własna transformacja musi na końcu wołać `defaultUrlTransform`, żeby nie przepuścić `javascript:`.

## What We're NOT Doing

- Renderowania surowego HTML (`rehype-raw`) ani `rehype-sanitize`. HTML w treści jest wyświetlany jako tekst.
- Blokowania `.md` w `UseStaticFiles` ani innych zmian w `Program.cs`. Ochronę zapewnia bezpiecznik w czytniku i konfiguracja.
- Przepisywania ścieżek obrazków po stronie serwera.
- Ochrony obrazków kursów. `wwwroot/media` pozostaje publiczne zgodnie z CLAUDE.md, publiczna jest tylko treść tekstowa.
- Spisu treści, postępu czytania i zadań przy kursie (S-01).
- Cache treści.
- Zmian w logowaniu, `RequireAuth`, `apiFetch` i `UserLoginPage`.
- Automatycznej migracji istniejących plików treści. Przeniesienie `.md` i poprawę ścieżek wykonuje administrator (krok ręczny).

## Implementation Approach

Serwer:
- Czytnik dostaje jedną metodę zwracającą metadane i treść. Korzysta z niej handler kafelków (ignoruje treść) i nowy handler treści.
- Bezpiecznik w czytniku porównuje pełną ścieżkę `ContentPath` z `IWebHostEnvironment.WebRootPath`. Działa dla obu endpointów naraz.
- Handler treści szuka kursu w bazie po slugu z requestu, ale ścieżkę pliku buduje ze sluga z encji (reguła CLAUDE.md). Każdy powód niedostępności daje ten sam 404, żeby nie zdradzać istnienia nieopublikowanych kursów.

Klient:
- Nowa strona pobiera treść metodą serwisu z Bearer (401 obsługuje `apiFetch`).
- Renderuje ją komponentem `CourseMarkdown`, który mapuje elementy na klasy Tailwind i przepisuje względne URL-e helperem z `src/utils/`.

## Critical Implementation Details

- **Bezpiecznik ścieżek:** oba katalogi przez `Path.GetFullPath`, z separatorem na końcu, porównanie bez wielkości liter na Windows. `ContentPath` równy `WebRootPath` albo leżący w nim → `LogError` i `null` dla każdego sluga. Pusty `WebRootPath` (brak `wwwroot`) wyłącza sprawdzenie.
- **Kolejność w handlerze treści:**
  1. walidacja (400);
  2. zapytanie EF: `Slug == request.Slug && PublishDate <= now`;
  3. brak → `NotFoundException`;
  4. czytnik z `course.Slug` (z encji);
  5. `null` → `NotFoundException`.
  Komunikat 404 taki sam we wszystkich przypadkach.
- **`urlTransform`:** URL jest względny, gdy nie zaczyna się od `/`, `#`, `?` ani schematu (`^[a-z][a-z0-9+.-]*:`). Względny → `${apiUrl}${mediaBaseUrl}/${url}` (bez podwójnych `/`). Każdy wynik przechodzi przez `defaultUrlTransform`.

## Phase 1: Serwer — treść kursu i bezpiecznik

### Overview

Rozszerzony czytnik, bezpiecznik `wwwroot`, wycinek `CourseContent` i testy.

### Changes Required:

#### 1. Czytnik treści kursu

**File**: `src/server/App01/App01.Modules.Courses/Content/CourseFrontmatterReader.cs` (+ dostosowanie `Features/CourseTiles/Handler.cs`)

**Intent**: Czytnik zwraca metadane i treść Markdown bez frontmattera oraz odmawia pracy, gdy katalog treści leży w publicznym `wwwroot`. Handler kafelków przechodzi na nowy typ bez zmiany zachowania.

**Contract**:
- `CourseFrontmatter` → `CourseDocument(string Title, string Description, IReadOnlyList<string> Tags, string? Image, string Body)`.
- `ICourseFrontmatterReader.ReadAsync(string slug, CancellationToken) → Task<CourseDocument?>`.
- `Body` to tekst po zamykającym `---`, z normalizacją CRLF→LF, bez BOM i bez wiodących pustych linii.
- Konstruktor dostaje `IWebHostEnvironment`. Bezpiecznik działa zgodnie z „Critical Implementation Details”.
- Dotychczasowe reguły (slug, granica `ContentPath`, brak wyjątków) bez zmian.

#### 2. Wycinek CourseContent

**File**: `src/server/App01/App01.Modules.Courses/Features/CourseContent/{Contracts,Validator,Handler,Endpoint}.cs` (+ rejestracja w `ModuleDI.UseModuleCoursesEndpoints`)

**Intent**: Treść opublikowanego kursu dla zalogowanego użytkownika.

**Contract**:
- `Contracts`:
  - `Request(string Slug) : IRequest<Response>`
  - `Response(string Slug, string Title, IReadOnlyList<string> Tags, string Content, string MediaBaseUrl)`
- `Validator`: `Slug` niepusty, ≤ 100 znaków, regex `^[a-z0-9][a-z0-9_-]*$` (reużyć `CourseFrontmatterReader.IsValidSlug`), komunikaty po polsku.
- `CourseContentHandler`:
  - wstrzykuje `ILogger`, `IValidator`, `AppDbContext`, `ICourseFrontmatterReader`, `TimeProvider`, `IConfiguration`;
  - kolejność kroków zgodnie z „Critical Implementation Details”;
  - `MediaBaseUrl` budowany jak w handlerze kafelków (domyślnie `/media/courses`, bez końcowego `/`) + `/{slug}`.
- `Endpoint`:
  - `MapGet("api/courses/course-content", async ([AsParameters] Contracts.Request request, IMediator mediator) => ...)`
  - `.WithName("CoursesCourseContent")`, `.WithTags("Courses")`
  - `.Produces<Contracts.Response>(200)` + `.Produces(400/401/403/404)`
  - `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()`
  - Jeśli `[AsParameters]` z rekordem pozycyjnym nie zadziała, przyjąć `string slug` z query i zbudować `Request` w lambdzie.

#### 3. Testy

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs` (+ nowy przypadek w `Features/Courses/CourseTiles/EndpointTests.cs`)

**Intent**: Pokrycie kontraktu, granicy publikacji i bezpiecznika.

**Contract**: fabryka jak w testach `CourseTiles` (InMemory ze stałą nazwą, fixture w katalogu tymczasowym, fake `TimeProvider`) + JWT jak w `ModuleHello`. Przypadki:
- Sukces: 200 z JWT i `X-TOKEN`. `title` i `tags` z frontmattera, `content` bez bloku `---` (zaczyna się od `# `), `mediaBaseUrl == "/media/courses/<slug>"`. Asercja treści JSON.
- `PublishDate == teraz` → 200. `PublishDate` minutę po „teraz” → 404.
- Slug nieistniejący w bazie → 404. Kurs bez pliku → 404. Błędny frontmatter → 404.
- Slug niepoprawny (`Wielkie`, `../etc`) → 400. Brak parametru `slug` → 400.
- Bez JWT → 401. Niepoprawny JWT → 401.
- Bez `X-TOKEN` → 403.
- Bezpiecznik: host testowy z webroot ustawionym na katalog tymczasowy (`UseSetting(WebHostDefaults.WebRootKey, …)`) i `ContentPath` wewnątrz niego → treść 404. W `CourseTiles/EndpointTests.cs` ten sam układ → pusta lista.

### Success Criteria:

#### Automated Verification:

- `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete`
- `dotnet test APPS.sln` przechodzi, w tym nowe `Features/Courses/CourseContent/EndpointTests.cs` i test bezpiecznika w `CourseTiles`
- `dotnet format APPS.sln --verify-no-changes` przechodzi

#### Manual Verification:

- Przy obecnej konfiguracji (`ContentPath` w `wwwroot/media/courses`) `/api/courses/course-tiles` zwraca pustą listę, a w logu jest błąd bezpiecznika
- Po przeniesieniu `.md` poza `wwwroot` (np. `C:/PROJEKTY/courses-content/<slug>/<slug>.md`) i zmianie `ContentPath` lista kafelków wraca, a `GET /media/courses/<slug>/<slug>.md` zwraca 404
- `GET api/courses/course-content?slug=korpo-cte-300` przez Swaggera z JWT zwraca treść, a bez JWT zwraca 401

**Implementation Note**: Po automatycznej weryfikacji zatrzymaj się na ręczne potwierdzenie przed fazą 2.

---

## Phase 2: Klient — strona kursu

### Overview

Kontrakty, metoda serwisu, helper URL mediów, komponent Markdown, strona szczegółowa, trasa chroniona i kafelek jako link.

### Changes Required:

#### 1. Kontrakty TS

**File**: `src/client/app01/src/services/contracts/courses-course-content-request.ts`, `courses-course-content-response.ts`

**Intent**: Interfejsy 1:1 z rekordami C#.

**Contract**: `CoursesCourseContentRequest { slug: string }`, `CoursesCourseContentResponse { slug: string; title: string; tags: string[]; content: string; mediaBaseUrl: string }`.

#### 2. Metoda serwisu

**File**: `src/client/app01/src/services/api-courses-service.ts`

**Intent**: Pobranie treści z nagłówkami z Bearer (`getHeaders()`), przez `apiFetch`. Wygasła sesja → redirect do logowania obsłużony przez `apiFetch`.

**Contract**: `getCourseContent(request: CoursesCourseContentRequest): Promise<CoursesCourseContentResponse>`. Query przez `URLSearchParams`. Przy `!response.ok` rzuca `Error` z komunikatem serwera. Dla 404 komunikat ma brzmieć „Kurs nie istnieje lub nie jest jeszcze opublikowany”.

#### 3. Helper URL mediów

**File**: `src/client/app01/src/utils/courseMediaUrl.ts` + `courseMediaUrl.test.ts`

**Intent**: Czysta funkcja zamieniająca względne URL-e z treści na adresy mediów kursu. Pokryta testami jednostkowymi.

**Contract**:
- `resolveCourseMediaUrl(url: string, apiUrl: string, mediaBaseUrl: string): string`, zgodnie z „Critical Implementation Details”, bez `defaultUrlTransform` (ten dokłada komponent).
- Testy:
  - `images/01.png` → `https://api.test/media/courses/kurs/images/01.png`;
  - `./thumbnail.png` → bez `./`;
  - `/images/przypadek.png` bez zmian;
  - `https://x.pl/a.png` bez zmian;
  - `#sekcja` bez zmian;
  - `mailto:a@b.pl` bez zmian;
  - pusty `apiUrl` → `/media/courses/kurs/images/01.png`.

#### 4. Komponent CourseMarkdown

**File**: `src/client/app01/src/components/CourseMarkdown.tsx` + `CourseMarkdown.test.tsx`

**Intent**: Render treści kursu istniejącym zestawem pakietów. Bez surowego HTML, czytelna typografia przez mapowanie elementów na klasy Tailwind.

**Contract**:
- `CourseMarkdown({ content, apiUrl, mediaBaseUrl })`.
- `ReactMarkdown` z `remarkPlugins={[remarkGfm]}`, `rehypePlugins={[rehypeHighlight]}` i `urlTransform={(url) => defaultUrlTransform(resolveCourseMediaUrl(url, apiUrl, mediaBaseUrl))}`.
- `components` dla h1–h3, p, a (linki zewnętrzne `target="_blank" rel="noopener noreferrer"`), ul/ol/li, code/pre, table, img (`max-w-full`), blockquote.
- Import motywu `highlight.js/styles/github-dark.css`.
- Testy:
  - nagłówek i tabela GFM renderują się jako `heading`/`table`;
  - `![](images/01.png)` daje `img` z adresem mediów kursu;
  - `<script>` w treści nie tworzy elementu `script` (jest tekstem);
  - link `javascript:alert(1)` nie ma `href` z `javascript:`.

#### 5. Strona szczegółowa

**File**: `src/client/app01/src/pages/courses/CourseDetailsPage.tsx` + `CourseDetailsPage.test.tsx`

**Intent**: Strona kursu ze stanami ładowania, błędu i treści, w stylu `CoursesPage`. Teksty po polsku.

**Contract**:
- `useParams<{ slug: string }>()` → `getCourseContent({ slug })` z tokenem z `localStorage`.
- `document.title = "<tytuł> | tomsoft1 workspace"`.
- Widok: przycisk „Powrót do kursów” (`navigate("/courses")`), tytuł `h1`, tagi jak w `CourseTile`, `CourseMarkdown`.
- Błąd → komunikat z serwisu.
- Testy (`vi.mock` serwisu, `MemoryRouter` z `initialEntries={["/courses/kurs-a"]}` i `<Route path="courses/:slug">`):
  - serwis wołany ze slugiem `kurs-a`;
  - tytuł, tagi i treść widoczne;
  - odrzucony promise → komunikat błędu;
  - przycisk „Powrót do kursów” obecny.

#### 6. Trasa i kafelek jako link

**File**: `src/client/app01/src/main.tsx`, `src/client/app01/src/components/CourseTile.tsx`, `CourseTile.test.tsx`, `src/client/app01/src/pages/courses/CoursesPage.test.tsx`

**Intent**: Strona kursu dostępna tylko po zalogowaniu, a kafelek prowadzi do kursu także dla gościa (logowanie z powrotem).

**Contract**:
- `<Route path="courses/:slug" element={<CourseDetailsPage />} />` wewnątrz `<Route element={<RequireAuth />}>`. Publiczna `courses` bez zmian.
- `CourseTile` opakowany w `Link` z `"react-router"` do `/courses/${slug}`.
- Test „nie jest linkiem” zastąpiony testem „jest linkiem do /courses/<slug>”. Wymaga `MemoryRouter` w testach `CourseTile` i `CoursesPage`, jeśli jeszcze go nie mają.

### Success Criteria:

#### Automated Verification:

- `npx prettier --check "src/**/*.{ts,tsx,css}"` przechodzi (w `src/client/app01`)
- `npm run lint` przechodzi
- `npm test` przechodzi, w tym `courseMediaUrl.test.ts`, `CourseMarkdown.test.tsx`, `CourseDetailsPage.test.tsx` i zaktualizowany `CourseTile.test.tsx`
- `npm run build` przechodzi (`tsc -b` + Vite)
- `npm audit --audit-level=high` przechodzi
- `dotnet build APPS.sln` i `dotnet test APPS.sln` nadal przechodzą

#### Manual Verification:

- Po poprawie ścieżek w pliku kursu (`images/01.png`, `thumbnail.png`, `/images/przypadek.png`) zalogowany użytkownik klika kafelek i widzi tytuł, tagi, sformatowaną treść, obrazki i podświetlony blok kodu
- Niezalogowany klika kafelek, trafia do logowania i po zalogowaniu wraca na `/courses/<slug>` (FR-012)
- Wejście na `/courses/<slug>` kursu z przyszłą datą albo nieistniejącego pokazuje „Kurs nie istnieje lub nie jest jeszcze opublikowany”
- „Powrót do kursów” wraca do listy; menu, Apki, Gry, logowanie i ekrany admina działają bez zmian
- Strona kursu jest czytelna na mobile (tabele i obrazki nie rozpychają layoutu)

**Implementation Note**: Po automatycznej weryfikacji zatrzymaj się na ręczne potwierdzenie przed zamknięciem zmiany.

---

## Testing Strategy

### Unit Tests:

- `resolveCourseMediaUrl` (wszystkie rodzaje URL-i), `CourseMarkdown` (GFM, obrazki, brak `script`, brak `javascript:`), `CourseDetailsPage` i `CourseTile` (link).

### Integration Tests:

- `CourseContent/EndpointTests.cs`: 200/400/401/403/404, granica `PublishDate == teraz`, wszystkie powody 404, bezpiecznik `wwwroot`.
- `CourseTiles/EndpointTests.cs`: bezpiecznik daje pustą listę.

### Manual Testing Steps:

1. Przy obecnej konfiguracji sprawdź, że bezpiecznik działa (pusta lista + błąd w logu).
2. Przenieś `korpo-cte-300.md` do `C:/PROJEKTY/courses-content/korpo-cte-300/`, zmień `ContentPath` i popraw ścieżki obrazków. Grafiki zostają w `wwwroot/media/courses/korpo-cte-300/`.
3. W oknie prywatnym kliknij kafelek → zaloguj się → wróć na kurs i przeczytaj treść.
4. Sprawdź, że `/media/courses/korpo-cte-300/korpo-cte-300.md` daje 404.

## Performance Considerations

Plik kursu (~10 KB) jest czytany przy każdym otwarciu strony. Przy skali „small” to akceptowalne, cache poza zakresem.

## Migration Notes

Brak migracji bazy. Wdrożenie wymaga, żeby na serwerze `Courses:ContentPath` wskazywał katalog poza `wwwroot`, bo inaczej bezpiecznik ukryje wszystkie kursy. Wycofanie zmiany przywraca brak strony szczegółowej, bez wpływu na dane.

## References

- Roadmapa: `context/foundation/roadmap.md` (S-04), PRD: `context/foundation/prd.md` (FR-002, FR-003, FR-010, FR-012, Guardrails)
- Poprzedni wycinek: `context/archive/2026-10-07-public-course-tiles/plan.md`
- Czytnik: `src/server/App01/App01.Modules.Courses/Content/CourseFrontmatterReader.cs`
- Testy S-03: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseTiles/EndpointTests.cs`
- Logowanie z powrotem: `src/client/app01/src/utils/auth.ts:76-82`, `src/client/app01/src/pages/portal/auth/UserLoginPage.tsx:18,59`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Serwer — treść kursu i bezpiecznik

#### Automated

- [x] 1.1 `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete`
- [x] 1.2 `dotnet test APPS.sln` przechodzi, w tym nowe `Features/Courses/CourseContent/EndpointTests.cs` i test bezpiecznika w `CourseTiles`
- [x] 1.3 `dotnet format APPS.sln --verify-no-changes` przechodzi

#### Manual

- [x] 1.4 Przy obecnej konfiguracji (`ContentPath` w `wwwroot/media/courses`) `/api/courses/course-tiles` zwraca pustą listę, a w logu jest błąd bezpiecznika
- [x] 1.5 Po przeniesieniu `.md` poza `wwwroot` (np. `C:/PROJEKTY/courses-content/<slug>/<slug>.md`) i zmianie `ContentPath` lista kafelków wraca, a `GET /media/courses/<slug>/<slug>.md` zwraca 404
- [x] 1.6 `GET api/courses/course-content?slug=korpo-cte-300` przez Swaggera z JWT zwraca treść, a bez JWT zwraca 401

### Phase 2: Klient — strona kursu

#### Automated

- [ ] 2.1 `npx prettier --check "src/**/*.{ts,tsx,css}"` przechodzi (w `src/client/app01`)
- [ ] 2.2 `npm run lint` przechodzi
- [ ] 2.3 `npm test` przechodzi, w tym `courseMediaUrl.test.ts`, `CourseMarkdown.test.tsx`, `CourseDetailsPage.test.tsx` i zaktualizowany `CourseTile.test.tsx`
- [ ] 2.4 `npm run build` przechodzi (`tsc -b` + Vite)
- [ ] 2.5 `npm audit --audit-level=high` przechodzi
- [ ] 2.6 `dotnet build APPS.sln` i `dotnet test APPS.sln` nadal przechodzą

#### Manual

- [ ] 2.7 Po poprawie ścieżek w pliku kursu (`images/01.png`, `thumbnail.png`, `/images/przypadek.png`) zalogowany użytkownik klika kafelek i widzi tytuł, tagi, sformatowaną treść, obrazki i podświetlony blok kodu
- [ ] 2.8 Niezalogowany klika kafelek, trafia do logowania i po zalogowaniu wraca na `/courses/<slug>` (FR-012)
- [ ] 2.9 Wejście na `/courses/<slug>` kursu z przyszłą datą albo nieistniejącego pokazuje „Kurs nie istnieje lub nie jest jeszcze opublikowany”
- [ ] 2.10 „Powrót do kursów” wraca do listy; menu, Apki, Gry, logowanie i ekrany admina działają bez zmian
- [ ] 2.11 Strona kursu jest czytelna na mobile (tabele i obrazki nie rozpychają layoutu)
