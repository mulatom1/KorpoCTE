# Lista zasłużonych — Plan Brief

> Full plan: `context/changes/leaderboard/plan.md`

## What & Why

Wycinek S-05 roadmapy (US-01, FR-008). Zalogowany użytkownik widzi ranking uczestników według liczby zdobytych flag, tej samej, którą widzi w hangarze. To ostatni brakujący element pełnej ścieżki z kryterium sukcesu „Primary”: kurs → zadanie → flaga → hangar → pozycja na liście zasłużonych.

## Starting Point

Flagi zdobywa się aktywacją kodem (S-06), a jednokrotność pilnuje unikalny indeks `(UserId, FlagId)`. Hangar (S-02) liczy zdobyte flagi tylko z opublikowanych kursów. Model `User` ma jedynie `Email` i `IsAdmin`, bez nazwy wyświetlanej, a PRD zabrania go zmieniać.

## Desired End State

W podmenu kursów jest pozycja „Lista zasłużonych” (`/leaderboard`, tylko dla zalogowanych). Strona pokazuje tabelę Miejsce | Uczestnik | Flagi, po 20 wierszy, z nawigacją Poprzednia / Następna. Na liście są wszyscy, którzy mają co najmniej jedną flagę z opublikowanego kursu, także administratorzy. Uczestnik jest pokazany jako część e-maila przed `@`.

## Key Decisions Made

| Decision            | Choice                                                                                  | Why (1 sentence)                                                                            |
| ------------------- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Kto jest na liście  | Osoby z ≥ 1 flagą z opublikowanego kursu, administratorzy też                            | Administratorzy też sprawdzają, czy flaga wchodzi; osoby z zerem nie są eksponowane          |
| Podstawa liczenia   | Tylko flagi kursów z `PublishDate <= teraz` (jak `earnedCount` w hangarze)              | Liczba w rankingu = liczba w hangarze (FR-008)                                               |
| Remis               | Wspólne miejsce (1, 1, 3); w remisie wyżej wcześniejsza data ostatniej flagi, potem Id   | Uczciwe miejsca, a kolejność nagradza tempo; deterministyczna kolejność                      |
| Tożsamość           | Część e-maila przed `@`, wyliczana na serwerze; bez domeny i `UserId` w odpowiedzi       | Znajomi się rozpoznają, adresy nie wyciekają do całej grupy                                 |
| Widok               | Paginacja po 20 (max 100), jak w hangarze; numer miejsca ciągły między stronami         | Spójne z hangarem; skaluje się przy większej grupie                                          |
| Własna pozycja      | Bez wyróżnienia wiersza i bez paska „Twoje miejsce”                                     | Decyzja użytkownika; prostszy kontrakt                                                       |
| S-07                | Kogo liczy „liczba użytkowników” — decyzja przy planowaniu S-07                         | Ta odpowiedź dotyczyła rankingu                                                              |

## Scope

**In scope:**
- Wycinek `Leaderboard` (4 pliki), rejestracja w `ModuleDI`, `EndpointTests.cs`.
- Kontrakty TS, `getLeaderboard()` i `LeaderboardPage` z testami Vitest.
- Trasa `/leaderboard` i pozycja „Lista zasłużonych” w podmenu kursów.

**Out of scope:**
- Wyróżnianie własnego wiersza.
- Osoby z 0 flagami.
- Zmiany modelu `User` i maskowanie e-maili.
- Dashboard (S-07).
- Zmiany w hangarze i aktywacji.
- Migracje.

## Architecture / Approach

`LeaderboardHandler` grupuje `UserFlags` z opublikowanych kursów po `UserId` (liczba i najpóźniejsze `EarnedAt`) i dołącza `Email`. W pamięci sortuje, nadaje miejsca (`1 + liczba osób z większą liczbą flag`), wylicza nazwę wyświetlaną i wybiera stronę. `LeaderboardPage` pokazuje stronę w tabeli z paginacją, na wzór `HangarPage`.

## Phases at a Glance

| Phase                                   | What it delivers                                                    | Key risk                                                              |
| --------------------------------------- | ------------------------------------------------------------------- | --------------------------------------------------------------------- |
| 1. Serwer — endpoint `leaderboard`      | Wycinek z rankingiem, remisami i paginacją, 12 testów endpointu     | Rozjazd z hangarem (inny filtr) lub wyciek e-maila — testy na surowym JSON |
| 2. Klient — strona Lista zasłużonych    | `/leaderboard`: tabela, paginacja, podmenu, testy Vitest            | Regresja stron kursów przez nową pozycję podmenu                       |

**Prerequisites:** S-01, S-02, S-06 zrobione. Do testu ręcznego: co najmniej dwa konta z aktywowanymi flagami.
**Estimated effort:** ~1 sesja, 2 fazy.

## Open Risks & Assumptions

- Cofnięcie daty publikacji kursu zabiera jego flagi z rankingu (spójnie z hangarem). Osoba, której wszystkie flagi pochodzą z takiego kursu, znika z listy.
- Dwie osoby z tym samym loginem w różnych domenach wyglądają na liście tak samo. W małej grupie znajomych to akceptowalne.
- Kod flagi można przekazać koledze (PRD). Ranking mierzy więc znajomość kodów, nie tylko wykonanie zadań.

## Success Criteria (Summary)

- Uczestnik widzi swoją pozycję i liczbę flag zgodną z hangarem; po aktywacji flagi ranking się aktualizuje.
- Remisy dają wspólne miejsce, a numeracja jest ciągła między stronami.
- CI przechodzi; hangar, terminal, menu i pozostałe moduły działają bez zmian.
