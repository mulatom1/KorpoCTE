# Lista zasłużonych — plan implementacji

## Overview

Wycinek S-05 roadmapy (US-01, FR-008). Zalogowany użytkownik widzi ranking uczestników według liczby zdobytych flag. To ta sama liczba, którą uczestnik widzi w hangarze, czyli tylko flagi z opublikowanych kursów. Ranking domyka kryterium sukcesu „Primary”: „pozycja uczestnika aktualizuje się na liście zasłużonych”.

## Current State Analysis

- `UserFlag` (`Courses.UserFlags`) ma `UserId`, `FlagId`, `EarnedAt` (UTC) i unikalny indeks `(UserId, FlagId)`, więc flaga jest liczona raz. Zapisuje go wyłącznie aktywacja kodem (S-06).
- Hangar (`Features/HangarFlags/Handler.cs`) liczy `earnedCount` tylko z flag kursów z `PublishDate <= now` (`TimeProvider`). Ranking musi używać tego samego filtra (ustalenie z S-02).
- `User` (`App01.Shared.Application/Entities/Portal/User.cs`) ma tylko `Id`, `Email`, `PasswordHash`, `IsAdmin` i `CreatedAt`. Nie ma nazwy wyświetlanej, a PRD zabrania zmian w modelu użytkownika.
- Wzorzec paginacji: `Lotto/Features/DrawsGetList` i `Courses/Features/HangarFlags`. Odpowiedź ma `TotalCount, Page, PageSize, TotalPages`, a `pageSize` ma zakres 1–100.
- Podmenu kursów (`src/client/app01/src/pages/courses/coursesSubMenu.ts`): Hangar, Terminal TOMO-AI-001.

## Desired End State

- `GET api/courses/leaderboard?page=1&pageSize=20` (JWT + `X-TOKEN`) zwraca ranking osób z co najmniej jedną flagą z opublikowanego kursu, razem z administratorami. Każdy wpis to `rank`, `displayName` (część e-maila przed `@`) i `flagCount`.
- Kolejność: `flagCount` malejąco, potem wcześniejsza data ostatniej zaliczonej flagi wyżej, potem `UserId`. Miejsce w rankingu jest wspólne przy tej samej liczbie flag (1, 1, 3) i ciągłe między stronami.
- Odpowiedź nie zawiera pełnego e-maila, domeny ani `UserId`.
- Zalogowany użytkownik wchodzi z podmenu kursów w „Lista zasłużonych” (`/leaderboard`). Podmenu ma kolejność: Hangar z trofeami, Terminal TOMO-AI-001, Lista zasłużonych. Widzi tabelę Miejsce | Uczestnik | Flagi po 20 wierszy, z nawigacją Poprzednia / Następna. Własny wiersz nie jest wyróżniany.

### Key Discoveries:

- Filtr publikacji i `TimeProvider`: `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Handler.cs`.
- Paginacja, walidacja i testy 400: `Features/HangarFlags/{Contracts,Validator,Endpoint}.cs`, `tests/.../Courses/HangarFlags/EndpointTests.cs`.
- Strona z tabelą i paginacją, z ignorowaniem nieaktualnych odpowiedzi: `src/client/app01/src/pages/courses/HangarPage.tsx`.

## What We're NOT Doing

- Wyróżniania własnego wiersza ani paska „Twoje miejsce”.
- Pokazywania osób z 0 flagami.
- Wykluczania administratorów; oni też sprawdzają, czy flaga wchodzi.
- Pełnego e-maila, maskowania e-maila i nowego pola nazwy w modelu `User`.
- Decyzji, kogo liczy wskaźnik „liczba użytkowników” w dashboardzie (S-07). To zostaje do planowania S-07.
- Zmian w hangarze, aktywacji i weryfikacji.
- Migracji.

## Implementation Approach

Nowy wycinek `Leaderboard`. Zapytanie EF grupuje `UserFlags` z opublikowanych kursów po `UserId` (liczba flag i najpóźniejsze `EarnedAt`) i dołącza `Email` użytkownika. Sortowanie, numer miejsca, nazwa wyświetlana i paginacja odbywają się w pamięci, bo grupa jest mała, a numer miejsca wymaga całej listy. Klient dostaje stronę na wzór `HangarPage` (tabela + paginacja).

## Critical Implementation Details

