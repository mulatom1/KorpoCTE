---
title: "Raport architektoniczny — moduł 4 (10xArchitect)"
created: 2026-10-15
sources:
  - context/map/repo-map.md (L2)
  - context/domain/post-flow-analysis/research.md (L3)
  - context/domain/refactor-opportunities/plan.md (+ plan-brief.md, research.md, reviews/plan-review.md) (L4)
  - context/domain/01-domain-distillation.md, 02-invariant-aggregate-refactor.md, 03-anti-corruption-layer.md, glossary.md (L5)
---

# Raport architektoniczny — moduł 4

## 1. Opisane projekty

| Repo | Stack | Skala (z artefaktów) | Artefakty |
| --- | --- | --- | --- |
| **eTMsLomb** („Lombard”), back-office lombardu | ASP.NET MVC 5 / .NET 4.5.2, AngularJS, SQL Server (ADO.NET, SQL w stringach), Autofac, AutoMapper | 5 projektów w `.sln`, 221 plików kodu, 28 tabel, 12 capability; 4 commity w całej historii, repo uśpione ~32 mies.; 0 testów | **L2, L3, L4** |
| **App01** (tomsoft1.pl), moduł Kursy | ASP.NET Core 10 (modularny monolit, Minimal APIs, MediatR 12, EF Core 10), React 19 + Vite | moduły Portal / Lotto / Flashcards / Courses; MediatR w 117 plikach, EF Core w 88; baseline `7daf069` | **L5** |

L2–L4 to jedna ciągła ścieżka na eTMsLomb. L5 robiłem na innym repo (App01), więc wnioski domenowe nie dotyczą lombardu.

## 2. Mapa projektu (L2, eTMsLomb)

- **Brak „buzzu”, więc ranking idzie po krytyczności × zasięgu.** Po imporcie w 2020 r. weszła jedna zmiana w kodzie (`set dateformat ymd` w 110 zapytaniach). Strefy ryzyka mają więc status *watch*, a nie hot.
- **Watch 1 — god context danych.** `AppDbContext.cs` (~3,9 tys. linii) zawiera całe SQL systemu i zależy od niego wszystkie 12 capability. 921 linii skleja stringi, 0 transakcji w C# (tylko 32 linie `BEGIN/COMMIT TRAN` w tekście SQL).
- **Watch 2 — monolit pożyczek (lokalne centrum).** `PozyczkaLombardowaDAL.cs` (3166 linii) i kontroler (802) obsługują 5 capability: pożyczki, kasę, sprzedaż, remanent i książkę (Ca 5 / Ce 8). Tędy płyną pieniądze, a testów nie ma.
- **Watch 3 — uprawnienia „tylko w menu”** (inference). Mapa nie znalazła żadnego `[Authorize]`, a `DBWykonaj` wykonuje dowolne SQL.
- **Entry pointy:** `CheckSessionAttribute` (globalny filtr sesji), `PozyczkaLombardowaController`, `AdminController`. **Najważniejsze unknowns:** autoryzacja serwerowa, powierzchnia SQL injection, integralność transakcji kasowych, dryf encje ↔ DDL ↔ SQL.

## 3. Analiza ficzera (L3, eTMsLomb)

**Przepływ:** spłata całkowita pożyczki (`PozyczkaLombardowaAkcjaSplataCalkowita`). Wybrałem go, bo leży w Watch 2: płyną nim pieniądze, sprzężenie jest najtwardsze (pożyczki i kasa w tych samych plikach) i nie ma testów. Przy okazji sprawdza unknowns U1–U5 mapy.

**Overview.** Input to POST z przeglądarki z `umoId`, datą i trzema kwotami, które wyliczył wcześniejszy podgląd `Wylicz`. Kontroler pomija BLL i woła DAL. DAL wykonuje 7 odczytów, sprawdza status 3, uprawnienia 5/6 w placówce umowy i sumę kwot. Stan zmienia jeden batch SQL w transakcji tekstowej: umowa przechodzi w status 5, powstaje operacja kasowa (akcja 7) z 1–3 rozliczeniami, zastawy dostają oznaczenie „zbyte”, a stan kasy placówki jest aktualizowany. Wraca `Result{code,message}`, po czym UI czyta umowę ponownie (10–12 zapytań). Skutki widzą raporty kasy, sprzedaży zastawów, remanentu i książki.

**Najważniejsze ryzyka:**
1. **Atomowość i współbieżność kasy (TD8–TD11).** W SQL nie ma `XACT_ABORT` ani `TRY…CATCH`. Strażnik `@ostatniStanKasy` nie zakłada blokady, więc możliwy jest lost update, a żadne z 11 `UPDATE … where umoId` nie sprawdza statusu. *ast-grep (S5, S20–S22):* strażnik ma 7 kopii, ale w **3 różnych wariantach**, więc wspólna poprawka to 3 różne edycje, a nie jedno „search & replace”.
2. **Luki autoryzacji (TD15).** Teza mapy jest obalona dla 21 metod DAL, które sprawdzają role 5/6. *ast-grep (S37, S39)* potwierdził jednak, że `KasaOperacjaDodaj` zapisuje operację kasową w placówce wskazanej w żądaniu bez żadnej kontroli, a `DBWykonaj` wykonuje SQL bez sprawdzenia roli.
3. **Luka testowa × blast radius (TD16, §4).** Każda z 4 warstw jest bez pokrycia. Akcja ma po 1 wywołującym na każdym poziomie, ale jest ręcznie sprzężona z 6 siostrzanymi przepływami kasowymi i ze współdzielonymi odczytami (po 9 wywołujących, S6/S7). Do tego serwer ufa odsetkom przysłanym przez klienta (TD1).

