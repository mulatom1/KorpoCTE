---
date: 2026-10-14T17:27:16+02:00
researcher: Claude Code (Opus 5.5) na zlecenie Tomasza Mularczyka
git_commit: 375c6fd14b9654a602cea3ed697fdb3bfe255bb5
branch: master
repository: Lombard (eTMsLomb)
topic: "Przepływ spłaty całkowitej pożyczki lombardowej (PozyczkaLombardowaAkcjaSplataCalkowita)"
tags: [research, pawn-loans, cashdesk, collateral-sale, stocktake, pawn-ledger, PozyczkaLombardowaDAL, AppDbContext, transakcje, autoryzacja, testy, blast-radius]
status: complete
last_updated: 2026-10-14
last_updated_by: Claude Code (Opus 5.5)
last_updated_note: "Weryfikacja strukturalna ast-grep 0.50.0 (§7): 42 twierdzenia strukturalne sprawdzone wzorcami AST, każde zero potwierdzone grepem; korekty naniesione w §Podsumowanie, §1, §2, §4, §6 i Otwarte pytania. Wcześniej: follow-up z trzema analizami (trace e2e, luki w testach, blast radius)."
---

# Research: Przepływ spłaty całkowitej pożyczki lombardowej

**Data**: 2026-10-14T17:27:16+02:00 (wersja 1), follow-up 2026-10-14
**Badacz**: Claude Code (Opus 5.5) na zlecenie Tomasza Mularczyka
**Commit**: 375c6fd14b9654a602cea3ed697fdb3bfe255bb5
**Gałąź**: master
**Repozytorium**: Lombard (eTMsLomb)

## Pytanie badawcze

**Wersja 1.** Jak przebiega spłata całkowita od akcji `PozyczkaLombardowaAkcjaSplataCalkowita` (`eTMsWEB/Controllers/PozyczkaLombardowaController.cs:284`) przez DAL i BLL do SQL w `AppDbContext.cs`? Potwierdzić albo obalić unknowns U1–U5 z `context/map/repo-map.md`.

**Follow-up.** Przeanalizować ten przepływ ze szczególną uwagą na powiązane obszary z mapy: kasę (`cashdesk`), sprzedaż zastawów (`collateral-sale`), remanent (`stocktake`), książkę lombardową (`pawn-ledger`), uprawnienia (`users-access`) i wspólną warstwę danych. Trzy wymiary:

1. trace e2e;
2. luki w testach;
3. blast radius.

Tylko opis stanu obecnego, bez rekomendacji implementacyjnych.

