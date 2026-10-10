# Aktywacja flagi kodem w hangarze z trofeami — plan implementacji

## Overview

Wycinek S-06 roadmapy (FR-004). Zalogowany uczestnik wpisuje kod flagi w sekcji „Aktywacja flagi” w hangarze. Serwer zapisuje `UserFlag`, więc flaga staje się zdobyta i od razu widać ją w tabeli oraz w liczniku. To jedyna droga zapisu zdobytej flagi: weryfikacja AI (S-01) tylko wydaje kod. Przy okazji strona dostaje tytuł „Hangar z trofeami”, a sekcja aktywacji stoi pod podmenu, przed tabelą flag.

## Current State Analysis

- `UserFlag` ma unikalny indeks `(UserId, FlagId)` (`App01.Shared.Infrastructure/Repositories/Configurations/Courses/UserFlagConfiguration.cs`). `Flag.Code` jest unikalny w całej tabeli i ma typ `varchar(50)` (`FlagConfiguration.cs`). Migracja nie jest potrzebna.
- Weryfikacja (`Features/VerifyAnswer/Handler.cs`) przy `Correct` zwraca `Flag.Code` bez zapisu. Komunikat brzmi: „Zapisz kod flagi i aktywuj go w formularzu aktywacji.”.
- Hangar (`src/client/app01/src/pages/courses/HangarPage.tsx`) ma tytuł „Hangar”, `SubMenu`, a pod nim jeden `FormCard` z filtrem, licznikiem, tabelą i paginacją. Dane pobiera z `GET api/courses/hangar-flags`.
- `CLAUDE.md` wymaga przy przyznawaniu flagi unikalnego indeksu i obsługi `DbUpdateException` (drugi zapis = `AlreadyOwned`). `Flag.Code` wolno zwrócić tylko przy `Correct` i w hangarze dla flag zdobytych.
- `DbUpdateException` nie jest dziś obsługiwany nigdzie w kodzie. To pierwszy taki przypadek.

## Desired End State

- `POST api/courses/activate-flag` (JWT + `X-TOKEN`) z body `{ code }` zwraca 200 z jednym z trzech statusów:
  - `Activated`: flaga zapisana jako zdobyta, z `flagTitle`;
  - `AlreadyOwned`: flaga była już zdobyta;
  - `Invalid`: kod nie pasuje do flagi opublikowanego kursu.
- Kod jest dopasowywany po obcięciu spacji, bez rozróżniania wielkości liter.
- Pusty lub zbyt długi kod daje 400. Odpowiedź nigdy nie zawiera kodu.
- Na stronie `/hangar` tytuł brzmi „Hangar z trofeami”. Pod podmenu jest sekcja „Aktywacja flagi” z polem kodu i przyciskiem „Aktywuj”. Po aktywacji strona pokazuje komunikat, czyści pole i odświeża tabelę oraz licznik na bieżącym filtrze i stronie.

### Key Discoveries:

- Wzorzec wycinka POST ze statusami 200: `src/server/App01/App01.Modules.Courses/Features/VerifyAnswer/` (`Contracts.Statuses`, stałe komunikaty, `TimeProvider`, `IJwtService.GetUserIdFromJwt()`).
- Wzorzec testów: `tests/server/App01/App01.Api.Tests/Features/Courses/VerifyAnswer/EndpointTests.cs` i `HangarFlags/EndpointTests.cs` (seed InMemory, `FixedTimeProvider`, surowy JSON).
- Wzorzec formularza z wynikami: `TomoAiTerminalPage.tsx` (`TextEdit`, `ButtonPrimary`, bloki `role="status"` per status).

## What We're NOT Doing

