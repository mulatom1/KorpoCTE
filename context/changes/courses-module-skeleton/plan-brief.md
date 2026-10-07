# Szkielet modułu Courses — Plan Brief

> Full plan: `context/changes/courses-module-skeleton/plan.md`

## What & Why

Dodajemy pusty moduł `App01.Modules.Courses`, wpięty w host i w projekt testów, z jednym technicznym endpointem `ModuleHello`. To foundation F-01 z roadmapy. Zmiana w hoście robiona jest raz, więc S-01 (ocena odpowiedzi → flaga) i S-03 (kafelki kursów) mogą iść równolegle bez konfliktu o założenie modułu. Gdyby rejestracja nowego modułu zepsuła start aplikacji, wyjdzie to przed jakąkolwiek logiką domenową.

## Starting Point

Host składa moduły Portal, Lotto i Flashcards (`Program.cs:30-32`, `:102-104`). Modułu Courses nie ma. Projekt testów referencjonuje Portal i Lotto. Pakiety są przypięte lock-file'ami, a CI robi `restore --locked-mode`.

## Desired End State

`GET api/courses/module-hello` z JWT i `X-TOKEN` zwraca `{ "message": "Hello from module Courses!" }`, bez JWT zwraca 401, bez `X-TOKEN` zwraca 403. Cztery testy endpointu to gotowy szablon dla kolejnych wycinków. Portal, Lotto i Flashcards działają bez zmian, a pełny zestaw kroków CI przechodzi lokalnie.

## Key Decisions Made

| Decyzja                          | Wybór                                           | Dlaczego                                                                                  |
| -------------------------------- | ----------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Dowód rejestracji modułu         | Endpoint `ModuleHello` w `Features/ModuleHello` | Decyzja użytkownika: twardy dowód rejestracji endpointów i wzorzec testu od pierwszego dnia |
| Dostęp do `ModuleHello`          | JWT (`RequireAuthorization`) + `XTokenFilter`   | Szablon testów (200/401/403) gotowy do skopiowania przez S-01; brak publicznej powierzchni |
| Klucze `Courses:*` w konfiguracji | Nie teraz, dopiszą je S-01 i S-03                | Żadnych martwych kluczy; równoległe wycinki nie konkurują o ten sam plik                  |
| Luka: testy bez Flashcards       | Poza zakresem                                    | Roadmapa: bez zmian w istniejących modułach; mały, czysty diff                            |
| Podział na fazy                  | 2 fazy: host, potem testy                        | Start aplikacji z modułem sprawdzony osobno, zanim dojdą testy                            |

## Scope

**In scope:**
- Projekt `App01.Modules.Courses` + wpis w `APPS.sln` + `ModuleDI`
- Wycinek `ModuleHello` (Contracts, Validator, Handler, Endpoint)
- Referencja i rejestracja w hoście (`csproj`, `Program.cs`)
- Referencja w projekcie testów + `Features/Courses/ModuleHello/EndpointTests.cs`
- Zaktualizowane `packages.lock.json` (moduł, host, testy)

**Out of scope:**
- Encje, EF, migracje, logika Kursów (S-01, S-03)
- Klucze `Courses:*` w `appsettings.Example.json`
- Frontend (menu, trasy, serwis API)
- Referencja do Flashcards w projekcie testów
- Workery modułu

## Architecture / Approach

Kopia kształtu modułu Flashcards (csproj + `ModuleDI`) i wycinka Portal/UserList (4 pliki). Endpoint przechodzi pełną ścieżkę MediatR → handler z ręczną walidacją → `Results.Ok`, więc jeden test dowodzi rejestracji MediatR, walidatorów i endpointów modułu. `UseModuleCoursesEndpoints()` stoi przed `MapFallbackToFile`, a test sukcesu sprawdza treść JSON, bo niezarejestrowana trasa zwróciłaby HTML SPA ze statusem 200.

## Phases at a Glance

| Faza                              | Co dostarcza                                              | Główne ryzyko                                       |
| --------------------------------- | --------------------------------------------------------- | --------------------------------------------------- |
| 1. Moduł Courses w hoście         | Projekt, `ModuleDI`, `ModuleHello`, wpięcie w host        | Brak zacommitowanych lock-file'ów → CI `--locked-mode` |
| 2. Testy modułu w projekcie testów | Referencja w testach + 4 testy endpointu, pełny CI lokalnie | Test 200 „przechodzący” na fallbacku SPA (mitygacja: asercja treści) |

**Prerequisites:** brak; F-01 nie ma zależności. SDK .NET 10.0.100, lokalne `appsettings.json` do ręcznego uruchomienia hosta.
**Estimated effort:** ~1 krótka sesja, 2 fazy.

## Open Risks & Assumptions

- Zakładamy, że `ModuleHello` zostaje jako endpoint techniczny. Usunięcie go po pojawieniu się pierwszego prawdziwego endpointu Kursów to decyzja na później, poza tą zmianą.
- Ręczne sprawdzenie w fazie 1 wymaga działającej lokalnej bazy i konfiguracji JWT/X-TOKEN.

## Success Criteria (Summary)

- Host startuje z modułem Courses, a `ModuleHello` odpowiada zgodnie z kontraktem (200/401/403).
- Wszystkie istniejące testy i kroki CI przechodzą, więc nie ma regresji FR-012, FR-013, FR-014.
- S-01 i S-03 mogą dodawać wycinki do modułu bez zmian w hoście.
