# Autoryzacja: jedna polityka w DAL i rola admin na granicy web — Plan Brief

> Full plan: `context/changes/refactor-opportunities/plan.md`
> Research: `context/changes/refactor-opportunities/research.md`

## What & Why

Realizujemy pozycję #1 rankingu refactor opportunities (K3, autoryzacja) wraz z prerekwizytem P1 (osłona testowa). Dziś uprawnienia egzekwuje głównie menu. Każdy zalogowany może wywołać całą administrację, w tym konsolę SQL `DBWykonaj`, oraz zaksięgować operację kasową w dowolnej placówce. Reguły dostępu w DAL są skopiowane ręcznie 21 razy, a 8 kontroli odczytu i tak wysyła dane. Spośród trzech pozycji rankingu ta ma najwyższy koszt długu w stosunku do kosztu zmiany.

## Starting Point

- Solucja nie buduje się na tej maszynie bez obejścia, bo brakuje pakietu referencyjnego .NET 4.5.2.
- Nie ma ani jednego testu.
- `PozyczkaLombardowaDAL` ma 21 identycznych kontroli zapisu (role 5/6) i 8 kontroli odczytu (role 4/5/6) bez `return`.
- `KasaOperacjaDodaj`, `KasaOdczytaj` i `RemanentSyntetyka` nie mają żadnej kontroli.
- Role są w sesji (`zalogowanyRole`), ale czyta je tylko menu.

## Desired End State

- Jedna komenda (`verify.ps1`) buduje solucję na oryginalnych frameworkach docelowych i uruchamia zielone testy.
- Każda kontrola dostępu do placówki w DAL pożyczek przechodzi przez jedną klasę `PolitykaUprawnien`. Odmowa nie zwraca danych.
- Trzy luki są domknięte.
- Administracja i zarządzanie typami zastawów wymagają roli `admin` po stronie serwera.
- Uprawnieni użytkownicy nie widzą różnicy.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Zakres zmiany | Tylko #1 (K3) + P1; K1 i K2 osobno | Najwyższy koszt długu przy najniższym koszcie zmiany, bez bazy testowej i bez ruszania SQL | Research (ranking) / Plan |
| Głębokość | Luki + jedna polityka w DAL; kontrola placówki zostaje w DAL | Usuwa 21 kopii i luki bez zmiany modelu ról w sesji | Plan |
| Domykane luki | `AdminController` + `ZastawTypDodaj`/`Usun` (admin); `KasaOperacjaDodaj`, `KasaOdczytaj`, `RemanentSyntetyka`; `return` w 8 kontrolach odczytu | Wszystkie potwierdzone zapisy bez kontroli i wycieki danych | Research (V3, V9, V10) / Plan |
| Semantyka ról | Odczyt {4,5,6}, zapis {5,6}, jak dziś | Zero zmian dla uprawnionych; WIZYTOR zachowuje odczyt | Plan |
| Toolchain | `Directory.Build.targets` + pakiety NuGet ReferenceAssemblies net452/net45; projekty zostają na swoich TFM | Build i testy na tym samym TFM co produkcja; sprawdzone na tej maszynie | Plan |
| Testy | Projekt SDK-style `net48`, MSTest + Moq, mock `IAppDbContext` | `PozyczkaLombardowaDAL` już przyjmuje `IAppDbContext`, więc nie trzeba bazy | Research (§3.0) / Plan |
| Mechanizm roli admin | Filtr akcji czytający `zalogowanyRole` po globalnym `CheckSessionAttribute`; POST → JSON `code=1`, GET → 403 | Filtr autoryzacji biegłby przed wypełnieniem sesji; JSON trafia do istniejącego `AlertWarningShow` | Plan |
| Kolejność faz | Osłona → refaktor bez zmiany zachowania → zmiany zachowania | Każdą fazę da się wycofać osobno, a refaktor jest sprawdzalny testami charakteryzacyjnymi | Plan |

## Scope

