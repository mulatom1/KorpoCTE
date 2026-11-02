---
date: 2026-10-14T19:30:33+02:00
researcher: Claude Code (Opus 5.5) na zlecenie Tomasza Mularczyka
git_commit: 375c6fd14b9654a602cea3ed697fdb3bfe255bb5
branch: master
repository: Lombard (eTMsLomb)
topic: "Refactor opportunities: które problemy z post-flow-analysis warto naprawić, w jakim docelowym kształcie i w jakiej kolejności"
tags: [research, refactor-opportunities, AppDbContext, PozyczkaLombardowaDAL, autoryzacja, transakcje-kasowe, SQL, DI, testowalność, verified]
status: complete
last_updated: 2026-10-14
last_updated_by: Claude Code (Opus 5.5)
verified_at: 2026-10-14T19:40:57+02:00
verified_commit: 375c6fd14b9654a602cea3ed697fdb3bfe255bb5
verified_with: "ast-grep 0.50.0 (+ grep dla każdego zera)"
last_updated_note: "Weryfikacja ast-grep 45 twierdzeń strukturalnych (sekcja „Weryfikacja twierdzeń (ast-grep)”): 30 potwierdzonych, 13 doprecyzowanych, 2 obalone. 15 korekt liczb, numerów linii i zakresów naniesionych w miejscu w formacie „nowa (raport: stara)”. Ranking (§6) i werdykty intencjonalności (§2.1) bez zmian; wpływ na ranking opisany wyłącznie w sekcji weryfikacji."
---

# Research: Refactor opportunities

**Data**: 2026-10-14T19:30:33+02:00
**Badacz**: Claude Code (Opus 5.5) na zlecenie Tomasza Mularczyka
**Commit**: 375c6fd14b9654a602cea3ed697fdb3bfe255bb5
**Gałąź**: master
**Repozytorium**: Lombard (eTMsLomb)

## Pytanie badawcze

Analiza `context/changes/post-flow-analysis/research.md` opisuje dług techniczny i ryzyka strukturalne, ale nie mówi, co z nimi zrobić. Ten raport odpowiada na trzy pytania, które tamta analiza zostawiła otwarte:

1. Które problemy warto naprawić?
2. Jaki ma być docelowy kształt kodu?
3. W jakiej kolejności?

Przebieg:

- Każdy problem z raportu źródłowego klasyfikuję jako **kandydata** albo **nie-kandydata**. Kandydat to problem, którego naprawa zmienia strukturę kodu.
- Każdego kandydata badam w trzech wymiarach: obecny kształt, historia i intencjonalność, wykonalność migracji.
- Na końcu podaję ranking 2–3 najmocniejszych opcji z trade-offami.

**Granice.** Nic nie zostało zmienione w kodzie. Nie projektuję architektury poza nazwaniem docelowego kształtu. Raport nie zawiera żadnej decyzji: ranking to propozycja na osobną sesję planowania.

**Metoda.** Raport źródłowy i mapę repo (`context/map/repo-map.md`) przyjmuję jako dowody i nie wyprowadzam ich ponownie. Trzy sub-agenty pracowały równolegle w trybie read-only, każdy w jednym wymiarze dla wszystkich kandydatów K1–K9. Ich decydujące twierdzenia sprawdziłem w kodzie punktowo (§5). Nic nie było budowane ani uruchamiane. `context/foundation/lessons.md` nie istnieje.

**Legenda.**

- **[E]** evidence, czyli fakt widziany w kodzie lub w gicie, z kotwicą.
- **[I]** inference, czyli wniosek, którego nie sprawdzono na działającym systemie.
- **[U]** unknown, czyli rzecz nierozstrzygalna z repozytorium.
- Kotwice `TDn` i `Sn` odsyłają do raportu źródłowego.

## 0. Inwentarz problemów i klasyfikacja (do audytu)

### 0.1 Kandydaci (naprawa zmienia strukturę kodu)

| ID | Kandydat | Źródło w raporcie / mapie |
| --- | --- | --- |
| K1 | Transakcja i strażnik `@ostatniStanKasy` jako tekst SQL: 7 kopii w 3 wariantach, brak `XACT_ABORT` i lock hintów, `UPDATE … where umoId` bez warunku na status | TD8, TD9, TD10, TD11; §6.1 |
| K2 | SQL sklejany ze stringów; API `params SqlParameter[]` istnieje, ale nikt go nie używa | TD12, TD13, §7.8; mapa Watch 1 |
| K3 | Autoryzacja ręcznie w DAL (21 kopii), brak sprawdzenia na granicy web, luki `KasaOperacjaDodaj` i `DBWykonaj` | TD15, §6.2, S37, S39; mapa Watch 3 |
| K4 | Logika domenowa w DAL, BLL pominięty (tylko `Wylicz`) | TD17 (TD1 jako objaw) |
| K5 | Monolit: jeden kontroler, DAL i `AppDbContext` obsługują 5 capability | mapa Watch 1, Watch 2; §4.1 |
| K6 | Okablowanie omija DI: `new` w UoW, `new ETMSCoreBLL()` 93× (raport: 94×), filtr sesji zarejestrowany 2× | TD18, S14, S32 |
| K7 | Mapowanie refleksją po nazwach, AutoMapper bez `Assert`, encje zduplikowane bez dziedziczenia | TD19, S33, S35; §4.1 |
| K8 | `DateTime.Now` wpisany na sztywno | TD21, S16 |
| K9 | Magiczne liczby słownikowe (statusy, akcje, rozliczenia, uprawnienia), `wplywNaStan` jako literał | TD7, S41; §4.1 („Magiczne liczby ↔ seed”) |

### 0.2 Nie-kandydaci (wejście do oceny wykonalności i kosztu)

| Problem | Źródło | Dlaczego nie kandydat | Jak wpływa na ocenę |
| --- | --- | --- | --- |
| Serwer ufa odsetkom od klienta | TD1 | Defekt zachowania plus otwarta decyzja produktowa („rabat?”, Open Q4). Poprawka to jedno wywołanie walidacji | Objaw K4. Zgłoszone jako niezależne od rankingu |
| Błąd netto zastawów przy ≥3 zastawach | TD2 | Lokalny błąd w 1 linii (`DAL:1407`) | — |
| Nadpisanie sprzedanego zastawu | TD3 | Brakująca walidacja | — |
| Pusta lista zastawów | TD4 | Brakująca walidacja | — |
| Rozjazd precyzji `minProcentZwrotu` | TD5 | Defekt kontraktu JS ↔ kolumna | — |
| RRSO dla 0 dni | TD6 | Defekt algorytmu w BLL | Najłatwiejszy pierwszy test jednostkowy, bo BLL jest czysty |
| Sekrety w repo | TD14 (+ jawne hasło ADMIN w `Initial.sql:465`, nowe w tym raporcie) | Konfiguracja i proces | — |
| Brak testów i CI | TD16, §3 | Brak osłony | **Główny czynnik kosztu każdego kandydata** (§4) |
| `SqlConnection` bez `using` | TD20 | 3 lokalne edycje w 1 pliku (`AppDbContextBase.cs:73`, `93`, `112`) | Wartość dodana przy okazji K2 |
| Diagnostyka (`ExceptionToMessage`, `QueryLog` połyka błędy) | TD22 | Obserwowalność, jeden punkt (`RepoBase.cs:12-21`) | Przy K2 log traci wartości (§3, K2) |
| Brak PostDeploy i procesu wdrożenia schematu | §4.2 | Proces | Każda zmiana SQL wdraża się razem z binarkami, bo SQL żyje w C# [I] |
| Podwójne kliknięcie WYKONAJ | §3 | Lokalny defekt UI | — |
| Binding liczb i dat zależny od kultury | §3, Open Q9 | [U] | — |
| `customErrors mode="Off"` | mapa Watch 3 | Konfiguracja | — |
| Seed bez placówek, kasy i uprawnień | nowe (§4) | Dane testowe | Podnosi koszt bazy testowej dla K1 i K2 |

## Podsumowanie

**Ranking (szczegóły w §6):**

1. **K3 — autoryzacja na granicy web.** Ma najwyższy koszt długu w przeliczeniu na koszt zmiany. Luki są realne i dotyczą całego `AdminController` (w tym konsoli SQL), `KasaOperacjaDodaj` i 8 kontroli odczytu, które nie przerywają metody. Istnieją już szwy: globalny filtr i role w sesji. Pierwszy krok jest addytywny i nie wymaga bazy testowej.
2. **K1 — jeden protokół zapisu operacji kasowej.** Pieniądze i spójność salda. Sam mechanizm strażnika to świadoma decyzja nośna, więc zostaje. Trzy warianty to przypadek. Zmiana jest zamknięta w 7 metodach jednego pliku i wycofywalna bez migracji danych. Jedyną osłoną może być test integracyjny na prawdziwym SQL Server, którego dziś nie ma.
3. **K2 — parametryzacja zapytań, wycinkami.** Wektor wstrzyknięcia jest szerszy, niż podał raport: 11× login, 33 linie `LIKE '%…%'` bez escapowania, 8× `order by` z nazwą kolumny z żądania (§5). API parametrów działa end-to-end. Całość to XL, ale wycinki są małe. Kolejność: po K1, bo dotykają tych samych 7 batchy.

**Odrzucone lub odroczone:**

- **K4 i K9:** prawdziwa naprawa wymaga przeprojektowania pojęć biznesowych. Reguły mają 4 sprzeczne definicje. Brakuje maszyny stanów i mapowania akcja → rozliczenia. Zatrzymuję się na tym; to temat na osobną analizę domeny.
- **K5:** świadoma konwencja „moduł = kontroler + DAL + region”. Kompilator ją chroni. Granica umowa–kasa jest pytaniem biznesowym.
- **K6 i K8:** przypadkowa złożoność, tania w naprawie, ale o niskim koszcie długu. Najlepiej traktować je jako prerekwizyty testowalności, nie jako cele.
- **K7:** podział X/Item/ListItem jest świadomy. Asercja AutoMappera jest tania, ale nie jest refaktorem. Deduplikacja encji wymaga rozdzielenia modelu zapisu od modelu odczytu, czyli znowu decyzji domenowej.

**Wspólny prerekwizyt [E+I].** Każdy kandydat startuje z zerowej osłony: 0 testów, 0 CI, kompilator nie widzi SQL w stringach. Potrzebne są dwa prerekwizyty:

- **P1 — projekt testowy C# z mockiem `IAppDbContext`.** Odblokowuje K3, K4, K8 i K9.
- **P2 — lokalna baza SQL Server.** Tworzona z `CreateDB.sql` i `Initial.sql` oraz z fixture placówki, kasy i uprawnień. Odblokowuje K1 i K2.

