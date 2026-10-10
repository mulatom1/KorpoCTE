# Postęp grupy (dashboard) — Plan Brief

> Full plan: `context/changes/group-progress-dashboard/plan.md`

## What & Why

Wycinek S-07 roadmapy (US-01, FR-009), ostatni w milestonie M-1. Zalogowany użytkownik widzi cztery wskaźniki postępu grupy na wybrany dzień. Dashboard uzupełnia ranking o obraz całej grupy: ile flag jest do zdobycia, ile grupa już odkryła i kto w ogóle zaczął.

## Starting Point

Hangar (S-02), aktywacja (S-06) i ranking (S-05) już działają. Flagi liczymy tylko z kursów opublikowanych, a ranking pokazuje osoby z co najmniej 1 flagą, także administratorów. `Courses.PublishDate`, `UserFlags.EarnedAt` i `Users.CreatedAt` mają daty, więc wskaźniki da się policzyć na dowolny dzień bez migracji.

## Desired End State

Na końcu podmenu kursów jest pozycja „Postęp grupy” (`/group-progress`, tylko dla zalogowanych). Strona ma pole „Stan na dzień” (domyślnie dziś, nie później niż dziś) i cztery kafelki:
- „Użytkownicy z flagą”;
- „Flagi do zdobycia”;
- „Flagi zdobyte przez grupę”;
- „Procent zdobytych flag”.

Przykład: 2 osoby, 10 flag, obie mają A, jedna także B → 2 / 10 / 2 / 20.0%.

## Key Decisions Made

| Decision            | Choice                                                                                       | Why (1 sentence)                                                                      |
| ------------------- | -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Użytkownicy         | Osoby z ≥ 1 zdobytą flagą z opublikowanego kursu, administratorzy też                        | Decyzja użytkownika; te same osoby co na liście zasłużonych                            |
| Flagi do zdobycia   | Flagi kursów opublikowanych przed wybranym momentem                                          | Spójne z licznikiem Y w hangarze (FR-003)                                              |
| Zdobyte przez grupę | Liczba różnych flag zdobytych przez kogokolwiek                                              | Decyzja użytkownika: „ile puli grupa odkryła”; flaga wspólna liczy się raz              |
| Procent             | Zdobyte / do zdobycia × 100, 1 miejsce po przecinku; `null` („—”) przy 0 flag               | Wynika z dwóch powyższych; nie zależy od liczby użytkowników                           |
| „Na dany dzień”     | Wybór daty, domyślnie dziś; klient wysyła początek następnego lokalnego dnia jako `asOf` UTC | Postęp w czasie bez stref czasowych na serwerze                                        |
| Przyszłe daty       | Serwer obcina `asOf` do „teraz”; pole daty ma maksimum dziś                                  | Nie ujawnia liczby flag kursów jeszcze nieopublikowanych                               |
| Miejsce             | Nowa strona „Postęp grupy”, ostatnia w podmenu kursów                                        | Wzorzec pozostałych stron; ranking zostaje bez zmian                                    |

## Scope

**In scope:**
- Wycinek `GroupProgress` (4 pliki), rejestracja w `ModuleDI`, `EndpointTests.cs`.
- Kontrakty TS, `getGroupProgress()`, helper `endOfLocalDay` z testem.
- `GroupProgressPage` z testami Vitest, trasa i pozycja w podmenu.

**Out of scope:**
- Liczenie wszystkich kont, wykluczanie adminów, suma zdobyć osoba–flaga.
- Wykresy i trendy.
- Strefy czasowe na serwerze.
- Zmiany w hangarze, rankingu i aktywacji.
- Migracje.

## Architecture / Approach

`GroupProgressHandler` obcina `asOf` do „teraz” i liczy trzy zapytania EF, każde z ostrą granicą `< asOf`:
- flagi kursów opublikowanych;
- różne zdobyte flagi;
- różni zdobywcy.

Potem wylicza procent. `GroupProgressPage` zamienia wybraną datę na `endOfLocalDay(data)` i pokazuje cztery kafelki.

## Phases at a Glance

| Phase                                   | What it delivers                                                    | Key risk                                                                  |
| --------------------------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| 1. Serwer — endpoint `group-progress`   | Wycinek z czterema wskaźnikami, obcięciem przyszłości, 11 testów    | Błąd o jeden na granicy dnia lub wyciek liczby flag z przyszłości         |
| 2. Klient — strona Postęp grupy         | `/group-progress`: pole daty, cztery kafelki, podmenu, testy Vitest | Zła granica lokalnego dnia (helper `endOfLocalDay` z testami)             |

**Prerequisites:** S-02, S-05, S-06 zrobione.
**Estimated effort:** ~1 sesja, 2 fazy.

## Open Risks & Assumptions

- Przy tych definicjach procent mierzy odkrycie puli flag przez grupę, a nie średni postęp uczestników. Jedna osoba z kompletem flag daje 100%. Kryterium PRD „≥70% flag zaliczonych przez uczestników” (OQ-3) może wymagać innej miary przy ocenie sukcesu.
- Cofnięcie daty publikacji kursu zmienia wskaźniki także wstecz (spójnie z hangarem i rankingiem).

## Success Criteria (Summary)

- Dashboard pokazuje cztery wskaźniki spójne z hangarem i listą zasłużonych, na dziś i na dowolny wcześniejszy dzień.
- Wybór daty nie ujawnia flag kursów, które jeszcze nie są opublikowane.
- CI przechodzi; pozostałe strony kursów i moduły działają bez zmian.
