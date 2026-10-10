# Hangar z listą flag — plan implementacji

## Overview

Wycinek S-02 roadmapy (US-01, FR-007). Zalogowany uczestnik otwiera stronę „Hangar” i widzi wszystkie flagi opublikowanych kursów w dwóch sekcjach: „Zdobyte” i „Niezdobyte”, z licznikiem zdobytych do wszystkich. Domyka to kryterium akceptacji US-01: „przed zdobyciem flaga widnieje w hangarze jako niezdobyta; po przyznaniu zmienia status na zdobytą”.

## Current State Analysis

- Model danych jest gotowy z S-01. `Flag` (`Courses.Flags`) należy do kursu i ma `Title`, `Code` (sekret aktywacji) oraz `Criteria` (tajne; puste = flaga zdobywalna tylko aktywacją). `UserFlag` (`Courses.UserFlags`) ma `UserId`, `FlagId`, `EarnedAt` (UTC) i unikalny indeks `(UserId, FlagId)`.
- `GET api/courses/hangar-tasks` (`App01.Modules.Courses/Features/HangarTasks/Handler.cs`) zwraca wyłącznie flagi z kryteriami z opublikowanych kursów (`PublishDate <= teraz`, `TimeProvider`) z polem `isOwned`. Używa go lista rozwijana w terminalu TOMO-AI-001 (`TomoAiTerminalPage.tsx`).
- Strony hangaru nie ma. Podmenu kursów (`src/client/app01/src/pages/courses/coursesSubMenu.ts`) ma jedną pozycję „Terminal TOMO-AI-001”. Pokazują je `CoursesPage` (tylko zalogowanym), `CourseDetailsPage` i `TomoAiTerminalPage`.
- Werdykt `Correct` z S-01 nie zapisuje `UserFlag`; zapis zrobi dopiero aktywacja kodem (S-06). Do czasu S-06 status „zdobyta” w hangarze da się zobaczyć tylko na wierszu `Courses.UserFlags` wstawionym SQL-em.
- `Course` ma w bazie tylko `Slug` i `PublishDate`. Tytuł kursu leży we frontmatterze pliku i nie jest tu używany.

## Desired End State

- `GET api/courses/hangar-flags` (JWT + `X-TOKEN`) zwraca wszystkie flagi kursów z `PublishDate <= teraz`, także te bez `Criteria`. Dla każdej flagi zwraca `flagId`, `title`, `courseSlug`, `isEarned`, `earnedAt` (UTC lub `null`) i `code` w kontekście bieżącego użytkownika. `code` jest wypełniony wyłącznie dla flag zdobytych przez bieżącego użytkownika, a dla niezdobytych ma wartość `null`. Uczestnik potrzebuje swoich kodów ostatniego dnia bootcampu. Odpowiedź nigdy nie zawiera `Criteria`. (Zmiana z 2026-10-10, decyzja użytkownika w trakcie fazy 1; zawęża regułę z `CLAUDE.md` „nigdy nie zwracaj `Code` poza `Correct`”.)
- W hangarze kod zdobytej flagi jest zamaskowany gwiazdkami i odsłania się po najechaniu myszką (oraz po fokusie lub dotknięciu, żeby działało z klawiatury i na telefonie).
- Zalogowany użytkownik wchodzi z podmenu kursów w „Hangar” (`/hangar`, chronione `RequireAuth`). Widzi licznik „Zdobyte flagi: X / Y” oraz sekcje „Zdobyte” (z datą zdobycia, od najnowszej) i „Niezdobyte” (według daty publikacji kursu, potem Id flagi).
- `hangar-tasks`, terminal TOMO-AI-001, menu główne i pozostałe moduły działają bez zmian.

### Key Discoveries:

- Wzorzec wycinka z zapytaniem „czy zdobyta przez bieżącego użytkownika”: `App01.Modules.Courses/Features/HangarTasks/Handler.cs` (filtr publikacji przez `TimeProvider`, `userId` z `IJwtService.GetUserIdFromJwt()`).
- Wzorzec testów z `FixedTimeProvider`, seedem InMemory i sprawdzaniem surowego JSON pod kątem wycieku: `tests/server/App01/App01.Api.Tests/Features/Courses/HangarTasks/EndpointTests.cs`.
- Wzorzec strony z podmenu i mockiem serwisu w Vitest: `src/client/app01/src/pages/courses/TomoAiTerminalPage.tsx` + `TomoAiTerminalPage.test.tsx`.
- Rejestracja endpointów: `App01.Modules.Courses/ModuleDI.cs` (`UseModuleCoursesEndpoints`).