**In scope:**
- `Directory.Build.targets`, projekt `eTMsTESTS`, `verify.ps1`
- `PolitykaUprawnien` i przepięcie 29 kontroli w `PozyczkaLombardowaDAL`
- Kontrole w `KasaOperacjaDodaj`, `KasaOdczytaj` (nowy parametr loginu) i `RemanentSyntetyka`; `return` w 8 kontrolach odczytu
- `WymagaRoliAttribute("admin")` na `AdminController` i 2 akcjach typów zastawów

**Out of scope:**
- K1 (protokół zapisu kasowego), K2 (parametryzacja SQL), P2 (baza LocalDB)
- Placówka docelowa w `AkcjaPrzenies`, kontrola w `Odczytaj(umoId)`
- Przeniesienie kontroli placówki do web, wyrównanie ról serwera do menu
- Retarget na .NET 4.8, CI, refaktor DI/UoW, inne kontrolery i DAL-e

## Architecture / Approach

Dwie warstwy egzekwowania:
- **Granica web.** Filtr `WymagaRoli` sprawdza rolę globalną `admin` z sesji.
- **DAL.** `PolitykaUprawnien` sprawdza rolę w konkretnej placówce. Tworzy ją `PozyczkaLombardowaDAL` z tym samym `IAppDbContext` i używa tych samych zapytań co dziś: zapis przez `UzytkownikUprawnienieLista`, odczyt przez `PozyczkaLombardowaPlacowkaUprawninegoLista`.

Testy mockują `IAppDbContext` i kontekst HTTP, więc nie potrzebują bazy ani IIS. Liczności (21/8 → 22/10) i brak kopii inline sprawdzają strukturalnie wzorce `ast-grep`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Build i projekt testowy (P1) | Build na oryginalnym TFM bez parametrów, `eTMsTESTS`, `verify.ps1`, 2 testy smoke | Kolejność importu MSBuild (warunek musi być w `.targets`); referencje MVC z projektu SDK-style |
| 2. Jedna polityka w DAL | 29 kontroli przez `PolitykaUprawnien`, zachowanie bez zmian | Pomyłka loginu lub `placowkaId` przy przepinaniu 29 miejsc |
| 3. Domknięcie luk w DAL | `return` w 8 odczytach, kontrola w 3 endpointach | Odcięcie użytkowników, którzy dziś korzystają z cudzych placówek przez endpointy bez kontroli |
| 4. Rola admin na granicy web | `WymagaRoli("admin")` na `AdminController` + 2 akcjach | Kolejność filtrów: pierwsze żądanie admina po zalogowaniu |

**Prerequisites:** VS 2026 (MSBuild), dotnet SDK, dostęp do nuget.org przy pierwszym restore; do testów ręcznych lokalna instancja aplikacji z kopią bazy oraz konta admin, LOMBARDZISTA i WIZYTOR.
**Estimated effort:** ~3–4 sesje: faza 1 około 1, fazy 2–3 około 1–2, faza 4 około 0,5–1.

## Open Risks & Assumptions

- Zakładamy, że w produkcji nikt nie korzysta legalnie z `Admin/*` bez flagi `czyAdministrator`. Zdaniem research to luka, nie decyzja (§2.1).
- Ograniczenia dostępu spoza aplikacji (IIS, sieć) są nieznane (Open Q1 w research). Nie zmieniają planu, ale wpływają na jego pilność.
- Pakiety ReferenceAssemblies 1.0.3 muszą być dostępne z nuget.org przy pierwszym restore na każdej maszynie bez pakietu deweloperskiego.
- Ręczny test z kopią bazy wymaga środowiska, którego repo nie dostarcza (brak seeda placówek, kasy i uprawnień).

## Success Criteria (Summary)

- `verify.ps1` jest zielony, a kontrolę strukturalną zapewniają wzorce ast-grep: 0 kopii inline, 22 kontrole zapisu i 10 odczytu przez politykę.
- LOMBARDZISTA nie zapisze ani nie odczyta danych cudzej placówki, nie wywoła administracji ani konsoli SQL. Dostaje `code=1` lub 403 bez danych.
- Admin, LOMBARDZISTA i WIZYTOR w swoich placówkach pracują bez zmian.
