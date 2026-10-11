---
date: 2026-10-11T14:55:23+02:00
researcher: Claude (Opus 5.5) dla Tomasz Mularczyk
git_commit: 7055527
branch: main
repository: 10xDevs4
topic: "Rollout Phase 2 test-plan.md — spójność flag na prawdziwym silniku (ryzyka #2, #3)"
tags: [research, testing, courses, activate-flag, leaderboard, hangar, group-progress, sql-server, localdb]
status: complete
last_updated: 2026-10-11
last_updated_by: Claude (Opus 5.5)
---

# Research: spójność flag na prawdziwym silniku (Phase 2, ryzyka #2 i #3)

**Date**: 2026-10-11T14:55:23+02:00
**Researcher**: Claude (Opus 5.5) dla Tomasz Mularczyk
**Git Commit**: 7055527 (kopia robocza: niezacommitowana zmiana §3 w `context/foundation/test-plan.md`)
**Branch**: main
**Repository**: 10xDevs4

## Research Question

Ugruntować rollout Phase 2 z `context/foundation/test-plan.md`:

- **#2** — dwie równoległe aktywacje tej samej flagi przez tego samego uczestnika dają dokładnie jeden zapis, druga dostaje `AlreadyOwned`, ranking pokazuje 1 — na prawdziwym SQL Server (nie InMemory, nie SQLite). Do zakwestionowania: „indeks w konfiguracji EF = baza go wymusza” oraz „test InMemory dowodzi jednokrotności”.
- **#3** — przy jednym seedzie (kursy opublikowane/nieopublikowane, granica daty, admin, kilku uczestników) liczba flag uczestnika w hangarze = liczba w rankingu, a dashboard jest z nimi zgodny; wyrocznia z PRD. Do zakwestionowania: „każdy endpoint ma swój test, więc są spójne”.

Dodatkowo: indeks `(UserId, FlagId)` w migracji, obsługa `DbUpdateException`, porównanie kodu bez rozróżniania wielkości liter, `asOf` z przyszłości, daty UTC, wykonalność prawdziwego SQL Server lokalnie i w CI.

## Summary

