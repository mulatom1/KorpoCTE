# Zweryfikowana odpowiedź daje flagę — Plan Brief

> Full plan: `context/changes/answer-verification-earns-flag/plan.md`

## What & Why

Wycinek S-01, gwiazda przewodnia roadmapy. Uczestnik w hangarze wkleja wynik zadania i w czasie poniżej 5 s dostaje werdykt modelu. Przy poprawnej odpowiedzi flaga zostaje przyznana raz na zawsze. To realizacja jedynej nowej reguły domenowej PRD („flaga dowodzi wykonania, nie przeczytania”) i największe ryzyko techniczne modułu.

## Starting Point

Moduł Kursy ma kafelki i treść kursów (`Course`: Slug + PublishDate, wstawiane SQL-em). Nie ma zadań, flag ani hangaru. `IOpenRouterService` jest już używany przez Fiszki. CLAUDE.md rozstrzyga mechanikę weryfikacji: statusy, timeout, prompt, unikalny indeks i testy z mockiem.

## Desired End State

Zalogowany użytkownik widzi w menu „Hangar”. Wybiera w nim z listy zadanie z opublikowanego kursu, wysyła odpowiedź i widzi jeden z wyników: „poprawna — flaga zdobyta”, „niepoprawna”, „masz już tę flagę” albo „ocena niedostępna” z przyciskiem „Spróbuj ponownie”. Administrator definiuje zadania SQL-em w `Courses.Flags`, a zdobyte flagi trafiają do `Courses.UserFlags`.

## Key Decisions Made

| Decision                | Choice                                                                                   | Why (1 sentence)                                                                          |
| ----------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Miejsce kryteriów       | Kolumna `Criteria` w tabeli `Courses.Flags`, wstawiana SQL-em                            | Zgodne z PRD („zmiana promptu = SQL”), kryteria nigdy nie trafiają na dysk publiczny ani do klienta |
| Model danych            | Flaga = zadanie, przypięta do kursu, a weryfikowalna, gdy `Criteria` niepuste            | Najprostszy model; ranking liczy flagi; S-06 doda kod aktywacji bez przebudowy             |
| Widoczność zadań        | Tylko zadania kursów z `PublishDate <= teraz`                                            | Spójne z FR-003: nieopublikowany kurs nie zdradza zadań                                   |
| Wybór zadania           | Lista rozwijana z `GET api/courses/hangar-tasks`, zdobyte wyłączone                      | Bez literówek; S-02 rozbuduje ten sam endpoint                                            |
| Werdykt `Incorrect`     | Stały komunikat „Odpowiedź niepoprawna.”, a `reason` modelu tylko w logu                 | Zero ryzyka wycieku kryteriów                                                             |
| Koszt i nadużycia       | Odpowiedź 1–4000 znaków, przycisk zablokowany w trakcie oceny, bez limitu prób           | Mała zamknięta grupa, brak nowej infrastruktury                                           |
| Menu                    | „Hangar” doklejany tylko przy sesji (jak „Users” dla admina), trasa za `RequireAuth`     | Gość nie widzi niedostępnej funkcji; istniejące pozycje bez zmian                         |
| Awarie modelu           | 200 + `Unavailable` (timeout ≤ 4 s, wyjątek, nieparsowalny JSON), bez zapisu             | Reguła CLAUDE.md; awaria to nie ocena negatywna                                           |
| Wyścig o flagę          | Unikalny indeks `(UserId, FlagId)` + `DbUpdateException` → `AlreadyOwned`                | Flaga nie może zostać policzona dwa razy (PRD)                                            |

## Scope

**In scope:**
- Encje `Flag` i `UserFlag` z migracją `CoursesFlags`.
- Endpointy `hangar-tasks` i `verify-answer` z testami.
- Klucz `Courses:VerificationTimeoutSeconds`.
- Strona `/hangar`, pozycja w menu, kontrakty TS i testy Vitest.

**Out of scope:**
- Pełny hangar z listą flag (S-02).
- Aktywacja flagi kodem (S-06).
- Ranking i dashboard (S-05, S-07).
- Uzasadnienie modelu dla uczestnika.
- Limit prób.
- Zarządzanie zadaniami w aplikacji.
- Zmiany w `OpenRouterService` i middleware.

## Architecture / Approach

`HangarPage` pobiera listę zadań (bez kryteriów) i wysyła `{flagId, answer}`. Handler `VerifyAnswer` kolejno:
1. waliduje żądanie;
2. pobiera `userId` z JWT;
3. wczytuje flagę z opublikowanego kursu (brak → 404);
4. jeśli flaga jest już zdobyta, zwraca `AlreadyOwned`;
5. woła `IOpenRouterService` z timeoutem (kryteria w `system`, odpowiedź w `<answer>` w `user`);
6. parsuje JSON `{verdict, reason}`;
7. przy `pass` zapisuje `UserFlag`.

Każda awaria techniczna kończy się statusem `Unavailable`.

## Phases at a Glance

| Phase                                         | What it delivers                                                 | Key risk                                                          |
| --------------------------------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------------- |
| 1. Serwer — model flag i lista zadań          | Tabele `Flags`/`UserFlags`, migracja, `hangar-tasks`, testy      | Wyciek `Criteria` w projekcji (test sprawdza surowy JSON)         |
| 2. Serwer — weryfikacja i przyznanie flagi    | `verify-answer` z timeoutem, parsowaniem, zapisem, testy z mockiem | Wstrzyknięcie polecenia; wyścigu nie da się przetestować na InMemory |
| 3. Klient — hangar                            | `/hangar`, formularz, „Spróbuj ponownie”, menu, testy Vitest     | Regresja menu w `Layout.tsx` (dotyka każdej strony)               |

**Prerequisites:** F-01 zrobiony. Do testu ręcznego potrzebne są klucz OpenRouter w user-secrets oraz co najmniej jedna flaga z kryteriami wstawiona SQL-em.
**Estimated effort:** ~2–3 sesje, 3 fazy.

## Open Risks & Assumptions

- Model jest niedeterministyczny. Jakość werdyktu zależy od kryteriów pisanych przez administratora, a treść pierwszego prawdziwego zadania jest otwarta (roadmapa).
- Tolerujemy jedno otaczające ogrodzenie ```` ```json ```` w odpowiedzi modelu. Każdy inny format daje `Unavailable`.
- Bez limitu prób jedno konto może generować koszt serią wysyłek. Akceptowane dla małej grupy.

## Success Criteria (Summary)

- Uczestnik zdobywa flagę za poprawną odpowiedź w czasie poniżej 5 s. Ponowne wysłanie jest blokowane bez wywołania modelu.
- Awaria modelu nigdy nie daje 500 ani oceny negatywnej, tylko komunikat z ponowieniem.
- CI przechodzi, a istniejące moduły i menu działają bez zmian.
