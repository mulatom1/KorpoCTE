# Spójność flag na prawdziwym silniku — Plan Brief

> Full plan: `context/changes/testing-flag-consistency/plan.md`
> Research: `context/changes/testing-flag-consistency/research.md`

## What & Why

Faza 2 test-planu, ryzyka #2 i #3.

- **#2:** flaga nie może zostać policzona drugi raz (PRD Guardrails), także przy równoległych aktywacjach. Dziś chroni przed tym tylko indeks w bazie, którego żaden test nie dotyka, bo testy działają na InMemory.
- **#3:** hangar, ranking i dashboard muszą pokazywać liczby zgodne z FR-008 i FR-009. Dziś dashboard liczy granicę dat ostro (`<`), a hangar i ranking włącznie (`<=`).

## Starting Point

- Unikalny indeks `(UserId, FlagId)` jest w migracji, a handler `ActivateFlag` obsługuje wyścig przez `catch (DbUpdateException)` i ponowne sprawdzenie. Ani indeks, ani ta gałąź nie mają testu.
- Trzy handlery liczą flagi niezależnie. Żaden test nie porównuje ich na jednym seedzie.
- Lokalnie nie ma Dockera, ale jest LocalDB.

## Desired End State

- Z ustawioną zmienną `APP01_SQLSERVER_TESTS` testy na LocalDB dowodzą dwóch rzeczy: indeks istnieje po migracjach, a 10 równoległych aktywacji daje 1 wiersz i ranking 1.
- Bez zmiennej te testy są pomijane.
- Test krzyżowy na InMemory potwierdza tożsamości z PRD na jednym seedzie z brzegami `== Now`.
- Dashboard liczy granicę włącznie.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Granica publikacji/zdobycia | Włączna (`<=`) wszędzie, poprawka `GroupProgress` | FR-003 „równa lub wcześniejsza” i FR-008 „ta sama liczba co hangar”. | Research (decyzja użytkownika) |
| Wyrocznia dashboardu | `available == allCount`, `userCount == TotalCount`, `earned ==` różne flagi grupy | PRD `:71` definiuje „różne flagi”, więc suma rankingu byłaby fałszywą wyrocznią. | Research |
| Silnik dla #2 | LocalDB lokalnie; nie Testcontainers, nie SQLite | Brak Dockera; SQLite ma inną semantykę. | Research |
| Bramka SQL | Lokalna, ad hoc; CI bez zmian | Wybór użytkownika: bez zmian w workflow. | Plan |
| Brak zmiennej | Skip wszędzie z czytelnym powodem | Spójne z bramką ad hoc; CI nie czerwienieje. | Plan |
| Baza testowa | Jawna kolacja `SQL_Latin1_General_CP1_CI_AS`, compat 110, `Migrate()` | Stabilny wynik niezależny od locale i bliżej produkcji 2012; `Migrate()` dowodzi indeksu w migracji. | Plan |
| Test hermetyczny | Tylko gałąź „błąd zapisu bez konkurenta → 500” | Gałąź `AlreadyOwned` dowodzi wyścig; nie dublujemy. | Plan |
| Warstwa #3 | InMemory + `FixedTimeProvider` | Reguły liczenia nie zależą od ograniczeń bazy; deterministyczne brzegi. | Plan |

## Scope

**In scope:**
- fixture LocalDB + atrybut skip;
- strażnik schematu (2 testy);
- test wyścigu (2 testy);
- test hermetyczny `catch`;
- test krzyżowy;
- poprawka `GroupProgress` + aktualizacja 2 testów;
- test-plan §2 (backport), §4, §5, §6.

**Out of scope:**
- zmiany CI;
- nowe pakiety NuGet;
- test krzyżowy na SQL Server;
- refaktor istniejących fabryk testów;
- zmiany klienta, kontraktów, `ActivateFlag`/`HangarFlags`/`Leaderboard`.

## Architecture / Approach

Nowa fabryka `SqlServerWebApplicationFactory` zostawia produkcyjne `UseSqlServer` + compat 110 i podaje connection string do bazy `App01Tests_<guid>`. Fixture zakłada tę bazę przez `master`, migruje ją i usuwa po testach. Wyścig to 10 żądań przez `TestServer` startujących z jednej bramki. Test krzyżowy używa jednego seedu InMemory i klienta z JWT per użytkownik. Wyrocznię zapisano literalnie: dashboard 3 / 5 / 4 / 80.0, ranking U2 = 3, U3 = 1, admin = 1.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Fixture SQL Server | LocalDB per klasa + strażnik indeksu | LocalDB 2025 odrzuci compat 110 → stop i pytanie |
| 2. Wyścig (#2) | 10 równoległych → 1 wiersz, ranking 1; hermetyczny `catch` | Wyścig nie trafi w okno; strażnik z fazy 1 i tak łapie brak indeksu |
| 3. Test krzyżowy (#3) | Red → `GroupProgress` `<=` → green | Zmiana zachowania produkcyjnego na granicy (świadoma) |
| 4. Test-plan | §2 backport, §4, §5 ad hoc, §6 cookbook | — |

**Prerequisites:** LocalDB `MSSQLLocalDB` (jest); zmienna `APP01_SQLSERVER_TESTS` ustawiona do przebiegów SQL.
**Estimated effort:** ~2 sesje w 4 fazach (1 i 2 największe).

## Open Risks & Assumptions

- Kolacja produkcji (Webio) nieznana. Bramka przypina CI_AS, co może różnić się od produkcji.
- Bramka ad hoc nie wymusza uruchomienia: regres indeksu wyjdzie tylko przy ręcznym przebiegu SQL.
- Test wyścigu dowodzi wyniku, nie tego, że gałąź `catch` faktycznie się wykonała. Brak indeksu łapie deterministycznie strażnik z fazy 1.

## Success Criteria (Summary)

- Usunięcie unikalnego indeksu czerwieni test na LocalDB.
- Dla jednego stanu danych liczba flag uczestnika w hangarze i w rankingu jest ta sama, a dashboard jest z nimi zgodny według PRD, także na granicy „teraz”.
- `dotnet test APPS.sln` przechodzi lokalnie i w CI bez zmiennej SQL.