1. **#2 — ochrona istnieje w schemacie, ale żaden test jej nie dotyka.** Unikalny indeks `(UserId, FlagId)` jest w konfiguracji (`UserFlagConfiguration.cs:35-37`), w migracji `20261008062237_CoursesFlags.cs:87-92` i w snapshocie (`AppDbContextModelSnapshot.cs:104-105`). Handler na ścieżce wyścigu łapie każdy `DbUpdateException`, odpina encję, ponownie sprawdza istnienie wiersza i zwraca `AlreadyOwned` tylko gdy wiersz jest; inaczej `throw` (`ActivateFlag/Handler.cs:92-106`). Wszystkie 11 testów `ActivateFlag/EndpointTests.cs` działa na EF InMemory, który nie wymusza indeksów unikalnych — `AlreadyOwned_ReturnsAlreadyOwnedWithoutSecondRow` (:266) pokrywa tylko pre-check `AnyAsync` (`Handler.cs:72-77`). Usunięcie indeksu z konfiguracji lub migracji nie zaczerwieni żadnego testu. Archiwalny plan to potwierdza: „gałąź z `DbUpdateException` jest zweryfikowana wyłącznie przeglądem kodu” (`archive/2026-10-10-flag-activation/plan-brief.md:70`). Wyrocznia #2 z test-planu jest trafna.
2. **#2 — ranking jest czuły na duplikat.** Leaderboard liczy wiersze (`g.Count()`, `Leaderboard/Handler.cs:52`), więc zduplikowany `UserFlag` dałby 2. Hangar liczy per flagę (podzapytanie, `HangarFlags/Handler.cs:60-63`) — duplikat by ukrył. Asercja „ranking pokazuje 1” po wyścigu jest więc właściwym sygnałem; asercja na hangarze nie.
3. **#3 — hangar i ranking mają tę samą regułę; dashboard ma inną.** Hangar i ranking: `Course.PublishDate <= now`, bez filtra `EarnedAt` (`HangarFlags/Handler.cs:52`, `Leaderboard/Handler.cs:47`). Dashboard: `PublishDate < asOf` **oraz** `EarnedAt < asOf`, z `asOf = min(requested, now)` (`GroupProgress/Handler.cs:42-53`). Przy `PublishDate == now` flaga jest w hangarze i w rankingu, ale nie na dashboardzie. Testy przypinają obie strony sprzeczności osobno (`Leaderboard_CountsOnlyPublishedCourses` — „PublishDate == teraz jest liczony”; `GroupProgress_SkipsUnpublishedCourses` — „granica ostra”). Plan dashboardu wiedział o `<=` w hangarze/rankingu (`archive/2026-10-10-group-progress-dashboard/plan.md:18`), wybrał `<` (`:24-30`) i jednocześnie twierdzi, że „możliwe == `allCount` hangaru” (`:135, :219, :239`) — to twierdzenie jest fałszywe na granicy. **To wymaga decyzji produktowej (Open Questions #1).**
4. **#3 — założenie „suma na dashboardzie zgodna z hangarem/rankingiem” jest błędne wobec PRD.** PRD definiuje wskaźnik jako „różne flagi zdobyte przez grupę / flagi opublikowanych kursów” (`prd.md:71`), a dashboard liczy `Distinct(FlagId)` (`GroupProgress/Handler.cs:55-58`). Suma `flagCount` z rankingu ≠ `EarnedFlagCount` dashboardu, gdy dwie osoby mają tę samą flagę — to zamierzone. Prawdziwe tożsamości krzyżowe (z PRD i planu dashboardu) to: `availableFlagCount` dashboardu == `allCount` hangaru; `userCount` dashboardu == liczba wierszy rankingu (`TotalCount`); `earnedCount` hangaru użytkownika == jego `flagCount` w rankingu (FR-008, `prd.md:127`); `EarnedFlagCount` == liczba różnych flag w zbiorze zdobyć z hangarów wszystkich uczestników.
5. **Brak testu krzyżowego.** Żaden test nie woła dwóch endpointów na jednym seedzie; każda klasa testów kopiuje własną fabrykę z `UseInMemoryDatabase($"TestDb_{Guid}")`, `FixedTimeProvider(Now = 2026-06-01T12:00Z)` i `GenerateJwtToken` (np. `Leaderboard/EndpointTests.cs:166-200`, `GroupProgress/EndpointTests.cs:25-175`). Wspólne `Infrastructure/TestWebApplicationFactory.cs` i `ConfigurableTestWebApplicationFactory.cs` używają tylko testy Portalu i nie mają `TimeProvider` ani seedu.
6. **Prawdziwy SQL Server: najtańsza wykonalna droga to LocalDB lokalnie + kontener `services: mssql` w CI**, obie przez jeden connection string ze zmiennej środowiskowej. Lokalnie: brak Dockera, brak WSL; `sqllocaldb info` → `MSSQLLocalDB`, 17.0.4025.3 (sonda z 2026-10-11). CI: `backend-ci` na `ubuntu-latest` (`.github/workflows/pull-request.yml:19`) — Docker jest. Testcontainers.MsSql odpada lokalnie bez instalacji Dockera. Aplikacja już rejestruje `UseSqlServer(..., UseCompatibilityLevel(110))` (`SharedInfrastructureDI.cs:34-35`), a `Microsoft.Data.SqlClient` jest tranzytywnie w projekcie testów (`packages.lock.json:243`) — wariant LocalDB/services nie wymaga nowego pakietu NuGet ani zmiany lock-file'a. SQLite odrzucony zgodnie z test-planem §2/§4.
7. **Fixture na prawdziwej bazie musi używać `Database.Migrate()`, nie `EnsureCreated()`.** Tylko migracja dowodzi, że indeks jest w migracji (nie tylko w modelu). Żaden kod w `src` nie woła `Migrate()`/`EnsureCreated()`; robią to wyłącznie fabryki testów. Migracje używają `migrationBuilder.Sql` tylko do seedów (`PortalFirstUserAdmin.cs:13`, `LottoDicts.cs:13,27`) — nic zależnego od wersji silnika (wniosek z lektury, migracji na LocalDB nie uruchomiono).