**Metoda.** Wersja 1 powstała lokalnie. Follow-up to 3 równoległe sub-agenty (read-only), których wyniki główny agent zweryfikował punktowo w kodzie; korekty są w sekcji „Korekty”. Wszystkie twierdzenia strukturalne (liczby call-site'ów, „tylko tutaj”, liczności, powtarzalne kształty) przeszły potem weryfikację wzorcami AST (`ast-grep` 0.50.0, języki `csharp`/`javascript`); każde zero z ast-grep sprawdzono klasycznym grepem. Wyniki są w §7. Wszystko to analiza statyczna: nic nie było budowane ani uruchamiane.

**Legenda.** **[E]** evidence, czyli widziane w kodzie lub historii git, z kotwicą `plik:linia`. **[I]** inference, czyli wniosek z kodu, niewykonany na działającym systemie. **[U]** unknown, czyli biała plama, której nie da się rozstrzygnąć z repozytorium.

## Podsumowanie

- **Co to jest [E].** Akcja zamyka umowę lombardową w statusie 3 (WYPŁACONA). Ustawia status 5, zapisuje operację kasową (akcja 7) z 1–3 rozliczeniami (kapitał, odsetki, odsetki zaległe), oznacza wszystkie zastawy jako zbyte i aktualizuje stan kasy placówki. Wszystko idzie jednym batchem SQL w transakcji (`AppDbContext.cs:2122-2247`).
- **Ryzyko główne [E+I].** Serwer nie przelicza odsetek, tylko sprawdza spójność sumy przysłanej przez klienta (`PozyczkaLombardowaDAL.cs:1262`). Atomowość batcha nie jest pełna: brak `XACT_ABORT`. Strażnik stanu kasy nie blokuje wiersza.
- **Testy [E].** W repo nie ma żadnego testu automatycznego, CI ani frameworka testowego. Każda z 4 warstw przepływu jest bez pokrycia.
- **Blast radius [E+I].** Sama akcja ma po 1 wywołującym na każdym poziomie (JS `PozyczkaLombardowa.js:722` → `Controller.cs:288` → `DAL:1420`; potwierdzone ast-grep, §7). Zmiana jest jednak sprzężona ręcznie z:
  - 6 siostrzanymi przepływami kasowymi ze strażnikiem `@ostatniStanKasy` (7 metod). Strażnik **nie jest jednym wzorcem**, tylko 3 wariantami: `rollback + THROW` (3 metody, w tym ta), `rollback + select errorCode` (3) i `THROW` bez `rollback` (`KasaOperacjaDodaj`) (§7, S5);
  - współdzielonymi odczytami (`PozyczkaLombardowaKasaOdczytaj` i `PozyczkaLombardowaZastawLista` mają po 9 wywołujących w DAL);
  - raportami kasy, sprzedaży zastawów, remanentu i książki lombardowej, które czytają zapisane kolumny.
- **Nowe po ast-grep [E].** `KasaOperacjaDodaj`, czyli jeden z 7 przepływów zmieniających stan kasy, **nie sprawdza uprawnień** w DAL, a `placowkaId` bierze z żądania (`DAL:2715-2850`, `Controller.cs:416-419`). `AdminController.DBWykonaj` wykonuje SQL z żądania bez żadnego sprawdzenia uprawnień (`AdminController.cs:340-364`). Żadne z 11 `UPDATE … where umoId` w metodach `PozyczkaLombardowaAkcja*` nie ma warunku na `statusId` (§7, S20).

  Historia git nie daje żadnego merytorycznego co-change.

| Unknown z mapy | Werdykt | Dowód |
| --- | --- | --- |
| U1 transakcje | częściowo potwierdzone [E]; ryzyko częściowego commitu i wyścigu [I] | `AppDbContext.cs:2127-2245` |
| U2 autoryzacja | teza mapy obalona dla tej akcji: DAL sprawdza uprawnienie 5/6 w placówce umowy [E] | `PozyczkaLombardowaDAL.cs:1248` |
| U3 SQL injection | parametry akcji typowane [E]; login z sesji bez escapowania, pochodzi z podatnego logowania [E] | `AppDbContext.cs:2134`, `:754` |
| U4 `dateformat ymd` | potwierdzone dla 6 z 6 zapytań [E] | 375c6fd |
| U5 `TB_PARAMETRY` | obalone dla tego przepływu [E] | `PozyczkaLombardowaBLL.cs:46-75` |

## 1. Feature overview

### 1.1 Cel biznesowy i warunki wejścia

- **[E]** Przycisk SPŁATA CAŁKOWITA pojawia się, gdy `pozyczka.statusId == 3 && pozyczka.zastawyIloscNaStanie == pozyczka.zastawy.length` (`eTMsWEB/Views/PozyczkaLombardowa/PozyczkaLombardowaEdycjaAkcje.cshtml:12`). `zastawyIloscNaStanie` to właściwość wyliczana z `dataZbycia` (`eTMsDB/ENTITIES/PozyczkaLombardowaItem.cs:138`).
- **[E]** Dwuetapowy UX:
  - WYLICZ: podgląd kwot przez `PozyczkaLombardowaWylicz`, bez zapisu.
  - WYKONAJ: zapis przez `PozyczkaLombardowaAkcjaSplataCalkowita`. Przycisk jest odblokowany flagą `splataCalkowita.wykonaj` (`.cshtml:386`, `:449`; `PozyczkaLombardowa.js:694-700`).
- **[E]** Wymagane po stronie serwera (`PozyczkaLombardowaDAL.cs:1226-1303`):
  - data spłaty ≤ dziś;
  - umowa istnieje i ma status 3;
  - użytkownik ma uprawnienie 5 (LOMBARDZISTA) lub 6 (KONTROLER) w placówce umowy (`Initial.sql:498-499`);
  - data spłaty ≥ data pożyczki;
  - `kwotaZwrotu == kwotaPozyczki + kwotaOdsetek + kwotaOdsetekZaleglych`;
  - istnieją słowniki rozliczeń 7/8/9;
  - istnieje rekord kasy placówki, a jego `dataZmiany ≤ dataSplaty`.

### 1.2 Trace e2e: sekwencja kroków

**Przed akcją: infrastruktura żądania**

0. **[E]** Startup (`eTMsWEB/Global.asax.cs:20-27`) rejestruje filtry globalne `HandleErrorAttribute` i `new CheckSessionAttribute()` (`eTMsWEB/App_Start/FilterConfig.cs:12-13`). Następnie konfiguruje Autofac (`eTMsWEB/ETMSWEBConfigure.cs:18-35`): kontrolery, moduły DB/DAL/BLL i drugą rejestrację `CheckSessionAttribute` jako filtra akcji.
   - **[I]** Filtr może się wykonać dwukrotnie. Drugie przejście nic nie robi, bo sesja jest już ustawiona.
1. **[E]** Uwierzytelnianie: Forms z `<deny users="?"/>` (`eTMsWEB/Web.config:45-50`). Token antyforgery jest w layoucie (`Views/Shared/_LayoutPage.cshtml:23`), a obie akcje mają `[ValidateAntiForgeryToken]` (`PozyczkaLombardowaController.cs:154`, `:283`; wcześniej błędnie `:153`, `:280`). Wszystkie 42 akcje `[HttpPost]` tego kontrolera mają ten atrybut; 14 pozostałych publicznych metod to `[HttpGet]` (§7, S31).
2. **[E]** `CheckSessionAttribute.OnActionExecuting` (`eTMsWEB/Filters/CheckSessionAttribute.cs:36-85`) przy pustej sesji ustawia `zalogowanyLogin = Identity.Name.ToUpper()`, placówkę i role. **Nie autoryzuje akcji.**
3. **[E]** Kontroler dostaje `IUnitOfWorkDAL` i `IUnitOfWorkBLL` z Autofac (`PozyczkaLombardowaController.cs:20-24`). UoW tworzy jednak zależności ręcznie, z pominięciem kontenera:
   - `new AppDbContext()` (`eTMsDAL/REPOSITORY/UnitOfWorkDAL.cs:23-30`);
   - `new PozyczkaLombardowaDAL(AppDbContext)` (`UnitOfWorkDAL.cs:96-103`);
   - `new PozyczkaLombardowaBLL()` (`eTMsBLL/REPOSITORY/UnitOfWorkBLL.cs:23-29`);
   - poza UoW: drugie `new AppDbContext()` w konstruktorze filtra (`eTMsWEB/Filters/CheckSessionAttribute.cs:22`), dodane po ast-grep.

**Etap WYLICZ (bez zapisu)**

4. **[E]** `SplataCalkowitaPokaz` (`PozyczkaLombardowa.js:659-664`) ustawia datę bieżącą i `minProcentZwrotu` z umowy. `SplataCalkowitaWylicz` (`js:665-708`) sprawdza datę po stronie klienta i wysyła POST `PozyczkaLombardowaWylicz` z polami `dataPozyczki`, `dataZwrotu`, `kwotaPozyczki`, `kwotaOdsetekZaleglych`, `procentNaDzien` (4 miejsca) i `minProcentZwrotu` (4 miejsca). Wartości pochodzą z `$scope.pozyczka`.
5. **[E]** `PozyczkaLombardowaController.cs:155-160` wywołuje `PozyczkaLombardowaBLL.Wylicz` (`eTMsBLL/REPOSITORY/PozyczkaLombardowaBLL.cs:46-75`):
   - `odsetki = round(kwota × procentNaDzien/100 × dni, 2)`, nie mniej niż `round(kwota × minProcent/100, 2)`;
   - `kwotaZwrotu`;
   - RRSO bisekcją (`:77-115`).

   Bez bazy. Wynik przechodzi przez `ETMSCoreBLL.ObjectToJson` (Newtonsoft, `eTMsBLL/REPOSITORY/ETMSCoreBLL.cs:16-25`) jako `Result<T> {code, message, data}` (`eTMsDAL/MODELS/Result.cs:8-29`).

**Etap WYKONAJ (zapis)**

6. **[E]** `PozyczkaAkcjaSplataCalkowita` (`js:709-745`) wysyła POST z polami `umoId`, `dataSplaty`, `kwotaZwrotu`, `kwotaOdsetek`, `kwotaOdsetekZaleglych` i `minProcentZwrotu`, tym razem z **2** miejscami po przecinku.
7. **[E]** Kontroler (`:282-292`) woła **bezpośrednio DAL**, z pominięciem BLL, i przekazuje `Session["zalogowanyLogin"]`.
8. **[E]** Odczyty w DAL (`PozyczkaLombardowaDAL.cs:1235-1395`), każdy przez `GetDataSet` (`eTMsDB/REPOSITORIES/AppDbContextBase.cs:70-87`) na nowym `SqlConnection`. ast-grep: metoda DAL ma 8 wywołań `appDbContext.*`, czyli 7 odczytów (6 różnych zapytań, `KasaRozliczenieOdczytaj` ×3) i 1 zapis.

   | Odczyt | Kotwica | Tabela |
   | --- | --- | --- |
   | `PozyczkaLombardowaOdczytaj` | `AppDbContext.cs:1425` | `TB_POZYCZKI_LOMBARDOWE` (`pl.*`) |
   | `UzytkownikUprawnienieLista(login, placowkaId)` | `:932` | `TB_UZYTKOWNICY_UPRAWNIENIA` |
   | `PozyczkaLombardowaKasaRozliczenieOdczytaj` ×3 | `:3246` | `SL_POZYCZKI_LOMBARDOWE_KASA_ROZLICZENIA` |
   | `PozyczkaLombardowaKasaOdczytaj` | `:3262` | `TB_POZYCZKI_LOMBARDOWE_KASA` |
   | `PozyczkaLombardowaZastawLista` | `:1732` | `TB_POZYCZKI_LOMBARDOWE_ZASTAWY` + `_ZASTAWY_TYPY` |

9. **[E]** Obliczenia w pamięci (`DAL:1304-1418`):
   - status 5, `dataSplaty`, `splacal`, `wplaty*`;
   - operacja kasowa akcja 7;
   - rozliczenie 7 (kapitał, `wplywNaStan = 1` na sztywno, `:1340`);
   - rozliczenie 8, jeśli odsetki > 0, i 9, jeśli zaległe > 0, z VAT liczonym „od brutto” ze stawki słownika;
   - nowy `stanKasy` jako wartość absolutna;
   - podział brutto/VAT na zastawy wg `udzialProcentowy`, z resztą na ostatni zastaw.
10. **[E]** Zapis (`AppDbContext.cs:2122-2247`, `ExecuteNonQuery` w `AppDbContextBase.cs:89-104`, bez `SqlParameter`):
    - `set dateformat ymd; begin tran`;
    - strażnik `@ostatniStanKasy` z `rollback` + `THROW 60000`;
    - `UPDATE TB_POZYCZKI_LOMBARDOWE … where umoId`;
    - `INSERT TB_POZYCZKI_LOMBARDOWE_KASA_OPERACJE` + `scope_identity()`;
    - 1–3× `INSERT …_KASA_OPERACJE_ROZLICZENIA`;
    - N× `UPDATE TB_POZYCZKI_LOMBARDOWE_ZASTAWY … where zastawId`;
    - `UPDATE TB_POZYCZKI_LOMBARDOWE_KASA … where placowkaId`;
    - `commit tran`.
11. **[E]** Każdy wyjątek łapie DAL i zwraca `{code: 1, message}` (`DAL:1424-1428`, `eTMsDAL/REPOSITORY/RepoBase.cs:12-21`). Kontroler serializuje `Result`.

**Powrót do UI**

12. **[E]** Przy `code == 0` JS woła `$scope.PozyczkaOdczytaj()` i zamyka modal (`js:733-736`). W przeciwnym razie pokazuje `AlertWarningShow` (błąd biznesowy) albo `AlertErrorShow` (błąd HTTP).
13. **[E]** `PozyczkaOdczytaj` (`js:395-426`) idzie przez `Controller.cs:133-139` do `PozyczkaLombardowaDAL.Odczytaj` (`DAL:262-337`) i czyta ponownie:
    - umowę, placówkę i status (`SL_POZYCZKI_LOMBARDOWE_STATUSY`);
    - klientów, upoważnionych i zastawy;
    - operacje kasowe i rozliczenia;
    - uwagi i historię.

    To 10 bezwarunkowych wywołań `appDbContext.*` i 2 warunkowe (`PlacowkaOdczytaj` dla przeniesienia/przyjęcia, `DAL:305`, `:317`), razem 10–12 zapytań. Nowy `statusId = 5` ukrywa przycisk.

### 1.3 Diagram sekwencji

```mermaid
sequenceDiagram
  participant B as Browser/JS
  participant F as Filtry (Forms auth, AntiForgery, CheckSession)
  participant C as PozyczkaLombardowaController
  participant BLL as PozyczkaLombardowaBLL
  participant DAL as PozyczkaLombardowaDAL
  participant DB as AppDbContext
  participant SQL as SQL Server
  B->>F: POST PozyczkaLombardowaWylicz (kwoty i stawki z $scope)
  F->>C: Wylicz(item, minProcentZwrotu)
  C->>BLL: Wylicz: odsetki, kwotaZwrotu, RRSO
  BLL-->>C: Result<Item>
  C-->>B: JSON, wykonaj=true
  B->>F: POST PozyczkaLombardowaAkcjaSplataCalkowita (umoId, data, 3 kwoty, minProcent)
  F->>C: akcja + Session[zalogowanyLogin]
  C->>DAL: AkcjaSplataCalkowita (BLL pominięty)
  DAL->>DB: Odczytaj / UprawnienieLista / KasaRozliczenieOdczytaj x3 / KasaOdczytaj / ZastawLista
  DB->>SQL: SELECT (osobne połączenia, poza transakcją)
  SQL-->>DAL: dane
  Note over DAL: walidacje: status 3, uprawn. 5/6, suma kwot, data kasy; wyliczenia w pamięci
  DAL->>DB: PozyczkaLombardowaAkcjaSplataCalkowita(...)
  DB->>SQL: begin tran; strażnik stanKasy; UPDATE umowa; INSERT operacja; INSERT rozliczenia; UPDATE zastawy; UPDATE kasa; commit
  SQL-->>DAL: OK albo THROW 60000 / SqlException
  DAL-->>C: Result{code,message}
  C-->>B: JSON
  B->>C: POST PozyczkaLombardowaOdczytaj(umoId)
  C->>DAL: Odczytaj (10-12 zapytań)
  DAL-->>B: JSON, $scope.pozyczka (status 5)
```

### 1.4 Powiązane obszary z mapy (kto konsumuje skutki spłaty)

| Obszar (mapa) | Jak jest powiązany | Status |
| --- | --- | --- |
| cashdesk (kasa) | zapis `TB_…_KASA`, `…_KASA_OPERACJE`, `…_ROZLICZENIA`; raport rozliczeń `PozyczkaLombardowaKasaOperacjaRozliczenieLista` (`AppDbContext.cs:3401`) i filtr `akcjaOperacjiId7` w `Scripts/…/PozyczkaLombardowaKasa.js:48` | [E] |
| collateral-sale (sprzedaż zastawów) | lista, wydruk i CSV `PozyczkaLombardowaOperacjaSprzedazyZastawuLista` (2 przeciążenia: `AppDbContext.cs:3670`, `:3761`; filtr `dataZbycia is not null` w `:3721`, `:3813`) filtrują `dataZbycia is not null`, status i akcję; JS domyślnie włącza status 5 i akcję 7 (`PozyczkaLombardowaOperacjaSprzedazyZastawu.js:43`, `:49`). Spłata całkowita trafia więc do raportu zbycia | [E] |
| stocktake (remanent) | analityka bierze tylko `pl.statusId = 3` (`AppDbContext.cs:3640`). Spłacona umowa (status 5) znika z remanentu | [E] |
| pawn-ledger (książka) | wyświetla `dataSplaty` (`eTMsWEB/Views/PozyczkaLombardowa/PozyczkaLombardowaKsiazkaLombardowaPozycja.cshtml:77-85`, `…KsiazkaLombardowaWydrukOdczytaj.cshtml`) | [E] |
| users-access | `CheckSessionAttribute` (sesja i login), `TB_UZYTKOWNICY_UPRAWNIENIA` (autoryzacja w DAL), logowanie (`AppDbContext.cs:754`) | [E] |
| shared (warstwa danych) | `AppDbContext`/`AppDbContextBase`, mapowanie refleksją `SetItemFromRow` (`AppDbContextBase.cs:52-66`) | [E] |
| parameters | brak powiązania w tym przepływie (U5) | [E] |

## 2. Technical debt

Pozycje uporządkowane wg wpływu na pieniądze i spójność danych. Każda ma podział na evidence, inference i unknown.

### 2.1 Poprawność finansowa

| # | Dług | Evidence | Inference | Unknown |
| --- | --- | --- | --- | --- |
| TD1 | Serwer ufa odsetkom od klienta | Sprawdza tylko sumę (`DAL:1262`); nie woła `Wylicz`, choć ten sam kontroler przelicza odsetki serwerowo i nadpisuje wartości klienta w `PozyczkaLombardowaDodaj` i `…MetrykaZmien` (`Controller.cs:166-170`, `:180-184`; to jedyne 3 wywołania BLL, §7 S13); nie porównuje z `pozyczka.kwotaOdsetekZaleglych` (`PozyczkaLombardowaItem.cs:41`); `minProcentZwrotu` też przychodzi z klienta i jest zapisywany | Żądanie spoza UI z odsetkami 0 (lub ujemnymi; nic tego nie blokuje w `DAL:1220-1303`) zamknie umowę z kasą zasiloną tylko kapitałem | Czy to świadoma furtka (rabat) |
| TD2 | Błąd netto zastawów | `kwotaZbyciaNetto = sumKwotaZbyciaBrutto - zastaw.kwotaZbyciaVat` (`DAL:1407`) używa sumy narastającej. ast-grep: kształt występuje **tylko tutaj**; pozostałe przepływy ustawiają netto na `0m` albo z operacji (`DAL:1206`, `:1547`, `:1783`, `:2166`) | Przy ≥3 zastawach zastawy `i = 1 … n-2` mają zawyżone netto, które trafia do raportu i CSV sprzedaży zastawów (§1.4) | Ile umów produkcyjnych ma ≥3 zastawy |
| TD3 | Nadpisanie sprzedanego zastawu | Spłata nie filtruje `dataZbycia != null` (`DAL:1395-1418`, `AppDbContext.cs:2227-2238`); sprzedaż jednego zastawu zostawia status 3 (`DAL:2168-2169`); blokuje to tylko UI (`.cshtml:12`) | Żądanie spoza UI nadpisze dane sprzedaży i rozliczy kapitał ponownie (`wplatyKwotaPozyczki = kwotaPozyczki`, `DAL:1312`) | — |
| TD4 | Pusta lista zastawów | `ZastawUsun` dopuszcza status 3 i fizyczny `delete` bez minimum (`DAL:831`, `AppDbContext` `PozyczkaLombardowaZastawUsun`); spłata indeksuje `zastawy[Count-1]` (`DAL:1410`) | Umowa WYPŁACONA bez zastawów nie da się spłacić całkowicie; użytkownik dostaje techniczny komunikat | Czy taki stan występuje w danych |
| TD5 | Rozjazd precyzji `minProcentZwrotu` | Wylicz wysyła 4 miejsca (`js:691`), akcja 2 (`js:731`), kolumna `DECIMAL(18,2)` (`TB_POZYCZKI_LOMBARDOWE.sql:16`), formularz dopuszcza 4 (`.cshtml:380`) | Próg minimalnych odsetek w podglądzie może się różnić od zapisanego | — |
| TD6 | RRSO dla 0 dni | Wykładnik `dni/365 = 0` daje stałą sumę (`PozyczkaLombardowaBLL.cs:93`); pętla do `lp > 1000000` (`:103`) | Spłata tego samego dnia daje w podglądzie RRSO rzędu 10^13–10^14 i do 10^6 iteracji. RRSO nie jest zapisywane przez spłatę całkowitą | — |
| TD7 | Słownik rozliczeń kapitałowych ignorowany | `wplywNaStan = 1`, VAT 0 na sztywno (`DAL:1340`), choć słownik jest czytany (`:1269`). ast-grep: to konwencja całego DAL, a nie wyjątek tego przepływu. `wplywNaStan` jest wpisany literałem w 8 miejscach: `+1` dla rozliczenia 7 w 5 przepływach (`DAL:1188`, `1340`, `1529`, `1765`, `2056`), `-1` dla rozliczenia 3 w 3 (`:1070`, `1634`, `1870`). Ze słownika bierze go 6 miejsc (8/9/10/11–13 i `KasaOperacjaDodaj`) | Zmiana słownika nie zadziała dla kapitału w żadnym przepływie | — |

### 2.2 Spójność i współbieżność

| # | Dług | Evidence | Inference | Unknown |
| --- | --- | --- | --- | --- |
| TD8 | Brak `XACT_ABORT` / `TRY…CATCH` | 0 wystąpień w literałach SQL `AppDbContext.cs` (ast-grep + grep); 16 batchy `begin tran … commit tran`; `ExecuteNonQuery` nie używa `SqlTransaction` (`AppDbContextBase.cs:89-104`) | Błąd na poziomie instrukcji nie przerywa batcha; `commit tran` może zatwierdzić częściowy zapis | Constrainty i triggery na produkcji |
| TD9 | Strażnik kasy bez blokady | `select @ostatniStanKasy` bez `UPDLOCK`/`HOLDLOCK` (`AppDbContext.cs:2129`); nowy stan zapisywany jako wartość absolutna (`:2240-2242`) | Wyścig dwóch operacji w placówce daje lost update na `stanKasy` | Poziom izolacji / RCSI bazy |
| TD10 | Status nie jest sprawdzany w SQL | `update … where umoId` bez `statusId = 3` (`AppDbContext.cs:2141`); odczyty poza transakcją (osobne połączenia). ast-grep: **żadne** z 11 `where umoId` w metodach `PozyczkaLombardowaAkcja*` nie ma warunku na status (`:1928`, `1941`, `1959`, `2024`, `2054`, `2141`, `2271`, `2515`, `2762`, `2916`, `3054`) | Podwójną spłatę blokuje tylko strażnik kasy (TD9). W 5 akcjach bez strażnika kasy (Usun, Podpisz, Wycofaj, Przenies, Przyjmij) przejście statusu chroni wyłącznie sprawdzenie w C# poza transakcją | — |
| TD11 | Strażnik skopiowany 7×, w 3 wariantach | `if @ostatniStanKasy` w `AppDbContext.cs:1951, 2046, 2130, 2262, 2506, 2751, 3302`. Wariant A `begin rollback tran; THROW 60000` w Wyplac, Rezygnuj i SplataCalkowita. Wariant B `begin rollback tran; select … errorCode` w SplataCzesciowa, Dopozyczenie i Sprzedaz; DAL czyta `errorCode` z wiersza (`AppDbContext.cs:2488`, `2735`, `2896`). Wariant C `THROW 60000` bez `rollback` w KasaOperacjaDodaj (`:3302`) | TD8–TD10 dotyczą wszystkich 7. Wspólna poprawka wymaga 3 różnych edycji, a nie jednego „search & replace” | Zachowanie wariantu C (otwarta transakcja po THROW) na produkcji |

### 2.3 Bezpieczeństwo

| # | Dług | Evidence | Inference | Unknown |
| --- | --- | --- | --- | --- |
| TD12 | SQL sklejany ze stringów | Wszystkie zapytania przepływu. `ExecuteNonQuery`/`GetDataSet` przyjmują `params SqlParameter[]` (`IAppDbContextBase.cs:11-12`), ale żadne ze 114 wywołań w `AppDbContext.cs` (85 `GetDataSet` + 29 `ExecuteNonQuery`) nie przekazuje parametru, a `new SqlParameter` występuje 0 razy. Batch zapisu skleja 28× `ToString("F2").Replace(",", ".")`, 4× `yyyy-MM-dd` i 2× `yyyy-MM-dd HH:mm:ss.fff` | Na tej ścieżce parametry są typowane; jedyny wektor to login (TD13) | — |
| TD13 | SQL injection w logowaniu, login w SQL bez escapowania | `where login = '" + login + "'` w 11 miejscach (`AppDbContext.cs:431`, `754`, `872`, `882`, `903`, `943`, `965`, `987`, `1050`, `1053`, `1169`); w batchu spłaty 5 loginów `ToUpper()` bez escapowania (`:2134`, `2136`, `2168`, `2232`, `2243`) i 1 escapowany opis (`:2166`). W całym pliku `Replace("'", "''")` występuje 11 razy. Hasło porównywane jawnym tekstem (`UzytkownikDAL.cs:26-27`) | Login z sesji dziedziczy podatność logowania | — |
| TD14 | Sekrety w repo | Hasło `admin` w komentarzu (`HomeController.cs:50`); connection string z kontem `sa` i hasłem (`eTMsWEB/Web.config:19`) | — | Czy te hasła są aktualne |
| TD15 | Autoryzacja tylko w DAL, kopiowana | 21 wywołań `UzytkownikUprawnienieLista` w DAL pożyczek, wszystkie w identycznym kształcie `.Where(x => new int[] { 5, 6 }.Contains(x.uprawnienieId))`. 0 atrybutów `[Authorize]` w całym `eTMsWEB`. ast-grep: publiczne metody DAL pożyczek **bez** sprawdzenia to odczyty, `ZastawTypDodaj`/`ZastawTypUsun` (`DAL:80`, `:98`) i **`KasaOperacjaDodaj` (`DAL:2715`)**, który zmienia stan kasy dla `placowkaId` z żądania. `AdminController.DBWykonaj` (`:340-364`) wykonuje dowolny SQL z żądania bez sprawdzenia | Każda nowa akcja musi pamiętać o ręcznym sprawdzeniu. Zalogowany użytkownik bez uprawnień 5/6 może [I] zaksięgować operację kasową w dowolnej placówce i wykonać dowolny SQL przez `DBWykonaj` | Czy menu/routing ogranicza dostęp poza kodem |

### 2.4 Struktura i testowalność

| # | Dług | Evidence | Inference |
| --- | --- | --- | --- |
| TD16 | Brak testów | 0 projektów testowych i frameworków (§3) | Żadna regresja TD1–TD15 nie zostanie wykryta |
| TD17 | Logika domenowa w DAL, BLL pominięty | Kontroler woła DAL (`Controller.cs:288`). Cały BLL to 3 wywołania `Wylicz` (`Controller.cs:158`, `166`, `180`): podgląd, utworzenie i zmiana metryki. 52 wywołania idą bezpośrednio do `unitOfWorkDAL.PozyczkiLombardowe` | Zmiana reguł wymaga edycji warstwy danych |
| TD18 | UoW obchodzi DI | `new AppDbContext()` w 2 miejscach (`UnitOfWorkDAL.cs:27`, `CheckSessionAttribute.cs:22`), `new PozyczkaLombardowaBLL()` (`UnitOfWorkBLL.cs:27`), `new ETMSCoreBLL()` 94× w 5 kontrolerach (44× w `PozyczkaLombardowaController`) | Podmiana zależności w teście wymaga obejścia UoW |
| TD19 | Mapowanie refleksją po nazwach | `SetItemFromRow` pomija brakujące właściwości bez błędu (`AppDbContextBase.cs:52-66`); AutoMapper po konwencji: 10 `CreateMap` (`AutoMapperDAL.cs:11-20`), z czego 2 dotyczą pożyczek (`:19-20`); `AssertConfigurationIsValid` 0× | Rename kolumny lub właściwości psuje dane po cichu (np. `stanKasy = 0`, wtedy strażnik rzuca zawsze) |
| TD20 | Zasoby bez `using` | 3× `new SqlConnection` (`AppDbContextBase.cs:73`, `93`, `112`), 0 w `using`, 0 `try-finally`; `Dispose()` tylko na ścieżce sukcesu | Wyciek połączenia przy wyjątku w `GetDataSet`/`ExecuteNonQuery` |
| TD21 | `DateTime.Now` na sztywno | W akcji: `DAL:1226`, `:1308`. W całym DAL pożyczek: 36 wystąpień | Testy dat zależne od zegara |
| TD22 | Diagnostyka | `ExceptionToMessage` (`eTMsDAL/REPOSITORY/RepoBase.cs:12-21`); `QueryLog` połyka błędy (`AppDbContextBase.cs:106-127`) | Utrudnione śledzenie błędów na produkcji |

## 3. Luki w testach

**Stan [E].** Szukano wszędzie poza `packages/`, `.claude/` i `context/`. Nie znaleziono niczego z poniższych:
- projektu testowego: `eTMsLomb.sln:6-14` ma 5 projektów (eTMsDB, eTMsDAL, eTMsBLL, eTMsWEB, eTMsDATABASE);
- plików `*test*`/`*.spec.js`;
- konfiguracji karma/jest, `package.json`, CI (`*.yml`, Jenkinsfile, azure-pipelines);
- referencji do MSTest/NUnit/xUnit/Moq/tSQLt/Jasmine w `*.csproj`, `packages.config`, `*.sqlproj`;
- ścieżki z „test” lub „spec” w historii git (4 commity).

`packages/` zawiera tylko Autofac, AutoMapper, MVC, jQuery, Newtonsoft i podobne. **Pokrycie każdej gałęzi poniżej: brak.**

**[U]** Testy ręczne lub UAT mogą istnieć poza repo.

| Gałąź | Kotwica | Ryzyko | Rodzaj testu, który by ją pokrył |
| --- | --- | --- | --- |
| Suma kwot zgodna, ale odsetki zaniżone/zerowe/ujemne | `DAL:1262` | krytyczne | unit DAL z mockiem `IAppDbContext` |
| Status ≠ 3 | `DAL:1242` | wysokie | unit + integracja współbieżna |
| Brak uprawnień 5/6 w placówce umowy | `DAL:1248-1254` | wysokie | unit |
| Odsetki > 0 / = 0 (rozliczenie 8, VAT) | `DAL:1349` | wysokie | unit |
| Zaległe > 0 / = 0 (rozliczenie 9) | `DAL:1369` | wysokie | unit |
| Zastawy: 0 / 1 / 2 / ≥3 | `DAL:1395-1418` | wysokie (≥3: TD2), średnie (0: TD4) | unit |
| Udziały ≠ 100% (reszta na ostatni) | `DAL:1403-1416` | średnie | unit |
| Data z przyszłości / < data pożyczki / < data zmiany kasy | `DAL:1226`, `:1256`, `:1298` | średnie | unit (wymaga wstrzykniętego zegara, TD21) |
| Brak słownika 7/8/9, brak kasy | `DAL:1269-1296` | niskie / średnie | unit |
| catch w DAL | `DAL:1424-1428` | niskie | unit |
| Strażnik `@ostatniStanKasy` → THROW | `AppDbContext.cs:2129-2130` | krytyczne | integracja SQL, dwie sesje |
| Błąd w środku batcha (częściowy commit) | `AppDbContext.cs:2127-2245` | krytyczne | integracja SQL z wymuszonym błędem |
| `rozliczenie8/9 == null` (brak INSERT) | `AppDbContext.cs:2187`, `:2207` | wysokie | integracja SQL |
| BLL: próg minimalnych odsetek | `PozyczkaLombardowaBLL.cs:53-55` | wysokie | unit (BLL czysty, testowalny od razu) |
| BLL: RRSO dla 0 dni / zwrot = pożyczka | `BLL.cs:93-103` | wysokie (TD6) | unit |
| BLL: błąd RRSO, catch | `BLL.cs:57-71`, `:106-111` | średnie / niskie | unit |
| JS: walidacja dat, `code != 0`, błąd ajax, flaga `wykonaj` | `js:665-745` | niskie / średnie | unit JS z mockami `$`/modal albo E2E |
| JS: podwójne kliknięcie WYKONAJ | `js:709-745` (brak blokady przycisku w trakcie żądania) | średnie [I] | E2E |
| Kontroler: binding liczb z przecinkiem (`parseNumer`) i dat `YYYY-MM-DD` | `Controller.cs:282-292`, `Scripts/skrypty.js:97` | [U] zależne od kultury | integracja MVC |

**Przeszkody w testowaniu [E].**
- `DateTime.Now` (TD21);
- `Session` i `new ETMSCoreBLL()` w kontrolerze;
- `new AppDbContext()` w UoW (TD18);
- SQL jako string, który da się sprawdzić tylko na prawdziwym SQL Serverze;
- logika w kontrolerze AngularJS z globalnym `$.ajax`.

**Ułatwienia [E].** `PozyczkaLombardowaDAL` przyjmuje `IAppDbContext` w konstruktorze (`DAL:14-19`), więc daje się mockować bezpośrednio, z pominięciem UoW. BLL nie ma zależności.

## 4. Blast radius

### 4.1 Co zmienia się razem

| Artefakt | Dlaczego sprzężony | Evidence | Mechaniczne / ręczne |
| --- | --- | --- | --- |
| JS POST ↔ parametry akcji | MVC model binding po nazwie pola | `js:724-737` ↔ `Controller.cs:284-286` | ręczne: literówka daje wartość domyślną albo 500, bez błędu kompilacji |
| JS Wylicz ↔ `PozyczkaLombardowaItem` ↔ BLL | binding pól na właściwości encji | `js:681-692`, `Controller.cs:155-158`, `IPozyczkaLombardowaBLL.cs:14` | ręczne |
| Widok ↔ JS | id modala, pola `splataCalkowita.*`, warunek `ng-show` | `.cshtml:12`, `:349-449`; `js:663`, `:740` | ręczne |
| Kontroler ↔ `IPozyczkaLombardowaDAL` ↔ DAL | ta sama sygnatura, 1 implementacja, 1 wywołujący | `Controller.cs:284-292`, `IPozyczkaLombardowaDAL.cs:86-88`, `DAL:1220` | mechaniczne (kompilator) |
| `IAppDbContext` ↔ `AppDbContext` | 8 parametrów (7 encji/kolekcji + `decimal ostatniStanKasy`), 1 implementacja (ast-grep) | `IAppDbContext.cs:39`, `AppDbContext.cs:2122`, `AutofacModuleDB.cs:12` | mechaniczne |
| String SQL ↔ DDL SSDT | nazwy kolumn i formaty F2/`yyyy-MM-dd` ↔ typy kolumn | `AppDbContext.cs:2126-2246` ↔ `eTMsDATABASE/dbo/Tables/TB_POZYCZKI_LOMBARDOWE*.sql`, `SL_POZYCZKI_LOMBARDOWE_KASA_ROZLICZENIA.sql` | ręczne, build tego nie wiąże |
| Encje POCO ↔ kolumny | refleksja po nazwie, z rozróżnieniem wielkości liter | `AppDbContextBase.cs:52-66`; encje w `eTMsDB/ENTITIES/` | ręczne, psuje się po cichu (TD19) |
| Zduplikowane encje | te same kolumny w `PozyczkaLombardowa`/`…Item`/`…ListItem`, `…Zastaw`/`…ZastawItem`/`…OperacjaSprzedazyZastawItem` (bez dziedziczenia) | `eTMsDB/ENTITIES/*` | ręczne |
| AutoMapper | mapowanie konwencyjne `PozyczkaLombardowa → Item`, `Item → Item` | `AutoMapperDAL.cs:19-20` | konwencyjne, ciche |
| Magiczne liczby ↔ seed | statusy 3→5, akcja 7, rozliczenia 7/8/9, uprawnienia 5/6 | `DAL:1242`, `:1248`, `:1269-1283`, `:1307`; `Initial.sql:458`, `:490-492`, `:498-499` | ręczne; seed nie jest PostDeploy, więc zmiana słownika wymaga ręcznego DML |
| 6 przepływów-sióstr | ten sam strażnik w 3 wariantach (TD11) i ta sama transakcja tekstowa | `AppDbContext.cs:1951` (Wyplac, A), `2046` (Rezygnuj, A), `2262` (SplataCzesciowa, B), `2506` (Dopozyczenie, B), `2751` (Sprzedaz, B), `3302` (KasaOperacjaDodaj, C) | ręczne, 7 równoległych edycji w 3 kształtach [I] |
| Współdzielone odczyty | `KasaOdczytaj`: 9 w DAL (7 przepływów ze strażnikiem + `AkcjaPodpisz` `:943` + endpoint `KasaOdczytaj` `:2703`). `ZastawLista`: 9 w DAL. `KasaRozliczenieOdczytaj`: 10 w 5 metodach. `UzytkownikUprawnienieLista`: 21 w DAL pożyczek + 1 w `PlacowkaDAL.cs:163` + 3 w `UzytkownikUprawnienieDAL.cs:27-61`; `AdminController` korzysta pośrednio przez `UzytkownicyUprawnienia.Lista` (`AdminController.cs:112`). `PozyczkaLombardowaOdczytaj`: 25, ale w 3 przeciążeniach (22× `(umoId)`, 2× `(numer, aneks, placówka)`, 1× `(numer, null)`) | ast-grep `$X.<Metoda>($$$)` po `eTMsDAL eTMsWEB eTMsBLL eTMsDB` (§7) | ręczne |
| Raporty-czytelnicy | kasa, sprzedaż zastawów i CSV, remanent (status 3), książka (`dataSplaty`), lista (`wplatyKwota*`) | §1.4 | ręczne |
| Testy | brak | §3 | — |

### 4.2 Warstwy generowane, migracje

- **[E]** Brak generowanego kodu w repo: `.tt`, `.edmx`, `*.Designer.cs`, Service References, `BundleConfig`. JS ładowany jest bezpośrednim `<script src=…?pvs=…>` (`Views/PozyczkaLombardowa/PozyczkaLombardowaIndex.cshtml:10`). Jedyną konwencyjną „generacją” jest AutoMapper.
- **[E]** Schemat: projekt SSDT `eTMsDATABASE.sqlproj` z 28 tabelami i skryptami `scripts/CreateDB.sql` + `Initial.sql` (oba jako `None`). Nie ma PreDeploy/PostDeploy, `*.publish.xml`, refactorlog ani EF Migrations. Jedyny profil publikacji, `LOKAL.pubxml`, publikuje samą aplikację WEB do folderu.
- **[I]** Nazwy PK `PK_dbo.X` sugerują, że schemat kiedyś wygenerowało EF Code First.
- **[U]** Ścieżka wdrożenia zmian schematu na produkcję i zgodność schematu produkcyjnego z SSDT.

### 4.3 Co-change z historii git

- **[E]** Każdy plik przepływu (JS, widok, kontroler, I-DAL, DAL, BLL, IAppDbContext, DDL) ma 1 commit: import 42292b7. `AppDbContext.cs` ma 2 (42292b7 + 375c6fd).
- **[E]** Commit 375c6fd zmienił 6 plików. Jedynym plikiem źródłowym jest `AppDbContext.cs` (+110/−110, prefiks `set dateformat ymd`), reszta to stan IDE. `git blame -L 2122,2247` daje 125 linii z importu i 1 linię (2127) z 375c6fd.
- **[I]** Historia nie zawiera żadnej merytorycznej pary plików zmienianych razem, więc sprzężenie wynika wyłącznie z grafu statycznego (§4.1). Precedens 375c6fd pokazuje, że zmiany „formatu SQL” idą masowo przez całe `AppDbContext`, a nie per przepływ.

## 5. Korekty i weryfikacja wyników sub-agentów

- **Netto zastawów.** Agent od trace e2e napisał „przy >1 zastawie”, a agent od blast radius „przy >2”. **Poprawnie: ≥3 zastawy.** Dla 2 zastawów pętla liczy tylko `i = 0`, gdzie suma równa się własnemu brutto (`DAL:1396-1407`).
- **`PozyczkaLombardowaOdczytaj`.** Agent od blast radius twierdził, że ma jawną listę kolumn. **Nieprawda:** zapytanie to `select pl.*` (`AppDbContext.cs:1425-1435`), więc nowa kolumna trafi do encji sama.
- **D5 z analizy testów** („spłata tego samego dnia odrzucona, jeśli `dataPozyczki` ma godzinę”). **Obalone dla danych zapisanych przez aplikację:** `dataPozyczki` jest zapisywana jako `yyyy-MM-dd` (`AppDbContext.cs:1540`, `:1577`, `:3129`). [U] dla danych z importu.
- **RRSO dla 0 dni.** Potwierdzone w kodzie (`BLL.cs:93-103`).
- **Uzupełnienie wersji 1.** Otwarte pytanie 3 („czy umowa bez zastawów może mieć status 3”) jest rozstrzygnięte jako **tak** [E]. `AkcjaPodpisz` wymaga ≥1 zastawu (`DAL:962`), ale `ZastawUsun` dopuszcza status 3 i usuwa fizycznie (`DAL:831`).
- **Potwierdzone punktowo:**
  - 7 strażników `@ostatniStanKasy`;
  - `pl.statusId = 3` w remanencie (`:3640`);
  - mapowania AutoMapper;
  - wynik blame;
  - filtry globalne;
  - konto `sa` w `Web.config:19` (hasło sprawdzone bez wypisywania).

## 6. Szczegóły U1–U5 (wersja 1, nadal aktualne)

### 6.1 U1 — transakcyjność

- **Obserwacja.** Zapisy idą jednym batchem `set dateformat ymd; begin tran … commit tran` (`AppDbContext.cs:2127`, `2245`). W tym samym batchu są:
  - `update TB_POZYCZKI_LOMBARDOWE`,
  - `insert TB_POZYCZKI_LOMBARDOWE_KASA_OPERACJE`,
  - 1–3 inserty `…_KASA_OPERACJE_ROZLICZENIA`,
  - `update` każdego zastawu (pętla `foreach`, `AppDbContext.cs:2227`),
  - `update TB_POZYCZKI_LOMBARDOWE_KASA` (`AppDbContext.cs:2240`).

  Odpowiedź na pytanie z mapy: **tak**, spłata i operacja kasowa są w tej samej transakcji tekstowej.
- **Obserwacja.** W `AppDbContext.cs` nie występują `XACT_ABORT` ani `BEGIN TRY`: grep, 0 wystąpień w całym pliku.
- **Inference (semantyka SQL Server, niezweryfikowana na bazie).** Bez `XACT_ABORT ON` wiele błędów na poziomie instrukcji nie przerywa transakcji, np. naruszenie constraintu czy konwersji. Kolejne instrukcje się wykonują i `commit tran` zatwierdza resztę. `SqlClient` rzuci wtedy wyjątek, DAL zwróci `code = 1` (`PozyczkaLombardowaDAL.cs:1424-1428`), a część zmian może już być zapisana. Gwarancja „wszystko albo nic” jest więc tylko częściowa.
- **Strażnik stanu kasy.** Batch czyta `stanKasy` i porównuje go z wartością odczytaną wcześniej w C#. Przy różnicy robi `rollback` i `THROW 60000` (`AppDbContext.cs:2129-2130`). Odczyt nie ma `UPDLOCK`/`HOLDLOCK`: grep na lock hinty w pliku, 0 wyników.
- **Inference.** Przy domyślnym READ COMMITTED dwie równoległe operacje w tej samej placówce mogą przeczytać ten sam stan, zanim którakolwiek zaktualizuje wiersz kasy. Obie przejdą wtedy strażnika. Druga nadpisze `stanKasy` swoją wartością absolutną (lost update), a obie operacje kasowe zostaną zapisane.
- **Status umowy w SQL.** `update` umowy ma `where umoId = …` bez warunku `statusId = 3` (`AppDbContext.cs:2141`). Status jest sprawdzany tylko w C# przed batchem (`PozyczkaLombardowaDAL.cs:1242`). Podwójną spłatę tej samej umowy blokuje więc tylko strażnik kasy, ze słabością opisaną wyżej.
- **Poboczne.** `QueryLog` (`AppDbContextBase.cs:106-127`) loguje zapytanie osobnym połączeniem i połyka błędy. Nie wpływa na transakcję.

### 6.2 U2 — autoryzacja

- **Obserwacja.** `UzytkownikUprawnienieLista(wykonal, pozyczka.placowkaId)` jest filtrowane do `uprawnienieId ∈ {5, 6}`. Pusta lista daje „Nie masz odpowiednich uprawnień!” (`PozyczkaLombardowaDAL.cs:1248-1254`).
- **Słownik.** 5 = LOMBARDZISTA, 6 = KONTROLER, 4 = WIZYTOR (`eTMsDATABASE/scripts/Initial.sql:497-499`). WIZYTOR nie może spłacić umowy.
- **Placówka.** Sprawdzana jest placówka *umowy* (`pozyczka.placowkaId`), a nie placówka wybrana w sesji. Zapytanie: `AppDbContext.cs:932-950`. Użytkownik bez uprawnienia 5/6 w placówce umowy nie spłaci jej po samym `umoId`.
- **Werdykt dla Watch 3 mapy.** Teza „uprawnienia tylko w menu” jest **obalona dla tej akcji**. Mapa nie znalazła tego sprawdzenia, bo leży w DAL, a nie w kontrolerze. **Po ast-grep (§7, S37, S39):** teza jest **potwierdzona** dla `KasaOperacjaDodaj` i `AdminController.DBWykonaj`, które w kodzie nie sprawdzają uprawnień. Dla 21 metod DAL pożyczek z `UzytkownikUprawnienieLista` jest obalona.
- **Ograniczenie autoryzacji (obserwacja).** Uprawniony użytkownik decyduje o kwocie odsetek. Serwer sprawdza wyłącznie spójność sumy (`PozyczkaLombardowaDAL.cs:1262`). Nie wywołuje `Wylicz` i nie porównuje `kwotaOdsetekZaleglych` z wartością zapisaną na umowie (`PozyczkaLombardowaItem.cs:41`). Każdy z 6 parametrów jest przesyłany z przeglądarki, więc żądanie spoza UI z `kwotaOdsetek = 0, kwotaOdsetekZaleglych = 0, kwotaZwrotu = kwotaPozyczki` przejdzie walidację. Wniosek wyprowadzony z kodu, niewykonany na działającej aplikacji.

### 6.3 U3 — SQL injection

- **Obserwacja.** W zapytaniu zapisu (`AppDbContext.cs:2122-2245`) doklejane są:
  - liczby `int` (`umoId`, `placowkaId`, `statusId`, identyfikatory rozliczeń);
  - `decimal` przez `ToString("F2").Replace(",", ".")`;
  - daty przez `ToString("yyyy-MM-dd")` / `"yyyy-MM-dd HH:mm:ss.fff"`.

  Te typy nie mogą przenieść składni SQL. Model binding MVC odrzuci tekst, którego nie da się sparsować.
- **Obserwacja.** Stringi:
  - `opisOperacji` to stała „SPŁATA CAŁKOWITA” z escapowaniem `'` (`AppDbContext.cs`, insert operacji);
  - `zmienial`/`splacal`/`dodal`/`wykonal` to login z sesji, doklejany przez `ToUpper()` **bez** escapowania (np. `AppDbContext.cs:2134`, `2243`).

  To samo dotyczy zapytania o uprawnienia (`AppDbContext.cs:943`, `where login = '" + login + "'`).
- **Pochodzenie loginu.** `Session["zalogowanyLogin"] = User.Identity.Name.ToUpper()` (`eTMsWEB/Filters/CheckSessionAttribute.cs:47`). `Identity.Name` to login wpisany w formularzu (`HomeController.cs:66`, `SetAuthCookie(login, …)`). Logowanie czyta użytkownika zapytaniem `where login = '" + login + "'` (`AppDbContext.cs:754`) i porównuje hasło jawnym tekstem w C# (`eTMsDAL/REPOSITORY/UzytkownikDAL.cs:26-27`).
- **Inference.** Sam przepływ spłaty nie wprowadza nowej powierzchni injection. Odziedziczył ją jednak po logowaniu, które jest podatne. Jeśli login z apostrofem przejdzie uwierzytelnienie, każde zapytanie tej ścieżki z loginem będzie miało złamaną składnię. Klasa problemu: SQL injection w logowaniu. Szczegółów eksploatacji celowo nie opisuję.
- **Poboczne (sekret).** W komentarzu w `HomeController.cs:50` jest jawne hasło konta `admin`.
- **Poboczne (kultura).** W formacie `HH:mm:ss` znak `:` jest separatorem czasu zależnym od kultury .NET. Dla `pl-PL` to `:`, więc ryzyko jest niskie, ale format nie używa `CultureInfo.InvariantCulture`.

### 6.4 U4 — `set dateformat ymd`

- **Obserwacja.** Ścieżka wykonuje 6 zapytań:
  1. `PozyczkaLombardowaOdczytaj(int)` — `AppDbContext.cs:1430`;
  2. `UzytkownikUprawnienieLista(string,int)` — `AppDbContext.cs:942`;
  3. `PozyczkaLombardowaKasaRozliczenieOdczytaj` — `AppDbContext.cs:3251`, ×3 wywołania;
  4. `PozyczkaLombardowaKasaOdczytaj` — `AppDbContext.cs:3267`;
  5. `PozyczkaLombardowaZastawLista` — `AppDbContext.cs:1737`;
  6. zapis — `AppDbContext.cs:2127`.

  Każde zaczyna się od `set dateformat ymd`. Linie każdego z nich są hunkami commita 375c6fd (110 hunków w `AppDbContext.cs`, 110 dodanych linii z `set dateformat ymd`). ast-grep potwierdza 110 literałów z `set dateformat ymd`: 95 zwykłych `string_literal` i 15 verbatim/interpolowanych. Fizycznie to 8 round-tripów (7 SELECT + 1 batch), bo zapytanie 3 idzie ×3.
- **Werdykt.** Poprawka obejmuje wszystkie 6 zapytań tego przepływu. Obejmuje też zapytanie logowania (`AppDbContext.cs:754`, hunk `-754` w 375c6fd), które leży poza przepływem, ale dostarcza mu login.

### 6.5 U5 — parametry

- **Obserwacja.** `PozyczkaLombardowaBLL.cs` (139 linii) i `PozyczkaLombardowaDAL.cs` nie odwołują się do `Parametry`/`ParametrDAL`. Jedyne użycie `unitOfWorkDAL.Parametry` w kontrolerze pożyczek to `ALLEGRO_SZUKAJ_LINK` (`PozyczkaLombardowaController.cs:97`).
- **Źródła stawek.** `procentNaDzien` i `minProcentZwrotu` pochodzą z rekordu umowy załadowanego w przeglądarce (`PozyczkaLombardowa.js:660`, `676-677`). Podczas spłaty klient może je zmienić. Nowy `minProcentZwrotu` zostaje zapisany na umowie (`PozyczkaLombardowaDAL.cs`, `pozyczka.minProcentZwrotu = minProcentZwrotu`; `AppDbContext.cs`, `,minProcentZwrotu = …`).
- **Werdykt.** Teza mapy (inference), że parametry zasilają wyliczenia kwot, jest **obalona dla tego przepływu**. Ten research nie sprawdzał, czy `TB_PARAMETRY` zasila stawki przy *tworzeniu* umowy.

### 6.6 Dodatkowe defekty w logice DAL (obserwacja + inference)

- **Netto zastawów.** W pętli po zastawach (bez ostatniego) netto liczone jest od sumy narastającej zamiast od brutto bieżącego zastawu: `zastaw.kwotaZbyciaNetto = sumKwotaZbyciaBrutto - zastaw.kwotaZbyciaVat` (`PozyczkaLombardowaDAL.cs:1407`).
  - Dla zastawu `i = 0` suma równa się jego brutto, więc wynik jest poprawny.
  - Dla umów z ≥3 zastawami zastawy `i = 1 … n-2` dostają zawyżone netto.
  - Ostatni zastaw liczony jest poprawnie (`PozyczkaLombardowaDAL.cs:1410-1418`).
- **Pusta lista zastawów.** `zastawy[zastawy.Count - 1]` (`PozyczkaLombardowaDAL.cs:1410`) dla pustej listy rzuci wyjątek. Zostanie on złapany i nic nie zostanie zapisane, ale użytkownik dostanie techniczny komunikat. UI pokazuje przycisk także przy 0 zastawów (warunek `0 == 0`). **Zaktualizowane w follow-upie:** umowa bez zastawów może mieć status 3 (§5, TD4).
- **Zastaw już sprzedany.**
  - Sprzedaż pojedynczego zastawu pozostawia umowę w statusie 3, jeśli zostały inne zastawy na stanie (`PozyczkaLombardowaDAL.cs:1927`, `2168-2169`).
  - Spłata całkowita nie filtruje zastawów z `dataZbycia != null` i nadpisuje dla wszystkich `operacjaId`, `dataZbycia` i kwoty zbycia (`PozyczkaLombardowaDAL.cs:1395-1418`, `AppDbContext.cs:2227-2238`).
  - Ustawia też `wplatyKwotaPozyczki = kwotaPozyczki` niezależnie od wcześniejszych wpłat.
  - Blokuje to tylko UI (`zastawyIloscNaStanie == zastawy.length`).
  - **Inference:** żądanie spoza UI dla takiej umowy nadpisze dane sprzedaży i może podwójnie rozliczyć część kwoty pożyczki.
- **Słownik rozliczenia 7.** Dla rozliczenia 7 `wplywNaStan` jest wpisany na sztywno jako `1` (`PozyczkaLombardowaDAL.cs:1340`), mimo że słownik jest odczytywany (`1269`). Dla 8 i 9 brany jest ze słownika. Zmiana słownika dla 7 nie będzie miała efektu. Po ast-grep: ten sam literał jest w 5 przepływach (TD7).

## 7. Weryfikacja strukturalna (ast-grep)

**Metoda.** Używam `ast-grep` 0.50.0: `run -p` dla wzorców wywołań, a `scan --inline-rules` dla reguł z `kind`/`has`/`inside`/`regex`, np. dla literałów SQL, atrybutów, parametrów i metod bez danego wywołania. Zakres to `eTMsDAL eTMsWEB eTMsBLL eTMsDB` (bez `packages/`). Do każdego trafienia skrypt pomocniczy dopisuje metodę otaczającą (`method_declaration`/`constructor_declaration`). Każde zero (oraz każda rozbieżność liczby) jest sprawdzone grepem.

**Werdykty.** ✅ potwierdzone · 🔶 doprecyzowane · ❌ obalone.

### 7.1 Call-site'y i liczności

| # | Twierdzenie z raportu | Wzorzec ast-grep | Wynik | Werdykt |
| --- | --- | --- | --- | --- |
| S1 | Akcję woła 1 miejsce w JS | `kind: string` + `regex: SplataCalkowita` (js) | 1 URL: `PozyczkaLombardowa.js:722`; pozostałe 8 trafień to id modala i komunikaty | ✅ |
| S2 | Kontroler → DAL: 1 wywołujący | `$X.AkcjaSplataCalkowita($$$)` | `PozyczkaLombardowaController.cs:288` | ✅ |
| S3 | DAL → `AppDbContext`: 1 wywołujący | `$X.PozyczkaLombardowaAkcjaSplataCalkowita($$$)` | `PozyczkaLombardowaDAL.cs:1420` | ✅ |
| S4 | `IPozyczkaLombardowaDAL`, `IAppDbContext`: po 1 implementacji | `class_declaration` z `base_list` ∋ interfejs | `PozyczkaLombardowaDAL.cs:12`, `AppDbContext.cs:6` (oraz `PozyczkaLombardowaBLL.cs:10` dla `IPozyczkaLombardowaBLL`) | ✅ |
| S5 | 7 metod ze strażnikiem `@ostatniStanKasy`, „ten sam wzorzec” | `string_literal` + `regex: 'if @ostatniStanKasy'` | 7: `AppDbContext.cs:1951` Wyplac, `2046` Rezygnuj, `2130` SplataCalkowita, `2262` SplataCzesciowa, `2506` Dopozyczenie, `2751` Sprzedaz, `3302` KasaOperacjaDodaj. Ogon literału ma **3 kształty**: A `rollback; THROW` (1951, 2046, 2130), B `rollback; select errorCode` (2262, 2506, 2751), C `THROW` bez `rollback` (3302) | 🔶 liczba ✅, „ten sam wzorzec” ❌ |
| S6 | `PozyczkaLombardowaKasaOdczytaj`: 9 wywołujących w DAL | `$X.PozyczkaLombardowaKasaOdczytaj($$$)` | 9 w `PozyczkaLombardowaDAL.cs`: 943 AkcjaPodpisz, 1020 AkcjaWyplac, 1145 AkcjaRezygnuj, 1291 AkcjaSplataCalkowita, 1485 AkcjaSplataCzesciowa, 1714 AkcjaDopozyczenie, 1997 AkcjaSprzedaz, 2703 KasaOdczytaj, 2762 KasaOperacjaDodaj; poza DAL 0 | ✅ |
| S7 | `PozyczkaLombardowaZastawLista`: 9 w DAL | `$X.PozyczkaLombardowaZastawLista($$$)` | 9: `DAL:293`, `444`, `736`, `962`, `1196`, `1395`, `1537`, `1773`, `1947` | ✅ |
| S8 | `PozyczkaLombardowaKasaRozliczenieOdczytaj`: 10 | `$X.PozyczkaLombardowaKasaRozliczenieOdczytaj($$$)` | 10 w 5 metodach: Wyplac 1012; Rezygnuj 1137; SplataCalkowita 1269/1276/1283; Sprzedaz 1968/1975/1982/1989; KasaOperacjaDodaj 2796 | ✅ |
| S9 | `UzytkownikUprawnienieLista`: 21 w DAL pożyczek + PlacowkaDAL, UzytkownikUprawnienieDAL, AdminController | `$X.UzytkownikUprawnienieLista($$$)` | 25 bezpośrednich: 21 w DAL pożyczek, 1 w `PlacowkaDAL.cs:163`, 3 w `UzytkownikUprawnienieDAL.cs:27`, `44`, `61`. `AdminController` nie woła jej bezpośrednio (0), tylko przez `UzytkownicyUprawnienia.Lista` (`AdminController.cs:112`) | 🔶 |
| S10 | `PozyczkaLombardowaOdczytaj`: 25 | `$X.PozyczkaLombardowaOdczytaj($$$)` | 25, ale to 3 przeciążenia: 22× `(umoId)`, 2× `(numer, aneks, placowkaId)` (`DAL:345`, `392`), 1× `(numer, null)` (`DAL:404`) | 🔶 |
| S11 | `IAppDbContext.PozyczkaLombardowaAkcjaSplataCalkowita`: „8 parametrów encji” | `kind: parameter` wewnątrz `method_declaration` o tej nazwie | 8 w interfejsie i 8 w implementacji: 7 encji/kolekcji + `decimal ostatniStanKasy` | 🔶 |
| S12 | Kontroler woła DAL z pominięciem BLL | `unitOfWorkDAL.PozyczkiLombardowe.$M($$$)` | 52 wywołania DAL w kontrolerze pożyczek, w tym akcja spłaty | ✅ |
| S13 | „BLL tylko liczy podgląd” | `unitOfWorkBLL.$P.$M($$$)` | 3 wywołania, wszystkie `Wylicz`: `Controller.cs:158` (podgląd), `166` (Dodaj), `180` (MetrykaZmien); w Dodaj i MetrykaZmien wynik **nadpisuje** kwoty klienta (`:168-170`, `:182-184`) | 🔶 wzmacnia TD1 |
| S14 | UoW tworzy zależności ręcznie | `new AppDbContext($$$)`, `new PozyczkaLombardowaDAL($$$)`, `new PozyczkaLombardowaBLL($$$)`, `new ETMSCoreBLL($$$)` | `UnitOfWorkDAL.cs:27`, `:99`; `UnitOfWorkBLL.cs:27`; **dodatkowo** `CheckSessionAttribute.cs:22`; `ETMSCoreBLL` 94× w 5 kontrolerach (44× w pożyczkach) | 🔶 |
| S15 | `UzytkownikUprawnienieLista` w DAL pożyczek ma powtarzalny kształt filtra 5/6 | `appDbContext.UzytkownikUprawnienieLista($$$A).Where(x => new int[] { $$$S }.Contains(x.uprawnienieId))` | 21/21 trafień, wszystkie `{5, 6}` | ✅ |
| S16 | `DateTime.Now` w akcji: `DAL:1226`, `:1308` | `DateTime.Now` + metoda otaczająca | W `AkcjaSplataCalkowita` dokładnie te 2; w całym DAL pożyczek 36 | ✅ |
| S17 | Catch w DAL zawsze przez `ExceptionToMessage` | `catch_clause` z `has: identifier ExceptionToMessage` vs. wszystkie `catch_clause` | 46/46 w `PozyczkaLombardowaDAL.cs` | ✅ |
| S18 | `PozyczkaLombardowaDAL.Odczytaj` wykonuje „10 zapytań” | `appDbContext.$M($$$)` w zakresie `DAL:262-337` | 12 call-site'ów: 10 bezwarunkowych + 2 warunkowe `PlacowkaOdczytaj` (`:305`, `:317`) | 🔶 10–12 |
| S19 | Ścieżka wykonuje 6 zapytań (U4) | `appDbContext.$M($$$)` w zakresie `DAL:1220-1431` | 8 wywołań: 7 odczytów (6 różnych, `KasaRozliczenieOdczytaj` ×3) + 1 zapis | 🔶 6 różnych / 8 round-tripów |

### 7.2 SQL w literałach

| # | Twierdzenie | Wzorzec | Wynik | Werdykt |
| --- | --- | --- | --- | --- |
| S20 | `update` umowy bez warunku `statusId` (`AppDbContext.cs:2141`) | literał z `regex: where +umoId` wewnątrz `method_declaration` `^PozyczkaLombardowaAkcja`; drugi z `regex: (and\|where) +(pl[.])?statusId` | 11 trafień `where umoId` (1928, 1941, 1959, 2024, 2054, 2141, 2271, 2515, 2762, 2916, 3054); **0** z warunkiem na status. Grep `(and\|where) +statusId` w zakresie 1900–3250: 0 | ✅ rozszerzone na 11/11 |
| S21 | 0× `XACT_ABORT`, 0× `BEGIN TRY` | literał z `regex: (?i)xact_abort` / `(?i)begin try` | 0 / 0; grep: 0 | ✅ |
| S22 | 0 lock hintów | literał z `regex: (?i)(updlock\|holdlock\|rowlock\|tablockx\|serializable)` | 0; grep: 0 | ✅ |
| S23 | 110× `set dateformat ymd` | literał z `regex: (?i)set dateformat ymd` | `string_literal`: 95; po dodaniu `verbatim_string_literal` i `interpolated_string_expression`: 110 = grep 110 | ✅ (zakres kindów doprecyzowany) |
| S24 | `begin tran` / `commit tran` | jw. | 16 / 16 | ✅ (nowa liczba) |
| S25 | Wywołania bez `SqlParameter` | `new SqlParameter($$$)`; `GetDataSet($A)` vs `GetDataSet($A, $$$B)`; to samo dla `ExecuteNonQuery` | `new SqlParameter`: 0 (grep: tylko sygnatury `params SqlParameter[]` w `IAppDbContextBase.cs:11-12`, `AppDbContextBase.cs:69`, `:89`). 85× `GetDataSet(q)`, 29× `ExecuteNonQuery(q)`, 0 wywołań z parametrami | 🔶 API wspiera parametry, nikt ich nie używa |
| S26 | Login doklejany bez escapowania (`:754`, `:943`, `:2134`) | `binary_expression` z `identifier ^login$` (`stopBy: end`) i literałem `login *= *'` | ast-grep: 8 (431, 754, 903, 943, 965, 987, 1050, 1053). Grep dodaje 3, których reguła nie łapie: 872 i 882 to literały verbatim, 1169 używa identyfikatora `ktoPyta`. Razem 11. W batchu spłaty `$X.ToUpper()` ×6, z tego 1 escapowany (`:2166`) | 🔶 |
| S27 | Remanent bierze tylko `statusId = 3` (`:3640`) | literał z `regex: statusId *(=\|in)` + metoda | `PozyczkaLombardowaRemanentAnalitykaLista`: jedyny filtr statusu `:3640` | ✅ |
| S28 | Sprzedaż zastawów filtruje `dataZbycia is not null` (`:3670`) | literał z `regex: (?i)dataZbycia is (not )?null` | 2 przeciążenia `…OperacjaSprzedazyZastawuLista` (`:3670`, `:3761`), filtr w `:3721` i `:3813` | 🔶 |
| S29 | `PozyczkaLombardowaOdczytaj` to `select pl.*` | najpierw `regex: "select +pl[.][*]"` → **0**; grep pokazał rozbicie na 2 literały (`:1430` `select`, `:1431` `pl.*`); poprawiony `regex: "pl[.][*]"` | `AppDbContext.cs:1431` | ✅ (zero było błędem wzorca) |

### 7.3 Atrybuty, DI, infrastruktura

| # | Twierdzenie | Wzorzec | Wynik | Werdykt |
| --- | --- | --- | --- | --- |
| S30 | Kontroler bez `[Authorize]` | `kind: attribute` z `name ^Authorize$` | 0 w całym `eTMsWEB`; grep: 0. `[AllowAnonymous]` 2× (`HomeController`) | ✅ |
| S31 | `[ValidateAntiForgeryToken]` na `:153` i `:280` | `attribute ^ValidateAntiForgeryToken$`; `method_declaration` publiczna **bez** tego atrybutu | Linie to `:154` i `:283`. W kontrolerze pożyczek 42 `[HttpPost]` = 42 `[ValidateAntiForgeryToken]`; 14 metod bez atrybutu to `[HttpGet]` | 🔶 linie |
| S32 | `CheckSessionAttribute` zarejestrowany 2× | `new CheckSessionAttribute($$$)` | `FilterConfig.cs:13` i `ETMSWEBConfigure.cs:28` | ✅ |
| S33 | AutoMapper: mapowania pożyczek bez `AssertConfigurationIsValid` | `$C.CreateMap<$A, $B>()`; `$C.AssertConfigurationIsValid($$$)` | 10 `CreateMap` (`AutoMapperDAL.cs:11-20`), z czego 2 to pożyczki (`:19-20`); Assert: 0, grep: 0 | ✅ |
| S34 | `SqlConnection` bez `using` | `new SqlConnection($$$)`; ten sam wzorzec `inside: using_statement` | 3 (`AppDbContextBase.cs:73`, `93`, `112`); w `using`: 0; grep `using (`: 0 | ✅ |
| S35 | Encje zduplikowane bez dziedziczenia | `class_declaration` dla 6 nazw | Żadna z 6 klas nie ma `base_list` | ✅ |
| S36 | `Parametry` nieużywane w BLL i DAL pożyczek; w kontrolerze pożyczek tylko `:97` | `$U.Parametry.$M($$$)`; `identifier` z `regex: ^Parametr` w BLL + DAL | Kontroler pożyczek: tylko `:97`; poza nim 3× w `AdminController` (CRUD parametrów). BLL/DAL: 0; grep `Parametr`: 0 i 0 | ✅ |

### 7.4 Autoryzacja: nowe ustalenia

| # | Twierdzenie | Wzorzec | Wynik | Werdykt |
| --- | --- | --- | --- | --- |
| S37 | (implikowane w TD11/TD15) przepływy kasowe sprawdzają uprawnienia w DAL tak jak spłata | `method_declaration` publiczna z `not: has: identifier ^UzytkownikUprawnienieLista$` (`stopBy: end`) | 26 metod bez sprawdzenia; poza odczytami są to `ZastawTypDodaj` `:80`, `ZastawTypUsun` `:98`, `ZastawyAktualizujUdzial` `:723` i **`KasaOperacjaDodaj` `:2715`**. Grep w `DAL:2715-2850`: brak `Uprawn`. `placowkaId` przychodzi z body (`Controller.cs:416-419`, `DAL:2762`) | ❌ dla `KasaOperacjaDodaj` |
| S38 | `ZastawyAktualizujUdzial` bez sprawdzenia: czy jest wystawiona? | `$X.ZastawyAktualizujUdzial($$$)` → **0**; grep pokazał wywołania niekwalifikowane; poprawiony `ZastawyAktualizujUdzial($$$)` | 4 wywołania wewnętrzne z metod już sprawdzonych (`DAL:458`, `533`, `801`, `854`); kontroler: 0 | ✅ nie jest wystawiona (zero było błędem wzorca) |
| S39 | Open Q5: czy `DBWykonaj` sprawdza uprawnienia | odczyt całej metody (`AdminController.cs:340-364`), w tym grep `uprawn\|session\|admin\|role` | Brak sprawdzenia; `GetDataSet(query)` na tekście z żądania (`:350`) | rozstrzygnięte: brak [E] |

### 7.5 Powtarzalne kształty z TD

| # | Twierdzenie | Wzorzec | Wynik | Werdykt |
| --- | --- | --- | --- | --- |
| S40 | TD2: błąd netto `sum - vat` | `$Z.kwotaZbyciaNetto = $A - $Z.kwotaZbyciaVat` | 2 trafienia w `AkcjaSplataCalkowita`: `:1407` (błędne) i `:1417` (poprawne dla ostatniego). Inne przepływy: 0 | ✅ tylko tutaj |
| S41 | TD7: `wplywNaStan = 1` na sztywno w `:1340` | `$O.wplywNaStan = $V`; reguła `integer_literal` po prawej | 14 przypisań: 5× `= 1` (rozliczenie 7: 1188, 1340, 1529, 1765, 2056), 3× `= -1` (rozliczenie 3: 1070, 1634, 1870; reguła `integer_literal` ich nie złapała, bo `-1` to `prefix_unary_expression`, a grep je pokazał), 6× ze słownika | 🔶 konwencja w 5 przepływach |
| S42 | TD4: indeks `[Count - 1]` w `:1410` | `$L[$L.Count - 1]` | 7 trafień; w spłacie tylko `:1410`. Reszta jest w `ZastawyAktualizujUdzial` i przy zastawach nowej umowy (Czesciowa, Dopozyczenie) | ✅ |

### 7.6 Twierdzenia plikowe (ast-grep nie dotyczy, sprawdzone `git ls-files`/grep)

- 28 tabel w `eTMsDATABASE/dbo/Tables` ✅.
- 0 plików `.tt`/`.edmx`/`*.Designer.cs`/Service References/BundleConfig ✅.
- 5 projektów w `eTMsLomb.sln` ✅.

### 7.7 Lekcje z fałszywych zer

Pięć z początkowych zer albo zaniżonych liczb wynikało z wzorca, a nie z kodu:

- literał SQL rozbity na kilka `query +=` (S29);
- wywołanie bez kwalifikatora `this.`/obiektu (S38);
- literały verbatim/interpolowane poza `kind: string_literal` (S23, S26);
- `-1` jako `prefix_unary_expression` (S41);
- w JS wzorzec `"$S"` nie łapie stringów w apostrofach, dlatego pierwsza próba S1 dała 0; poprawiono na `kind: string`.

Wszystkie 5 wyłapała kontrola grepem.

### 7.8 Obserwacja poboczna (poza przepływem)

**[E]** `UzytkownikOdczytaj(string login, DateTime doDaty)` dokleja login **bez apostrofów**: `where login = " + login + "` (`AppDbContext.cs:771`, `:776`). Jedyny wywołujący to `UzytkownikDAL.cs:68`. **[I]** Dla loginu alfanumerycznego zapytanie zwróci błąd „invalid column name”.

## Odnośniki do kodu (najważniejsze)

- `eTMsWEB/Views/PozyczkaLombardowa/PozyczkaLombardowaEdycjaAkcje.cshtml:12`, `:349-449` — przycisk i modal
- `eTMsWEB/Scripts/PozyczkaLombardowa/PozyczkaLombardowa.js:395-426`, `:659-745` — klient: odczyt, wyliczenie, wykonanie
- `eTMsWEB/Controllers/PozyczkaLombardowaController.cs:133-139`, `:155-160`, `:282-292` — akcje
- `eTMsWEB/Filters/CheckSessionAttribute.cs:36-85`, `eTMsWEB/App_Start/FilterConfig.cs:12-13`, `eTMsWEB/ETMSWEBConfigure.cs:18-35` — sesja, filtry, DI
- `eTMsBLL/REPOSITORY/PozyczkaLombardowaBLL.cs:46-115` — odsetki i RRSO
- `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs:1220-1431` — walidacje, kasa, zastawy
- `eTMsDAL/REPOSITORY/UnitOfWorkDAL.cs:23-30`, `:96-103` — ręczne tworzenie zależności
- `eTMsDB/REPOSITORIES/AppDbContext.cs:2122-2247` — batch zapisu; `:1425`, `:932`, `:1732`, `:3246`, `:3262` — odczyty
- `eTMsDB/REPOSITORIES/AppDbContextBase.cs:52-66`, `:70-104` — mapowanie refleksją, wykonanie SQL
- `eTMsDATABASE/scripts/Initial.sql:458`, `:490-499` — seed statusów, rozliczeń, uprawnień

## Kontekst historyczny (z wcześniejszych zmian)

`context/changes/` i `context/archive/` nie zawierają innych zmian. Werdykty dla tez z `context/map/repo-map.md`:

- Watch 2 („monolit, brak testów”): **potwierdzone** [E] (§3, §4).
- Watch 3 („uprawnienia tylko w menu”): **obalone dla tej akcji** i 20 innych metod DAL pożyczek [E], bo sprawdzenie jest w DAL. **Potwierdzone** dla `KasaOperacjaDodaj` i `DBWykonaj` [E] (§7).
- Watch 1 („SQL sklejany, transakcje tylko w tekście”): **potwierdzone** [E], z zastrzeżeniami TD8–TD10.
- „Parametry zasilają wyliczenia kwot” (inference mapy): **obalone** dla spłaty całkowitej [E].
- „Folder ≠ capability” (pożyczki obsługują kasę, sprzedaż, remanent, książkę): **potwierdzone** [E]. Skutki spłaty czytają raporty wszystkich czterech obszarów (§1.4).

## Powiązane badania

Brak innych `research.md` w repo.

## Otwarte pytania / białe plamy [U]

1. Czy brak `XACT_ABORT` daje częściowy commit dla realnych błędów tej ścieżki? Wymaga testu na bazie.
2. Jaki poziom izolacji (RCSI) ma baza produkcyjna? Od tego zależy, czy wyścig z TD9 jest realny.
3. ~~Czy umowa bez zastawów może mieć status 3?~~ Rozstrzygnięte: **tak** (§5).
4. Czy to, że odsetki przychodzą od klienta, jest świadomą decyzją (rabat)? Kto może ją podejmować? Pytanie do właściciela produktu.
5. ~~Czy wszystkie akcje pieniężne i `AdminController.DBWykonaj` mają sprawdzenie uprawnień w DAL?~~ Rozstrzygnięte przez ast-grep: **nie**. `KasaOperacjaDodaj` i `DBWykonaj` nie mają sprawdzenia (§7, S37, S39). Otwarte pozostaje, czy dostęp ogranicza coś poza kodem (np. IIS, sieć).
6. Jak wdraża się schemat na produkcję i czy produkcyjny schemat zgadza się z SSDT?
7. Czy szablony wydruków z bazy (`TB_SZABLONY`) czytają pola zapisywane przez spłatę? Poza repo.
8. Czy istnieją testy ręczne lub UAT poza repo?
9. Czy binding liczb z przecinkiem i dat `YYYY-MM-DD` zależy od kultury serwera?