- Limitu prób aktywacji (rate limiting). Nieudane próby są tylko logowane jako Warning.
- Rozróżniania „kod nieznany” od „flaga kursu nieopublikowanego”. Oba przypadki dają `Invalid`.
- Wyboru flagi z listy przed wpisaniem kodu, bo kod jednoznacznie wskazuje flagę.
- Zmian w weryfikacji (`VerifyAnswer`), terminalu, `hangar-flags` i jego kontrakcie.
- Zmiany etykiety „Hangar” w podmenu kursów i w menu głównym.
- Migracji i zmian encji.
- Rankingu (S-05) i dashboardu (S-07).

## Implementation Approach

Nowy wycinek `ActivateFlag` według wzorca `VerifyAnswer`: wynik biznesowy jako 200 + `Status`, a błędy wejścia jako 400 przez `ValidationException`. Klient dostaje osobną sekcję w hangarze, która po sukcesie ponownie wywołuje istniejące pobieranie listy.

## Critical Implementation Details

- **Dopasowanie kodu:** wpisany kod obciąć (`Trim`) i porównać z `Flag.Code` bez rozróżniania wielkości liter, przez `ToUpper()` po obu stronach w zapytaniu EF. Tłumaczy się to na `UPPER()` w SQL Server, a InMemory zachowuje się tak samo. Nie polegać na kolacji bazy, bo InMemory rozróżnia wielkość liter.
- **Wyścig zapisu:** najpierw sprawdzić `AnyAsync` po `(UserId, FlagId)`. Jeśli flaga jest już zdobyta, zwrócić `AlreadyOwned`. W przeciwnym razie `Add` + `SaveChangesAsync`, a `DbUpdateException` złapać i zwrócić `AlreadyOwned`, bo unikalny indeks odrzucił drugi równoczesny zapis. EF InMemory nie wymusza unikalnych indeksów, więc tej gałęzi nie da się pokryć testem endpointu.

## Phase 1: Serwer — endpoint `activate-flag`

### Overview

Wycinek `Features/ActivateFlag/` (4 pliki), rejestracja w `ModuleDI`, testy endpointu.

### Changes Required:

#### 1. Kontrakty

**File**: `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Contracts.cs`

**Intent**: Żądanie z kodem i odpowiedź ze statusem, stałym komunikatem i tytułem aktywowanej flagi, bez kodu.

**Contract**: `Request(string Code) : IRequest<Response>`; `Response(string Status, string Message, string? FlagTitle = null)`. `FlagTitle` jest wypełniony dla `Activated` i `AlreadyOwned`, a dla `Invalid` ma wartość `null`. Stałe `Statuses.{Activated, AlreadyOwned, Invalid}`.

#### 2. Walidator

**File**: `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Validator.cs`

**Intent**: Odrzucić puste i za długie kody, zanim dotkną bazy.

**Contract**: `Code` niepusty po obcięciu spacji („Kod flagi jest wymagany”), długość do 50 znaków („Kod flagi może mieć maksymalnie 50 znaków”).

#### 3. Handler

**File**: `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Handler.cs`

**Intent**: Przyznać flagę bieżącemu użytkownikowi na podstawie kodu, najwyżej raz.

**Contract**: `ActivateFlagHandler`. Wstrzykuje `ILogger`, `IValidator`, `AppDbContext`, `IJwtService` i `TimeProvider`. Kolejność w `Handle`:
1. walidacja;
2. `userId` z JWT;
3. wyszukanie flagi po kodzie (zob. „Critical Implementation Details”) wśród kursów z `PublishDate <= now`, także flag bez `Criteria`;
4. brak flagi → `Invalid` „Nieprawidłowy kod flagi.” i log Warning z `userId` (bez wpisanego kodu w logu);
5. flaga już zdobyta → `AlreadyOwned` „Masz już tę flagę.”;
6. zapis `UserFlag { UserId, FlagId, EarnedAt = TimeProvider UTC }`, a `DbUpdateException` → `AlreadyOwned`;
7. `Activated` „Flaga aktywowana!” z `FlagTitle` i log Information.

#### 4. Endpoint i rejestracja