## Detailed Findings

### Ryzyko #2 — aktywacja flagi (`Features/ActivateFlag/Handler.cs`)

- `:45-49` walidacja; `Validator.cs:15-16` odrzuca pusty kod i dłuższy niż 50 znaków.
- `:52` `userId` wyłącznie z JWT.
- `:57-62` wyszukanie **po samym kodzie**: `normalizedCode = request.Code.Trim().ToUpperInvariant()`, `.Where(f => f.Code.ToUpper() == normalizedCode && f.Course.PublishDate <= now)`. Komentarz: nie polegamy na kolacji bazy (InMemory rozróżnia wielkość liter). Obie strony porównania są wielkimi literami, więc wynik nie zależy od kolacji (CI na SQL Server, CS w InMemory) — dla tego zapytania nie ma rozjazdu silników.
- `:72-77` pre-check `AnyAsync(UserId, FlagId)` → `AlreadyOwned`.
- `:80-90` `Add(UserFlag{UserId, FlagId, EarnedAt = now})` + `SaveChangesAsync`, bez jawnej transakcji.
- `:92-106` `catch (DbUpdateException)` — bez sprawdzania `SqlException.Number` 2601/2627; odpięcie encji, ponowne `AnyAsync`; brak wiersza → `throw` (np. FK → 500 przez middleware), jest wiersz → `LogWarning` + `AlreadyOwned`. Że ponowne zapytanie zobaczy zatwierdzony wiersz konkurenta, wynika z blokady klucza pod READ COMMITTED — **wniosek, nie zweryfikowany**; dokładnie to sprawdzi test równoległy na prawdziwym silniku.
- `:86` to jedyne miejsce w `src/server`, które wstawia `UserFlag`.

### Schemat

- `Entities/Courses/UserFlag.cs:5-13` — `int Id` (PK identity), `long UserId`, `int FlagId`, `DateTime EarnedAt`.
- `Configurations/Courses/UserFlagConfiguration.cs:35-37` — `HasIndex(e => new { e.UserId, e.FlagId }).IsUnique()`; FK Flag → Restrict, User → Cascade (`:39-47`).
- `Configurations/Courses/FlagConfiguration.cs:26-29, :39-40` — `Code varchar(50)`, unikalny indeks na `Code`.
- Migracja `20261008062237_CoursesFlags.cs:68-73` (`IX_Flags_Code`), `:87-92` (`IX_UserFlags_UserId_FlagId`, unique). Snapshot `:75-76`, `:104-105`.
- Brak `UseCollation` w `src/server` — kolacja = domyślna bazy. Skutek uboczny (nie ścieżka podwójnego zaliczenia): kody różniące się tylko wielkością liter SQL Server (CI) odrzuci indeksem, InMemory przyjmie.

### Ryzyko #2 — istniejące testy (`tests/.../Courses/ActivateFlag/EndpointTests.cs`)

11 metod testowych (:215–:392): aktywacja, kod przycięty/małe litery, flaga bez kryteriów, `AlreadyOwned` (pre-check), flaga posiadana przez innego, nieznany kod, kurs nieopublikowany (+ równość `PublishDate == now` → aktywacja), brak kodu w JSON, 400 (Theory), 401, 403. **Brak** testu gałęzi `DbUpdateException` (ani równoległego, ani hermetycznego). Fabryka per klasa: `CreateFactory()` (:68-120), InMemory `TestDb_{Guid}`, `FixedTimeProvider(Now)`, `EnsureCreated()` + seed (user 2 posiada flagę 11, user 3 flagę 10).