**Kontekst historyczny [I, pewność średnio-wysoka].** Historia publikacji w imporcie wskazuje, że kod to wycinek lombardowy wydzielony w czerwcu 2019 z większego systemu „eTMs”. Ten system miał EF z migracjami, procedurami i widokami oraz moduł kredytowy (§2.0). Wiele kształtów (SQL w stringach, puste usingi `DataAnnotations`, połowiczny DI) to ślady tego przejścia, a nie decyzje projektowe.

## 1. Obecny kształt per kandydat

Wnioski z raportu źródłowego przyjmuję bez powtarzania. Niżej jest tylko to, czego raport nie zawierał.

### K1 — transakcja i strażnik kasowy w SQL

- [E] `AppDbContext.cs` nie ma żadnej metody prywatnej ani helpera. Jedyne niepubliczne człony to `disposed` (`:8`) i `Dispose(bool)` (`:3859`). Każdy batch jest budowany od zera przez `query +=`.
- [E] Fragmenty SQL są powielone ręcznie. Tekst `insert … KASA_OPERACJE_ROZLICZENIA` występuje 14 razy, `insert … KASA_OPERACJE (` 9 razy, `update TB_POZYCZKI_LOMBARDOWE_KASA set` 7 razy (grep sub-agenta).
- [E] Wspólny szkielet 7 metod:
  - prefiks `set dateformat ymd; begin tran`;
  - `declare/select @ostatniStanKasy` i porównanie z wartością z C#;
  - końcowe `update … KASA set stanKasy = <wartość absolutna>`;
  - `commit tran`.
- [E] Część zmienna: ogon strażnika (A/B/C), liczba i kolejność rozliczeń, zastawy, nowa umowa (wariant B).
- [E] Sygnatury nie mają wspólnego kształtu. Wyplac ma 5 parametrów i zwraca `void` (`:1945`). SplataCalkowita ma 8, z rozliczeniami 7/8/9 jako osobnymi nullowalnymi argumentami (`:2122`). SplataCzesciowa i Dopozyczenie mają po 13 i trzy parametry `out` (`:2249`, `:2493`). KasaOperacjaDodaj zwraca `int` (`:3294`).
- [E] **Wariant zależy od sygnatury metody** (sprawdzone punktowo, §5):
  - A (`rollback; THROW`) jest w 3 metodach `void` bez `out` (`:1945`, `:2039`, `:2122`) i idzie przez `ExecuteNonQuery`. Wyjątek → `catch` → `Result.code=1` (`DAL:1424-1428`).
  - B (`rollback; select errorCode`) jest w 3 metodach z `out int errorCode, out string errorMessage` (`:2249`, `:2493`, `:2740`) i idzie przez `GetDataSet`. Wynik czyta `Rows[0][0..2]` po indeksie (`:2484-2490`), a DAL przepisuje go do `result.code` (`DAL:1649`, `1885`, `2176`).
  - Ten sam kontrakt `out errorCode` mają metody bez strażnika: Przenies, Przyjmij i Kopiuj (`:2901`, `:3042`, `:3066`).
- [E] Zapisy poza batchem:
  - `KasaOperacjaDodaj` zapisuje `PozyczkaLombardowaKasaDodaj` w trakcie walidacji, przed batchem (`DAL:2773` (raport: 2761-2772));
  - `ZastawyAktualizujUdzial` zapisuje każdy zastaw osobnym round-tripem (`DAL:723-766`) już po zapisie innych akcji (`DAL:458`, `533`, `801`, `854`). Nie jest atomowy z nimi.
- **Szwy:** interfejs `IAppDbContext` z 1 implementacją per metoda, `Result` jako jednolity kanał błędu w DAL, rozliczenia już traktowane jako kolekcja w `KasaOperacjaDodaj` (`:3294`, tablica).
- **Pojęcia biznesowe?** Częściowo. Atomowość, blokady i jeden kontrakt błędu to struktura. To, że saldo jest przechowywane jako wartość absolutna wyliczona w C#, a nie jako przyrost, jest decyzją modelu. Tu się zatrzymuję.

### K2 — SQL sklejany ze stringów

- [E] Cała infrastruktura I/O to `AppDbContextBase.cs` (129 linii). `GetDataSet` i `ExecuteNonQuery` już robią `cmd.Parameters.AddRange(sqlPrms)` (`:76`, `:96`), więc parametry działają end-to-end. Przez to jedno wąskie gardło przechodzi 114 wywołań.
- [E] `QueryLog(string)` dostaje tylko tekst zapytania (`:71`, `:91`, `:106-127`). Logowanie jest włączone (`Web.config:15 queryLogging=true`).
  - [I] Dziś log zawiera wartości, więc przypadkiem służy za audyt. Po parametryzacji zobaczy tylko placeholdery.
- [E] Skala budowania warunkowego w `AppDbContext.cs`: 77 (raport: 62) instrukcji `if (…) query +=`, około 65 konkatenacji warunkowych, 18 (raport: 19) pętli `foreach` doklejających fragmenty, 226× `ToString("F2")`, 151× `yyyy-MM-dd`. Helperów formatowania nie ma.
- [E] **Wektor szerszy niż w raporcie źródłowym** (sprawdzone, §5):
  - 33 linie `like '%" + …` bez `Replace`, np. lista pożyczek `:1366-1368`;
  - 8× `" order by " + sortColumn + " " + sortOrder` (np. `:1371`). `sortColumn` przychodzi z parametru akcji (`PozyczkaLombardowaController.cs:109`, `:430`, `:674` (raport: `:109`, `:115`, `:430`); także `KlientController.cs:47`, `AdminController.cs:45`, `:212`). Nazwy kolumny nie da się sparametryzować, potrzebna jest biała lista.
- **Szwy:** `params SqlParameter[]` w obu metodach bazowych. `CreateListFromTable`/`CreateItemFromRow` nie zależą od sposobu budowania zapytania.
- **Pojęcia biznesowe?** Nie. Wyjątek: decyzja, czy log zapytań ma być audytem.

### K3 — autoryzacja

- [E] `CheckSessionAttribute` liczy dwie rzeczy:
  - `zalogowanyUprawnienia`: pełną listę z `placowkaId` (`:72`, `:127`). Nikt jej nie czyta (grep w `eTMsWEB`: tylko te 2 zapisy);
  - `zalogowanyRole`: spłaszczoną listę stringów, czyli `"admin"` plus `Distinct(uprawnienieId)` (`:74-86`). Wymiar placówki ginie.
- [E] Logika jest skopiowana do `OnResultExecuting` (`:90-143`) z błędem. `:125` (raport: 124) rzuca `resultUser.message` zamiast `resultUserUpraw.message` (sprawdzone, §5).
- [E] Role są egzekwowane wyłącznie w menu:
  - `_MySubMenu.cshtml:17-28`: KASA, UMOWY, REMANENT i SPRZEDAŻ mają `"5,6"`, KSIĄŻKA `"4,5,6"`, a 6 pozycji ADMIN, w tym DBADMIN (`:28`), ma `"admin"`;
  - `_MyMenu.cshtml:4-7`;
  - trzecia kopia w JS: `'uprs': [5,6]` / `[4,5,6]` w 5 plikach `Scripts/PozyczkaLombardowa/*.js` (np. `PozyczkaLombardowa.js:13`).
- [E] `AdminController`, `KlientController`, `SzablonController` i `HomeController` nie sprawdzają ani ról, ani `czyAdministrator` (grep: 0). Pozostałe DAL-e też nie. **Luka obejmuje cały `AdminController`:** użytkowników, uprawnienia, placówki, parametry, szablony i konsolę SQL, a nie tylko `DBWykonaj`.
- [E] **8 kontroli odczytu w DAL pożyczek nie przerywa metody** (sprawdzone, §5). Ustawiają `code=1`, ale bez `return`, więc zapytanie wykonuje się dalej i nadpisuje `result.data`. Miejsca: `DAL:141-146`, `:180-185`, `:220-225` oraz `:2906`, `2948`, `2991`, `3031`, `3079` (raport: 2905, 2947, 2990, 3030, 3078). [U] Czy JS pokazuje `data` przy `code=1`.
- [E] W DAL pożyczek działają dwa mechanizmy:
  - zapisy: `UzytkownikUprawnienieLista(wykonal, placowkaId).Where({5,6})` (21×);
  - odczyty list: `PozyczkaLombardowaPlacowkaUprawninegoLista(ktoPyta)` z SQL `in (4,5,6)` (8×; `AppDbContext.cs:1169`).
- [E] Model uprawnień już istnieje: `SL_MODULY` → `SL_UPRAWNIENIA` → `TB_UZYTKOWNICY_UPRAWNIENIA (uprawnienieId, placowkaId, login)` (`TB_UZYTKOWNICY_UPRAWNIENIA.sql:7`). Seed zawiera tylko moduł 2 i role 4/5/6 (`Initial.sql:497-499`, `:507`).
- **Szwy:**
  - globalny filtr MVC w potoku (`FilterConfig.cs:13`);
  - `Session["zalogowanyRole"]` (wystarcza dla `"admin"`);
  - `Session["zalogowanyUprawnienia"]` z placówką;
  - `IUzytkownikUprawnienieDAL.Lista`;
  - `IPlacowkaDAL.PlacowkaUprawnionegoLista`.
- **Pojęcia biznesowe?** Częściowo. Model danych (rola × placówka × moduł) jest gotowy. Brakuje jawnego mapowania „operacja → wymagana rola” i decyzji, co wolno roli 4 (WIZYTOR), np. czy może czytać szczegóły umowy (`Odczytaj` nie ma kontroli, S37).

### K4 — logika domenowa w DAL

- [E] BLL to 2 klasy (`ETMSCoreBLL`: `IsNrb`, `IsPesel`, `ObjectToJson`; `PozyczkaLombardowaBLL`: 2× `Wylicz` i `WyliczRRSO`) oraz `UnitOfWorkBLL`. BLL nie zależy od DAL ani od DB.
- [E] Kształt `AkcjaSplataCalkowita` (`DAL:1220-1431`):
  - odczyty przeplecione z walidacjami (`1226-1301`);
  - obliczenia mutujące encje (`1303-1418`), w których środku jest jeden odczyt (`:1395` `ZastawLista`);
  - jeden zapis (`1420-1422`).
- [E] Reguły obliczeniowe są wpisane inline: VAT „w stu” 6×, alokacja z resztą na ostatni element (`:739-750`, `:1397-1418`, Czesciowa, Dopozyczenie), `Math.Round` 20×.
- [E] **Reguła „po terminie / do sprzedaży” ma 4 niespójne definicje:**
  - encja: `PozyczkaLombardowaItem.cs:29`, `:31` (`dataZwrotu+1 <= DateTime.Now`);
  - SQL list: 9 (raport: 6) literałów w `AppDbContext.cs:1225`, `1267-1268`, `1290`, `1334-1335`, `1357`, `1395-1396` (`dateadd(DAY,1,dataZwrotu) <= GETDATE()`);
  - Wycofaj: `DAL:2372` (`AddDays(1) >= DateTime.Today`);
  - remanent: `AppDbContext.cs:3628` (`dataZwrotu > GETDATE()`, bez +1).
