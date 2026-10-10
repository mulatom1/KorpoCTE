# Hangar z listą flag — Plan Brief

> Full plan: `context/changes/hangar-flag-list/plan.md`

## What & Why

Wycinek S-02 roadmapy (US-01, FR-007). Zalogowany uczestnik widzi w hangarze wszystkie flagi z rozróżnieniem zdobytych i niezdobytych. Domyka to kryterium akceptacji US-01: „przed zdobyciem flaga widnieje jako niezdobyta; po przyznaniu zmienia status na zdobytą”. Hangar jest też miejscem, w którym uczestnik ma pod ręką kody swoich zdobytych flag na koniec kursu.

## Starting Point

S-01 dał encje `Flag`/`UserFlag` (unikalny indeks `(UserId, FlagId)`), endpoint `hangar-tasks` (tylko flagi z kryteriami, dla listy w terminalu) i stronę `/tomo-ai-001` w podmenu kursów. Strony hangaru nie było. `Correct` nie zapisuje flagi; zapis zrobi aktywacja (S-06).

## Desired End State

W podmenu kursów jest pozycja „Hangar” (`/hangar`, tylko dla zalogowanych). Strona pokazuje:
- licznik „Zdobyte flagi: X / Y”;
- przyciski filtra Wszystkie / Zdobyte / Niezdobyte;
- jedną tabelę: Flaga | Kurs (link do kursu) | Status | Data zdobycia | Kod, po 20 flag na stronę.

Kod zdobytej flagi jest zamaskowany i odsłania się po najechaniu myszką, fokusie lub dotknięciu. Dane pochodzą z nowego `GET api/courses/hangar-flags`. Endpoint zwraca kod tylko dla flag zdobytych przez bieżącego użytkownika i nigdy nie zwraca kryteriów.

## Key Decisions Made

| Decision                | Choice                                                                                                     | Why (1 sentence)                                                                          |
| ----------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Zakres listy            | Wszystkie flagi kursów z `PublishDate <= teraz`, także bez `Criteria`                                      | Spójne z FR-003 i `hangar-tasks`; nie zdradza zadań nieotwartych kursów                   |
| Flagi przyszłych kursów | Ukryte, nawet zdobyte                                                                                      | Jeden prosty filtr; S-05/S-07 powinny użyć tego samego, by liczby się zgadzały            |
| API                     | Nowy wycinek `GET api/courses/hangar-flags`; `hangar-tasks` bez zmian                                      | Terminal i jego testy nietknięte; jeden wycinek = jedna funkcja                           |
| Strona                  | `/hangar` w `RequireAuth`, pozycja „Hangar” w podmenu kursów                                               | Wzorzec jak terminal; menu główne bez zmian (FR-013)                                      |
| Filtr i lista           | Filtr `All/Earned/Unearned` w query endpointu, wybierany przyciskami; jedna tabela zamiast dwóch sekcji    | Ponad 100 flag; tabela z kolumnami czyta się lepiej niż dwie listy                        |
| Paginacja               | Po stronie serwera, `page`/`pageSize` (domyślnie 20, max 100), wzorzec `DrawsGetList`; licznik `earnedCount/allCount` niezależny od filtra | Przy 100+ flagach klient nie pobiera wszystkiego naraz                 |
| Kolejność               | Po dacie zdobycia od najnowszej; niezdobyte na końcu wg daty publikacji kursu i Id                         | Przewidywalna, stała między stronami                                                      |
| Kolumna Kurs            | Slug kursu jako link do `/courses/<slug>` (jak `CourseTile`)                                               | Tylko dane z bazy, bez czytania plików kursów; jedno kliknięcie do treści zadania         |
| Kod flagi               | Zwracany tylko dla flag zdobytych przez bieżącego użytkownika; w UI `********`, odsłaniany po najechaniu/fokusie/dotknięciu | Uczestnik wylicza kod w zadaniu i potrzebuje swoich kodów na koniec kursu; kodów niezdobytych i cudzych flag API nie zwraca |
| Data zdobycia           | Serwer zwraca UTC z `Z`, klient formatuje lokalnie `yyyy-MM-dd HH:mm:ss` (`src/utils/formatDateTime.ts`)    | Bez przesunięcia strefy czasowej w przeglądarce                                           |