## What We're NOT Doing

- Zmian w `hangar-tasks` i terminalu TOMO-AI-001 (kontrakt i testy zostają).
- Flag kursów z przyszłą datą publikacji. Są ukryte, także gdy użytkownik ma je zdobyte (np. po cofnięciu daty kursu).
- Tytułów kursów z frontmattera i grupowania listy po kursach. Pozycja pokazuje slug kursu.
- Przycisku „Sprawdź w terminalu” przy niezdobytej fladze i przekazywania `flagId` do terminala.
- Formularza aktywacji (S-06), rankingu (S-05) i dashboardu (S-07).
- Zmiany zapisu flagi przy `Correct`; to zostaje w gestii S-06.
- Zmian w menu głównym (`Layout.tsx`), migracji i encjach.

## Implementation Approach

Nowy, osobny wycinek `HangarFlags` według wzorca `HangarTasks`. Serwer zwraca jedną posortowaną listę, a klient dzieli ją na dwie sekcje według `isEarned`, zachowując kolejność z serwera. Najpierw serwer z testami endpointu (weryfikacja w Swaggerze), potem klient.

## Critical Implementation Details

- **Strefa czasowa `earnedAt`:** EF czyta `datetime2` jako `DateTimeKind.Unspecified`, więc JSON nie miałby sufiksu `Z`, a przeglądarka zinterpretowałaby datę jako lokalną. Handler musi oznaczyć `EarnedAt` jako UTC (`DateTime.SpecifyKind(..., DateTimeKind.Utc)`) przed zbudowaniem DTO. Klient formatuje ją jako datę lokalną (`pl-PL`).
- **Sortowanie:** zdobyte malejąco po `earnedAt`, przy remisie rosnąco po `flagId`; niezdobyte rosnąco po `Course.PublishDate`, potem po `flagId`; zdobyte przed niezdobytymi. Przy małej liczbie flag wolno sortować w pamięci po projekcji. Projekcja `earnedAt` przez podzapytanie do `UserFlags` musi działać na EF InMemory i SQL Server (bez surowego SQL).

## Phase 1: Serwer — endpoint `hangar-flags`

### Overview

Nowy wycinek `Features/HangarFlags/` z czterema plikami, rejestracja w `ModuleDI` i testy endpointu.

### Changes Required:

#### 1. Kontrakty

**File**: `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Contracts.cs`

**Intent**: Zdefiniować żądanie bez pól i odpowiedź z listą flag hangaru bez sekretów.

**Contract**: `Request() : IRequest<Response>`; `Response(IReadOnlyList<HangarFlagDto> Flags)`; `HangarFlagDto(int FlagId, string Title, string CourseSlug, bool IsEarned, DateTime? EarnedAt, string? Code)`. `Code` jest wypełniony tylko dla zdobytej flagi, w pozostałych przypadkach `null`. Bez `Criteria`.

#### 2. Walidator

**File**: `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Validator.cs`

**Intent**: Pusty walidator dla spójności wzorca (jak `HangarTasks/Validator.cs`).

**Contract**: `Validator : AbstractValidator<Contracts.Request>` bez reguł.

#### 3. Handler

**File**: `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Handler.cs`

**Intent**: Zwrócić wszystkie flagi opublikowanych kursów ze statusem zdobycia przez bieżącego użytkownika.

**Contract**: `HangarFlagsHandler : IRequestHandler<Contracts.Request, Contracts.Response>`. Wstrzykuje `ILogger`, `IValidator`, `AppDbContext`, `IJwtService` i `TimeProvider`. Kolejność: walidacja → `userId` z JWT → zapytanie `Flags` z `Course.PublishDate <= now` (bez filtra po `Criteria`) z projekcją `EarnedAt` bieżącego użytkownika (podzapytanie do `UserFlags`), z `cancellationToken`. Potem `EarnedAt` oznaczony jako UTC i sortowanie opisane w „Critical Implementation Details”. Log debug z liczbą flag.

