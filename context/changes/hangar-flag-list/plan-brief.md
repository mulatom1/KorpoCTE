# Hangar z listą flag — Plan Brief

> Full plan: `context/changes/hangar-flag-list/plan.md`

## What & Why

Wycinek S-02 roadmapy (US-01, FR-007). Zalogowany uczestnik widzi w hangarze wszystkie flagi z rozróżnieniem zdobytych i niezdobytych. Domyka to kryterium akceptacji US-01: „przed zdobyciem flaga widnieje jako niezdobyta; po przyznaniu zmienia status na zdobytą”.

## Starting Point

S-01 dał encje `Flag`/`UserFlag` (unikalny indeks `(UserId, FlagId)`), endpoint `hangar-tasks` (tylko flagi z kryteriami, dla listy w terminalu) i stronę `/tomo-ai-001` w podmenu kursów. Strony hangaru nie ma. `Correct` nie zapisuje flagi; zapis zrobi aktywacja (S-06).

## Desired End State

W podmenu kursów jest pozycja „Hangar” (`/hangar`, tylko dla zalogowanych). Strona pokazuje licznik „Zdobyte flagi: X / Y” oraz sekcje „Zdobyte” (z datą zdobycia i zamaskowanym kodem flagi, który odsłania się po najechaniu myszką) i „Niezdobyte”. Dane pochodzą z nowego `GET api/courses/hangar-flags`. Endpoint zwraca kod tylko dla flag zdobytych przez bieżącego użytkownika i nigdy nie zwraca kryteriów.

## Key Decisions Made

| Decision          | Choice                                                                        | Why (1 sentence)                                                                 |
| ----------------- | ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| Zakres listy      | Wszystkie flagi kursów z `PublishDate <= teraz`, także bez `Criteria`         | Spójne z FR-003 i `hangar-tasks`; nie zdradza zadań nieotwartych kursów          |
| Flagi przyszłych kursów | Ukryte, nawet zdobyte                                                   | Jeden prosty filtr; S-05/S-07 powinny użyć tego samego, by liczby się zgadzały  |
| API               | Nowy wycinek `GET api/courses/hangar-flags`; `hangar-tasks` bez zmian         | Terminal i jego testy nietknięte; jeden wycinek = jedna funkcja                  |
| Strona            | `/hangar` w `RequireAuth`, pozycja „Hangar” w podmenu kursów                  | Wzorzec jak terminal; menu główne bez zmian (FR-013)                             |
| Opis pozycji      | Tytuł flagi + slug kursu; zdobyta z datą zdobycia                             | Tylko dane z bazy, bez czytania plików kursów                                    |
| Kolejność         | Zdobyte od najnowszej (`earnedAt`), niezdobyte wg daty publikacji kursu i Id  | Przewidywalna i taka sama jak w `hangar-tasks` dla niezdobytych                  |
| Kod flagi         | Zwracany tylko dla flag zdobytych przez bieżącego użytkownika; w UI zamaskowany `********`, odsłaniany po najechaniu/fokusie/dotknięciu — zmiana z 2026-10-10 | Uczestnik potrzebuje swoich kodów ostatniego dnia bootcampu; zna je już, więc nic nie wycieka |
| Data zdobycia     | Serwer zwraca UTC z `Z`, klient formatuje `pl-PL`                             | Bez przesunięcia strefy czasowej w przeglądarce                                  |

## Scope

**In scope:**
- Wycinek `HangarFlags` (4 pliki), rejestracja w `ModuleDI`, `EndpointTests.cs`.
- Kontrakt TS, `getHangarFlags()`, `HangarPage` z testami Vitest, trasa `/hangar`, pozycja w podmenu.

**Out of scope:**
- Zmiany `hangar-tasks` i terminala.
- Tytuły kursów z frontmattera i grupowanie po kursach.
- Link „Sprawdź w terminalu”.
- Aktywacja (S-06), ranking (S-05), dashboard (S-07).
- Migracje i zmiany menu głównego.

## Architecture / Approach

`HangarFlagsHandler`: walidacja → `userId` z JWT → zapytanie `Flags` opublikowanych kursów z `EarnedAt` bieżącego użytkownika (podzapytanie do `UserFlags`) → oznaczenie UTC → sortowanie (zdobyte, potem niezdobyte). `HangarPage` pobiera jedną listę i dzieli ją według `isEarned`, zachowując kolejność z serwera.

## Phases at a Glance

| Phase                               | What it delivers                                         | Key risk                                                         |
| ----------------------------------- | -------------------------------------------------------- | ---------------------------------------------------------------- |
| 1. Serwer — endpoint `hangar-flags` | Wycinek, rejestracja, 10 testów endpointu                | Wyciek `Code` niezdobytej flagi lub `Criteria`, `earnedAt` bez strefy (testy JSON) |
| 2. Klient — strona Hangar           | `/hangar`, sekcje + licznik, podmenu, testy Vitest       | Regresja stron kursów przez nową pozycję podmenu                 |

**Prerequisites:** S-01 zrobiony (tabele istnieją). Do testu ręcznego: flagi i wiersz `Courses.UserFlags` wstawione SQL-em.
**Estimated effort:** ~1 sesja, 2 fazy.

## Open Risks & Assumptions

- Do czasu S-06 nic w aplikacji nie zapisuje `UserFlag`, więc sekcja „Zdobyte” jest pusta poza danymi wstawionymi SQL-em. Wynik roadmapy „flaga przyznana w S-01 od razu zmienia status” spełni się dopiero po aktywacji.
- Zwracanie kodu zdobytej flagi zmienia regułę z `CLAUDE.md` („nigdy nie zwracaj `Code` poza `Correct`”). `CLAUDE.md` trzeba zaktualizować, żeby kolejne zmiany (S-05, S-06) nie traktowały tego jako błędu.
- Cofnięcie daty publikacji kursu ukrywa jego zdobyte flagi w hangarze. Ranking (S-05) musi stosować ten sam filtr, by liczba się zgadzała.

## Success Criteria (Summary)

- Uczestnik widzi w hangarze wszystkie flagi opublikowanych kursów, a zdobyte są oddzielone od niezdobytych i mają licznik.
- Po zapisaniu flagi (SQL teraz, aktywacja w S-06) odświeżenie hangaru przenosi ją do „Zdobyte”.
- CI przechodzi; terminal, menu główne i pozostałe moduły działają bez zmian.