Decyzje o kodzie flagi, filtrze, tabeli, paginacji, linku do kursu i formacie daty zapadły 2026-10-10, w trakcie implementacji. Szczegóły są w planie, w notkach „zmiana z 2026-10-10”.

## Scope

**In scope:**
- Wycinek `HangarFlags` (4 pliki): filtr, sortowanie, paginacja, kod tylko dla zdobytych flag. Do tego rejestracja w `ModuleDI` i `EndpointTests.cs`.
- Kontrakty TS (request/response), `getHangarFlags(request)` i helper `formatDateTime` z testem.
- `HangarPage` (tabela, filtr, paginacja, licznik, maskowanie kodu, link do kursu) z testami Vitest.
- Trasa `/hangar` i pozycja „Hangar” w podmenu kursów.
- Doprecyzowanie reguły `Flag.Code` w `CLAUDE.md`.

**Out of scope:**
- Zmiany `hangar-tasks` i terminala.
- Tytuły kursów z frontmattera i grupowanie po kursach.
- Link „Sprawdź w terminalu”.
- Aktywacja (S-06), ranking (S-05), dashboard (S-07).
- Migracje i zmiany menu głównego.

## Architecture / Approach

`HangarFlagsHandler` przetwarza żądanie w kolejności:
1. walidacja (filtr, strona, rozmiar strony);
2. `userId` z JWT;
3. zapytanie `Flags` opublikowanych kursów z `EarnedAt` bieżącego użytkownika (podzapytanie do `UserFlags`);
4. oznaczenie UTC;
5. sortowanie;
6. liczniki;
7. filtr;
8. wybór strony.

Kroki 4–8 odbywają się w pamięci. `HangarPage` trzyma filtr i numer strony, pobiera daną stronę, ignoruje nieaktualne odpowiedzi i pokazuje wiersze w kolejności z serwera.

## Phases at a Glance

| Phase                               | What it delivers                                                            | Key risk                                                                     |
| ----------------------------------- | --------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1. Serwer — endpoint `hangar-flags` | Wycinek z filtrem, sortowaniem i paginacją, rejestracja, testy endpointu    | Wyciek `Code` niezdobytej/cudzej flagi lub `Criteria`, `earnedAt` bez strefy (testy na surowym JSON) |
| 2. Klient — strona Hangar           | `/hangar`: tabela, filtr, paginacja, licznik, kod do najechania, podmenu, testy Vitest | Regresja stron kursów przez nową pozycję podmenu                    |

**Prerequisites:** S-01 zrobiony (tabele istnieją). Do testu ręcznego: flagi i wiersz `Courses.UserFlags` wstawione SQL-em.
**Estimated effort:** ~1 sesja, 2 fazy.

## Open Risks & Assumptions

- Do czasu S-06 nic w aplikacji nie zapisuje `UserFlag`, więc filtr „Zdobyte” daje pustą listę poza danymi wstawionymi SQL-em. Wynik roadmapy „flaga przyznana w S-01 od razu zmienia status” spełni się dopiero po aktywacji.
- Cofnięcie daty publikacji kursu ukrywa jego zdobyte flagi w hangarze. Ranking (S-05) musi stosować ten sam filtr, by liczba się zgadzała.
- Rozstrzygnięte: reguła `Flag.Code` w `CLAUDE.md` dopuszcza teraz zwrot kodu w hangarze dla flag zdobytych przez bieżącego użytkownika (`fa9a623`).

## Success Criteria (Summary)

- Uczestnik widzi w hangarze wszystkie flagi opublikowanych kursów w jednej tabeli ze statusem. Może ją filtrować (Wszystkie/Zdobyte/Niezdobyte) i stronicować, a licznik X / Y nie zależy od filtra.
- Po zapisaniu flagi (SQL teraz, aktywacja w S-06) odświeżony hangar pokazuje ją jako „Zdobyta”, z datą i kodem do odsłonięcia.
- CI przechodzi; terminal, menu główne i pozostałe moduły działają bez zmian.