## 4. Plan refaktoryzacji (L4, eTMsLomb)

**Co refaktoryzujemy.** Pozycję #1 rankingu, czyli K3 (autoryzacja), razem z prerekwizytem P1 (osłona testowa). Docelowo każda kontrola dostępu do placówki w `PozyczkaLombardowaDAL` przechodzi przez jedną klasę `PolitykaUprawnien` (zapis {5,6}, odczyt {4,5,6}). Odmowa nie zwraca danych, 3 luki są domknięte, a `AdminController` i akcje typów zastawów wymagają roli `admin` przez filtr `WymagaRoli` na granicy web.

**Czego świadomie NIE robimy:** K1 (protokół zapisu kasowego: XACT_ABORT, blokady), K2 (parametryzacja SQL), P2 (baza LocalDB), zmiany modelu ról i przeniesienia kontroli placówki do warstwy web, retargetu na .NET 4.8, CI, refaktoru UoW/DI ani zmian w innych kontrolerach i DAL-ach.

| Faza | Jedna linijka | Weryfikacja |
| --- | --- | --- |
| 1 | Build na oryginalnym TFM (`Directory.Build.targets`), projekt `eTMsTESTS` (MSTest + Moq), `verify.ps1`, 2 testy smoke | auto (`verify.ps1`, `git diff` csproj) + ręcznie (Test Explorer w VS) |
| 2 | 21 kontroli zapisu i 8 kontroli odczytu przechodzi przez `PolitykaUprawnien`, **bez zmiany zachowania** | auto (testy charakteryzacyjne, ast-grep: 0 kopii inline, 21/8 wywołań) + ręcznie (przegląd diffu 29 miejsc) |
| 3 | `return` w 8 kontrolach odczytu, kontrola w `KasaOperacjaDodaj`, `KasaOdczytaj` i `RemanentSyntetyka` | auto (testy odmowy, ast-grep 22/10) + ręcznie (DevTools: żądania do cudzej placówki) |
| 4 | `[WymagaRoli("admin")]` na `AdminController` i 2 akcjach typów zastawów | auto (testy filtra, ast-grep) + ręcznie (admin po świeżym logowaniu, LOMBARDZISTA → 403 / `code=1`) |

## 5. Domena wg DDD (L5, App01 — moduł Kursy)

**Ubiquitous language:** *Flaga* (trofeum należące do kursu), *Kod flagi* (sekret `SAD{…}` służący do aktywacji), *Weryfikacja* (ocena odpowiedzi modelem, która wydaje kod), *Aktywacja* (jedyny moment zdobycia flagi), *Zdobycie* (`UserFlag`, najwyżej raz). **Najważniejsze rozjazdy model ↔ kod:**
- DR-01: PRD mówi, że poprawna odpowiedź „przyznaje flagę”. W kodzie przyznaje ją dopiero aktywacja kodem.
- DR-03: „flaga dowodzi wykonania” nie trzyma, bo wystarczy znać kod.
- „Flaga” ma trzy znaczenia: trofeum, `IsAdmin` i ciąg `SAD{…}`. „Uczestnik”, „Grupa”, „Pula” i „Bootcamp” nie istnieją w kodzie jako byty.

**Niezmiennik #1 (I-01):** zdobycie flagi powstaje tylko z dowodu wykonania. Dla flagi z kryteriami tym dowodem jest `Correct` tego samego użytkownika. Dziś reguły nie egzekwuje żadna warstwa serwera, pilnuje jej tylko choreografia UI, a istniejący test utrwala naruszenie. **Agregat:** nowy `FlagClaim` z tożsamością `(UserId, FlagId)` i nową encją `FlagVerification`. To jedyne miejsce, które może utworzyć `UserFlag`. Plan czeka na decyzję właściciela (Q-01).

**ACL:** przecieka dostawca LLM, czyli OpenRouter (L-01), przez **4 warstwy / 7 plików**: port nazwany od dostawcy, adapter infrastruktury, handler Courses (stringi ról, obejście ogrodzenia Markdown), pełną kopię protokołu w Flashcards, testy i tekst w odpowiedzi 500. Do tego `CLAUDE.md` twierdzi, że Flashcards używa `OpenRouterService`, a kod temu przeczy. Docelowo powstaje wąski port `ILlmCompletion` + `IAnswerAssessor` / `AnswerVerdict` w module.

## 6. Decyzje, które należą do mnie

AI zbudowało mapę, inwentarz długu (TD1–TD22) i ranking kandydatów K1–K3 z trade-offami. Zweryfikowało też twierdzenia ast-grepem i grepem i wyłapało przy tym 5 fałszywych zer, które wynikały z błędnych wzorców. Ja zdecydowałem, że eksploracja kończy się bez decyzji, a o zakresie rozstrzygam dopiero w planie. Tam wybrałem K3 + P1 i odłożyłem K1/K2, bo K3 nie wymaga bazy testowej ani zmian w SQL, a luki (`DBWykonaj`, `KasaOperacjaDodaj`) są realne i dotyczą pieniędzy oraz dostępu do bazy. Kolejność faz (osłona → refaktor bez zmiany zachowania → zmiany zachowania) wybrałem po to, żeby każdą fazę dało się wycofać osobno, a semantykę ról zostawiłem bez zmian, by uprawnieni użytkownicy nie zauważyli różnicy. W L5 nie pozwoliłem planowi przesądzić reguł produktowych: Q-01 (czy aktywacja wymaga `Correct`), Q-03 i Q-10 to decyzje właściciela produktu, nie architekta.