- **Numer miejsca:** `rank = 1 + liczba osób z większym flagCount`, liczony na pełnej posortowanej liście przed `Skip/Take`. Na stronie 2 numeracja jest więc kontynuacją, a remis na granicy stron daje ten sam numer.
- **Data ostatniej flagi:** liczona tylko z flag opublikowanych kursów, tych samych co `flagCount`. Przy remisie wyżej jest osoba z wcześniejszą datą, bo wcześniej osiągnęła wynik.
- **Nazwa wyświetlana:** część `Email` przed pierwszym `@`. Bez `@` cały e-mail, a gdy to pusty tekst, „—”. Wyliczana na serwerze, żeby domena nigdy nie trafiła do odpowiedzi.

## Phase 1: Serwer — endpoint `leaderboard`

### Overview

Wycinek `Features/Leaderboard/` (4 pliki), rejestracja w `ModuleDI`, testy endpointu.

### Changes Required:

#### 1. Kontrakty

**File**: `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Contracts.cs`

**Intent**: Żądanie z paginacją i odpowiedź z wpisami rankingu bez danych identyfikujących poza nazwą wyświetlaną.

**Contract**: `Request(int Page = 1, int PageSize = 20) : IRequest<Response>`; `Response(IReadOnlyList<LeaderboardEntryDto> Entries, int TotalCount, int Page, int PageSize, int TotalPages)`; `LeaderboardEntryDto(int Rank, string DisplayName, int FlagCount)`.

#### 2. Walidator

**File**: `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Validator.cs`

**Intent**: Te same reguły paginacji co w `HangarFlags`.

**Contract**: `Page >= 1`, `PageSize` w zakresie 1–100, komunikaty po polsku.

#### 3. Handler

**File**: `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Handler.cs`

**Intent**: Policzyć ranking osób ze zdobytymi flagami opublikowanych kursów.

**Contract**: `LeaderboardHandler`. Wstrzykuje `ILogger`, `IValidator`, `AppDbContext` i `TimeProvider`. `IJwtService` nie jest potrzebny, bo wynik nie zależy od wywołującego, a autoryzację pilnuje endpoint. Kolejność w `Handle`:
1. walidacja;
2. `UserFlags` z `Flag.Course.PublishDate <= now`, grupowane po `UserId`: liczba i `Max(EarnedAt)`, z `cancellationToken`;
3. dołączenie `Email` z `Users`;
4. sortowanie: `FlagCount` malejąco, `LastEarnedAt` rosnąco, `UserId` rosnąco;
5. numer miejsca;
6. nazwa wyświetlana;
7. `Skip/Take`.

Log Debug z liczbą osób.

#### 4. Endpoint i rejestracja

**File**: `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Endpoint.cs`, `src/server/App01/App01.Modules.Courses/ModuleDI.cs`

**Intent**: Wystawić trasę dla zalogowanych i zarejestrować ją.

**Contract**: `MapGet("api/courses/leaderboard")` z parametrami query `page = 1`, `pageSize = 20`, `.WithName("CoursesLeaderboard")`, `.WithTags("Courses")`, `.Produces<Contracts.Response>(200)`, `.Produces(400)`, `.Produces(401)`, `.Produces(403)`, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()`. W `UseModuleCoursesEndpoints` dopisać `Features.Leaderboard.Endpoint.AddEndpoint(app);`.

#### 5. Testy endpointu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/Leaderboard/EndpointTests.cs`

**Intent**: Pokryć reguły rankingu według wzorca `HangarFlags/EndpointTests.cs` (`FixedTimeProvider`, seed InMemory).

**Contract**: Przypadki:
- `Leaderboard_OrdersByFlagCountDesc`.
- `Leaderboard_CountsOnlyPublishedCourses`: flaga kursu z przyszłą datą nie jest liczona; `PublishDate == now` jest liczona.
- `Leaderboard_SkipsUsersWithoutCountedFlags`: brak flag oraz tylko flagi nieopublikowanego kursu → brak na liście.
- `Leaderboard_IncludesAdmins`.
- `Leaderboard_TiesShareRankAndEarlierLastFlagFirst`: miejsca 1, 1, 3, a w remisie wcześniejsza ostatnia flaga wyżej.
- `Leaderboard_RankContinuesAcrossPages`: `pageSize=2`, strona 2 ma miejsca liczone globalnie, także remis na granicy stron.
- `Leaderboard_DisplayNameIsEmailLocalPart`.
- `Leaderboard_ResponseDoesNotContainEmailDomainOrUserId`: surowy JSON nie zawiera `@`, domeny ani pola `userId`.
- `Leaderboard_WithoutFlags_ReturnsEmptyList`.
- `Leaderboard_WithInvalidPaging_ReturnsBadRequest` (`page=0`, `pageSize=0`, `pageSize=101`).
- `Leaderboard_WithoutJwtToken_ReturnsUnauthorized`.
- `Leaderboard_WithoutXToken_ReturnsForbidden`.

