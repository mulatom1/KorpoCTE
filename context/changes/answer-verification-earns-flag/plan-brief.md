# Zweryfikowana odpowiedź daje flagę — Plan Brief

> Full plan: `context/changes/answer-verification-earns-flag/plan.md`

## What & Why

Wycinek S-01, gwiazda przewodnia roadmapy. Uczestnik w hangarze wkleja wynik zadania i w czasie poniżej 5 s dostaje werdykt modelu. Przy poprawnej odpowiedzi uczestnik dostaje kod flagi, którym sam aktywuje flagę w funkcji aktywacji (S-06). To realizacja jedynej nowej reguły domenowej PRD („flaga dowodzi wykonania, nie przeczytania”) i największe ryzyko techniczne modułu.

## Starting Point

Moduł Kursy ma kafelki i treść kursów (`Course`: Slug + PublishDate, wstawiane SQL-em). Nie ma zadań, flag ani hangaru. `IOpenRouterService` jest już używany przez Fiszki. CLAUDE.md rozstrzyga mechanikę weryfikacji: statusy, timeout, prompt, unikalny indeks i testy z mockiem.

## Desired End State

Zalogowany użytkownik widzi w podmenu listy kursów i szczegółów kursu pozycję „Terminal TOMO-AI-001” (`/tomo-ai-001`). Wybiera w terminalu z listy zadanie z opublikowanego kursu, wysyła odpowiedź i widzi jeden z wyników: „poprawna” z kodem flagi do aktywacji, „niepoprawna”, „masz już tę flagę” albo „ocena niedostępna” z przyciskiem „Spróbuj ponownie”. Administrator definiuje zadania SQL-em w `Courses.Flags`, a zdobyte flagi trafią do `Courses.UserFlags` dopiero przez aktywację (S-06).

## Key Decisions Made

| Decision                | Choice                                                                                   | Why (1 sentence)                                                                          |
| ----------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Miejsce kryteriów       | Kolumna `Criteria` w tabeli `Courses.Flags`, wstawiana SQL-em                            | Zgodne z PRD („zmiana promptu = SQL”), kryteria nigdy nie trafiają na dysk publiczny ani do klienta |
| Model danych            | Flaga = zadanie, przypięta do kursu, a weryfikowalna, gdy `Criteria` niepuste            | Najprostszy model; ranking liczy flagi; S-06 aktywuje flagę istniejącym `Flag.Code`             |
| Widoczność zadań        | Tylko zadania kursów z `PublishDate <= teraz`                                            | Spójne z FR-003: nieopublikowany kurs nie zdradza zadań                                   |
| Wybór zadania           | Lista rozwijana z `GET api/courses/hangar-tasks`, zdobyte wyłączone                      | Bez literówek; S-02 rozbuduje ten sam endpoint                                            |
| Werdykt `Incorrect`     | Stały komunikat „Odpowiedź niepoprawna.”, a `reason` modelu tylko w logu                 | Zero ryzyka wycieku kryteriów                                                             |
| Koszt i nadużycia       | Odpowiedź 1–4000 znaków, przycisk zablokowany w trakcie oceny, bez limitu prób           | Mała zamknięta grupa, brak nowej infrastruktury                                           |
| Nawigacja               | Podmenu kursów (`SubMenu`) z „Terminal TOMO-AI-001”, tylko dla zalogowanych; menu główne bez zmian — zmiana z 2026-10-08 | Decyzja użytkownika: funkcja należy do kursów, nie do menu głównego |
| Awarie modelu           | 200 + `Unavailable` (timeout ≤ 4 s, wyjątek, nieparsowalny JSON), bez zapisu             | Reguła CLAUDE.md; awaria to nie ocena negatywna                                           |
| Wynik `Correct`         | Zwraca `Flag.Code`, bez zapisu; flagę zapisuje aktywacja (S-06) — zmiana z 2026-10-08    | Decyzja użytkownika: uczestnik sam rejestruje flagę w funkcji aktywacji                   |
| Wyścig o flagę          | Unikalny indeks `(UserId, FlagId)` (faza 1); `DbUpdateException` obsłuży aktywacja (S-06) | Flaga nie może zostać policzona dwa razy (PRD)                                            |

## Scope

**In scope:**
- Encje `Flag` i `UserFlag` z migracją `CoursesFlags`.
- Endpointy `hangar-tasks` i `verify-answer` z testami.
- Klucz `Courses:VerificationTimeoutSeconds`.
- Strona `/tomo-ai-001` (Terminal TOMO-AI-001), podmenu kursów, kontrakty TS i testy Vitest.

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
7. przy `pass` zwraca `Correct` z `Flag.Code`, bez zapisu.

Każda awaria techniczna kończy się statusem `Unavailable`.

## Phases at a Glance

| Phase                                         | What it delivers                                                 | Key risk                                                          |
| --------------------------------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------------- |
| 1. Serwer — model flag i lista zadań          | Tabele `Flags`/`UserFlags`, migracja, `hangar-tasks`, testy      | Wyciek `Criteria` w projekcji (test sprawdza surowy JSON)         |
| 2. Serwer — weryfikacja i przyznanie flagi    | `verify-answer` z timeoutem, parsowaniem, zapisem, testy z mockiem | Wstrzyknięcie polecenia; wyścigu nie da się przetestować na InMemory |
| 3. Klient — terminal TOMO-AI-001              | `/tomo-ai-001`, formularz, „Spróbuj ponownie”, podmenu kursów, testy Vitest | Regresja stron kursów (podmenu zastępuje „Powrót do kursów”)     |

**Prerequisites:** F-01 zrobiony. Do testu ręcznego potrzebne są klucz OpenRouter w user-secrets oraz co najmniej jedna flaga z kryteriami wstawiona SQL-em.
**Estimated effort:** ~2–3 sesje, 3 fazy.

## Open Risks & Assumptions

- Model jest niedeterministyczny. Jakość werdyktu zależy od kryteriów pisanych przez administratora, a treść pierwszego prawdziwego zadania jest otwarta (roadmapa).
- Tolerujemy jedno otaczające ogrodzenie ```` ```json ```` w odpowiedzi modelu. Każdy inny format daje `Unavailable`.
- Bez limitu prób jedno konto może generować koszt serią wysyłek. Akceptowane dla małej grupy.

- `Flag.Code` jest zarazem sekretem aktywacji — administrator musi nadawać kody trudne do zgadnięcia. Do czasu S-06 flagi nie da się zdobyć.

## Success Criteria (Summary)

- Uczestnik dostaje kod flagi za poprawną odpowiedź w czasie poniżej 5 s. Ponowne wysłanie dla flagi już zdobytej jest blokowane bez wywołania modelu.
- Awaria modelu nigdy nie daje 500 ani oceny negatywnej, tylko komunikat z ponowieniem.
- CI przechodzi, a istniejące moduły i menu działają bez zmian.
