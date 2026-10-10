# Postęp grupy (dashboard) — plan implementacji

## Overview

Wycinek S-07 roadmapy (US-01, FR-009), ostatni w milestonie M-1. Zalogowany użytkownik widzi dashboard z czterema wskaźnikami postępu grupy na wybrany dzień (domyślnie dziś):

- liczba użytkowników;
- liczba flag możliwych do zdobycia;
- liczba flag zdobytych przez grupę;
- procent zdobytych flag.

## Current State Analysis

- Dane są dostępne bez migracji:
  - `Users` (`CreatedAt`, `IsAdmin`);
  - `Flags` przypięte do `Courses` (`PublishDate`, UTC);
  - `UserFlags` (`UserId`, `FlagId`, `EarnedAt` UTC, unikalny indeks `(UserId, FlagId)`).
- Hangar (`Features/HangarFlags/Handler.cs`) i ranking (`Features/Leaderboard/Handler.cs`) liczą tylko flagi kursów opublikowanych (`PublishDate <= now`, `TimeProvider`). Ranking pokazuje osoby z co najmniej 1 flagą, także administratorów.
- Podmenu kursów (`src/client/app01/src/pages/courses/coursesSubMenu.ts`): Hangar z trofeami → Terminal TOMO-AI-001 → Lista zasłużonych.
- Wspólne komponenty klienta: `Card`, `FormCard`, `SubMenu`, `TextEdit`. Helper `src/utils/formatDateTime.ts` nie jest tu potrzebny, bo pokazujemy wybrany dzień, nie znacznik czasu.

## Desired End State

- `GET api/courses/group-progress?asOf=<ISO UTC>` (JWT + `X-TOKEN`) zwraca `asOf` (użyty moment), `userCount`, `availableFlagCount`, `earnedFlagCount` i `earnedPercent` (liczba lub `null`). Bez `asOf` liczy stan na teraz. `asOf` z przyszłości jest obcinane do „teraz”.
- Definicje (wszystko „przed `asOf`”, czyli `< asOf`):
  - **flagi możliwe** — flagi kursów z `PublishDate < asOf`;
  - **flagi zdobyte przez grupę** — liczba **różnych** flag z tej puli, które ktokolwiek zdobył z `EarnedAt < asOf`;
  - **użytkownicy** — liczba różnych osób, które mają co najmniej jedną taką zdobytą flagę, razem z administratorami;
  - **procent** — `zdobyte / możliwe × 100`, zaokrąglony do 1 miejsca po przecinku; `null`, gdy możliwych jest 0.
- Strona „Postęp grupy” (`/group-progress`) jest ostatnią pozycją podmenu kursów. Ma pole daty (domyślnie dziś, maksimum dziś) i cztery kafelki ze wskaźnikami.
- Przykład: 2 osoby, 10 flag, obie zdobyły A, jedna także B → 2 użytkowników, 10 możliwych, 2 zdobyte, 20%.

### Key Discoveries:

- Filtr publikacji i `TimeProvider`: `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Handler.cs`.
- Wzorzec testów z `FixedTimeProvider` i surowym JSON: `tests/server/App01/App01.Api.Tests/Features/Courses/Leaderboard/EndpointTests.cs`.
- Wzorzec strony kursów z podmenu i ignorowaniem nieaktualnych odpowiedzi: `src/client/app01/src/pages/courses/LeaderboardPage.tsx`.

## What We're NOT Doing

- Liczenia wszystkich kont ani wykluczania administratorów. Użytkownik = osoba z co najmniej 1 zdobytą flagą.
- Liczenia sumy zdobyć osoba–flaga i procentu względem (osoby × flagi).
- Wykresów, trendów i porównań wielu dni naraz.
- Stref czasowych na serwerze. Granicę dnia wyznacza klient, a serwer dostaje chwilę UTC.
- Zmian w hangarze, rankingu i aktywacji.
- Migracji.

## Implementation Approach

Nowy wycinek `GroupProgress`. Handler obcina `asOf` do „teraz” i liczy trzy zapytania EF (możliwe, różne zdobyte, różni użytkownicy). Procent oblicza w C#. Klient zamienia wybraną datę na początek następnego dnia w lokalnej strefie (jako ISO UTC) i wysyła ją jako `asOf`.

## Critical Implementation Details

- **Obcięcie `asOf`:** `effectiveAsOf = min(asOf ?? now, now)`, liczone z `TimeProvider`. Bez tego wybór jutrzejszej daty ujawniłby liczbę flag kursów jeszcze nieopublikowanych (FR-003). Odpowiedź zwraca `effectiveAsOf`, oznaczony jako UTC (`Z`).
- **Granica „przed”:** wszystkie porównania są ostre (`< asOf`). Dla wybranego dnia D klient wysyła początek dnia D+1 w lokalnej strefie, więc obejmuje cały dzień D. Dla dzisiejszej daty obcięcie daje „teraz”.
- **Spójność zdobytych z możliwymi:** zdobyta flaga liczy się tylko wtedy, gdy jej kurs ma `PublishDate < asOf`. Dzięki temu zdobyte nigdy nie przekroczą możliwych.