### Ryzyko #3 — reguły liczenia

| Widok | Plik | Granica `PublishDate` | Filtr `EarnedAt` | Co liczy | Admini |
|---|---|---|---|---|---|
| Hangar | `HangarFlags/Handler.cs:50-63, :86-87` | `<= now` | brak | `allCount` = flagi opublikowanych kursów (także bez kryteriów); `earnedCount` = flagi z wierszem bieżącego usera (per flaga) | n/d (bieżący user) |
| Ranking | `Leaderboard/Handler.cs:45-60` | `<= now` | brak | `FlagCount = g.Count()` (wiersze), inner join do `Users`; userzy z 0 pominięci | wliczeni (`archive/.../leaderboard/plan.md:32`) |
| Dashboard | `GroupProgress/Handler.cs:40-68` | `< asOf` | `< asOf` | `Available` = flagi z `PublishDate < asOf`; `Earned` = `Distinct(FlagId)`; `UserCount` = `Distinct(UserId)` bez joinu do `Users`; `%` = round(earned·100/available, 1) lub `null` | wliczeni (`archive/.../group-progress-dashboard/plan.md:41`) |
| Zadania (kontekst) | `HangarTasks/Handler.cs:50-51` | `<= now` | — | tylko flagi z kryteriami — lista zadań, nie licznik | — |

- `asOf` z przyszłości jest obcinane do `now` (`GroupProgress/Handler.cs:42-43`); `Unspecified` traktowane jako UTC, `Local` konwertowane (`:83-91`); walidator wymaga tylko `asOf >= 2000-01-01`.
- Zegar: wszystkie trzy handlery biorą `_timeProvider.GetUtcNow().UtcDateTime`; `TimeProvider.System` rejestruje `ModuleDI.cs:20`. Brak `DateOnly`/`.Today`.
- UTC w DTO: hangar `SpecifyKind` na `EarnedAt` (`HangarFlags/Handler.cs:80`), dashboard na `AsOf` (`GroupProgress/Handler.cs:75`) — zgodnie z `lessons.md`. Lekcja dotyczy formatu JSON, nie liczenia; na prawdziwym silniku `datetime2` wraca jako `Unspecified`, a porównania w SQL i tak działają na wartościach.

### Rozbieżności (#3), od najistotniejszej

1. **Granica `PublishDate`**: `<=` (hangar, ranking) vs `<` (dashboard). Przy `PublishDate == now` dashboard ma mniej możliwych, mniej zdobytych i potencjalnie mniej userów niż hangar/ranking. Deterministyczne w testach z `FixedTimeProvider`; w produkcji wymaga trafienia w dokładny tick (wpływ niski, ale sprzeczność specyfikacji realna).
2. **Filtr `EarnedAt < asOf`** tylko na dashboardzie. `ActivateFlag` ustawia `EarnedAt = now` (`Handler.cs:85`), więc przy zamrożonym zegarze flaga zdobyta „teraz” jest w rankingu, a nie na dashboardzie. W produkcji późniejszy request ma większe `now` — wpływ niski.
3. **Join do `Users`**: ranking ma inner join, `UserCount` dashboardu nie. Na SQL Server FK Cascade czyni to nieistotnym; w InMemory osierocony `UserFlag` dałby `UserCount > TotalCount`. Artefakt InMemory.
4. **Duplikat `(UserId, FlagId)`**: ranking liczy wiersze, hangar per flagę, dashboard distinct. Rozjazd tylko przy braku indeksu — łączy się z #2.
5. **Zamierzona różnica semantyki**: `EarnedFlagCount` dashboardu to różne flagi grupy (`prd.md:71`), nie suma per uczestnik — porównanie „suma rankingu == dashboard” byłoby fałszywą wyrocznią.

### Wyrocznia z PRD (dla testu krzyżowego)

