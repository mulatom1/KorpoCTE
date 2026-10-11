# Spójność flag na prawdziwym silniku — Implementation Plan

## Overview

Faza 2 rolloutu z `context/foundation/test-plan.md` (ryzyka #2 i #3). Plan robi trzy rzeczy:

- dowodzi na prawdziwym SQL Server, że równoległe aktywacje tej samej flagi dają jeden zapis i ranking pokazuje 1;
- dowodzi na jednym seedzie, że hangar, ranking i dashboard pokazują liczby zgodne z PRD;
- poprawia w dashboardzie (`GroupProgress`) granicę dat, którą test krzyżowy ujawnia jako sprzeczną z FR-003 i FR-008.

## Current State Analysis

Źródło: `context/changes/testing-flag-consistency/research.md`.

**Ryzyko #2 (podwójne zaliczenie flagi):**
- Unikalny indeks `(UserId, FlagId)` jest w konfiguracji, w migracji `CoursesFlags` i w snapshocie.
- `ActivateFlag/Handler.cs:92-106` obsługuje równoległy zapis: łapie `DbUpdateException`, sprawdza ponownie, czy wiersz istnieje, i zwraca `AlreadyOwned` albo `throw`.
- Wszystkie testy działają na EF InMemory, który nie wymusza indeksów. Ani indeks, ani gałąź `catch` nie mają testu.

**Ryzyko #3 (różne liczby flag w trzech widokach):**
- Hangar i ranking liczą kursy z `PublishDate <= now`.
- Dashboard liczy `PublishDate < asOf` i dodatkowo `EarnedAt < asOf` (`GroupProgress/Handler.cs:46-53`). Testy przypinają obie granice osobno.
- Żaden test nie porównuje endpointów na jednym seedzie.

**Środowisko:** brak Dockera. LocalDB `MSSQLLocalDB` 17.0.4025.3 jest dostępny. CI (`backend-ci`, `ubuntu-latest`) nie ma SQL Server i ta zmiana go nie dodaje.

## Desired End State

- `dotnet test` z ustawioną zmienną `APP01_SQLSERVER_TESTS` uruchamia testy na prawdziwym SQL Server (LocalDB). Baza na klasę testów jest tworzona migracjami, z kolacją `SQL_Latin1_General_CP1_CI_AS` i compat 110.
- Bez zmiennej (lokalnie i w CI) te testy są oznaczone jako pominięte z czytelnym powodem; reszta przechodzi jak dziś.
- Test wyścigu dowodzi: 10 równoległych aktywacji daje 1 wiersz, 1 `Activated`, 9 `AlreadyOwned`, 0 odpowiedzi 500, a ranking pokazuje `flagCount = 1`.
- Test krzyżowy (InMemory) dowodzi tożsamości z PRD na jednym seedzie. Dashboard liczy granicę włącznie (`<=`), tak jak hangar i ranking.
- `test-plan.md` §4, §5 i §6 opisują nową warstwę i wzorce.

### Key Discoveries:

- `SharedInfrastructureDI.cs:27-35` rejestruje `UseSqlServer(..., UseCompatibilityLevel(110))`, gdy connection string zaczyna się od `Server=`. Fabryka SQL podaje prawdziwy connection string i **nie** podmienia `DbContext`.
- Żaden kod w `src` nie woła `Migrate()`/`EnsureCreated()`. Fixture musi wywołać `Migrate()`, bo `EnsureCreated()` nie dowodzi, że indeks jest w migracji.
- Migracja `PortalFirstUserAdmin` seeduje użytkownika-admina (`20260927063628_PortalFirstUserAdmin.cs:13`). Seed testów na SQL nie może zakładać wolnych Id; Id użytkowników bierze z bazy po zapisie.
- `Leaderboard/Handler.cs:52` liczy wiersze (`g.Count()`), więc duplikat dałby 2. Hangar ukrywa duplikat (podzapytanie per flaga). Asercja „ranking = 1” jest właściwym sygnałem dla #2.
- Testy Courses nie mają wspólnej infrastruktury. Każda klasa ma własną `CreateFactory`, `FixedTimeProvider(Now = 2026-06-01T12:00Z)` i `GenerateJwtToken`, np. `GroupProgress/EndpointTests.cs:25-175`.
- Projekt testów nie ma `[Collection]` ani ustawień równoległości. Klasy biegną równolegle, więc baza SQL musi mieć unikalną nazwę na klasę.
- Klient wysyła `asOf = endOfLocalDay(date)` (`GroupProgressPage.tsx:53`). Zmiana `<` na `<=` przesuwa granicę o jeden tick na końcu dnia, więc klienta nie trzeba zmieniać.

## What We're NOT Doing

- Nie zmieniamy `.github/workflows/pull-request.yml`. Bramka SQL Server jest **lokalna, ad hoc** (decyzja planu). W CI testy SQL są pomijane.
- Nie dodajemy Testcontainers ani żadnego pakietu NuGet, więc lock-file się nie zmienia. SQLite nie wchodzi w grę.
- Nie uruchamiamy testu krzyżowego (#3) na SQL Server. Reguły liczenia nie zależą od ograniczeń bazy, a InMemory z `FixedTimeProvider` daje deterministyczną granicę.
- Nie wydzielamy wspólnej infrastruktury testów dla istniejących klas Courses. Nowa fabryka SQL jest nowym plikiem, istniejące `CreateFactory` zostają.
- Nie zmieniamy `ActivateFlag`, `HangarFlags` ani `Leaderboard`. Nie zmieniamy definicji „liczba użytkowników = osoby z ≥1 flagą” ani wliczania adminów (decyzje S-06/S-07).
- Nie zmieniamy klienta ani kontraktów API.
- Nie uruchamiamy `/10x-test-plan` do zmiany strategii. §2 dostaje tylko dozwolony backport poprawek z research.

## Implementation Approach

Kolejność według cost × signal i zależności:

1. Środowisko (fixture SQL) z testem-strażnikiem schematu, czyli najtańszym deterministycznym dowodem, że indeks istnieje w migracji.
2. Wyścig na prawdziwym silniku: dowód zachowania.
3. Test hermetyczny dla gałęzi `catch`, której silnik nie wywoła bez konkurenta.
4. Test krzyżowy (red), potem poprawka granicy w `GroupProgress` (green).
5. Cookbook.

Wyrocznie pochodzą z PRD (FR-003 `prd.md:110`, FR-004/Guardrails `prd.md:82`, FR-008 `prd.md:127`, FR-009 `prd.md:128`, `prd.md:71`), nie z kodu.

## Critical Implementation Details

- **Tworzenie bazy przed `Migrate()`.** Bazę trzeba założyć połączeniem do `master`: `CREATE DATABASE [App01Tests_<guid>] COLLATE SQL_Latin1_General_CP1_CI_AS`, a potem `ALTER DATABASE … SET COMPATIBILITY_LEVEL = 110`. `Migrate()` na nieistniejącej bazie utworzyłby ją z kolacją serwera. Jeśli LocalDB odrzuci poziom 110, faza 1 zatrzymuje się i wraca do użytkownika; nie schodzimy z compat po cichu.
- **Sprzątanie.** Przed `EnsureDeleted()` / `DROP DATABASE` wywołaj `SqlConnection.ClearAllPools()`, inaczej pula połączeń trzyma bazę.
- **Wyścig wymaga realnej współbieżności.** Wszystkie żądania startują z jednej bramki (`TaskCompletionSource`), a potem `Task.WhenAll`. Każde żądanie przez `TestServer` ma własny scope i `DbContext`. Transakcja per test z rollbackiem odpada, bo oba zapisy muszą się zatwierdzić.

## Phase 1: Fixture prawdziwego SQL Server

### Overview

Infrastruktura testów na LocalDB, uruchamiana tylko przy ustawionej zmiennej, plus test-strażnik schematu.

### Changes Required:

#### 1. Atrybut pomijania

**File**: `tests/server/App01/App01.Api.Tests/Infrastructure/SqlServer/SqlServerFactAttribute.cs`

**Intent**: Bez zmiennej `APP01_SQLSERVER_TESTS` test ma być oznaczony jako pominięty z powodem typu „ustaw APP01_SQLSERVER_TESTS, aby uruchomić testy na SQL Server”. Tak samo lokalnie i w CI (decyzja: skip wszędzie bez zmiennej).

**Contract**: `SqlServerFactAttribute : FactAttribute`; w konstruktorze ustawia `Skip`, gdy zmienna jest pusta. Zmienna przechowuje connection string **na poziomie serwera, bez `Database=`**, np. `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True`.

#### 2. Fixture bazy i fabryka hosta

**File**: `tests/server/App01/App01.Api.Tests/Infrastructure/SqlServer/SqlServerDatabaseFixture.cs`, `SqlServerWebApplicationFactory.cs`

**Intent**: Na klasę testów: unikalna baza `App01Tests_<guid>` z jawną kolacją i compat 110, potem `Migrate()` produkcyjnymi migracjami, na koniec usunięcie bazy. Fabryka uruchamia host w środowisku `Test` z prawdziwym connection stringiem, konfiguracją JWT i X-TOKEN jak w istniejących testach, bez podmiany `DbContext`.

**Contract**: fixture implementuje `IAsyncLifetime` (`InitializeAsync`: create + compat + `Migrate()`; `DisposeAsync`: `ClearAllPools` + drop) i udostępnia `ConnectionString` oraz `CreateDbContext()`. Fabryka dziedziczy po `WebApplicationFactory<Program>`; `ConnectionStrings:DefaultConnection` = connection string fixture'a; hosted services usunięte. Pomocnik generowania JWT ma ten sam kształt co istniejące `GenerateJwtToken(userId, email, isAdmin)`.

#### 3. Test-strażnik schematu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/ActivateFlag/UserFlagUniquenessSqlServerTests.cs`

**Intent**: Deterministyczny dowód, że baza wymusza unikalność, niezależny od tego, czy wyścig trafi w okno. Łapie usunięcie indeksu z konfiguracji lub migracji.

**Contract**: dwa testy `[SqlServerFact]`:
- `UserFlags_UniqueIndexOnUserIdFlagId_ExistsAfterMigrations`: `sys.indexes` + `sys.index_columns` dla `UserFlags` zawiera indeks `is_unique = 1` na dokładnie `(UserId, FlagId)`.
- `UserFlags_DuplicateRowInsertedDirectly_IsRejectedByDatabase`: dwa zapisy tego samego `(UserId, FlagId)` przez dwa osobne `DbContext` → drugi rzuca `DbUpdateException` z `SqlException.Number` ∈ {2601, 2627}; w tabeli 1 wiersz.

Przypadek brzegowy: ten sam `FlagId` dla dwóch różnych userów jest dozwolony (2 wiersze), czyli indeks nie jest na samym `FlagId`.

### Success Criteria:

#### Automated Verification:

- `dotnet build APPS.sln` bez błędów i nowych ostrzeżeń
- Bez `APP01_SQLSERVER_TESTS`: `dotnet test APPS.sln` przechodzi, testy SQL raportowane jako skipped z powodem
- Z `APP01_SQLSERVER_TESTS` (LocalDB): testy strażnika przechodzą, a po przebiegu nie zostaje baza `App01Tests_*` (`sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT name FROM sys.databases WHERE name LIKE 'App01Tests_%'"` zwraca 0 wierszy)
- `dotnet format APPS.sln --verify-no-changes` czysto

#### Manual Verification:

- Celowe usunięcie `.IsUnique()` z `UserFlagConfiguration` + świeża migracja lokalnie (bez commitu) czerwieni test strażnika; zmiana wycofana

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Wyścig aktywacji (#2) + gałąź hermetyczna

### Overview

Dowód zachowania na prawdziwym silniku oraz test gałęzi „błąd zapisu bez konkurenta”, której silnik łatwo nie wywoła.

### Changes Required:

#### 1. Test wyścigu na SQL Server

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/ActivateFlag/ActivateFlagRaceSqlServerTests.cs`

**Intent**:
- **Zachowanie:** równoległe aktywacje tej samej flagi przez tego samego uczestnika dają dokładnie jeden zapis (Guardrails `prd.md:82`, FR-004), a pozostali wołający dostają `AlreadyOwned`, nie błąd.
- **Regresja łapana:** usunięcie lub osłabienie indeksu, zamiana `catch` na zwrot 500, zamiana ponownego zapytania na bezwarunkowe `AlreadyOwned`.
- **Brzeg:** dwóch różnych userów równolegle aktywuje tę samą flagę i każdy dostaje `Activated`.
- **Anty-wzorzec unikany:** mock zamiast realnych równoległych żądań, SQLite.

**Contract**:
- Seed: kurs opublikowany wczoraj (zegar rzeczywisty), flaga z kodem; użytkownicy zapisani przez EF, Id odczytane z bazy.
- `ActivateFlag_TenParallelRequestsSameUser_ExactlyOneRowAndRankingShowsOne`: 10 POST `api/courses/activate-flag` z tym samym JWT i kodem, start z jednej bramki.
  - Statusy HTTP: same 200.
  - `Status`: dokładnie 1× `Activated`, 9× `AlreadyOwned`.
  - W `UserFlags` jest 1 wiersz dla `(UserId, FlagId)`.
  - `GET api/courses/leaderboard` → wpis tego usera ma `flagCount = 1`.
- `ActivateFlag_ParallelRequestsDifferentUsers_EachGetsOwnRow`: 2 userów × 5 równoległych żądań → każdy ma dokładnie 1 `Activated`, w tabeli są 2 wiersze, ranking pokazuje po 1.

#### 2. Test hermetyczny gałęzi „brak wiersza”

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/ActivateFlag/EndpointTests.cs`

**Intent**: Błąd zapisu, który nie jest konfliktem z równoległym requestem (np. naruszenie FK), nie może zostać zgłoszony jako `AlreadyOwned`. Regresja łapana: `catch` zwracający `AlreadyOwned` bez ponownego sprawdzenia. Gałąź `AlreadyOwned` dowodzi test wyścigu, więc tu jej nie dublujemy (decyzja planu).

**Contract**: `ActivateFlag_SaveFailsWithoutConcurrentRow_ReturnsServerError`. Fabryka InMemory z `SaveChangesInterceptor`, który przy zapisie dodanego `UserFlag` rzuca `DbUpdateException`, przy czym żaden wiersz nie istnieje. Oczekiwane:
- status 500;
- ciało odpowiedzi nie zawiera `AlreadyOwned` ani `Activated`;
- w `UserFlags` 0 wierszy dla tej pary.

### Success Criteria:

#### Automated Verification:

- Z `APP01_SQLSERVER_TESTS`: oba testy wyścigu przechodzą w 5 kolejnych przebiegach (`dotnet test --filter "FullyQualifiedName~ActivateFlagRaceSqlServerTests"` ×5)
- `dotnet test APPS.sln` (bez zmiennej) przechodzi, w tym nowy test hermetyczny
- `dotnet format APPS.sln --verify-no-changes` czysto

#### Manual Verification:

- Celowa zmiana `catch` na `return AlreadyOwned` bez ponownego zapytania czerwieni test hermetyczny; zmiana wycofana

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Test krzyżowy hangar = ranking = dashboard (#3) + granica włączna

### Overview

Test-first: jeden seed, trzy endpointy, wyrocznia policzona ręcznie z PRD. Test jest czerwony na obecnym kodzie; poprawka `GroupProgress` go zazielenia.

### Changes Required:

#### 1. Test krzyżowy (red)

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/FlagCountConsistency/EndpointTests.cs`

**Intent**:
- **Zachowanie:** dla jednego stanu bazy liczby w hangarze, rankingu i dashboardzie spełniają FR-008 i FR-009 / `prd.md:71`.
- **Regresja łapana:**
  - dryf predykatu „opublikowany” w którymkolwiek z trzech handlerów;
  - ostra granica `PublishDate` lub `EarnedAt` na dashboardzie;
  - wyłączenie adminów w jednym widoku, a w innym nie;
  - liczenie flag nieopublikowanego kursu.
- **Brzegi:** kurs z `PublishDate == Now`, zdobycie z `EarnedAt == Now`, admin, uczestnik bez flag, zdobycie na kursie nieopublikowanym, flaga bez kryteriów.
- **Anty-wzorce unikane:** wyrocznia przepisana z obliczeń produkcyjnych, trzy osobne seedy, porównanie sumy rankingu z dashboardem.

**Contract**: jedna fabryka InMemory (wzorzec `GroupProgress/EndpointTests.cs:55-175`, `Now = 2026-06-01T12:00Z`) i klient per user (JWT per userId).

Seed:

| Kurs | `PublishDate` | Flagi |
|---|---|---|
| K1 | Now − 10 d | F1, F2, F3, F4 (F4 bez kryteriów) |
| K2 | **Now** | F5 |
| K3 | Now + 1 min | F6 |

| User | Rola | Zdobycia (`EarnedAt`) |
|---|---|---|
| U2 | uczestnik | F1 (Now − 5 d), F2 (Now − 1 d), F5 (**Now**) |
| U3 | uczestnik | F1 (Now − 3 d), F6 (Now − 1 d; kurs nieopublikowany) |
| U4 | admin (`IsAdmin = true`) | F3 (Now − 2 d) |
| U5 | uczestnik | — |

Wyrocznia (policzona ręcznie z PRD, wpisana literalnie):

| Widok | Oczekiwane |
|---|---|
| hangar `allCount` (każdy user) | 5 |
| hangar `earnedCount` | U2 = 3, U3 = 1, U4 = 1, U5 = 0 |
| ranking | U2 = 3, U3 = 1, U4 = 1; `TotalCount` = 3; brak U5 |
| dashboard (bez `asOf`) | `UserCount` 3, `AvailableFlagCount` 5, `EarnedFlagCount` 4, `EarnedPercent` 80.0 |

Asercje krzyżowe, oprócz literałów:
- dla każdego usera `ranking.flagCount == hangar.earnedCount` (brak w rankingu ⇔ 0);
- `dashboard.AvailableFlagCount == hangar.allCount`;
- `dashboard.UserCount == ranking.TotalCount`;
- `dashboard.EarnedFlagCount ==` liczba różnych `flagId` zdobytych w odpowiedziach hangarów U2–U5.

Na obecnym kodzie dashboard daje 3 / 4 / 3 / 75.0, więc test jest czerwony. Gdyby poprawiono tylko `PublishDate`, a `EarnedAt` został ostry, dashboard dałby `Earned` 3 i 60.0, więc test nadal jest czerwony.

Test nazwany np. `FlagCounts_OneSeed_HangarLeaderboardAndDashboardAgree`.

#### 2. Poprawka granicy w dashboardzie (green)

**File**: `src/server/App01/App01.Modules.Courses/Features/GroupProgress/Handler.cs`

**Intent**: Dashboard liczy kursy opublikowane i zdobycia **włącznie** z momentem `asOf`, tak jak hangar i ranking (FR-003 „równa lub wcześniejsza”, FR-008 „ta sama liczba”). Decyzja użytkownika z 2026-10-11.

**Contract**: `Flags.Where(f => f.Course.PublishDate <= asOf)` oraz `UserFlags.Where(uf => uf.EarnedAt <= asOf && uf.Flag.Course.PublishDate <= asOf)`. Komentarze w kodzie zaktualizowane („do `asOf` włącznie”). Obcinanie przyszłego `asOf` do `now` bez zmian.

#### 3. Aktualizacja sprzecznych testów dashboardu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/GroupProgress/EndpointTests.cs`

**Intent**: Testy, które przypinały granicę ostrą, przechodzą na nową wyrocznię. Zachowują swoje przypadki brzegowe, ale z odwróconym oczekiwaniem na granicy.

**Contract**:
- `GroupProgress_SkipsUnpublishedCourses` (:282): kurs z `PublishDate == Now` **jest** liczony.
- `GroupProgress_AsOfInPast_CountsOnlyEarlierEvents` (:311): kurs opublikowany dokładnie w `asOf` i zdobycie dokładnie w `asOf` **są** liczone, zdobycie po `asOf` nie. Nowe literały policzone ręcznie z seedu testu.
- Komentarze „granica ostra” usunięte.
- Nazwy testów można doprecyzować; reszta testów bez zmian.

### Success Criteria:

#### Automated Verification:

- Przed poprawką handlera: `dotnet test --filter "FullyQualifiedName~FlagCountConsistency"` czerwony, z rozbieżnością na dashboardzie (zapis wyniku w notatce fazy)
- Po poprawce: `dotnet test APPS.sln` przechodzi w całości
- `dotnet build APPS.sln` bez ostrzeżeń, `dotnet format APPS.sln --verify-no-changes` czysto

#### Manual Verification:

- Strona „Postęp grupy” dla dzisiejszej daty pokazuje te same liczby co przed zmianą dla danych lokalnych, w których żaden kurs nie ma publikacji równej końcowi dnia (sanity check UI)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Test-plan — cookbook, stack, bramka, backport

### Overview

Zapis wzorców i decyzji tej fazy w `context/foundation/test-plan.md`.

### Changes Required:

#### 1. Aktualizacja test-planu

**File**: `context/foundation/test-plan.md`

**Intent**: Następna osoba ma wiedzieć, jak uruchomić testy na prawdziwym silniku i jak pisać test krzyżowy, bez ponownego researchu.

**Contract**:
- §4 Stack: wiersz „integration na prawdziwej bazie” = LocalDB (lokalnie) przez `APP01_SQLSERVER_TESTS`, `Migrate()`, kolacja `SQL_Latin1_General_CP1_CI_AS`, compat 110; brak w CI.
- §5 Quality Gates: wiersz SQL Server → „local, ad hoc”, „skipped bez zmiennej”, z uzasadnieniem (brak Dockera lokalnie, decyzja planu).
- §6: zastąpić dwa „TBD — see §3 Phase 2” wzorcami „flaga liczona raz przy równoległych aktywacjach” (bramka startowa, ranking jako sygnał, strażnik `sys.indexes`, hermetyczny `catch`) oraz „jeden seed, hangar = ranking = dashboard” (tożsamości z PRD, wyrocznia literalna, brzegi `== Now`).
- §2 backport z research (dozwolona edycja): w Risk Response Guidance #3 zamiast „suma na dashboardzie jest z nimi zgodna” wpisać tożsamości `available == allCount`, `userCount == TotalCount`, `earned == różne flagi grupy`; w #2 dopisać hermetyczny `catch` jako tańszy sygnał uzupełniający. Bez kotwic plik:linia.

### Success Criteria:

#### Automated Verification:

- `grep -c "TBD — see §3 Phase 2" context/foundation/test-plan.md` zwraca 0

#### Manual Verification:

- Przegląd §4–§6: instrukcja uruchomienia testów SQL jest kompletna (nazwa zmiennej, przykładowy connection string, polecenie)

---

## Testing Strategy

### Unit Tests:

- Brak; logika jest w handlerach testowanych na poziomie endpointów.

### Integration Tests:

- SQL Server (LocalDB, ad hoc): strażnik schematu (2 testy), wyścig (2 testy).
- InMemory: test hermetyczny `catch` (1), test krzyżowy (1), zaktualizowane testy `GroupProgress` (2).

### Manual Testing Steps:

1. `$env:APP01_SQLSERVER_TESTS = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True"`, potem `dotnet test APPS.sln` → testy SQL wykonane, nie pominięte.
2. Bez zmiennej → testy SQL pominięte z powodem.
3. Mutacje ręczne z faz 1–2 (usunięcie indeksu, `catch` bez ponownego zapytania) czerwienią odpowiednie testy.

## Performance Considerations

Każda klasa testów SQL tworzy i migruje własną bazę (kilka sekund na LocalDB). Dwie klasy to akceptowalny koszt przebiegu ad hoc.

## Migration Notes

Brak zmian schematu i brak nowych migracji. Zmiana `GroupProgress` zmienia wynik tylko dla zdarzeń dokładnie równych `asOf`.

## References

- Research: `context/changes/testing-flag-consistency/research.md`
- Test plan: `context/foundation/test-plan.md` (§2 #2/#3, §3 Phase 2)
- PRD: `context/foundation/prd.md:71, 82, 110, 127, 128`
- Wzorzec testu endpointu z seedem i `FixedTimeProvider`: `tests/server/App01/App01.Api.Tests/Features/Courses/GroupProgress/EndpointTests.cs:25-175`
- Archiwum: `context/archive/2026-10-10-flag-activation/plan.md:48`, `context/archive/2026-10-10-group-progress-dashboard/plan.md:18-30, 135`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Fixture prawdziwego SQL Server

#### Automated

- [x] 1.1 `dotnet build APPS.sln` bez błędów i nowych ostrzeżeń
- [x] 1.2 Bez `APP01_SQLSERVER_TESTS`: `dotnet test APPS.sln` przechodzi, testy SQL raportowane jako skipped z powodem
- [x] 1.3 Z `APP01_SQLSERVER_TESTS` (LocalDB): testy strażnika przechodzą, po przebiegu nie zostaje baza `App01Tests_*`
- [x] 1.4 `dotnet format APPS.sln --verify-no-changes` czysto

#### Manual

- [x] 1.5 Celowe usunięcie `.IsUnique()` + świeża migracja lokalnie czerwieni test strażnika; zmiana wycofana

### Phase 2: Wyścig aktywacji (#2) + gałąź hermetyczna

#### Automated

- [ ] 2.1 Z `APP01_SQLSERVER_TESTS`: oba testy wyścigu przechodzą w 5 kolejnych przebiegach
- [ ] 2.2 `dotnet test APPS.sln` (bez zmiennej) przechodzi, w tym nowy test hermetyczny
- [ ] 2.3 `dotnet format APPS.sln --verify-no-changes` czysto

#### Manual

- [ ] 2.4 Celowa zmiana `catch` na `return AlreadyOwned` bez ponownego zapytania czerwieni test hermetyczny; zmiana wycofana

### Phase 3: Test krzyżowy hangar = ranking = dashboard (#3) + granica włączna

#### Automated

- [ ] 3.1 Przed poprawką handlera test `FlagCountConsistency` czerwony z rozbieżnością na dashboardzie
- [ ] 3.2 Po poprawce `dotnet test APPS.sln` przechodzi w całości
- [ ] 3.3 `dotnet build APPS.sln` bez ostrzeżeń, `dotnet format APPS.sln --verify-no-changes` czysto

#### Manual

- [ ] 3.4 Strona „Postęp grupy” dla dzisiejszej daty pokazuje spójne liczby (sanity check UI)

### Phase 4: Test-plan — cookbook, stack, bramka, backport

#### Automated

- [ ] 4.1 `grep -c "TBD — see §3 Phase 2" context/foundation/test-plan.md` zwraca 0

#### Manual

- [ ] 4.2 Przegląd §4–§6: instrukcja uruchomienia testów SQL jest kompletna
