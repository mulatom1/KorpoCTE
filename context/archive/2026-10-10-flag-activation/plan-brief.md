# Aktywacja flagi kodem w hangarze z trofeami — Plan Brief

> Full plan: `context/changes/flag-activation/plan.md`

## What & Why

Wycinek S-06 roadmapy (FR-004). Uczestnik aktywuje flagę, wpisując jej kod w hangarze. To jedyna droga zapisania zdobytej flagi: weryfikacja AI (S-01) tylko wydaje kod. Bez tego hangar, a później ranking i dashboard, nie mają czego liczyć.

## Starting Point

Encje `Flag`/`UserFlag` z unikalnymi indeksami `Code` i `(UserId, FlagId)` już istnieją. Terminal TOMO-AI-001 przy werdykcie `Correct` pokazuje kod i każe „aktywować go w formularzu aktywacji”, ale takiego formularza jeszcze nie ma. Hangar (`/hangar`) ma tytuł „Hangar”, podmenu oraz tabelę flag z filtrem, licznikiem i paginacją.

## Desired End State

Na stronie „Hangar z trofeami”, między podmenu a tabelą, jest sekcja „Aktywacja flagi”: pole kodu i przycisk „Aktywuj”. Poprawny kod zapisuje flagę, pokazuje komunikat z jej tytułem i odświeża tabelę oraz licznik. Kod już użyty daje komunikat „Masz już tę flagę.”, a błędny — „Nieprawidłowy kod flagi.”.

## Key Decisions Made

| Decision               | Choice                                                                                   | Why (1 sentence)                                                                                 |
| ---------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Wejście                | Sam kod; flagę wskazuje unikalny `Flag.Code`                                             | Bez wybierania flagi z listy; mniej pomyłek                                                      |
| Dopasowanie kodu       | Trim + bez rozróżniania wielkości liter (`UPPER` po obu stronach)                        | Kod przepisywany ręcznie działa; ten sam wynik na SQL Server i EF InMemory                       |
| Odpowiedź              | 200 + `Activated / AlreadyOwned / Invalid`, stałe komunikaty, bez kodu w odpowiedzi      | Wzorzec `VerifyAnswer`; jeden kształt odpowiedzi w kliencie                                      |
| Kurs nieopublikowany   | Kod jego flagi daje `Invalid`, tak samo jak nieznany kod                                 | Nie zdradza istnienia flag kursów, które jeszcze nie ruszyły (FR-003)                            |
| Flagi bez kryteriów    | Aktywowalne kodem                                                                        | To ich jedyna droga zdobycia                                                                      |
| Jednokrotność          | Sprawdzenie `AnyAsync` + unikalny indeks; `DbUpdateException` → `AlreadyOwned`          | Reguła `CLAUDE.md` i guardrail PRD „flaga liczona raz”                                           |
| Limit prób             | Brak; nieudana próba logowana jako Warning z `userId`, bez wpisanego kodu w logu         | Mała zamknięta grupa, kody nieodgadywalne; bez nowej infrastruktury                              |
| UI po aktywacji        | Komunikat, czyszczenie pola, odświeżenie tabeli i licznika na bieżącym filtrze/stronie   | Uczestnik od razu widzi flagę jako „Zdobyta”                                                     |
| Układ hangaru          | Tytuł „Hangar z trofeami”; sekcja aktywacji pod podmenu, przed tabelą                    | Decyzja użytkownika (notatki zmiany)                                                             |

## Scope

**In scope:**
- Wycinek `ActivateFlag` (4 pliki), rejestracja w `ModuleDI`, `EndpointTests.cs`.
- Kontrakty TS, `activateFlag()` i sekcja aktywacji w `HangarPage`.
- Nowy tytuł strony i testy Vitest.

**Out of scope:**
- Limit prób.
- Rozróżnienie „nieznany kod” / „kurs nieopublikowany”.
- Zmiany w weryfikacji, terminalu i `hangar-flags`.
- Etykieta „Hangar” w podmenu.
- Migracje.
- Ranking (S-05) i dashboard (S-07).

## Architecture / Approach

`ActivateFlagHandler` przetwarza żądanie w kolejności:
1. walidacja (kod wymagany, najwyżej 50 znaków);
2. `userId` z JWT;
3. wyszukanie flagi po znormalizowanym kodzie w opublikowanych kursach;
4. `Invalid` albo `AlreadyOwned`;
5. zapis `UserFlag` z `EarnedAt` z `TimeProvider` (`DbUpdateException` → `AlreadyOwned`);
6. `Activated` z tytułem flagi.

`HangarPage` po `Activated` ponownie pobiera bieżącą stronę tabeli.

## Phases at a Glance

| Phase                                         | What it delivers                                                    | Key risk                                                                       |
| --------------------------------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| 1. Serwer — endpoint `activate-flag`          | Wycinek, rejestracja, 11 testów endpointu                           | Gałąź `DbUpdateException` nietestowalna na InMemory (indeks unikalny ignorowany) |
| 2. Klient — sekcja aktywacji i tytuł hangaru  | Sekcja nad tabelą, odświeżenie po sukcesie, nowy tytuł, testy Vitest | Ponowne pobranie listy bez zmiany filtra/strony a ignorowanie starych odpowiedzi |

**Prerequisites:** S-01 i S-02 zrobione. Do testu ręcznego: klucz OpenRouter (żeby dostać kod z terminalu) albo kod flagi odczytany z bazy.
**Estimated effort:** ~1 sesja, 2 fazy.

## Open Risks & Assumptions

- Wyścig dwóch równoczesnych aktywacji chroni unikalny indeks w SQL Server. Testy InMemory sprawdzają tylko wcześniejsze `AnyAsync`, więc gałąź z `DbUpdateException` jest zweryfikowana wyłącznie przeglądem kodu.
- Kod można przekazać koledze (PRD: zarzut rozważony, FR bez zmian). Ranking mierzy więc znajomość kodów, nie tylko wykonanie zadań.
- Bez limitu prób zgadywanie kodów jest możliwe i widoczne tylko w logach. Administrator musi nadawać kody trudne do odgadnięcia.

## Success Criteria (Summary)

- Kod z terminalu aktywuje flagę: zapisuje się dokładnie raz, a hangar od razu pokazuje ją jako „Zdobyta” i aktualizuje licznik.
- Błędny kod i kod flagi z nieopublikowanego kursu nic nie zapisują i nie zdradzają, czy flaga istnieje.
- CI przechodzi, a weryfikacja, terminal i tabela hangaru działają bez zmian.