- [E] Mapowanie akcji kasowej na rozliczenie (1→1, 2→2, 3→1, 4→2) istnieje tylko w JS (`PozyczkaLombardowaKasa.js:156-184`). Serwer sprawdza jedynie, czy akcja jest w słowniku (`DAL:2753`).
- **Pojęcia biznesowe?** **Tak, w istotnej części.** Przeniesienie kodu do BLL bez jednej definicji reguł przeniesie sprzeczności.

### K5 — monolit

- [E] W `AppDbContext.cs` jest 8 regionów per encja. Region POZYCZKA LOMBARDOWA (`:1063-3854`) to około 2,8 tys. z 3876 linii. Wewnątrz metody są ułożone kolejno: słowniki, umowa, podzasoby, akcje, kasa, remanent, sprzedaż.
- [E] Kontroler jest ułożony tak samo:
  - umowa i akcje: `:29-381`;
  - kasa: `:383-533`;
  - książka: `:535-572`;
  - remanent: `:574-661`;
  - sprzedaż: `:663-802`.
  `IPozyczkaLombardowaDAL` ma identyczne grupy.
- [E] Precedens podziału istnieje. 8 DAL-i (`KlientDAL` itd.) ma ten sam szablon: `: RepoBase, IXxxDAL`, konstruktor `(IAppDbContext)` i `Result` z `try/catch` (np. `KlientDAL.cs:8-36`). Wszystkie dzielą jeden `IAppDbContext` (111 metod).
- [I] Remanent, książka i sprzedaż to odczyty, więc da się je wydzielić mechanicznie. Granica umowa | kasa przecina te same batche zapisu (K1).
- **Pojęcia biznesowe?** Częściowo: kto jest właścicielem operacji kasowej wywołanej akcją na umowie.

### K6 — okablowanie DI

- [E] Moduły rejestrują wszystko z domyślnym lifetime (`AutofacModuleDB.cs:12`, `AutofacModuleDAL.cs:12-21`, `AutofacModuleBLL.cs:11-15`). `AutofacModuleBLL : AutofacModuleDAL` woła `base.Load`, a `ETMSWEBConfigure.cs:25-26` rejestruje oba moduły, więc DAL jest rejestrowany 2×.
- [E] Z kontenera resolwowane są tylko UoW i 3 DAL-e filtra. UoW tworzy resztę przez `new` (`UnitOfWorkDAL.cs:27`, `36-99`; `UnitOfWorkBLL.cs:18`, `27`), więc rejestracje DAL/BLL są martwe dla ścieżki kontrolerów.
- [E] UoW niczego nie koordynuje: nie ma transakcji ani wspólnego połączenia. Jest leniwym lokatorem z kaskadowym `Dispose` (`UnitOfWorkDAL.cs:99-111`). `AppDbContext` jest bezstanowy, bo każde zapytanie ma własne `SqlConnection`.
- [E] `ETMSCoreBLL` jest bezstanowy. Wszystkie 93 (raport: 94) instancje służą tylko do `ObjectToJson`.
- **Pojęcia biznesowe?** Nie.

### K7 — mapowanie i encje

- [E] `PozyczkaLombardowaItem` (79 (raport: 78) właściwości) jest ścisłym nadzbiorem `PozyczkaLombardowa` (49), a `…ListItem` (22) ścisłym podzbiorem `Item`. `ZastawItem` = `Zastaw` + 2 pola. `OperacjaSprzedazyZastawItem` to osobny, spłaszczony wiersz raportu (3 pola wspólne), a nie duplikat.
- [E] AutoMapper jest wołany 6×: 5× encja → historia w innych DAL-ach i 1× klon `Item→Item` (`DAL:2222`). Mapa `PozyczkaLombardowa→Item` (`AutoMapperDAL.cs:19`) ma 0 użyć.
- [E] `Item` jest jednocześnie modelem zapisu we wszystkich akcjach i modelem odczytu z regułami wyliczanymi (`statusSprzedazyId` z `DateTime.Now`).
- **Pojęcia biznesowe?** Częściowo. Rozdział zapisu i odczytu wymaga ustalenia, co jest stanem umowy.

### K8 — zegar

- [E] Rozkład 36 wystąpień w DAL pożyczek:
  - 7× walidacja „nie z przyszłości”;
  - 2× generowanie numeru umowy z `ToString("yyyyMMddHHmmss")` (`:387`, `:2324`), więc unikalność numeru zależy od zegara;
  - 27× znaczniki czasu.
- [E] Poza DAL pożyczek: inne DAL-e 13×, encje 5×, `CheckSessionAttribute`, 18× `GETDATE()` w SQL. W jednej operacji (Przenies, Kopiuj) zegar aplikacji miesza się z zegarem bazy.
- [E] Abstrakcji czasu brak (grep `IClock|ITimeProvider|SystemTime`: 0).
- **Pojęcia biznesowe?** Częściowo: „dzień biznesowy” ma kilka definicji (zob. K4).

### K9 — magiczne liczby

- [E] W C# nie ma żadnego `enum` ani `const` (grep: 0).
- [E] Liczba literałów (przybliżenie grepem):
  - w DAL: `statusId` 38×, `akcjaOperacjiId` 7×, `rozliczenieOperacjiId` 10×;
  - w SQL: `statusId` 19×;
  - w widokach: 28 porównań `statusId`;
  - w JS kasy: literały akcji i rozliczeń.
- [E] Słowniki są czytane dynamicznie wyłącznie do nazw, do `stawkaVat`/`wplywNaStan` i do sprawdzenia, czy akcja istnieje. Przejścia statusów, wybór rozliczeń i role to literały. Słowniki nie mają kolumn semantycznych: nie modelują akcja → rozliczenia ani „status końcowy”.
- **Pojęcia biznesowe?** **Tak, w przeważającej części.** Literały zastępują brakujące pojęcia: maszynę stanów umowy, regułę akcja kasowa → rozliczenia, uprawnienia operacji.

## 2. Historia i intencjonalność

### 2.0 Ślad pochodzenia kodu

- [E] Wersja `eTMsWEB/Properties/PublishProfiles/LOKAL.pubxml.user` z importu 42292b7 zawiera wpisy `<File>` z `publishTime`, m.in.:
  - `bin/EntityFramework.dll` i `EntityFramework.SqlServer.dll` (`publishTime` 2015-03-02);
  - `bin/eTMsCORE.dll`;
  - `bin/Migrations/SQL/INITIAL_{DATA,FUNCTIONS,PROCEDURES,VIEWS}_Up.sql`;
  - widoki `PozyczkaKredytowa*`.

  Sprawdzone punktowo (§5).
- [E] `PLIKI/Web.config` i `Web.config.SUKURS` mają sekcję `<entityFramework>`, a bieżący `Web.config` nie.
  - Klucze `PK_dbo.X` występują we wszystkich tabelach poza `TB_QUERY_LOG`.
  - 8 encji ma nieużywane `using System.ComponentModel.DataAnnotations(.Schema)`.
  - Menu zawiera zakomentowany moduł KREDYTY z rolami `1,2,3` (`_MySubMenu.cshtml:15`).
- [I] Kod to wycinek lombardowy z większego systemu „eTMs”, wydzielony około czerwca 2019. Wcześniej warstwa danych stała na EF z migracjami, procedurami, funkcjami i widokami. Interpretacja `publishTime` jako czasu modyfikacji źródła jest wnioskiem, pewność średnio-wysoka.
- [E] Komentarzy z intencją brak. `AppDbContext.cs` ma 0 komentarzy `//` i `--`, tylko 8 `#region`. `git blame` dla badanych zakresów wskazuje import 42292b7. Commit 375c6fd nie podaje uzasadnienia.

### 2.1 Werdykty