#### 4. Endpoint i rejestracja

**File**: `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Endpoint.cs`, `src/server/App01/App01.Modules.Courses/ModuleDI.cs`

**Intent**: Wystawić trasę dla zalogowanych i zarejestrować ją w module.

**Contract**: `MapGet("api/courses/hangar-flags")`, `.WithName("CoursesHangarFlags")`, `.WithTags("Courses")`, `.Produces<Contracts.Response>(200)`, `.Produces(401)`, `.Produces(403)`, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()`. W `UseModuleCoursesEndpoints` dopisać `Features.HangarFlags.Endpoint.AddEndpoint(app);`.

#### 5. Testy endpointu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/HangarFlags/EndpointTests.cs`

**Intent**: Pokryć zachowanie endpointu według wzorca `HangarTasks/EndpointTests.cs` (`FixedTimeProvider`, seed InMemory, JWT testowy).

**Contract**: Przypadki:
- `HangarFlags_ReturnsEarnedOnlyForCurrentUser`: flaga zdobyta przez innego użytkownika jest `isEarned=false`, a flaga bieżącego użytkownika ma `isEarned=true` i `earnedAt`.
- `HangarFlags_IncludesFlagsWithoutCriteria`.
- `HangarFlags_SkipsUnpublishedCourses`, także gdy flaga takiego kursu jest zdobyta; `PublishDate == now` jest widoczny.
- `HangarFlags_OrdersEarnedByEarnedAtDescThenUnearnedByPublishDate`.
- `HangarFlags_EarnedAtIsUtc`: surowy JSON `earnedAt` kończy się `Z`.
- `HangarFlags_ResponseDoesNotContainCriteria`: surowy JSON nie zawiera `Criteria`.
- `HangarFlags_ReturnsCodeOnlyForFlagsEarnedByCurrentUser`: kody zdobytych flag bieżącego użytkownika są w JSON. Kodów niezdobytych flag (także flagi zdobytej tylko przez innego użytkownika i flagi kursu nieopublikowanego) w nim nie ma.
- `HangarFlags_WithoutFlags_ReturnsEmptyList`.
- `HangarFlags_WithoutJwtToken_ReturnsUnauthorized`.
- `HangarFlags_WithoutXToken_ReturnsForbidden`.

Przypadek 400 nie dotyczy, bo żądanie nie ma pól.

### Success Criteria:

#### Automated Verification:

- `dotnet format APPS.sln --verify-no-changes` przechodzi
- `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete`
- `dotnet test APPS.sln` przechodzi, w tym nowe `HangarFlags/EndpointTests.cs` oraz istniejące testy `HangarTasks` i `VerifyAnswer` bez zmian

#### Manual Verification:

- Na lokalnej bazie z flagą bez kryteriów i flagą z kryteriami w opublikowanym kursie oraz wierszem `Courses.UserFlags` wstawionym SQL-em `GET api/courses/hangar-flags` (Swagger, JWT + `X-TOKEN`) zwraca obie flagi. Zdobyta ma `isEarned=true` i `earnedAt` z `Z`, a odpowiedź nie zawiera kodu ani kryteriów
  - Uwaga (zmiana z 2026-10-10): tytuł kroku 1.4 zostaje bez zmian, ale obowiązuje nowa reguła. Zdobyta flaga ma `code` ze swoim kodem, niezdobyta ma `code: null`. Kryteriów nie ma nigdzie.

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie przed fazą 2.

---

## Phase 2: Klient — strona Hangar

### Overview

Kontrakt TS, metoda serwisu, strona `/hangar` z dwiema sekcjami i licznikiem, pozycja „Hangar” w podmenu kursów, testy Vitest.

### Changes Required:

#### 1. Kontrakt TS

**File**: `src/client/app01/src/services/contracts/courses-hangar-flags-response.ts`

**Intent**: Odwzorować 1:1 rekordy `HangarFlags.Contracts`.