- FR-008 (`prd.md:126-127`): kolejność rankingu wg liczby zdobytych flag — „ta sama liczba, którą uczestnik widzi w hangarze”. → `ranking.flagCount(u) == hangar(u).earnedCount`.
- FR-009 (`prd.md:128`) + `prd.md:71`: cztery wskaźniki „na dany dzień”; procent = różne flagi zdobyte przez grupę / flagi opublikowanych kursów. → `dashboard.available == hangar.allCount`, `dashboard.earned == |⋃ flag zdobytych w hangarach|`, `dashboard.%` z tych dwóch liczb.
- Admini: PRD milczy; plany S-06/S-07 wliczają adminów (`leaderboard/plan.md:32`, `group-progress-dashboard/plan.md:41`). „Liczba użytkowników” = osoby z ≥1 flagą to decyzja planu S-07 (`plan.md:24-30`), nie litera PRD.
- „Opublikowany”: FR-003 mówi o dacie „równej aktualnej lub wcześniejszej” (`prd.md:110`) — literalnie granica **włączna**, zgodna z hangarem/rankingiem, niezgodna z dashboardem.

### Prawdziwy SQL Server — wykonalność

| Opcja | Lokalnie (sondy 2026-10-11) | CI (`ubuntu-latest`) | Koszt |
|---|---|---|---|
| **A. LocalDB lokalnie + `services: mssql` w CI**, wspólna zmienna z connection stringiem | działa: `(localdb)\MSSQLLocalDB` 17.0.4025.3, auto-create | działa: obraz `mcr.microsoft.com/mssql/server:2022-latest`, `ACCEPT_EULA`, `MSSQL_SA_PASSWORD`, health-check | bez nowych pakietów; dwa silniki (2025 lokalnie, 2022 w CI); zmiana `pull-request.yml` (dopisanie bloku `services`, nie nowy pipeline) |
| B. Testcontainers.MsSql | **niewykonalne** bez instalacji Docker Desktop + WSL2 | działa | nowy pakiet + lock-file; Context7: `new MsSqlBuilder(<image>).Build()` + `IAsyncLifetime` (checked: 2026-10-11) |
| C. LocalDB w CI | działa | wymaga joba `windows-latest`, obecność LocalDB na runnerze niezweryfikowana | wolniej, rozdzielony job |
| SQLite | — | — | **odrzucone** (test-plan §2/§4: inna semantyka porównań, inne ograniczenia) |

Uwagi projektowe (EF Core testing docs przez Context7, checked: 2026-10-11):
- Test wyścigu musi zatwierdzić dwa realne requesty HTTP — rollback transakcji per test nie wchodzi w grę. Baza per klasa testów (`App01Tests_{Guid}`), `Migrate()` w `InitializeAsync`, `EnsureDeleted()` w `DisposeAsync`.
- Fabryka na prawdziwej bazie nie podmienia `DbContext` — wystarczy podać prawdziwy `ConnectionStrings:DefaultConnection`; zostaje produkcyjne `UseSqlServer` + `UseCompatibilityLevel(110)`.
- Brak zmiennej środowiskowej: lokalnie skip, w CI (zmienna `CI`) fail — inaczej bramka „required” z test-planu §5 przechodziłaby po cichu.
- `UseCompatibilityLevel(110)` zmienia tylko generowany SQL; ani LocalDB 2025, ani kontener 2022 nie są silnikiem 2012 (produkcja: Webio, SQL Server 2012 — `context/foundation/infrastructure.md`).

## Code References