| ID | Werdykt | Uzasadnienie | Pewność |
| --- | --- | --- | --- |
| K1 | **Mechanizm strażnika: świadome ograniczenie (decyzja nośna). Trzy warianty: przypadkowa złożoność. Kolejność powstania: unknown** | Komunikat „Niezgodny stan kasy placówki lub inna transakcja w toku!” wprost wyraża optymistyczną kontrolę współbieżności, a od strażnika zależy spójność `stanKasy`. Wariant wynika z kształtu sygnatury (`void` albo `out errorCode`), a nie z różnicy w konsumpcji: kontroler i JS dostają ten sam `Result`. `return;` po `THROW` w wariancie A to martwy kod, typowy ślad kopiuj-wklej | wysoka / średnia |
| K2 | **Przypadkowa złożoność** | `params SqlParameter[]` pochodzi z bazowego snippetu z angielskimi komentarzami i nigdy nie został użyty. Sklejanie to konwencja z okresu przejścia z EF na ADO.NET. Łatka 375c6fd (110 prefiksów) wybrała minimalną zmianę zamiast parametrów; jej powód jest [U] | średnio-wysoka |
| K3 | **Uwierzytelnianie Forms + menu: świadomy model zaufania do UI. Brak kontroli serwerowej w `AdminController`/`DBWykonaj`: luka, nie decyzja. Pominięcie w `KasaOperacjaDodaj`: przypadek** | `DBWykonaj` to zamierzone narzędzie admina (menu DBADMIN, widok, JS), ale żadna warstwa nie sprawdza roli. KASA ma te same role `5,6` co UMOWY, więc nie istnieje osobna rola kasjera, która tłumaczyłaby pominięcie. Kontrole bez `return` wskazują, że wzorzec stosowano niedbale | średnia / średnio-wysoka |
| K4 | **BLL jako biblioteka kalkulacji: prawdopodobnie świadoma konwencja. Szkielet warstwowy przy 2 metodach: przypadek odziedziczony z szablonu** | Opieram się wyłącznie na strukturze; komentarzy brak. `eTMsCORE.dll` w historii sugeruje dawny projekt „Core” | niska-średnia |
| K5 | **Świadoma konwencja (granica = moduł produktowy), rozrost przez dopisywanie** | Ten sam podział (kontroler + DAL + region) dla modułu kredytowego w historii. Raporty, kasa i remanent żyły w module lombardowym już w latach 2016-2017 | średnio-wysoka |
| K6 | **Przypadkowa złożoność (migracja do DI przerwana w połowie)** | Dwa konstruktory filtra (bezparametrowy z `new` i DI-owy), podwójna rejestracja, martwe rejestracje DAL/BLL. Autofac 4.9.2 jest młodszy niż reszta kodu | średnia |
| K7 | **Podział X/Item/ListItem: świadoma konwencja. Duplikacja bez dziedziczenia, mapowanie „cicho po nazwie”, brak `Assert`: przypadek po przejściu z EF** | Spójne nazewnictwo rodzin encji; puste usingi `DataAnnotations` | średnia |
| K8 | **Przypadkowa złożoność** | Brak śladu decyzji. Rozbieżne źródła czasu (C# i SQL) i etykiety wskazują na dryf | średnio-wysoka |
| K9 | **Id słowników `SL_*`: świadome stałe referencyjne (decyzja nośna). Literały `wplywNaStan` obok odczytu słownika: przypadek** | `SL_*` mają ręcznie nadane klucze `INT` bez IDENTITY. W kodzie nie ma żadnych zapisów do `SL_*` ani ekranów do nich. Id uprawnień 1–3 były zarezerwowane dla usuniętego modułu. `AkcjaWyplac` czyta `wplywNaStan` ze słownika do walidacji (`DAL:1011-1036`), a zapisuje literał (`:1070`) | średnio-wysoka / średnia |

## 3. Wykonalność migracji

### 3.0 Osłony i ograniczenia wspólne [E]

- **Testy i CI.** Brak testów; brak `*.yml`, `azure-pipelines`, `Jenkinsfile` i `.github/`.
- **Build.** Wszystkie projekty C# mają `TargetFrameworkVersion v4.5.2`. Brak `TreatWarningsAsErrors` i `LangVersion`; solucja pochodzi z VS 2019 (`eTMsLomb.sln:4`). Kod jest w stylu C# 5: 0 `async`/`await`, `?.`, `$""`.
- **Biblioteki testowe.** [E, Context7 `/xunit/xunit`] xUnit v3 wymaga .NET Framework 4.7.2+. [I] Na net452 zostają starsze linie (xUnit 2.x, NUnit 3, MSTest v2) albo projekt testowy na net472+, który referencjonuje projekty net452. Toolchain [U].
- **SSDT.** `eTMsDATABASE.sqlproj` buduje tylko 28 tabel (`:64-91`), a `SqlServerVerification` jest `False`. SQL w stringach C# leży poza zasięgiem walidacji.
- **Baza testowa.**
  - DDL jest kompletny względem kodu: każda tabela `TB_*`/`SL_*` użyta w C# ma plik DDL.
  - 0 kluczy obcych, 0 triggerów, 0 procedur i widoków.
  - `CreateDB.sql` (621 l.) da się uruchomić bez publikacji SSDT [I].
  - `Initial.sql` (z `SET XACT_ABORT ON`) zawiera słowniki, ale **nie zawiera** `TB_PLACOWKI`, `TB_POZYCZKI_LOMBARDOWE_KASA` ani `TB_UZYTKOWNICY_UPRAWNIENIA`. Każdy test przepływu kasowego musi sam dodać placówkę, stan kasy i uprawnienie.
- **Najtańszy punkt wejścia testów.** `PozyczkaLombardowaDAL(IAppDbContext)` (`DAL:17-20`) z luźnym mockiem. `IAppDbContext` ma 111 metod, więc ręczny fake byłby duży.
- **Kontrolery.** 48× `Session[` w kontrolerze pożyczek. Testy wymagają fałszywego `HttpContextBase`: wykonalne, ale droższe niż test DAL.
- **AutoMapper** jest statyczny (`Mapper.Initialize`), więc testy muszą raz wywołać `AutoMapperDAL.Configure()`.

### 3.1 Per kandydat

| ID | (a) Szew istniejący vs nowa abstrakcja | (b) Blast radius: pierwszy krok → całość | (d) Strangler / wycofanie | (e) Pierwszy prerekwizyt | (f) Koszt |
| --- | --- | --- | --- | --- | --- |
| K1 | Istniejący: 1 metoda `IAppDbContext` = 1 implementacja = 1 wywołujący per przepływ. Poprawki T-SQL to zmiany wewnątrz literału. Nowa abstrakcja byłaby potrzebna dopiero dla transakcji po stronie C# (`ExecuteNonQuery` nie przyjmuje `SqlTransaction`, `AppDbContextBase.cs:89-104`) | 1 metoda (`:2122-2246`), 0 zmian sygnatur → 7 metod w 3 kształtach, 11 klauzul `where`; wariant B sprzężony z DAL przez `errorCode` | tak, per metoda; wycofanie = rewert literału, bez schematu i danych; [U] zachowanie blokad pod obciążeniem | P2 + test integracyjny charakteryzujący `PozyczkaLombardowaAkcjaSplataCalkowita` (sukces, niezgodny stan → THROW 60000, dwie sesje) | M (1 metoda S po P2) |
| K2 | Istniejący: `params SqlParameter[]` działa end-to-end. Dla `order by`: nowa biała lista kolumn | 11 miejsc loginu (regiony UZYTKOWNIK, UZYTKOWNIK UPRAWNIENIE oraz `:431` PALCOWKA i `:1169` POZYCZKA LOMBARDOWA; raport: „regiony UZYTKOWNIK”) → 114 wywołań + 33 `LIKE` + 8 `order by` | tak, per metoda; bez zmian sygnatur i schematu | P2 + test charakteryzujący zapytania logowania | wycinek S–M; całość XL |
| K3 | Częściowo istniejący: globalny filtr i `zalogowanyRole` wystarczają dla roli `admin`. Sprawdzenie „5/6 w placówce umowy” wymaga odczytu zasobu, więc albo zostaje w DAL, albo przechodzi do nowej polityki per zasób | `AdminController` (1 atrybut/filtr) + `KasaOperacjaDodaj` + 8× brak `return` → 21 kopii filtra + decyzja dla 42 akcji POST | tak, każda kontrola jest addytywna; [U] ryzyko odcięcia użytkowników, którzy dziś działają bez ról | P1 (mock `IAppDbContext`) dla DAL; test kontrolera z fałszywą sesją dla `Admin` | luki S; ujednolicenie M; przeniesienie z placówką do web L |
| K4 | Istniejący: `IUnitOfWorkBLL` wstrzyknięty do kontrolera. Brak: BLL nie ma dostępu do DAL, więc potrzebny serwis aplikacyjny z zależnością od DAL (zależy od K6) | 1 akcja → 52 wywołania, ~3166 l. | tak, per akcja | P1 + test charakteryzujący `AkcjaSplataCalkowita` | 1 przepływ M; całość XL |
| K5 | Istniejący: regiony, wzorzec `XxxDAL`/`IXxxDAL`/UoW. Nowa: segregacja `IAppDbContext` per capability | partial class: 2 pliki → 10 (raport: 17) typów-konsumentów `IAppDbContext` (8 DAL, `UnitOfWorkDAL`, `CheckSessionAttribute`) + rejestracje | tak; kompilator jest wystarczającą osłoną | smoke test kontenera (opcjonalny) | partial S; segregacja L; rozcięcie DAL L–XL |
| K6 | Istniejący: Autofac z rejestracjami wszystkich typów, konstruktor DI filtra | 2 pliki UoW (11 (raport: ~12) `new`) → + 93 (raport: 94) `new ETMSCoreBLL()` w 5 kontrolerach + decyzja o podwójnej rejestracji filtra | tak | test kompozycji kontenera (`Resolve` każdego kontrolera) | UoW S; całość M (mechaniczne) |
| K7 | Istniejący: `SetItemFromRow` jako 1 punkt; `AutoMapperDAL.Configure()`. Nowa: wspólna baza / DTO projekcji | sam test (0 zmian prod.) → 61 wywołań mapowania, 6 klas | tak | test `AssertConfigurationIsValid` (bez bazy) | asercja S; tryb ścisły M; deduplikacja L |
| K8 | Brak; nowa abstrakcja źródła czasu, wpinana przez konstruktor DAL. Encje wyliczane (`Item.cs:29`, `:31`) wymagają zmiany modelu | 2 miejsca w spłacie → 36 w DAL pożyczek (+13 w innych DAL, 18× `GETDATE()`) | tak, z domyślną implementacją systemową | P1 | S–M |
| K9 | Brak (`enum`/`const`: 0); nowa: typy wyliczeniowe odwzorowujące seed. Dla `wplywNaStan` szew już jest, bo słownik jest czytany | 8 przypisań `wplywNaStan` → ~60–80 miejsc C# + SQL + widoki/JS | neutralne przy tej samej wartości; [U] zgodność seedu produkcyjnego | P1 + zgodność seedu z produkcją [U] | `wplywNaStan` S; stałe M |

### 3.2 Zależności między kandydatami [I]

- **Prerekwizyty:** P1 odblokowuje K3, K4, K8 i K9. P2 odblokowuje K1 i K2.
- **K6 → K4:** BLL potrzebuje DAL przez DI.
- **K8 → K4:** testy dat w K4 wymagają K8.
- **K1 → K2:** K1 i K2 dotykają tych samych 7 batchy kasowych. Rozsądna kolejność to K1 (mniejszy zakres), a potem parametryzacja tych batchy w K2.
- **K3 ∩ K9:** dzielą 21 literałów `{5, 6}`.
- **K5 (partial)** jest niezależny.

## 4. Wejście z nie-kandydatów do kosztu

- **Brak testów (TD16)** sprawia, że każda zmiana zaczyna się od testów charakteryzujących. To jest koszt stały całego programu, nie koszt pojedynczego kandydata.
- **Seed bez placówek, kasy i uprawnień** dokłada pracy przy fixture'ach P2.
- **`QueryLog`/TD22:** parametryzacja (K2) zmienia zawartość logu, który dziś de facto służy za audyt.
- **TD20** (`using` przy `SqlConnection`) naturalnie wchodzi do K2, bo dotyczy tego samego pliku.
- **TD1** (odsetki od klienta) nie zależy od żadnego refaktoru. Wymaga decyzji produktowej (Open Q4) i jednego wywołania walidacji.

## 5. Weryfikacja wyników sub-agentów

Twierdzenia, które główny agent sprawdził w kodzie:

- **Wariant strażnika a sygnatura.** `AppDbContext.cs:2249`, `:2493` i `:2740` mają `out … errorCode`. `:1945`, `:2039` i `:2122` są `void` bez `out`. ✅
- **Ślad EF i modułu kredytowego.** `git show 42292b7:…/LOKAL.pubxml.user` zawiera `bin/EntityFramework.dll` (`publishTime 03/02/2015`), `bin/Migrations/SQL/INITIAL_*_Up.sql` i `bin/eTMsCORE.dll`. ✅
- **Brak `return` w kontrolach odczytu.** `PozyczkaLombardowaDAL.cs:141-151` i `:179-190`: po `code=1` następuje `result.data = appDbContext…`. ✅
- **`LIKE` bez escapowania.** Sub-agent podał 26 linii. Grep `like '%" +` daje **33** linie, z czego 0 z `Replace`. Przyjmuję 33. 🔶
- **`order by` z `sortColumn`.** 8×; `sortColumn` jest parametrem akcji (`Controller.cs:109`, `:430`). ✅
- **Błąd w filtrze.** `CheckSessionAttribute.cs:125` (raport: 124) rzuca `resultUser.message` w gałęzi `resultUserUpraw`. Sub-agent podał linię `:125` i miał rację; korekta na 124 była błędem głównego agenta (ast-grep, V7). ✅
- **`zalogowanyUprawnienia`.** Ma tylko 2 zapisy (`:72`, `:127`) i 0 odczytów. ✅
- **Role w menu** (`_MySubMenu.cshtml:17-28`, DBADMIN `"admin"`). ✅

**Rozbieżność liczności.** `DateTime.Now`: raport źródłowy podaje 36 dla `PozyczkaLombardowaDAL.cs`, sub-agent od historii 49 dla całego `eTMsDAL/REPOSITORY` (+1 `DateTime.Today`). To różne zakresy, nie sprzeczność.

**Przybliżenia.** Liczności literałów w K9 (38/19/28) i w K1 (14/9/7) pochodzą z grepa, nie z ast-grep. Mogą być zaniżone tak jak „fałszywe zera” z raportu źródłowego (§7.7).

## 6. Refactor opportunities

Ranking jest propozycją do osobnej sesji planowania. Kryterium: koszt długu (pieniądze, bezpieczeństwo, spójność danych) w stosunku do kosztu zmiany (osłony, blast radius, odwracalność). Bez zmian w kodzie i bez decyzji.

### #1 — K3: autoryzacja egzekwowana na granicy web, jedna polityka zamiast 21 kopii

- **Obecny kształt.**
  - Uwierzytelnianie Forms (`deny ?`), role liczone w filtrze, ale używane tylko w menu.
  - Cały `AdminController` (w tym `DBWykonaj`, który wykonuje dowolny SQL) i `KasaOperacjaDodaj` (`placowkaId` z body) nie mają kontroli serwerowej.
  - 21 ręcznych kopii filtra `{5,6}` w DAL; 8 kontroli odczytu bez `return`.
  - Role są wpisane w 3 miejscach: DAL/SQL, menu, JS.
- **Docelowy kształt.**
  - Deklaratywna kontrola roli na granicy kontrolera (atrybut lub filtr), oparta na istniejących rolach w sesji.
  - Jedna polityka per zasób „rola ∈ R w placówce zasobu” w miejsce 21 kopii, oparta na `zalogowanyUprawnienia` lub `UzytkownikUprawnienieLista`.
  - Kontrole odczytu kończące metodę.
- **Dlaczego #1.**
  - Koszt długu jest najwyższy z kandydatów: ekspozycja surowego dostępu do bazy dla każdego zalogowanego [I] oraz księgowanie kasy w dowolnej placówce [I].
  - Koszt pierwszego kroku jest najniższy (S): zmiana addytywna, oparta na istniejących szwach (globalny filtr, `zalogowanyRole` z `"admin"`), bez bazy testowej.
  - Werdykt historyczny (§2.1) mówi, że brak kontroli to luka, a nie decyzja nośna, więc naprawa niczego nie łamie z założenia.
- **Blast radius.**
  - Pierwszy krok: `AdminController` (1 atrybut na klasie), `KasaOperacjaDodaj` (1 kontrola), 8 linii `return`.
  - Całość: 21 kopii w `PozyczkaLombardowaDAL`, 42 akcje POST, menu i JS (role).
  - Sprzężenie z K9: literały `{5,6}`.
- **Szkic ścieżki.**
  1. Rola `admin` na `AdminController`.
  2. Kontrola placówki w `KasaOperacjaDodaj` w tym samym kształcie co pozostałe 21.
  3. `return` w 8 kontrolach odczytu.
  4. Wydzielenie jednej polityki z 21 kopii, przepinanej metoda po metodzie.
  5. Opcjonalnie przeniesienie kontroli placówki na granicę web.
  Każdy krok da się wycofać rewertem, bez danych i schematu.
- **Pierwszy krok-prerekwizyt.** P1: projekt testowy z mockiem `IAppDbContext` i test „brak uprawnień → `code=1`” dla `KasaOperacjaDodaj`, plus test kontrolera `Admin` z fałszywą sesją.
- **Do rozstrzygnięcia poza kodem:**
  - co wolno roli 4 (WIZYTOR);
  - czy dostęp ogranicza coś poza aplikacją (Open Q5 raportu źródłowego);
  - czy istnieją użytkownicy, którzy dziś działają bez ról [U].

### #2 — K1: jeden protokół zapisu operacji kasowej

- **Obecny kształt.**
  - 7 ręcznie pisanych batchy T-SQL z tym samym szkieletem (`begin tran`, strażnik `@ostatniStanKasy`, rozliczenia, `update` kasy wartością absolutną, `commit`) w 3 wariantach ogona (A `THROW`, B `select errorCode`, C `THROW` bez `rollback`).
  - Dwa kontrakty błędu (wyjątek vs `out errorCode`).
  - Brak `XACT_ABORT` i lock hintów; 11× `where umoId` bez statusu.
  - Zapisy `KasaDodaj` i `ZastawyAktualizujUdzial` poza batchem.
- **Docelowy kształt.** Jeden wspólny szkielet transakcji kasowej:
  - jeden wariant strażnika z blokadą wiersza kasy;
  - przerwanie przy pierwszym błędzie (XACT_ABORT lub TRY/CATCH);
  - warunek na oczekiwany status w `update` umowy;
  - jeden kontrakt błędu do DAL.
  Część specyficzna dla akcji wchodzi jako fragment. Mechanizm optymistycznej kontroli stanu kasy **zostaje**, bo jest decyzją nośną (§2.1).
- **Dlaczego #2.**
  - Koszt długu jest wysoki: pieniądze, lost update salda [I], częściowy commit [I].
  - Zmiana jest zamknięta w 1 pliku i 1 warstwie, per metoda, bez zmian sygnatur w pierwszym kroku, wycofywalna bez migracji danych.
  - Nie jest #1, bo jedyna realna osłona to test integracyjny na SQL Server (P2), którego infrastruktura nie istnieje, a seed nie ma placówek, kasy ani uprawnień. Do tego niewiadome środowiskowe: RCSI, constrainty i triggery produkcji (Open Q1, Q2 raportu źródłowego).
- **Blast radius.**
  - Pierwszy krok: `AppDbContext.PozyczkaLombardowaAkcjaSplataCalkowita` (`:2122-2246`), 0 zmian sygnatur.
  - Całość: 7 metod w 3 kształtach, 11 klauzul `where`, 3 miejsca w DAL czytające `errorCode` (`DAL:1649`, `1885`, `2176`), 2 zapisy poza batchem.
  - Raporty kasy, sprzedaży i remanentu czytają te same kolumny, ale ich nie zapisują, więc nie są w zasięgu zmiany.
- **Szkic ścieżki.**
  1. Test charakteryzujący na 1 metodzie.
  2. Wzmocnienie T-SQL w tej metodzie: XACT_ABORT, blokada odczytu kasy, warunek statusu.
  3. To samo kolejno w pozostałych 2 metodach wariantu A, potem B (ujednolicenie kontraktu błędu z DAL), na końcu C.
  4. Wydzielenie wspólnego szkieletu, kiedy 7 metod ma już ten sam kształt.
  5. Wciągnięcie `KasaDodaj` i `ZastawyAktualizujUdzial` do batcha.
- **Pierwszy krok-prerekwizyt.** P2: lokalna baza z `CreateDB.sql` + `Initial.sql` + fixture placówki, kasy i uprawnień oraz test integracyjny: sukces, niezgodny stan → THROW 60000, dwie równoległe sesje.
- **Granica analizy.** Zmiana zapisu salda z wartości absolutnej na przyrost to decyzja modelu, poza tym refaktorem.

### #3 — K2: zapytania parametryzowane, wycinkami od najbardziej eksponowanych

- **Obecny kształt.** 114 zapytań sklejanych ze stringów. Działające API `params SqlParameter[]` jest nieużywane. Niewalidowane wejścia:
  - login 11×;
  - `LIKE '%…%'` w 33 liniach;
  - `order by` z nazwą kolumny z żądania 8×;
  - `UzytkownikOdczytaj` bez apostrofów (`:771`, `:776`).
- **Docelowy kształt.** Każda wartość z zewnątrz przechodzi przez `SqlParameter` w istniejącym API, nazwy kolumn sortowania przez białą listę, a `QueryLog` dostaje parametry (decyzja o roli audytu).
- **Dlaczego #3.**
  - Koszt długu jest wysoki (bezpieczeństwo), a wektor szerszy, niż podał raport źródłowy (§5).
  - Szew istnieje, więc nie trzeba nowej abstrakcji.
  - Koszt całości to XL: 114 wywołań w 3,9 tys. linii, każde do sprawdzenia ręcznie na SQL. Potrzebuje tej samej bazy co K1.
  - Nakłada się z K1 na 7 batchy kasowych. Lepiej robić go po K1, inaczej te same metody będą ruszane dwa razy.
  - Wycinki logowania, `LIKE` i `order by` są małe (S–M) i dałoby się je zrobić wcześniej, bo leżą poza batchami kasowymi.
- **Blast radius.**
  - Pierwszy wycinek: 11 zapytań w regionach UZYTKOWNIK i UZYTKOWNIK UPRAWNIENIE + 8× `order by` + 33 linie `LIKE`, wszystko w `AppDbContext.cs`, 0 zmian sygnatur `IAppDbContext`.
  - Całość: 114 wywołań, `QueryLog`, przy okazji TD20.
- **Szkic ścieżki.**
  1. Test charakteryzujący logowania.
  2. Parametryzacja 11 zapytań z loginem.
  3. Biała lista `sortColumn`/`sortOrder`.
  4. Parametryzacja `LIKE`.
  5. Region po regionie, a batche kasowe dopiero po K1.
  6. `QueryLog` z parametrami.
- **Pierwszy krok-prerekwizyt.** P2 (wspólny z K1) + test zapytania logowania (`UzytkownikDAL.Odczytaj`).

### Rozważone i odrzucone lub odroczone

| ID | Decyzja | Dlaczego |
| --- | --- | --- |
| K4 | **Odroczony: wymaga przeprojektowania pojęć biznesowych** | Reguły domenowe mają kilka sprzecznych definicji w C#, SQL i JS: termin sprzedaży 4×, mapowanie akcja → rozliczenie tylko w JS, „dzień biznesowy”. Przeniesienie kodu do BLL przeniosłoby sprzeczności. **Zatrzymuję się tutaj.** To przedmiot osobnej analizy domeny (np. `/10x-domain-distillation`) |
| K9 | **Odroczony: w przeważającej części pojęcia biznesowe** | Id `SL_*` to świadome stałe, a literały zastępują brakującą maszynę stanów umowy, regułę akcja → rozliczenia i uprawnienia operacji. Same nazwy stałych mają niską wartość. Wymiana 8 literałów `wplywNaStan` na wartość ze słownika to drobna poprawka zachowania zależna od zgodności seedu produkcyjnego [U], a nie refaktor |
| K5 | **Odrzucony na teraz** | Świadoma konwencja projektu. Kompilator chroni podział, więc koszt długu jest głównie poznawczy. Granica umowa \| kasa przecina batche K1 i wymaga decyzji o właścicielu operacji kasowej. Mechaniczny podział plików (partial, S) można zrobić w dowolnej chwili, ale nie zmienia ryzyka |
| K6 | **Nie jako cel, tylko jako prerekwizyt K4** | Przypadek, tani (S–M), niski koszt długu: UoW i `ETMSCoreBLL` są bezstanowe, podwójna rejestracja filtra jest no-op [I]. Wartość pojawia się dopiero wtedy, gdy BLL ma dostać DAL (K4) |
| K7 | **Odrzucony** | Podział X/Item/ListItem jest świadomy. `AssertConfigurationIsValid` to test, nie refaktor (S, można dodać przy P1). Deduplikacja wymaga rozdzielenia modelu zapisu od modelu odczytu w `Item`, czyli decyzji domenowej (zob. K4) |
| K8 | **Nie jako cel, tylko jako prerekwizyt testów dat** | Przypadek, tani (S–M), ale wartość daje głównie testowalność. „Dzień biznesowy” to część problemu K4 |

## Weryfikacja twierdzeń (ast-grep)

**Zakres.** Weryfikuję 45 twierdzeń strukturalnych, na których opiera się ranking: liczby metod, „robi X, ale nie Y”, liczności call-site'ów i pary lustrzanych typów. Dotyczą one K1–K9 w §0, §1, §3 i §6.

**Narzędzia.** `ast-grep` 0.50.0:

- `run -p` dla wzorców wywołań;
- `scan --inline-rules` / `-r` dla reguł z `kind`/`has`/`inside`/`follows`/`regex`, np. literały SQL wszystkich rodzajów (`string_literal`, `verbatim_string_literal`, `interpolated_string_expression`), atrybuty, parametry i metody bez danego wywołania.

**Zakres plików.** `eTMsDAL eTMsWEB eTMsBLL eTMsDB` bez `packages/`. Do każdego trafienia skrypt pomocniczy dopisuje metodę otaczającą. Widoki `.cshtml` są poza zasięgiem parsera, więc sprawdzam je grepem.

**Kontrola.** Każde zero i każdą rozbieżność sprawdzam klasycznym grepem.

**Commit:** `375c6fd`. Kod nie był zmieniany.

**Werdykty.** ✅ potwierdzone (30) · 🔶 doprecyzowane (13; w tym V33 i V35 oznaczone „✅ / 🔶”, bo część twierdzenia się potwierdziła, a część wymagała korekty) · ❌ obalone (2).

Korekty liczb, numerów linii i zakresów naniosłem w miejscu, w formacie „nowa (raport: stara)”. Sekcja §6 i werdykty §2.1 nie są zmienione.

### Tabela

| # | Twierdzenie (gdzie w raporcie) | Werdykt | Dowód (plik:linia) | Metoda (wzorzec / reguła) |
| --- | --- | --- | --- | --- |
| V1 | K3: filtr `{5,6}` skopiowany 21× w DAL pożyczek (§1 K3, §6 #1) | ✅ | 21 trafień w `PozyczkaLombardowaDAL.cs`, np. `:2431` (`UwagaDodaj`), wszystkie `{ 5, 6 }` | `appDbContext.UzytkownikUprawnienieLista($$$A).Where(x => new int[] { $$$S }.Contains(x.uprawnienieId))` |
| V2 | K3: odczyty list sprawdzają placówkę przez `PozyczkaLombardowaPlacowkaUprawninegoLista` 8× (§1 K3) | ✅ | `DAL:140`, `179`, `219`, `2905`, `2947`, `2990`, `3030`, `3078` (linie wywołań) | `appDbContext.PozyczkaLombardowaPlacowkaUprawninegoLista($$$)` |
| V3 | K3: 8 kontroli odczytu ustawia `code=1` bez `return`, a następna instrukcja nadpisuje `result.data` (§1 K3, §6 #1) | 🔶 linie | Linie `if`: `DAL:141`, `180`, `220`, `2906`, `2948`, `2991`, `3031`, `3079`. Raport podawał linie wywołań (2905…3078). 0/8 z `return`. Bezpośrednio po każdym `if`: `result.data = appDbContext…` (`:148`, `187`, `227`, `2913`, `2955`, `2998`, `3038`, `3086`) | reguła `kind: if_statement` + `has: {field: condition, regex: '^!placupr\.Contains'}`; wariant `has: return_statement` → 0; `kind: expression_statement` + `follows: <ten if>` → 8. Wzorzec `if (!placupr.Contains($X)) {…}` dawał ERROR node i 0 (fałszywe zero), sprawdzone grepem: 8 `if`, 0 `return` |
| V4 | K3: 0 `[Authorize]` w `eTMsWEB` (§1 K3) | ✅ | 0; grep `\[Authorize`: 0 | `kind: attribute`, `name ^Authorize` |
| V5 | K3: role są egzekwowane tylko w menu; kontrolery nie czytają ról ani `czyAdministrator` (§1 K3, §6 #1) | ✅ | `Session["zalogowanyRole"]`/`["zalogowanyUprawnienia"]` w C# tylko w filtrze (`CheckSessionAttribute.cs:72`, `86`, `127`, `141`, same zapisy). Odczyt ról: `_MyMenu.cshtml:17`, `_MySubMenu.cshtml:33` (grep). `czyAdministrator` w kontrolerach: 0 (grep; jedyne poza filtrem: `Views/Admin/UzytkownikEdycja.cshtml:68`, pole formularza) | `kind: element_access_expression` + `regex: zalogowanyRole\|zalogowanyUprawnienia`; grep dla `.cshtml` |
| V6 | K3: `zalogowanyUprawnienia` ma 2 zapisy i 0 odczytów (§1 K3) | ✅ | zapisy `CheckSessionAttribute.cs:72`, `:127`; odczyty 0 (C# i `.cshtml`, grep) | jak V5 |
| V7 | K3: błędny komunikat w `OnResultExecuting` (§1 K3, §5) | 🔶 linia | `throw (new Exception(resultUser.message))` w gałęzi `resultUserUpraw.code == 1`: **`CheckSessionAttribute.cs:125`** (raport: 124). Pozostałe `throw`: `:45`, `:70` (poprawny), `:99` | `throw (new Exception($R.message))` |
| V8 | K6/K3: filtr ma 2 konstruktory i 2 rejestracje; `new AppDbContext()` 2× (§1 K6, §3.1) | ✅ | konstruktory `CheckSessionAttribute.cs:20`, `:28`; `new CheckSessionAttribute(…)`: `FilterConfig.cs:13`, `ETMSWEBConfigure.cs:28`; `new AppDbContext()`: `CheckSessionAttribute.cs:22`, `UnitOfWorkDAL.cs:27` | `kind: constructor_declaration`; `new CheckSessionAttribute($$$)`; `new AppDbContext()` |
| V9 | K3: `KasaOperacjaDodaj` nie sprawdza uprawnień (§1 K3, §6 #1) | ✅ | `DAL:2715` dopasowana przez regułę „metoda bez wywołania”; grep `uprawn` w `DAL:2715-2850`: 0 | `kind: method_declaration`, `name ^KasaOperacjaDodaj$`, `not: has: appDbContext.UzytkownikUprawnienieLista($$$)` |
| V10 | K3: `DBWykonaj` wykonuje SQL z żądania przez UoW (§6 #1) | ✅ | `AdminController.cs:350` `unitOfWorkDAL.AppDbContext.GetDataSet(query)` | `$U.AppDbContext.GetDataSet($Q)` |
| V11 | K3: menu `"5,6"` dla KASA/UMOWY/REMANENT/SPRZEDAŻ, `"4,5,6"` dla KSIĄŻKA, 6 pozycji ADMIN z `"admin"` (§1 K3) | ✅ | `_MySubMenu.cshtml:17-28`; dodatkowo zakomentowana pozycja KREDYTY `"1,2,3"` (`:15`) | grep (Razor poza ast-grep) |
| V12 | K3: role wpisane po raz trzeci w JS, `uprs` w 5 plikach (§1 K3) | ✅ | `PozyczkaLombardowa.js:13`, `…Kasa.js:11`, `…KsiazkaLombardowa.js:13` (`[4,5,6]`), `…OperacjaSprzedazyZastawu.js:11`, `…Remanent.js:16`; grep: 5 plików | JS: `kind: pair`, `key regex uprs` |
| V13 | K3: 42 akcje POST w kontrolerze pożyczek (§3.1, §6 #1) | ✅ (+ nowa liczba) | 42 `[HttpPost]` w `PozyczkaLombardowaController.cs`; **`AdminController.cs`: 29 `[HttpPost]`**, wszystkie bez kontroli roli (V5) | `kind: attribute`, `name ^HttpPost$` |
| V14 | K1: 7 metod ze strażnikiem `@ostatniStanKasy` (§1 K1, §6 #2) | ✅ | `AppDbContext.cs:1951` Wyplac, `2046` Rezygnuj, `2130` SplataCalkowita, `2262` SplataCzesciowa, `2506` Dopozyczenie, `2751` Sprzedaz, `3302` KasaOperacjaDodaj | literał (3 rodzaje) + `regex: 'if @ostatniStanKasy'` |
| V15 | K1: wariant strażnika wynika z sygnatury; `out errorCode` ma 3 metody ze strażnikiem i 3 bez (§1 K1, §2.1) | ✅ | z `out int errorCode`: `:2249`, `:2493`, `:2740` (B) oraz `:2901` Przenies, `:3042` Przyjmij, `:3066` Kopiuj (bez strażnika). Bez `out`: `:1945`, `:2039`, `:2122` (A) | `kind: method_declaration` + `has: parameter regex 'out int errorCode'` |
| V16 | K1: A idzie przez `ExecuteNonQuery`, B i C przez `GetDataSet` (§1 K1) | ✅ | A: `:2011`, `:2119`, `:2246` `ExecuteNonQuery(query)`; B: `:2483`, `:2730`, `:2892` `GetDataSet(query)`; C: `:3358` `GetDataSet(query)` | `ExecuteNonQuery($Q)` / `GetDataSet($Q)` + metoda otaczająca |
| V17 | K1: DAL przepisuje `errorCode` do `result.code` w `:1649`, `1885`, `2176` (§1 K1, §6 #2) | 🔶 zakres | Te 3 dla B potwierdzone. Ten sam kształt jest też w `:2240` (AkcjaPrzenies), `:2287` (AkcjaPrzyjmij), `:2347` (AkcjaKopiuj): razem 6 | `result.code = $V` z filtrem trafień na `errorCode` (wzorzec z literalnym `errorCode` dopasował też `= 1`; odfiltrowane) |
| V18 | K1: `insert …_KASA_OPERACJE_ROZLICZENIA` 14×, `insert …_KASA_OPERACJE (` 9×, `update …_KASA set` 7× (§1 K1) | ✅ | 14 / 9 / 7, wszystkie w 7 metodach z V14 (np. rozliczenia `:1988`…`:3333`; operacje `:1961`…`:3304`; kasa `:2005`, `2113`, `2240`, `2330`, `2721`, `2883`, `3351`); grep: 14 / 9 / 7 | literał + `regex` per fragment |
| V19 | K1: w SplataCalkowita blok rozliczenia występuje 3× (7/8/9), w Sprzedaz 4× (§1 K1) | ✅ | SplataCalkowita `:2170`, `2189`, `2209`; Sprzedaz `:2791`, `2811`, `2832`, `2853` | jak V18 |
| V20 | K1: liczba parametrów: Wyplac 5, SplataCalkowita 8, SplataCzesciowa/Dopozyczenie 13 (§1 K1) | ✅ (+ nowe liczby) | Wyplac 5, Rezygnuj 6, SplataCalkowita 8, SplataCzesciowa 13, Dopozyczenie 13, **Sprzedaz 11**, **KasaOperacjaDodaj 4**; długości metod: 68, 82, 126, 243, 246, 160, 70 linii | `kind: parameter` + `inside: method_declaration name` |
| V21 | K1/K4: `AppDbContext` i `PozyczkaLombardowaDAL` nie mają metod prywatnych (§1 K1, K4) | ✅ | jedyna niepubliczna metoda: `protected virtual void Dispose(bool)` (`AppDbContext.cs:3859`, `DAL:3148`) | `kind: method_declaration`, `not: has: modifier ^public$` |
| V22 | K1: `KasaOperacjaDodaj` zapisuje kasę poza batchem (§1 K1, §6 #2) | 🔶 linia + warunek | 1 wywołanie `appDbContext.PozyczkaLombardowaKasaDodaj(kasaPlacowki)` w **`DAL:2773`** (raport: 2761-2772), tylko gdy `kasaPlacowki == null` (`:2763`), czyli przy pierwszej operacji w placówce | `appDbContext.PozyczkaLombardowaKasaDodaj($$$)` |
| V23 | K1: `ZastawyAktualizujUdzial` zapisuje poza batchem „po zapisie innych akcji” (`DAL:458`, `533`, `801`, `854`) (§1 K1, §6 #2) | 🔶 kontekst | 4 wywołania potwierdzone, ale wywołujący to `UmowaDodaj` (`:458`), `MetrykaZmien` (`:533`), `ZastawDodaj` (`:801`), `ZastawUsun` (`:854`): **żaden nie jest przepływem kasowym z V14**. Wywołań z kontrolera: 0 | `ZastawyAktualizujUdzial($$$)` (bez kwalifikatora) + metoda otaczająca |
| V24 | K1: 11 `… where umoId` w `PozyczkaLombardowaAkcja*` bez warunku na status (§0, §6 #2) | ✅ | 11 literałów, 0 z `statusId` (pełne linie w raporcie źródłowym, S20) | literał `regex: 'where +umoId'` + `inside: method_declaration ^PozyczkaLombardowaAkcja` |
| V25 | K2: 114 wywołań = 85 `GetDataSet` + 29 `ExecuteNonQuery`, 0 z parametrami, 0 `new SqlParameter` (§1 K2, §6 #3) | ✅ | 85 / 29 / 0 / 0 / 0; grep: 85, 29, 0 | `GetDataSet($A)` vs `GetDataSet($A, $$$B)`; to samo dla `ExecuteNonQuery`; `new SqlParameter($$$)` |
| V26 | K2: API parametrów działa end-to-end (`AddRange`) (§1 K2) | ✅ | `AppDbContextBase.cs:76`, `:96` `cmd.Parameters.AddRange(sqlPrms)` | `$C.Parameters.AddRange($P)` |
| V27 | K2: `QueryLog` dostaje tylko tekst zapytania (§1 K2) | ✅ | `AppDbContextBase.cs:71`, `:91` `QueryLog(query)` | `QueryLog($$$)` |
| V28 | K2: 33 linie `like '%" + …` bez escapowania (§1 K2, §5, §6 #3) | 🔶 jednostka | 33 linie = **43 literały** (część linii ma dwa `LIKE`); 0 linii z `Replace` (grep) | literał `regex: "like .%"` → 43; grep `like '%" +`: 33 linie / 43 wystąpienia |
| V29 | K2: 8× `" order by " + sortColumn`, `sortColumn` z akcji pożyczek (§1 K2, §6 #3) | 🔶 zasięg | 8 konkatenacji: `AppDbContext.cs:136`, `360`, `408`, `603`, `736`, `1371`, `3605`, `3845`. `string sortColumn` jest parametrem **6 akcji w 3 kontrolerach**: `PozyczkaLombardowaController.cs:109`, `:430`, `:674`, `KlientController.cs:47`, `AdminController.cs:45`, `:212` (raport: tylko pożyczki, `:109`, `:115`, `:430`; `:115` to przekazanie, nie parametr) | `kind: identifier ^sortColumn$` + `inside: binary_expression has literal 'order by'`; `kind: parameter regex 'string sortColumn'`; grep: 8 |
| V30 | K2: login doklejany 11× w „regionach UZYTKOWNIK i UZYTKOWNIK UPRAWNIENIE” (§3.1, §6 #3) | 🔶 regiony | 11 potwierdzone (grep; ast-grep 8 + 3 verbatim/`ktoPyta`, jak S26 raportu źródłowego). Regiony: PALCOWKA `:431`; UZYTKOWNIK `:754`, `872`, `882`; UZYTKOWNIK UPRAWNIENIE `:903`, `943`, `965`, `987`, `1050`, `1053`; POZYCZKA LOMBARDOWA `:1169` | grep + mapowanie linii na `#region` |
| V31 | K2: 62 linie `if (…) query +=` (§1 K2) | ❌ | **77** instrukcji `if` z konsekwencją `query += …` bez klamer. 62 to tylko te w jednej linii; grep/awk: 62 jednoliniowe + 16 z `query +=` w następnej linii = 78 (±1 przez `else if`) | `kind: if_statement` + `has: {field: consequence, regex: '^query \+='}` |
| V32 | K2: 19 pętli `foreach` doklejających fragmenty (§1 K2) | 🔶 | **18** `foreach` zawiera `query +=`; 19 to wszystkie `foreach` w pliku | `kind: foreach_statement` + `has: regex 'query \+=' stopBy: end`; grep `foreach`: 19 |
| V33 | K2: 226× `ToString("F2")`, 151× `yyyy-MM-dd` (§1 K2) | ✅ / 🔶 | F2: 226 (ast-grep = grep). `yyyy-MM-dd`: 151 wystąpień tekstu formatu, w tym 58 dokładnie `$X.ToString("yyyy-MM-dd")`; reszta to formaty z czasem lub inne konteksty | `$X.ToString("F2")`; `$X.ToString("yyyy-MM-dd")`; grep |
| V34 | K4: BLL wołany 3× (wszystkie `Wylicz`), 52 wywołania kontroler → DAL (§1 K4, §3.1) | ✅ | `PozyczkaLombardowaController.cs:158`, `166`, `180`; 52 `unitOfWorkDAL.PozyczkiLombardowe.$M(…)` | `unitOfWorkBLL.$P.$M($$$)`; `unitOfWorkDAL.PozyczkiLombardowe.$M($$$)` |
| V35 | K4: reguła „po terminie / do sprzedaży” ma 4 niespójne definicje (§1 K4) | ✅ / 🔶 linie | encja `PozyczkaLombardowaItem.cs:29`, `:31` (`dataZwrotu.AddDays(1) <= DateTime.Now`); SQL: **9 literałów** `dateadd(DAY,1,dataZwrotu) <= GETDATE()` w `:1225`, `1267-1268`, `1290`, `1334-1335`, `1357`, `1395-1396` (raport: 6 linii); Wycofaj `DAL:2372` (`>= DateTime.Today`); remanent `AppDbContext.cs:3628` (`dataZwrotu > GETDATE()`); grep SQL: 9 | literał `regex: (?i)dateadd\(day, *1, *(\w+\.)?dataZwrotu\) *<= *getdate`; `$X.dataZwrotu.AddDays(1) >= DateTime.Today`; `dataZwrotu.AddDays(1) <= DateTime.Now` |
| V36 | K4: VAT „w stu” 6×, `Math.Round` 20× (§1 K4) | ✅ | VAT: `DAL:1356`, `1376` (SplataCalkowita), `2085`, `2116`, `2138` (Sprzedaz), `2806` (KasaOperacjaDodaj); `Math.Round`: 20 (= grep) | `Math.Round(($K * $S) / (100m + $T), 2)`. Pierwsza wersja bez nawiasu wokół iloczynu dała 0 (fałszywe zero); grep: 6 |
| V37 | K5: 8 regionów, region pożyczek `:1063-3854` z 63 metodami publicznymi, plik 3876 l. (§1 K5) | ✅ | `#region` w `:12`, `54`, `98`, `373`, `616`, `702`, `890`, `1063`; 63 ze 112 metod publicznych w `1063-3854`; `wc -l` 3876 | `kind: method_declaration` + `modifier ^public$`, filtr po zakresie linii; grep `#region` |
| V38 | K5: `IAppDbContext` 111 metod, `IPozyczkaLombardowaDAL` 46 (§1 K5, §3.0) | ✅ | 111 (grep `);`: 111); 46 | `kind: method_declaration` + `inside: interface_declaration` |
| V39 | K5: segregacja `IAppDbContext` dotyka „17 konsumentów” (§3.1) | ❌ | **10 typów-konsumentów**: 8 klas DAL (`: RepoBase`), `UnitOfWorkDAL` (+ `IUnitOfWorkDAL.AppDbContext`), `CheckSessionAttribute`. 17 to liczba wystąpień pól i parametrów (ast-grep: 19 deklaracji w `eTMsDAL`) | `kind: parameter\|field_declaration\|property_declaration` + `has: identifier ^IAppDbContext$`; `kind: class_declaration` + `base_list regex RepoBase` → 8; grep `-lw IAppDbContext` |
| V40 | K6: `new ETMSCoreBLL()` 94× w 5 kontrolerach, wyłącznie do `ObjectToJson` (§0, §1 K6, §3.1) | 🔶 liczba | **93** w 5 kontrolerach (Admin 29, Home 12, Klient 6, Pożyczki 44, Szablon 2); 94-te to `UnitOfWorkBLL.cs:18` (poza kontrolerami); grep: 94 / 93. Wszystkie 93 to `(new ETMSCoreBLL()).ObjectToJson(…)` | `new ETMSCoreBLL()`; `(new ETMSCoreBLL()).ObjectToJson($$$)`. Wzorzec bez nawiasu `new ETMSCoreBLL().$M` dawał 0 (fałszywe zero) |
| V41 | K7: pary lustrzane: `Item` (78) ⊇ `PozyczkaLombardowa` (49), `ListItem` (22) ⊂ `Item`, `ZastawItem` = `Zastaw` + 2, `OperacjaSprzedazyZastawItem` ma 3 wspólne pola (§1 K7) | 🔶 liczba | `PozyczkaLombardowaItem`: **79** właściwości (raport: 78); 49/49 wspólne z `PozyczkaLombardowa` (+30); `ListItem` 22/22 ⊂ `Item`; `ZastawItem` 21 = `Zastaw` 19 + `dataNabycia`, `typNazwa`; `OperacjaSprzedazyZastawItem` 28, wspólne z `ZastawItem`: 3. Relacje nad- i podzbioru potwierdzone; żadna klasa nie dziedziczy | `kind: property_declaration` + `inside: class_declaration`, nazwy porównane `comm`; grep (CRLF-safe): 49 / 79 / 22 / 19 / 21 / 28 |
| V42 | K7: AutoMapper wołany 6×, mapa `PozyczkaLombardowa→Item` bez użyć, klon `Item→Item` w `DAL:2222`; 10 `CreateMap` (§1 K7) | ✅ | `Mapper.Map<T>`: `KlientDAL.cs:141`, `PlacowkaDAL.cs:125`, `:178`, `UzytkownikDAL.cs:177`, `UzytkownikUprawnienieDAL.cs:125`; `Mapper.Map<S,T>`: `DAL:2222` (AkcjaPrzenies); `CreateMap` 10 (`AutoMapperDAL.cs:11-20`); grep `Mapper.Map`: 6 | `Mapper.Map<$T>($$$)`, `Mapper.Map<$S, $T>($$$)`, `$C.CreateMap<$A, $B>()` |
| V43 | K8: `DateTime.Now` 36× w DAL pożyczek: 7 walidacji, 2 numery umowy, 27 znaczników; 1× `Today`; 18× `GETDATE()`; 0 abstrakcji czasu (§1 K8) | ✅ | 36 (= grep); walidacje `$X.Date > DateTime.Now.Date`: `DAL:370`, `487`, `1093`, `1226`, `1440`, `1670`, `1904`; numery `DateTime.Now.ToString($F)`: `:387`, `:2324`; reszta 27; `DateTime.Today` 1 (`:2372`); `GETDATE()` 18 (= grep); `IClock\|ITimeProvider\|SystemTime\|DateTimeProvider`: 0 (grep) | `DateTime.Now`; `$X.Date > DateTime.Now.Date`; `DateTime.Now.ToString($F)`; literał `regex: (?i)getdate\(\)` |
| V44 | K9: 0 `enum` i 0 `const` w C# (§1 K9, §3.1) | ✅ | 0; grep `-w enum\|const`: 0 | `kind: enum_declaration` / `field_declaration` lub `local_declaration_statement` z `modifier ^const$` |
| V45 | K9: `wplywNaStan` literałem 8×, ze słownika 6×; `AkcjaWyplac` czyta słownik do walidacji, a zapisuje literał (§2.1 K9) | ✅ | literały: `DAL:1070` (−1), `1188`, `1340`, `1529`, `1634` (−1), `1765`, `1870` (−1), `2056`; słownik: `:1359`, `1379`, `2086`, `2117`, `2139`, `2811`; odczyt `rozliczenie3.wplywNaStan` w AkcjaWyplac `:1034` | `$O.wplywNaStan = $V`; `kind: member_access_expression regex wplywNaStan$` + `inside: method_declaration ^AkcjaWyplac$` |

### Fałszywe zera i rozjazdy narzędzia

Cztery zera wynikały z błędu wzorca, a nie z kodu. Wszystkie wyłapała kontrola grepem:

- `if (…) { … }` jako wzorzec C# parsuje się z węzłem ERROR (V3), więc trzeba użyć reguły `kind: if_statement`;
- iloczyn w nawiasie `Math.Round((a * b) / …)` nie pasuje do `Math.Round($K * $S / …)` (V36);
- `(new ETMSCoreBLL()).M()` nie pasuje do `new ETMSCoreBLL().$M()` (V40);
- `result.code = errorCode` dopasował także `= 1` (V17), więc wynik wymagał filtra.

Dodatkowo regexy w `--inline-rules` wymagały pliku reguły zamiast podwójnego escapowania w powłoce.

### Wpływ na ranking — do decyzji na etapie planowania

Żaden wynik nie obala pozycji #1–#3 ani odrzuceń. Poniższe rozbieżności dotyczą treści §6, której ta weryfikacja nie zmienia:

1. **§6 #2 (K1), blast radius i krok 5** — *do decyzji na etapie planowania.* §6 #2 wymienia „2 zapisy poza batchem” i proponuje „wciągnięcie `KasaDodaj` i `ZastawyAktualizujUdzial` do batcha”. Według V23 `ZastawyAktualizujUdzial` wołają wyłącznie `UmowaDodaj`, `MetrykaZmien`, `ZastawDodaj` i `ZastawUsun`, a żaden z tych przepływów nie zmienia stanu kasy. Jego atomowość to osobny problem, a nie część protokołu operacji kasowej. Według V22 `KasaDodaj` działa tylko przy pierwszej operacji w placówce. Zakres K1 jest więc węższy niż w §6 #2, a pozycja K1 się nie zmienia.
2. **§6 #3 (K2), pierwszy wycinek** — *do decyzji na etapie planowania.* §6 #3 umieszcza 11 zapytań z loginem w „regionach UZYTKOWNIK i UZYTKOWNIK UPRAWNIENIE”. Według V30 2 z 11 leżą gdzie indziej: `:431` w PALCOWKA i `:1169` w POZYCZKA LOMBARDOWA. Według V29 biała lista `sortColumn` dotyczy 6 akcji w 3 kontrolerach, a nie tylko pożyczek. Według V31 warunkowe budowanie SQL obejmuje 77, a nie 62 instrukcje. Koszt całości (XL) pozostaje adekwatny; wycinek „login + `order by`” rozlewa się na więcej regionów i kontrolerów, niż opisuje §6 #3.
3. **§6 #1 (K3)** — *do decyzji na etapie planowania.* V13 dodaje liczbę: `AdminController` ma 29 akcji POST bez kontroli roli. To wzmacnia uzasadnienie #1 i nie zmienia pierwszego kroku (1 atrybut na klasie).
4. **K5 (odrzucony)** — *do decyzji na etapie planowania.* V39: segregacja `IAppDbContext` dotyka 10 typów, a nie 17, więc koszt „L” w §3.1 może być zawyżony. Odrzucenie K5 w §6 opiera się na intencjonalności i granicy biznesowej umowa | kasa, a nie na tej liczbie.
5. **K6, K7 (odrzucone / prerekwizyt):** V40 (93 zamiast 94) i V41 (79 zamiast 78) nie zmieniają uzasadnienia.

## Odnośniki do kodu (najważniejsze)

- `eTMsWEB/Filters/CheckSessionAttribute.cs:20-34`, `:72-86`, `:120-127` — dwa konstruktory, role w sesji, błąd komunikatu
- `eTMsWEB/Views/Shared/_MySubMenu.cshtml:15-28`, `_MyMenu.cshtml:4-7` — role egzekwowane tylko w menu
- `eTMsWEB/Controllers/AdminController.cs:340-364` — `DBWykonaj`
- `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs:141-151`, `:179-190` — kontrola bez `return`; `:2715-2850` — `KasaOperacjaDodaj`; `:1220-1431` — spłata całkowita
- `eTMsDB/REPOSITORIES/AppDbContext.cs:1945`, `2039`, `2122`, `2249`, `2493`, `2740`, `3294` — 7 zapisów kasowych; `:1364-1371` — `LIKE` i `order by`
- `eTMsDB/REPOSITORIES/AppDbContextBase.cs:69-127` — I/O, parametry, `QueryLog`
- `eTMsDAL/REPOSITORY/UnitOfWorkDAL.cs:27-111`, `eTMsBLL/UnitOfWorkBLL.cs:18-27`, `eTMsWEB/ETMSWEBConfigure.cs:22-29` — okablowanie
- `eTMsDB/ENTITIES/PozyczkaLombardowaItem.cs:29-31` — reguła wyliczana z `DateTime.Now`
- `eTMsWEB/Scripts/PozyczkaLombardowa/PozyczkaLombardowaKasa.js:156-184` — reguła akcja → rozliczenie tylko w JS
- `eTMsDATABASE/scripts/Initial.sql:3`, `:465`, `:497-507` — seed, jawne hasło ADMIN, role
- `git show 42292b7:eTMsWEB/Properties/PublishProfiles/LOKAL.pubxml.user` — ślad EF i dawnych modułów

## Wnioski architektoniczne

- **Konwencje świadome.** Granica modułu produktowego, BLL jako kalkulator, rodziny X/Item/ListItem, stałe id słowników, optymistyczna kontrola stanu kasy. Refaktor powinien je zachować albo zmieniać jawnie.
- **Złożoność przypadkowa.** Jej źródło to głównie przejście EF → ADO.NET i przerwana migracja do DI: SQL w stringach, nieużywane parametry, warianty strażnika, `new` w UoW, ciche mapowanie.
- **Najważniejsze luki nie są decyzjami.** Brak serwerowej kontroli ról i brak atomowości nie mają żadnego śladu intencji.

## Kontekst historyczny (z wcześniejszych zmian)

- `context/changes/post-flow-analysis/research.md` to raport źródłowy. Jego ustalenia zostały przyjęte. Dwa zostały **rozszerzone**:
  - TD13/U3: wektor wstrzyknięcia to nie tylko login, ale też `LIKE` i `order by` (§5);
  - TD15: luka obejmuje cały `AdminController`, a nie tylko `DBWykonaj`; dochodzi 8 kontroli bez `return`.

  Żadne ustalenie nie zostało obalone.
- `context/map/repo-map.md`:
  - Watch 3 („uprawnienia tylko w menu”) **potwierdzone** dla całego `AdminController`;
  - założenie „rozwój trwa gdzie indziej / kod sprzed 2020 niewidoczny” **częściowo rozjaśnione** przez historię publikacji (§2.0).
- `context/archive/` jest pusty.

## Powiązane badania

- `context/changes/post-flow-analysis/research.md`

## Otwarte pytania [U]

1. Czy dostęp do `AdminController` ogranicza coś poza aplikacją (IIS, sieć)? Od tego zależy pilność #1.
2. Co wolno roli 4 (WIZYTOR), np. czy może czytać szczegóły umowy? Decyzja właściciela produktu.
3. Czy JS pokazuje `data`, gdy `code=1` (kontrole bez `return`)?
4. Poziom izolacji (RCSI), constrainty i triggery produkcji. Wpływają na K1.
5. Zgodność seedu produkcyjnego z `Initial.sql` (K9, `wplywNaStan`).
6. Toolchain testowy dla net452: czy na maszynie buildu jest Developer Pack 4.7.2+?
7. Czy log zapytań (`TB_QUERY_LOG`) jest gdzieś używany jako audyt? Decyzja przed K2.
8. Czy `publishTime` w `.pubxml.user` to czas modyfikacji źródła? Od tego zależy pewność osi czasu z §2.0.