### Success Criteria:

#### Automated Verification:

- `dotnet format APPS.sln --verify-no-changes` przechodzi
- `dotnet build APPS.sln` przechodzi bez ostrzeżeń
- `dotnet test APPS.sln` przechodzi, w tym nowe `Leaderboard/EndpointTests.cs`

#### Manual Verification:

- Na lokalnej bazie z kilkoma użytkownikami (w tym admin) i flagami aktywowanymi w hangarze `GET api/courses/leaderboard` (Swagger, JWT + `X-TOKEN`) zwraca ranking zgodny z liczbami `earnedCount` w hangarze każdego z nich, bez e-maili z domeną

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie przed fazą 2.

---

## Phase 2: Klient — strona Lista zasłużonych

### Overview

Kontrakty TS, metoda serwisu, `LeaderboardPage` z tabelą i paginacją, trasa i pozycja w podmenu kursów, testy Vitest.

### Changes Required:

#### 1. Kontrakty TS

**File**: `src/client/app01/src/services/contracts/courses-leaderboard-request.ts`, `courses-leaderboard-response.ts`

**Intent**: Odwzorować 1:1 rekordy `Leaderboard.Contracts`.

**Contract**: `CoursesLeaderboardRequest { page: number; pageSize: number }`; `CoursesLeaderboardEntryDto { rank: number; displayName: string; flagCount: number }`; `CoursesLeaderboardResponse { entries; totalCount; page; pageSize; totalPages }`.

#### 2. Serwis API

**File**: `src/client/app01/src/services/api-courses-service.ts`

**Intent**: Dodać `getLeaderboard()` według wzorca `getHangarFlags()` (`apiFetch`, `URLSearchParams`, `getProblemMessage`).

**Contract**: `getLeaderboard(request: CoursesLeaderboardRequest): Promise<CoursesLeaderboardResponse>` → `GET ${apiUrl}/api/courses/leaderboard?page=&pageSize=`.

#### 3. Strona Lista zasłużonych

**File**: `src/client/app01/src/pages/courses/LeaderboardPage.tsx`

**Intent**: Pokazać ranking w tabeli z paginacją, w stylu `HangarPage` (nagłówek, `SubMenu` z `backPath="/courses"`, `FormCard`).

**Contract**: Default export `LeaderboardPage`, nagłówek i `document.title` „Lista zasłużonych”.
- Tabela (`overflow-x-auto`) z kolumnami Miejsce | Uczestnik | Flagi, w kolejności z serwera, bez wyróżniania własnego wiersza.
- Paginacja po 20: Poprzednia / Następna (wyłączone na krańcach) i „Strona X z Y”, ukryta przy jednej stronie. Nieaktualne odpowiedzi są ignorowane.
- Stany: ładowanie („Ładowanie...”), błąd (`role="alert"`), pusta lista („Nikt jeszcze nie zdobył flagi”).

#### 4. Routing i podmenu

**File**: `src/client/app01/src/main.tsx`, `src/client/app01/src/pages/courses/coursesSubMenu.ts`

**Intent**: Udostępnić stronę zalogowanym i dodać ją do podmenu kursów. Menu główne bez zmian.

**Contract**: `<Route path="leaderboard" element={<LeaderboardPage />} />` wewnątrz `<Route element={<RequireAuth />}>`. W `coursesSubMenu.ts` stała `LEADERBOARD_PATH = "/leaderboard"` i pozycja `{ label: "Lista zasłużonych", path: LEADERBOARD_PATH }` po „Terminal TOMO-AI-001” (kolejność: Hangar z trofeami, Terminal TOMO-AI-001, Lista zasłużonych; etykieta „Hangar” zmieniona na „Hangar z trofeami” — zmiany z 2026-10-10).

#### 5. Testy Vitest

**File**: `src/client/app01/src/pages/courses/LeaderboardPage.test.tsx`

**Intent**: Sprawdzić zachowanie strony z zamockowanym `ApiCoursesService` (wzorzec `HangarPage.test.tsx`).

**Contract**: Przypadki:
- tabela pokazuje miejsce, uczestnika i liczbę flag, także wspólne miejsce przy remisie, a serwis jest wołany z `{ page: 1, pageSize: 20 }`;
- „Następna” woła stronę 2; Poprzednia/Następna są wyłączone na krańcach; paginacja jest ukryta przy jednej stronie;
- pusta lista pokazuje komunikat;
- błąd serwisu pokazuje `role="alert"`.