- `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Handler.cs:57-62` — wyszukanie po kodzie, upper-case po obu stronach
- `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Handler.cs:72-77` — pre-check `AlreadyOwned`
- `src/server/App01/App01.Modules.Courses/Features/ActivateFlag/Handler.cs:92-106` — `catch (DbUpdateException)` + ponowne zapytanie
- `src/server/App01/App01.Shared.Infrastructure/Repositories/Configurations/Courses/UserFlagConfiguration.cs:35-37` — unikalny indeks
- `src/server/App01/App01.Shared.Infrastructure/Migrations/20261008062237_CoursesFlags.cs:87-92` — indeks w migracji
- `src/server/App01/App01.Modules.Courses/Features/HangarFlags/Handler.cs:50-63, 86-87` — reguła hangaru
- `src/server/App01/App01.Modules.Courses/Features/Leaderboard/Handler.cs:45-60` — reguła rankingu (`g.Count()`)
- `src/server/App01/App01.Modules.Courses/Features/GroupProgress/Handler.cs:40-68` — reguła dashboardu (`<`, `EarnedAt`, distinct)
- `src/server/App01/App01.Shared.Infrastructure/SharedInfrastructureDI.cs:27-35` — `UseSqlServer` + compat 110
- `tests/server/App01/App01.Api.Tests/Features/Courses/ActivateFlag/EndpointTests.cs:68-120, 266` — fabryka InMemory, test pre-checku
- `tests/server/App01/App01.Api.Tests/Infrastructure/TestWebApplicationFactory.cs:28, 36-53` — wzorzec podmiany na InMemory (Portal)
- `.github/workflows/pull-request.yml:19, 44-48` — runner i krok testów

## Architecture Insights

- Gwarancja „flaga raz” jest dwuwarstwowa: pre-check w handlerze (pokryty) + indeks w bazie z obsługą wyścigu (niepokryty). Tylko druga warstwa chroni przed równoległymi requestami.
- Trzy niezależne miejsca liczenia bez wspólnego predykatu „flaga opublikowana” — reguła jest powielona w czterech handlerach (`HangarFlags`, `HangarTasks`, `Leaderboard`, `ActivateFlag` z `<=`; `GroupProgress` z `<`). Test krzyżowy na jednym seedzie jest jedyną rzeczą, która złapie dryf przy następnej zmianie.
- Testy Courses nie mają wspólnej infrastruktury — każda klasa ma kopię fabryki, JWT i seedów. Test krzyżowy będzie potrzebował jednej fabryki dla trzech endpointów; fabryka na prawdziwy SQL Server będzie pierwszą współdzieloną.

## Historical Context (from prior changes)

- `context/archive/2026-10-10-flag-activation/plan.md:48` — „EF InMemory nie wymusza unikalnych indeksów, więc tej gałęzi nie da się pokryć testem endpointu.” — **supported** (nadal prawda dla InMemory; prawdziwy silnik to zmienia).
- `context/archive/2026-10-10-flag-activation/plan.md:13` — „`DbUpdateException` nie jest dziś obsługiwany nigdzie w kodzie” — **supported** w chwili pisania; dziś jedyne miejsce to `ActivateFlag/Handler.cs:92`. Plan mówił „`DbUpdateException` → `AlreadyOwned`”; implementacja jest ostrzejsza (ponowne zapytanie + `throw`).
- `context/archive/2026-10-10-flag-activation/plan-brief.md:70` — gałąź zweryfikowana tylko przeglądem kodu — **supported**.
- `context/archive/2026-10-10-leaderboard/plan.md:5,10,127` — ranking używa tego samego filtra co hangar i zgadza się z `earnedCount` — **supported** (`<=` w obu).
- `context/archive/2026-10-10-group-progress-dashboard/plan.md:135, 219, 239` — „możliwe == `allCount` hangaru”, „użytkownicy == wiersze rankingu” — **partial**: prawda, gdy żaden `PublishDate` ani `EarnedAt` nie jest `>= now`; fałsz na granicy (`:24-30` wybiera `<`, `:18` znał `<=`).
- `context/archive/2026-10-11-testing-course-content-access/research.md:106` — faza 1 nie potrzebowała prawdziwej bazy — nie dotyczy tej fazy.

## Related Research