**Contract**: `CoursesHangarFlagDto { flagId: number; title: string; courseSlug: string; isEarned: boolean; earnedAt: string | null; code: string | null }`, `CoursesHangarFlagsResponse { flags: CoursesHangarFlagDto[] }`.

#### 2. Serwis API

**File**: `src/client/app01/src/services/api-courses-service.ts`

**Intent**: Dodać `getHangarFlags()` według wzorca `getHangarTasks()` (`apiFetch`, `getHeaders()`, komunikat z `getProblemMessage`).

**Contract**: `getHangarFlags(): Promise<CoursesHangarFlagsResponse>` → `GET ${apiUrl}/api/courses/hangar-flags`.

#### 3. Strona Hangar

**File**: `src/client/app01/src/pages/courses/HangarPage.tsx`

**Intent**: Pokazać listę flag w dwóch sekcjach z licznikiem, w stylu terminala (nagłówek, `SubMenu` z `backPath="/courses"`, `Card`/`FormCard`).

**Contract**: Default export `HangarPage`, `document.title = "Hangar | tomsoft1 workspace"`. Stany: ładowanie („Ładowanie...”), błąd (`role="alert"`), pusta lista („Brak flag do zdobycia”). Licznik „Zdobyte flagi: X / Y”. Sekcja „Zdobyte” zawiera tytuł, slug kursu, datę zdobycia (`toLocaleDateString("pl-PL")`) i kod flagi zamaskowany gwiazdkami (`********`). Prawdziwy kod pokazuje się po najechaniu myszką na pole kodu, a także po fokusie (pole z `tabIndex=0`) lub dotknięciu na telefonie. Odsłonięty kod daje się zaznaczyć i skopiować. Przy braku pozycji sekcja pokazuje „Nie masz jeszcze żadnej flagi”. Sekcja „Niezdobyte” zawiera tytuł i slug kursu. Kolejność pozycji jest taka jak z serwera.

#### 4. Routing i podmenu

**File**: `src/client/app01/src/main.tsx`, `src/client/app01/src/pages/courses/coursesSubMenu.ts`

**Intent**: Udostępnić stronę zalogowanym i dodać ją do podmenu kursów. Menu główne bez zmian.

**Contract**: `<Route path="hangar" element={<HangarPage />} />` wewnątrz `<Route element={<RequireAuth />}>`. W `coursesSubMenu.ts` stała `HANGAR_PATH = "/hangar"` i pozycja `{ label: "Hangar", path: HANGAR_PATH }` przed „Terminal TOMO-AI-001”.

#### 5. Testy Vitest

**File**: `src/client/app01/src/pages/courses/HangarPage.test.tsx`

**Intent**: Sprawdzić zachowanie strony z zamockowanym `ApiCoursesService` (wzorzec `TomoAiTerminalPage.test.tsx`).

**Contract**: Przypadki:
- flagi trafiają do właściwych sekcji, a licznik pokazuje „1 / 2”;
- zdobyta flaga pokazuje datę i zamaskowany kod (`********`), a po `mouseEnter`/`focus` pokazuje prawdziwy kod; `mouseLeave`/`blur` znowu go maskuje;
- niezdobyta flaga nie ma pola kodu;
- pusta lista pokazuje komunikat;
- błąd serwisu pokazuje `role="alert"`;
- brak zdobytych pokazuje „Nie masz jeszcze żadnej flagi”.

Jeśli istniejące testy stron kursów liczą pozycje podmenu, zaktualizować je o „Hangar”.

### Success Criteria:

#### Automated Verification:

- `npx prettier --check "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- `npm run lint` przechodzi
- `npm test` przechodzi, w tym `HangarPage.test.tsx` i istniejące testy stron kursów
- `npm run build` przechodzi (`tsc -b`)

#### Manual Verification:

- Zalogowany użytkownik widzi „Hangar” w podmenu kursów (lista, szczegóły kursu, terminal). Strona `/hangar` pokazuje licznik i sekcje zgodne z danymi z bazy, a data zdobycia jest poprawna w lokalnej strefie
- Po dodaniu SQL-em wiersza `Courses.UserFlags` i odświeżeniu strony flaga przechodzi z „Niezdobyte” do „Zdobyte”
- Niezalogowany wchodzący na `/hangar` trafia do logowania; menu główne, terminal TOMO-AI-001, Apki i Gry działają bez zmian

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie.

---

## Testing Strategy

### Unit Tests:

- Vitest dla `HangarPage`: podział na sekcje, licznik, data, stany pusty i błędu.

### Integration Tests:

- `HangarFlags/EndpointTests.cs`: przynależność zdobycia do bieżącego użytkownika, filtr publikacji (z granicą równości), flagi bez kryteriów, kolejność, UTC `earnedAt`, brak sekretów w JSON, 401 i brak `X-TOKEN`.

### Manual Testing Steps:

1. Wstaw SQL-em kurs opublikowany z dwiema flagami (jedna z `Criteria`, jedna bez) i kurs z przyszłą datą z jedną flagą.
2. Otwórz `/hangar`. Widać 2 flagi w „Niezdobyte”, licznik 0 / 2, flagi przyszłego kursu brak.
3. Wstaw wiersz `Courses.UserFlags` dla jednej flagi i odśwież. Licznik 1 / 2, flaga w „Zdobyte” z dzisiejszą datą.
4. Wyloguj się i wejdź na `/hangar`, co powinno przekierować do logowania.

## Performance Considerations

Mała zamknięta grupa i kilkadziesiąt flag; jedno zapytanie z podzapytaniem per flaga i sortowanie w pamięci są wystarczające.

## Migration Notes

Brak migracji. Tabele `Courses.Flags` i `Courses.UserFlags` istnieją od S-01 (`CoursesFlags`).

## References

- Roadmapa: `context/foundation/roadmap.md` (S-02)
- PRD: `context/foundation/prd.md` (US-01, FR-007)
- Poprzedni wycinek: `context/archive/2026-10-08-answer-verification-earns-flag/plan.md`
- Wzorzec serwera: `src/server/App01/App01.Modules.Courses/Features/HangarTasks/Handler.cs`
- Wzorzec testów: `tests/server/App01/App01.Api.Tests/Features/Courses/HangarTasks/EndpointTests.cs`
- Wzorzec klienta: `src/client/app01/src/pages/courses/TomoAiTerminalPage.tsx`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Serwer — endpoint `hangar-flags`

#### Automated

- [x] 1.1 `dotnet format APPS.sln --verify-no-changes` przechodzi
- [x] 1.2 `dotnet build APPS.sln` przechodzi bez ostrzeżeń `obsolete`
- [x] 1.3 `dotnet test APPS.sln` przechodzi, w tym nowe `HangarFlags/EndpointTests.cs` oraz istniejące testy `HangarTasks` i `VerifyAnswer` bez zmian

#### Manual

- [x] 1.4 Na lokalnej bazie z flagą bez kryteriów i flagą z kryteriami w opublikowanym kursie oraz wierszem `Courses.UserFlags` wstawionym SQL-em `GET api/courses/hangar-flags` (Swagger, JWT + `X-TOKEN`) zwraca obie flagi. Zdobyta ma `isEarned=true` i `earnedAt` z `Z`, a odpowiedź nie zawiera kodu ani kryteriów

### Phase 2: Klient — strona Hangar

#### Automated

- [ ] 2.1 `npx prettier --check "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- [ ] 2.2 `npm run lint` przechodzi
- [ ] 2.3 `npm test` przechodzi, w tym `HangarPage.test.tsx` i istniejące testy stron kursów
- [ ] 2.4 `npm run build` przechodzi (`tsc -b`)

#### Manual

- [ ] 2.5 Zalogowany użytkownik widzi „Hangar” w podmenu kursów (lista, szczegóły kursu, terminal). Strona `/hangar` pokazuje licznik i sekcje zgodne z danymi z bazy, a data zdobycia jest poprawna w lokalnej strefie
- [ ] 2.6 Po dodaniu SQL-em wiersza `Courses.UserFlags` i odświeżeniu strony flaga przechodzi z „Niezdobyte” do „Zdobyte”
- [ ] 2.7 Niezalogowany wchodzący na `/hangar` trafia do logowania; menu główne, terminal TOMO-AI-001, Apki i Gry działają bez zmian