### Success Criteria:

#### Automated Verification:

- `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- `npm run lint` przechodzi
- `npm test` przechodzi, w tym `LeaderboardPage.test.tsx` i istniejące testy stron kursów
- `npm run build` przechodzi (`tsc -b`)

#### Manual Verification:

- Zalogowany użytkownik widzi „Lista zasłużonych” w podmenu kursów (lista kursów, szczegóły kursu, hangar, terminal); strona pokazuje ranking zgodny z danymi z bazy, z nazwami bez domeny
- Po aktywacji flagi w hangarze i odświeżeniu listy zasłużonych liczba flag i miejsce użytkownika się aktualizują
- Niezalogowany wchodzący na `/leaderboard` trafia do logowania; menu główne, hangar, terminal, Apki i Gry działają bez zmian

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie.

---

## Testing Strategy

### Unit Tests:

- Vitest dla `LeaderboardPage`: tabela, remis, paginacja, stany pusty i błędu.

### Integration Tests:

- `Leaderboard/EndpointTests.cs`: filtr publikacji (z granicą równości), pomijanie 0 flag, admin, remis i rozstrzyganie po dacie, numeracja między stronami, nazwa wyświetlana, brak e-maila/domeny/`userId` w JSON, 400, 401, brak `X-TOKEN`.

### Manual Testing Steps:

1. Aktywuj flagi kodem na dwóch kontach (np. uczestnik 2 flagi, admin 1 flaga).
2. Otwórz „Lista zasłużonych”. Uczestnik ma miejsce 1 i 2 flagi, admin miejsce 2 i 1 flagę. Nazwy są bez domeny.
3. Aktywuj adminowi drugą flagę i odśwież. Obaj mają po 2 flagi i miejsce 1, a wyżej jest ten, kto drugą flagę zdobył wcześniej.
4. Wyloguj się i wejdź na `/leaderboard`, co powinno przekierować do logowania.

## Performance Considerations

Jedno zapytanie z grupowaniem po `UserFlags` i dołączeniem `Users`. Sortowanie i paginacja w pamięci przy małej grupie są wystarczające.

## Migration Notes

Brak migracji.

## References

- Roadmapa: `context/foundation/roadmap.md` (S-05, Open Roadmap Question 5)
- PRD: `context/foundation/prd.md` (US-01, FR-008)
- Poprzednie wycinki: `context/archive/2026-10-10-hangar-flag-list/`, `context/archive/2026-10-10-flag-activation/`
- Wzorzec serwera: `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Handler.cs`
- Wzorzec klienta: `src/client/app01/src/pages/courses/HangarPage.tsx`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Serwer — endpoint `leaderboard`

#### Automated

- [x] 1.1 `dotnet format APPS.sln --verify-no-changes` przechodzi — aee3cdb
- [x] 1.2 `dotnet build APPS.sln` przechodzi bez ostrzeżeń — aee3cdb
- [x] 1.3 `dotnet test APPS.sln` przechodzi, w tym nowe `Leaderboard/EndpointTests.cs` — aee3cdb

#### Manual

- [x] 1.4 Na lokalnej bazie z kilkoma użytkownikami (w tym admin) i flagami aktywowanymi w hangarze `GET api/courses/leaderboard` (Swagger, JWT + `X-TOKEN`) zwraca ranking zgodny z liczbami `earnedCount` w hangarze każdego z nich, bez e-maili z domeną — aee3cdb

### Phase 2: Klient — strona Lista zasłużonych

#### Automated

- [x] 2.1 `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`) — 1d69276
- [x] 2.2 `npm run lint` przechodzi — 1d69276
- [x] 2.3 `npm test` przechodzi, w tym `LeaderboardPage.test.tsx` i istniejące testy stron kursów — 1d69276
- [x] 2.4 `npm run build` przechodzi (`tsc -b`) — 1d69276

#### Manual

- [x] 2.5 Zalogowany użytkownik widzi „Lista zasłużonych” w podmenu kursów (lista kursów, szczegóły kursu, hangar, terminal); strona pokazuje ranking zgodny z danymi z bazy, z nazwami bez domeny — 1d69276
- [x] 2.6 Po aktywacji flagi w hangarze i odświeżeniu listy zasłużonych liczba flag i miejsce użytkownika się aktualizują — 1d69276
- [x] 2.7 Niezalogowany wchodzący na `/leaderboard` trafia do logowania; menu główne, hangar, terminal, Apki i Gry działają bez zmian — 1d69276