## Phase 1: Serwer — endpoint `group-progress`

### Overview

Wycinek `Features/GroupProgress/` (4 pliki), rejestracja w `ModuleDI`, testy endpointu.

### Changes Required:

#### 1. Kontrakty

**File**: `src/server/App01/App01.Modules.Courses/Features/GroupProgress/Contracts.cs`

**Intent**: Żądanie z opcjonalnym momentem i odpowiedź z czterema wskaźnikami.

**Contract**: `Request(DateTime? AsOf) : IRequest<Response>`; `Response(DateTime AsOf, int UserCount, int AvailableFlagCount, int EarnedFlagCount, double? EarnedPercent)`.

#### 2. Walidator

**File**: `src/server/App01/App01.Modules.Courses/Features/GroupProgress/Validator.cs`

**Intent**: Odrzucić nierealne daty.

**Contract**: `AsOf`, gdy podany, nie może być wcześniejszy niż `2000-01-01` („Data musi być późniejsza niż 2000-01-01”). Daty z przyszłości nie są błędem, bo obcina je handler.

#### 3. Handler

**File**: `src/server/App01/App01.Modules.Courses/Features/GroupProgress/Handler.cs`

**Intent**: Policzyć cztery wskaźniki na `effectiveAsOf`.

**Contract**: `GroupProgressHandler`. Wstrzykuje `ILogger`, `IValidator`, `AppDbContext` i `TimeProvider`. Kolejność w `Handle`:
1. walidacja;
2. `effectiveAsOf` (zob. „Critical Implementation Details”; `AsOf` z `Kind` innym niż UTC jest konwertowany na UTC);
3. `AvailableFlagCount` = `Flags` z `Course.PublishDate < asOf`;
4. `EarnedFlagCount` = różne `FlagId` w `UserFlags` z `EarnedAt < asOf` i `Flag.Course.PublishDate < asOf`;
5. `UserCount` = różne `UserId` w tym samym zbiorze;
6. `EarnedPercent` = `Math.Round(earned * 100.0 / available, 1)` albo `null`, gdy `available == 0`.

Zapytania z `cancellationToken`, bez surowego SQL.

#### 4. Endpoint i rejestracja

**File**: `src/server/App01/App01.Modules.Courses/Features/GroupProgress/Endpoint.cs`, `src/server/App01/App01.Modules.Courses/ModuleDI.cs`

**Intent**: Wystawić trasę dla zalogowanych i zarejestrować ją.

**Contract**: `MapGet("api/courses/group-progress")` z parametrem query `DateTime? asOf`, `.WithName("CoursesGroupProgress")`, `.WithTags("Courses")`, `.Produces<Contracts.Response>(200)`, `.Produces(400)`, `.Produces(401)`, `.Produces(403)`, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()`. W `UseModuleCoursesEndpoints` dopisać `Features.GroupProgress.Endpoint.AddEndpoint(app);`.

#### 5. Testy endpointu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/GroupProgress/EndpointTests.cs`

**Intent**: Pokryć definicje wskaźników według wzorca `Leaderboard/EndpointTests.cs` (`FixedTimeProvider`, seed InMemory).

**Contract**: Przypadki:
- `GroupProgress_WithoutAsOf_ComputesForNow`: przykład z planu daje 2 / 10 / 2 / 20.0.
- `GroupProgress_SharedFlagCountedOnce`.
- `GroupProgress_IncludesAdmins`.
- `GroupProgress_SkipsUnpublishedCourses`: flagi i zdobycia kursu z `PublishDate >= asOf` nie są liczone.
- `GroupProgress_AsOfInPast_CountsOnlyEarlierEvents`: zdobycie z `EarnedAt == asOf` nie jest liczone, a o 1 tick wcześniej jest.
- `GroupProgress_AsOfInFuture_IsClampedToNow`: wynik jak dla „teraz”, a w JSON `asOf` równe „teraz”.
- `GroupProgress_NoFlags_ReturnsNullPercent`.
- `GroupProgress_AsOfIsUtc`: surowy JSON `asOf` kończy się `Z`.
- `GroupProgress_WithTooEarlyAsOf_ReturnsBadRequest`.
- `GroupProgress_WithoutJwtToken_ReturnsUnauthorized`.
- `GroupProgress_WithoutXToken_ReturnsForbidden`.

### Success Criteria:

#### Automated Verification:

- `dotnet format APPS.sln --verify-no-changes` przechodzi
- `dotnet build APPS.sln` przechodzi bez ostrzeżeń
- `dotnet test APPS.sln` przechodzi, w tym nowe `GroupProgress/EndpointTests.cs`

#### Manual Verification:

- Na lokalnej bazie `GET api/courses/group-progress` (Swagger, JWT + `X-TOKEN`) bez `asOf` zwraca wskaźniki zgodne z danymi: liczba możliwych = `allCount` w hangarze, użytkownicy = liczba osób na liście zasłużonych. Z `asOf` sprzed pierwszej aktywacji zwraca 0 zdobytych i 0 użytkowników.

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie przed fazą 2.

---

## Phase 2: Klient — strona Postęp grupy

### Overview

Kontrakty TS, metoda serwisu, `GroupProgressPage` z polem daty i czterema kafelkami, trasa i pozycja w podmenu kursów, testy Vitest.

### Changes Required:

#### 1. Kontrakty TS

**File**: `src/client/app01/src/services/contracts/courses-group-progress-request.ts`, `courses-group-progress-response.ts`

**Intent**: Odwzorować 1:1 rekordy `GroupProgress.Contracts`.

**Contract**: `CoursesGroupProgressRequest { asOf: string | null }`; `CoursesGroupProgressResponse { asOf: string; userCount: number; availableFlagCount: number; earnedFlagCount: number; earnedPercent: number | null }`.

#### 2. Serwis API

**File**: `src/client/app01/src/services/api-courses-service.ts`

**Intent**: Dodać `getGroupProgress()` według wzorca `getLeaderboard()`.

**Contract**: `getGroupProgress(request: CoursesGroupProgressRequest): Promise<CoursesGroupProgressResponse>` → `GET ${apiUrl}/api/courses/group-progress[?asOf=]`; parametr jest pomijany, gdy `asOf` ma wartość `null`.

#### 3. Helper granicy dnia

**File**: `src/client/app01/src/utils/endOfLocalDay.ts` (+ `endOfLocalDay.test.ts`)

**Intent**: Zamienić wybraną datę `yyyy-MM-dd` na początek następnego dnia w lokalnej strefie, jako ISO UTC.

**Contract**: `endOfLocalDay(date: string): string`. Np. dla `2026-10-10` zwraca `new Date(2026, 9, 11).toISOString()`. Testy sprawdzają przejście miesiąca i roku (`2026-12-31` → 1 stycznia 2027 lokalnie).

#### 4. Strona Postęp grupy

**File**: `src/client/app01/src/pages/courses/GroupProgressPage.tsx`

**Intent**: Pokazać cztery wskaźniki na wybrany dzień w stylu pozostałych stron kursów (nagłówek, `SubMenu` z `backPath="/courses"`).

**Contract**: Default export `GroupProgressPage`, nagłówek i `document.title` „Postęp grupy”.
- Pole daty (`type="date"`, etykieta „Stan na dzień”) z domyślną datą dzisiejszą (lokalną) i `max` = dziś. Zmiana daty pobiera dane z `asOf = endOfLocalDay(data)` i ignoruje nieaktualne odpowiedzi.
- Cztery kafelki (`Card`):
  - „Użytkownicy z flagą” — `userCount`;
  - „Flagi do zdobycia” — `availableFlagCount`;
  - „Flagi zdobyte przez grupę” — `earnedFlagCount`;
  - „Procent zdobytych flag” — `earnedPercent` z jednym miejscem po przecinku i `%`, albo „—” dla `null`.
- Stany: ładowanie („Ładowanie...”) i błąd (`role="alert"`).

#### 5. Routing i podmenu

**File**: `src/client/app01/src/main.tsx`, `src/client/app01/src/pages/courses/coursesSubMenu.ts`

**Intent**: Udostępnić stronę zalogowanym i dodać ją na końcu podmenu kursów.

**Contract**: `<Route path="group-progress" element={<GroupProgressPage />} />` wewnątrz `<Route element={<RequireAuth />}>`. W `coursesSubMenu.ts` stała `GROUP_PROGRESS_PATH = "/group-progress"` i pozycja `{ label: "Postęp grupy", path: GROUP_PROGRESS_PATH }` po „Lista zasłużonych”.

#### 6. Testy Vitest

**File**: `src/client/app01/src/pages/courses/GroupProgressPage.test.tsx`

**Intent**: Sprawdzić zachowanie strony z zamockowanym `ApiCoursesService` (wzorzec `LeaderboardPage.test.tsx`).

**Contract**: Przypadki:
- start woła serwis z `asOf = endOfLocalDay(dziś)` i pokazuje cztery wartości;
- procent 20 pokazuje się jako „20.0%”, a `null` jako „—”;
- zmiana daty woła serwis z nowym `asOf`;
- błąd serwisu pokazuje `role="alert"`.

### Success Criteria:

#### Automated Verification:

- `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- `npm run lint` przechodzi
- `npm test` przechodzi, w tym `GroupProgressPage.test.tsx` i `endOfLocalDay.test.ts`
- `npm run build` przechodzi (`tsc -b`)

#### Manual Verification:

- „Postęp grupy” jest ostatnią pozycją podmenu kursów, a strona pokazuje cztery wskaźniki zgodne z hangarem (flagi do zdobycia) i listą zasłużonych (użytkownicy z flagą)
- Po aktywacji nowej (wcześniej przez nikogo niezdobytej) flagi i odświeżeniu strony rosną „Flagi zdobyte przez grupę” i procent. Wybór wczorajszej daty pokazuje stan bez dzisiejszych zdobyć, a w polu daty nie da się wybrać jutra
- Niezalogowany wchodzący na `/group-progress` trafia do logowania; pozostałe strony kursów, menu, Apki i Gry działają bez zmian

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie.

---

## Testing Strategy

### Unit Tests:

- Vitest: `endOfLocalDay` (zwykły dzień, koniec miesiąca, koniec roku) i `GroupProgressPage` (wartości, formatowanie procentu, zmiana daty, błąd).

### Integration Tests:

- `GroupProgress/EndpointTests.cs`: przykład z planu, wspólna flaga liczona raz, administratorzy, filtr publikacji, ostra granica `asOf`, obcięcie przyszłości, procent `null`, UTC w JSON, 400, 401, brak `X-TOKEN`.

### Manual Testing Steps:

1. Otwórz „Postęp grupy”. Porównaj „Flagi do zdobycia” z licznikiem Y w hangarze, a „Użytkownicy z flagą” z liczbą wierszy na liście zasłużonych.
2. Aktywuj flagę, której nikt jeszcze nie miał, i odśwież. Rosną zdobyte i procent.
3. Aktywuj na innym koncie flagę już zdobytą przez kogoś i odśwież. Zdobyte stoją w miejscu, a użytkownicy rosną, jeśli to jego pierwsza flaga.
4. Wybierz datę sprzed pierwszej aktywacji. Zdobyte i użytkownicy wynoszą 0.

## Performance Considerations

Trzy proste zapytania z `COUNT`/`DISTINCT` na małych tabelach, bez cache.

## Migration Notes

Brak migracji.

## References

- Roadmapa: `context/foundation/roadmap.md` (S-07, Open Roadmap Question 5)
- PRD: `context/foundation/prd.md` (US-01, FR-009, kryterium „Primary” ≥70%)
- Poprzednie wycinki: `context/archive/2026-10-10-hangar-flag-list/`, `context/archive/2026-10-10-leaderboard/`
- Wzorzec serwera: `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Handler.cs`
- Wzorzec klienta: `src/client/app01/src/pages/courses/LeaderboardPage.tsx`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Serwer — endpoint `group-progress`

#### Automated

- [x] 1.1 `dotnet format APPS.sln --verify-no-changes` przechodzi — ac07f14
- [x] 1.2 `dotnet build APPS.sln` przechodzi bez ostrzeżeń — ac07f14
- [x] 1.3 `dotnet test APPS.sln` przechodzi, w tym nowe `GroupProgress/EndpointTests.cs` — ac07f14

#### Manual

- [x] 1.4 Na lokalnej bazie `GET api/courses/group-progress` (Swagger, JWT + `X-TOKEN`) bez `asOf` zwraca wskaźniki zgodne z danymi: liczba możliwych = `allCount` w hangarze, użytkownicy = liczba osób na liście zasłużonych. Z `asOf` sprzed pierwszej aktywacji zwraca 0 zdobytych i 0 użytkowników. — ac07f14

### Phase 2: Klient — strona Postęp grupy

#### Automated

- [x] 2.1 `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- [x] 2.2 `npm run lint` przechodzi
- [x] 2.3 `npm test` przechodzi, w tym `GroupProgressPage.test.tsx` i `endOfLocalDay.test.ts`
- [x] 2.4 `npm run build` przechodzi (`tsc -b`)

#### Manual

- [x] 2.5 „Postęp grupy” jest ostatnią pozycją podmenu kursów, a strona pokazuje cztery wskaźniki zgodne z hangarem (flagi do zdobycia) i listą zasłużonych (użytkownicy z flagą)
- [x] 2.6 Po aktywacji nowej (wcześniej przez nikogo niezdobytej) flagi i odświeżeniu strony rosną „Flagi zdobyte przez grupę” i procent. Wybór wczorajszej daty pokazuje stan bez dzisiejszych zdobyć, a w polu daty nie da się wybrać jutra
- [x] 2.7 Niezalogowany wchodzący na `/group-progress` trafia do logowania; pozostałe strony kursów, menu, Apki i Gry działają bez zmian