**File**: `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Endpoint.cs`, `src/server/App01/App01.Modules.Courses/ModuleDI.cs`

**Intent**: Wystawić trasę dla zalogowanych i zarejestrować ją.

**Contract**: `MapPost("api/courses/activate-flag")` z body `Contracts.Request`, `.WithName("CoursesActivateFlag")`, `.WithTags("Courses")`, `.Produces<Contracts.Response>(200)`, `.Produces(400)`, `.Produces(401)`, `.Produces(403)`, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()`. W `UseModuleCoursesEndpoints` dopisać `Features.ActivateFlag.Endpoint.AddEndpoint(app);`.

#### 5. Testy endpointu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/ActivateFlag/EndpointTests.cs`

**Intent**: Pokryć zachowanie według wzorca `VerifyAnswer/EndpointTests.cs`.

**Contract**: Przypadki:
- `ActivateFlag_WithValidCode_SavesUserFlagAndReturnsActivated`: w bazie jest `UserFlag` bieżącego użytkownika z `EarnedAt` równym czasowi z `FixedTimeProvider`, a odpowiedź ma `FlagTitle`.
- `ActivateFlag_WithTrimmedLowercaseCode_Activates`: ` kod-abc-1 ` aktywuje `KOD-ABC-1`.
- `ActivateFlag_FlagWithoutCriteria_Activates`.
- `ActivateFlag_AlreadyOwned_ReturnsAlreadyOwnedWithoutSecondRow`.
- `ActivateFlag_OwnedByOtherUser_ActivatesForCurrentUser`.
- `ActivateFlag_UnknownCode_ReturnsInvalid` (bez zapisu).
- `ActivateFlag_UnpublishedCourseCode_ReturnsInvalid` (bez zapisu; `PublishDate == now` aktywuje).
- `ActivateFlag_ResponseDoesNotContainCode`: surowy JSON nie zawiera wartości kodu.
- `ActivateFlag_WithEmptyOrTooLongCode_ReturnsBadRequest` (Theory: `""`, `"   "`, 51 znaków).
- `ActivateFlag_WithoutJwtToken_ReturnsUnauthorized`.
- `ActivateFlag_WithoutXToken_ReturnsForbidden`.

### Success Criteria:

#### Automated Verification:

- `dotnet format APPS.sln --verify-no-changes` przechodzi
- `dotnet build APPS.sln` przechodzi bez ostrzeżeń
- `dotnet test APPS.sln` przechodzi, w tym nowe `ActivateFlag/EndpointTests.cs` oraz istniejące testy `HangarFlags` i `VerifyAnswer`

#### Manual Verification:

- Na lokalnej bazie `POST api/courses/activate-flag` (Swagger, JWT + `X-TOKEN`) z kodem flagi opublikowanego kursu zwraca `Activated`, a w `Courses.UserFlags` pojawia się wiersz. Drugie wywołanie zwraca `AlreadyOwned`, a błędny kod zwraca `Invalid`. `GET api/courses/hangar-flags` pokazuje flagę jako zdobytą.

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie przed fazą 2.

---

## Phase 2: Klient — sekcja aktywacji i tytuł hangaru

### Overview

Kontrakty TS, metoda serwisu, sekcja „Aktywacja flagi” w `HangarPage` między podmenu a tabelą, nowy tytuł, testy Vitest.

### Changes Required:

#### 1. Kontrakty TS

**File**: `src/client/app01/src/services/contracts/courses-activate-flag-request.ts`, `courses-activate-flag-response.ts`

**Intent**: Odwzorować 1:1 rekordy `ActivateFlag.Contracts`.

**Contract**:
- `CoursesActivateFlagRequest { code: string }`.
- `CoursesActivateFlagStatus = "Activated" | "AlreadyOwned" | "Invalid"`.
- `CoursesActivateFlagResponse { status; message: string; flagTitle: string | null }`.