- `context/archive/2026-10-11-testing-course-content-access/research.md` — faza 1 (granica dat `PublishDate <= now` w `CourseContent`, ta sama konwencja włączna).

## Corrections to test-plan §2 (do backportu przez `/10x-test-plan`)

- **#3 Risk Response Guidance**: „suma na dashboardzie jest z nimi zgodna” → zastąpić tożsamościami z PRD: `dashboard.available == hangar.allCount`, `dashboard.userCount == ranking.TotalCount`, `dashboard.earned == liczba różnych flag zdobytych przez grupę` (`prd.md:71`). Porównanie sumy rankingu z dashboardem byłoby fałszywą wyrocznią.
- **#3 Source/likelihood**: rozbieżność nie jest hipotetyczna — granica `<=` vs `<` istnieje dziś i jest przypięta sprzecznymi testami.
- **#2 cheapest layer**: oprócz testu wyścigu na SQL Server tani sygnał daje test hermetyczny gałęzi `catch` (stub `SaveChanges` rzuca `DbUpdateException`: wiersz jest → `AlreadyOwned`; brak → 500) — działa w zwykłym `dotnet test` bez bazy.
- **§4 Stack**: wybór warstwy prawdziwej bazy — LocalDB lokalnie + `services: mssql` w CI (Testcontainers niewykonalne lokalnie bez Dockera).

## Open Questions

1. **ROZSTRZYGNIĘTE (2026-10-11, decyzja użytkownika): granica włączna wszędzie.** Wyrocznia #3: kurs z `PublishDate == Now` liczy się w hangarze, rankingu **i** na dashboardzie (FR-003, FR-008). Test krzyżowy z takim kursem ma być czerwony na obecnym kodzie; faza poprawia `GroupProgress` (`PublishDate <= asOf`; analogicznie `EarnedAt <= asOf`, żeby flaga zdobyta „teraz” była w rankingu i na dashboardzie) i aktualizuje sprzeczny test `GroupProgress_SkipsUnpublishedCourses`. Pierwotne pytanie poniżej dla kontekstu.
   *Pierwotnie:* **[blokuje wyrocznię #3] Granica publikacji na dashboardzie: `<=` czy `<`?** PRD FR-003 (`prd.md:110`) mówi „równa aktualnej dacie lub wcześniejsza” (włącznie) i FR-008 wymaga tej samej liczby co hangar. Dashboard (S-07) świadomie wybrał `<`. Opcje: (a) wyrocznia = granica włączna wszędzie → test krzyżowy z kursem `PublishDate == Now` będzie czerwony, a faza musi poprawić `GroupProgress` (zmiana zachowania produkcyjnego, nie tylko test); (b) zaakceptować `<` dla `asOf` w przeszłości, ale dla „teraz” wymagać zgodności; (c) uznać rozjazd na ticku za akceptowalny i wyłączyć granicę z testu krzyżowego (ryzyko spisać w §7). Decyzja należy do właściciela produktu.
2. **Kolacja bazy testowej.** LocalDB bierze kolację z locale systemu (prawdopodobnie `Polish_CI_AS`), kontener — `SQL_Latin1_General_CP1_CI_AS`, produkcji (Webio) nie znamy. Dla #2 nie ma to wpływu (porównanie upper-case po obu stronach), ale unikalny indeks na `Flag.Code` zależy od kolacji. Przypiąć kolację bazy testowej jawnie lub ustalić produkcyjną.
3. **Poziom kompatybilności 110 na bazie testowej** (`ALTER DATABASE … SET COMPATIBILITY_LEVEL = 110`) — czy ustawiać; nie zweryfikowano, że SQL Server 2025 wspiera 110. Nie odtwarza i tak silnika 2012.
4. **Migracja na świeżej bazie** — wniosek z lektury, że `Migrate()` przejdzie na LocalDB; nieuruchomione.
5. **Zachowanie bramki bez SQL Server** — skip lokalnie / fail w CI (propozycja) — do potwierdzenia w planie.