#### 2. Serwis API

**File**: `src/client/app01/src/services/api-courses-service.ts`

**Intent**: Dodać `activateFlag()` według wzorca `verifyAnswer()` (`apiFetch`, POST, `getProblemMessage`).

**Contract**: `activateFlag(request: CoursesActivateFlagRequest): Promise<CoursesActivateFlagResponse>` → `POST ${apiUrl}/api/courses/activate-flag`.

#### 3. Strona hangaru

**File**: `src/client/app01/src/pages/courses/HangarPage.tsx`

**Intent**: Nowy tytuł i sekcja aktywacji nad tabelą. Po sukcesie odświeżenie listy.

**Contract**:
- `<h1>` „Hangar z trofeami”, `document.title = "Hangar z trofeami | tomsoft1 workspace"`.
- Między `SubMenu` a `FormCard` z tabelą nowa sekcja (`FormCard`) z nagłówkiem „Aktywacja flagi”, polem `TextEdit` „Kod flagi” (`maxLength` 50) i przyciskiem „Aktywuj”. Przycisk jest wyłączony przy pustym polu i w trakcie wysyłki („Aktywuję…”).
- Wyniki (`role="status"`), z treścią `message` z serwera:
  - `Activated`: komunikat zielony z tytułem flagi, czyszczenie pola i ponowne pobranie tabeli oraz licznika na bieżącym filtrze i stronie;
  - `AlreadyOwned`: komunikat cyan;
  - `Invalid`: komunikat pomarańczowy, pole zostaje do poprawki.
- Błąd HTTP (400 i inne) pokazuje się w `role="alert"` w sekcji aktywacji, niezależnie od błędu tabeli.
- Pobieranie listy musi dać się wywołać ponownie bez zmiany filtra i strony (np. licznik odświeżeń w zależnościach efektu), z zachowaniem ignorowania nieaktualnych odpowiedzi.

#### 4. Testy Vitest

**File**: `src/client/app01/src/pages/courses/HangarPage.test.tsx`

**Intent**: Pokryć sekcję aktywacji i nowy tytuł. Istniejące testy tabeli zostają.

**Contract**: Przypadki:
- nagłówek „Hangar z trofeami”;
- sekcja aktywacji jest w DOM przed tabelą;
- „Aktywuj” jest wyłączony przy pustym polu;
- wysyłka woła `activateFlag({ code })`;
- `Activated` pokazuje komunikat, czyści pole i woła `getHangarFlags` ponownie z bieżącym filtrem i stroną;
- `AlreadyOwned` i `Invalid` pokazują komunikat bez ponownego pobierania listy, a przy `Invalid` pole zostaje wypełnione;
- błąd serwisu pokazuje `role="alert"`.

### Success Criteria:

#### Automated Verification:

- `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- `npm run lint` przechodzi
- `npm test` przechodzi, w tym rozszerzony `HangarPage.test.tsx`
- `npm run build` przechodzi (`tsc -b`)

#### Manual Verification:

- Strona `/hangar` ma tytuł „Hangar z trofeami”, a sekcja „Aktywacja flagi” jest pod podmenu i nad tabelą (także na telefonie)
- Kod skopiowany z terminalu TOMO-AI-001 (werdykt `Correct`) aktywuje flagę. Komunikat pokazuje tytuł flagi, tabela pokazuje ją jako „Zdobyta”, a licznik rośnie o 1
- Ponowna aktywacja tego samego kodu pokazuje „Masz już tę flagę.”, a wymyślony kod pokazuje „Nieprawidłowy kod flagi.”

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie.

---

## Testing Strategy

### Unit Tests:

- Vitest dla `HangarPage`: tytuł, położenie sekcji, stany przycisku, trzy statusy, odświeżenie listy po `Activated`, błąd HTTP.

### Integration Tests:

- `ActivateFlag/EndpointTests.cs`: zapis i `EarnedAt`, dopasowanie z trim i bez rozróżniania wielkości liter, flagi bez kryteriów, `AlreadyOwned` bez drugiego wiersza, przynależność do bieżącego użytkownika, `Invalid` dla nieznanego kodu i kursu nieopublikowanego (z granicą równości), brak kodu w JSON, 400, 401 i brak `X-TOKEN`.

### Manual Testing Steps:

1. W terminalu TOMO-AI-001 uzyskaj werdykt `Correct` i skopiuj kod.
2. W hangarze wklej kod (np. małymi literami, ze spacją) i kliknij „Aktywuj”. Pojawia się komunikat z tytułem flagi, flaga ma status „Zdobyta”, a licznik rośnie.
3. Aktywuj ten sam kod ponownie i sprawdź, że pojawia się „Masz już tę flagę.”.
4. Wpisz wymyślony kod i sprawdź, że pojawia się „Nieprawidłowy kod flagi.”, a w `Courses.UserFlags` nie ma nowego wiersza.

## Performance Considerations

Jedno zapytanie po kodzie (`UPPER()` bez użycia indeksu na małej tabeli flag) i jeden zapis. Bez wpływu na wydajność.

## Migration Notes

Brak migracji. Unikalne indeksy `Courses.Flags(Code)` i `Courses.UserFlags(UserId, FlagId)` istnieją od S-01.

## References

- Roadmapa: `context/foundation/roadmap.md` (S-06)
- PRD: `context/foundation/prd.md` (FR-004)
- Poprzednie wycinki: `context/archive/2026-10-08-answer-verification-earns-flag/`, `context/archive/2026-10-10-hangar-flag-list/`
- Wzorzec serwera: `src/server/App01/App01.Modules.Courses/Features/VerifyAnswer/Handler.cs`
- Wzorzec klienta: `src/client/app01/src/pages/courses/TomoAiTerminalPage.tsx`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Serwer — endpoint `activate-flag`

#### Automated

- [x] 1.1 `dotnet format APPS.sln --verify-no-changes` przechodzi
- [x] 1.2 `dotnet build APPS.sln` przechodzi bez ostrzeżeń
- [x] 1.3 `dotnet test APPS.sln` przechodzi, w tym nowe `ActivateFlag/EndpointTests.cs` oraz istniejące testy `HangarFlags` i `VerifyAnswer`

#### Manual

- [x] 1.4 Na lokalnej bazie `POST api/courses/activate-flag` (Swagger, JWT + `X-TOKEN`) z kodem flagi opublikowanego kursu zwraca `Activated`, a w `Courses.UserFlags` pojawia się wiersz. Drugie wywołanie zwraca `AlreadyOwned`, a błędny kod zwraca `Invalid`. `GET api/courses/hangar-flags` pokazuje flagę jako zdobytą.

### Phase 2: Klient — sekcja aktywacji i tytuł hangaru

#### Automated

- [ ] 2.1 `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"` przechodzi (z `src/client/app01`)
- [ ] 2.2 `npm run lint` przechodzi
- [ ] 2.3 `npm test` przechodzi, w tym rozszerzony `HangarPage.test.tsx`
- [ ] 2.4 `npm run build` przechodzi (`tsc -b`)

#### Manual

- [ ] 2.5 Strona `/hangar` ma tytuł „Hangar z trofeami”, a sekcja „Aktywacja flagi” jest pod podmenu i nad tabelą (także na telefonie)
- [ ] 2.6 Kod skopiowany z terminalu TOMO-AI-001 (werdykt `Correct`) aktywuje flagę. Komunikat pokazuje tytuł flagi, tabela pokazuje ją jako „Zdobyta”, a licznik rośnie o 1
- [ ] 2.7 Ponowna aktywacja tego samego kodu pokazuje „Masz już tę flagę.”, a wymyślony kod pokazuje „Nieprawidłowy kod flagi.”
