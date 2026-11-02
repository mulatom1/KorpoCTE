# Autoryzacja: jedna polityka w DAL i rola admin na granicy web — plan wdrożenia

## Overview

Realizujemy pozycję #1 rankingu z `research.md` (K3) oraz jej prerekwizyt P1.

1. Najpierw powstaje osłona: build działa na oryginalnym TFM i istnieje projekt testowy z mockiem `IAppDbContext`.
2. Potem 21 ręcznych kopii kontroli zapisu i 8 kontroli odczytu w `PozyczkaLombardowaDAL` przechodzi na jedną politykę uprawnień, bez zmiany zachowania.
3. Następnie domykamy potwierdzone luki w DAL.
4. Na końcu egzekwujemy rolę `admin` na granicy web.

Pozycje #2 (K1, protokół zapisu kasowego) i #3 (K2, parametryzacja SQL) to osobne, późniejsze zmiany.

## Current State Analysis

- **Build nie działa na tej maszynie bez obejścia.** `MSBuild eTMsLomb.sln` kończy się błędem MSB3644: brak pakietu referencyjnego .NET 4.5.2 (projekty C#) i 4.5 (`eTMsDATABASE.sqlproj`). Sprawdzone 2026-10-14 na VS 2026 18.10.
  - Wszystkie 5 projektów buduje się bez błędów, gdy `TargetFrameworkRootPath` wskazuje pakiety NuGet `Microsoft.NETFramework.ReferenceAssemblies.net452` / `.net45` 1.0.3. Sprawdzone w osobnym katalogu, bez zmian w repo.
  - Na maszynie są: dotnet SDK 10.0.401, LocalDB, `sqlcmd`, pakiety referencyjne 4.7.2/4.8.
- **Testy i CI.** Nie ma żadnych testów ani CI (`research.md` §3.0).
  - `PozyczkaLombardowaDAL(IAppDbContext)` (`eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs:17`) da się testować z mockiem.
  - BLL nie ma zależności.
- **Kontrole zapisu: 21 kopii jednego bloku.** Kształt: `UzytkownikUprawnienieLista(login, placowkaId).Where({5,6})`, potem pusta lista → `code=1`, komunikat „Nie masz odpowiednich uprawnień!” i `return` (V1 w research).
  - Login zawsze pochodzi z `Session["zalogowanyLogin"]`.
  - `placowkaId` w 20 metodach pochodzi z umowy odczytanej z bazy, w `UmowaDodaj` (`DAL:379`) z żądania.
- **Kontrole odczytu: 8 bloków `PozyczkaLombardowaPlacowkaUprawninegoLista(ktoPyta).Contains(placowkaId)`.** Dotyczą ról 4,5,6 (`AppDbContext.cs:1162-1183`). Linie `if`: `DAL:141`, `180`, `220`, `2906`, `2948`, `2991`, `3031`, `3079`.
  - Ustawiają `code=1` bez `return`, więc zapytanie nadpisuje `result.data`, a `ObjectToJson` (`eTMsBLL/REPOSITORY/ETMSCoreBLL.cs:16-25`) wysyła pełne dane w JSON.
  - JS pokazuje listę tylko przy `code == 0`. Ścieżki wydruków rzucają wyjątek przy `code == 1`.
- **Luki bez kontroli** (sprawdzone w kodzie):
  - `KasaOperacjaDodaj` (`DAL:2715`) przyjmuje `operacja.placowkaId` z żądania i zapisuje operację kasową;
  - `KasaOdczytaj(int placowkaId)` (`DAL:2696`, `IPozyczkaLombardowaDAL.cs:126`, `PozyczkaLombardowaController.cs:408`) nie ma nawet loginu;
  - `RemanentSyntetyka(ktoPyta, placowkaId)` (`DAL:3098`) nie sprawdza placówki.
- **Granica web.**
  - 0 `[Authorize]`.
  - Role trafiają do `Session["zalogowanyRole"]` (`List<string>`: `"admin"` z `czyAdministrator` oraz `uprawnienieId`) w `CheckSessionAttribute.OnActionExecuting` (`eTMsWEB/Filters/CheckSessionAttribute.cs:74-86`), czyli w globalnym filtrze akcji (`FilterConfig.cs:13`).
  - Czyta je tylko menu (`_MyMenu.cshtml:17`, `_MySubMenu.cshtml:33`).
  - `AdminController` (29 POST + 8 GET) jest wołany wyłącznie z `Views/Admin/*` i `Scripts/Admin/*`.
  - Funkcje admina poza nim: `PozyczkaLombardowaZastawTypDodaj` (`PozyczkaLombardowaController.cs:75`) i `…Usun` (`:84`). `…ZastawTypLista` (`:67`) jest współdzielona z formularzem umowy (`Scripts/PozyczkaLombardowa/PozyczkaLombardowa.js:1127`).

## Desired End State

- **Build i testy.** `MSBuild eTMsLomb.sln -restore` buduje wszystkie projekty na ich oryginalnych TFM bez ręcznych parametrów. `vstest.console.exe eTMsTESTS\bin\Debug\net48\eTMsTESTS.dll` uruchamia zielony zestaw testów (`dotnet test` nie działa: eTMsWEB importuje `Microsoft.WebApplication.targets`, którego nie ma w dotnet SDK, MSB4019). Jeden skrypt `verify.ps1` wykonuje restore → build → test.
- **Jedna polityka w DAL.** Każda kontrola uprawnień w `PozyczkaLombardowaDAL` przechodzi przez jedną klasę `PolitykaUprawnien` z dwiema regułami:
  - zapis: rola ∈ {5,6} w placówce;
  - odczyt: rola ∈ {4,5,6} w placówce.

  W DAL nie zostaje żadna kopia inline.
- **Kontrole odczytu.** Odmowa kończy metodę: `code=1`, pusta lista lub `0`, bez danych w JSON.
- **Domknięte luki.**
  - `KasaOperacjaDodaj` wymaga zapisu w `operacja.placowkaId`;
  - `KasaOdczytaj` i `RemanentSyntetyka` wymagają odczytu w `placowkaId`.
- **Granica web.**
  - Każda akcja `AdminController` oraz `PozyczkaLombardowaZastawTypDodaj`/`Usun` wymaga roli `admin`.
  - Odmowa dla POST to JSON `{code:1, message}` w kontrakcie `Result`. Odmowa dla GET to HTTP 403.
  - `PozyczkaLombardowaZastawTypLista` i wszystkie akcje `Home`/`Szablon` pozostają dostępne dla każdego zalogowanego.
- **Bez wpływu na uprawnionych.** Użytkownicy z rolami zgodnymi z modelem (5/6 do zapisu, 4/5/6 do odczytu w danej placówce, `admin` dla administracji) nie widzą żadnej różnicy.

### Key Discoveries:

- `CheckSessionAttribute` wypełnia sesję w `OnActionExecuting` jako filtr globalny (`FilterConfig.cs:13`). Rolę trzeba więc sprawdzać filtrem akcji o zakresie kontrolera/akcji, który biegnie po filtrze globalnym. Filtr autoryzacji (`IAuthorizationFilter`) biegnie wcześniej i przy pierwszym żądaniu po zalogowaniu widziałby pustą sesję.
- Wszystkie wywołania AJAX używają `dataType: 'json'` i obsługują `result.code != 0` przez `AlertWarningShow` (np. `Scripts/Admin/DBIndex.js:10-21`). Odmowa w kształcie `Result` jest więc obsłużona przez istniejące UI.
- `KasaOdczytaj` z DAL ma jednego wywołującego, kontroler (`PozyczkaLombardowaController.cs:410`). Wewnętrzne przepływy kasowe wołają `appDbContext.PozyczkaLombardowaKasaOdczytaj` bezpośrednio (`DAL:943`, `1020`, …). Zmiana sygnatury DAL nie dotyka przepływów kasowych.
- Projekty C# używają starego formatu csproj z jawnymi `<Compile Include>` (np. `eTMsDAL/eTMsDAL.csproj:93`, `eTMsWEB/eTMsWEB.csproj:124`). Każdy nowy plik `.cs` trzeba dopisać do csproj.
- Pakiety: `Microsoft.AspNet.Mvc.5.2.3`, `Newtonsoft.Json.12.0.2`, `AutoMapper.7.0.1`, `Autofac.4.9.2` (vendored w `packages/`).

## What We're NOT Doing

- **K1 i K2.** Protokół zapisu operacji kasowej (XACT_ABORT, blokady, `statusId` w `where`) oraz parametryzacja SQL i biała lista `sortColumn` to osobne zmiany.
- **P2.** Lokalna baza testowa na LocalDB nie powstaje.
- **Placówka docelowa w `AkcjaPrzenies`.** Kontrola uprawnień w placówce docelowej to zmiana reguły biznesowej. Dziś przyjmujący jest sprawdzany w `AkcjaPrzyjmij`.
- **`Odczytaj(umoId)`.** Kontrola placówki przy odczycie szczegółów umowy nie wchodzi do tej zmiany.
- **Model ról.** Nie przenosimy kontroli placówki na granicę web (polityka per zasób) i nie zmieniamy modelu ról w sesji (`zalogowanyRole` dalej bez wymiaru placówki).
- **Rozjazd menu i serwera.** Nie wyrównujemy ról serwera do menu. WIZYTOR (4) zachowuje odczyt list, który ma dziś.
- **Pozostałe kontrolery i DAL-e.** `HomeController`, `KlientController`, `SzablonController` oraz DAL-e inne niż `PozyczkaLombardowaDAL` zostają bez zmian.
- **Szersze K9.** Poza dwiema nazwanymi listami ról w polityce nie zamieniamy literałów słownikowych na stałe.
- **K5, K6, K7.** Bez retargetu na .NET 4.8, bez CI, bez refaktoru UoW/DI i bez `AssertConfigurationIsValid`.
- **Kosmetyka.** Nie poprawiamy komunikatu w `CheckSessionAttribute.cs:125` ani podwójnej rejestracji filtra.

## Implementation Approach

Kolejność to osłona → refaktor bez zmiany zachowania → zmiany zachowania. Każdą fazę da się wycofać rewertem, bez danych i bez schematu.

Polityka to zwykła klasa w `eTMsDAL`, tworzona w `PozyczkaLombardowaDAL` z tym samym `IAppDbContext`, bez zmian w DI. Dzięki temu testy obejmują ją razem z DAL przez jeden mock. Reguły używają tych samych zapytań co dziś: zapis przez `UzytkownikUprawnienieLista`, odczyt przez `PozyczkaLombardowaPlacowkaUprawninegoLista`. To gwarantuje identyczną semantykę.

Strukturalne kryteria sukcesu sprawdza `ast-grep` (zainstalowany lokalnie, 0.50.0). Wzorce są podane w kryteriach.

## Critical Implementation Details

**Kolejność importu MSBuild.** Stary csproj importuje `Directory.Build.props` przed ustawieniem `TargetFrameworkVersion` w treści projektu. Warunek na TFM musi więc żyć w `Directory.Build.targets`, który jest importowany po treści projektu. Pakiety referencyjne musi pobrać restore projektu SDK-style, zanim zbudują się projekty `net452`/`net45`, dlatego build idzie przez `MSBuild … -restore`.

**Kolejność filtrów.** `WymagaRoliAttribute` musi być filtrem akcji (`ActionFilterAttribute`) z domyślnym `Order`, tak aby biegł po globalnym `CheckSessionAttribute`. Faza 4 weryfikuje ręcznie pierwsze żądanie admina po świeżym zalogowaniu.

## Phase 1: Build na oryginalnym TFM i projekt testowy (P1)

### Overview

Solucja buduje się bez ręcznych parametrów, a nowy projekt testowy ma pierwsze zielone testy. Projekt testowy służy też jako źródło restore pakietów referencyjnych.

### Changes Required:

#### 1. Pakiety referencyjne dla starych projektów

**File**: `Directory.Build.targets` (nowy, w katalogu głównym repo)

**Intent**: Gdy na maszynie nie ma pełnego pakietu deweloperskiego dla TFM projektu, wskaż MSBuild-owi pakiety referencyjne z lokalnego cache NuGet. Na maszynach z zainstalowanym pakietem nic się nie zmienia.

**Contract**: Ustawia `TargetFrameworkRootPath` tylko wtedy, gdy spełnione są oba warunki:
- `TargetFrameworkVersion` to `v4.5.2` lub `v4.5`;
- `$(MSBuildProgramFiles32)\Reference Assemblies\Microsoft\Framework\.NETFramework\$(TargetFrameworkVersion)\mscorlib.dll` nie istnieje, a katalog `$(UserProfile)\.nuget\packages\microsoft.netframework.referenceassemblies.<net452|net45>\1.0.3\build\` istnieje.

Gdy `TargetFrameworkRootPath` wskazuje na pakiet `microsoft.netframework.referenceassemblies`, ustawia też `FrameworkPathOverride` na `$(TargetFrameworkRootPath).NETFramework\$(TargetFrameworkVersion)`. Bez tego `mscorlib.dll` pochodzi z zainstalowanego runtime 4.8 (`FrameworkPathOverride` jest wyliczany w `Microsoft.Common.CurrentVersion.targets:80-85`, przed importem `Directory.Build.targets`). Skutkiem jest kompilacja względem API 4.8 i ostrzeżenia MSB3270. Sprawdzone w kopii repo: cała solucja, 0 ostrzeżeń i 0 błędów.

Nie dotyka projektów SDK-style (TFM `v4.8`).

#### 2. Projekt testowy

**File**: `eTMsTESTS/eTMsTESTS.csproj` (nowy), `eTMsLomb.sln`

**Intent**: Projekt SDK-style `net48` z MSTest i Moq, dodany do solucji. Referencjonuje produkcyjne projekty i przez restore pobiera pakiety referencyjne dla nich.

**Contract**:
- `TargetFramework` `net48`.
- `PackageReference`:
  - `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`, `Moq` (najnowsze wersje wspierające `net48`);
  - `Microsoft.NETFramework.ReferenceAssemblies.net452` i `.net45` 1.0.3 z `PrivateAssets="all"`.
- `ProjectReference` do `eTMsDB`, `eTMsDAL`, `eTMsBLL`, `eTMsWEB`.
- Dodanie do `eTMsLomb.sln` ręczną edycją pliku (wpis `Project(...)` oraz konfiguracje Debug/Release w `ProjectConfigurationPlatforms`). `dotnet sln add` kończy się MSB4019.
- `Reference` z `HintPath` do `packages\Microsoft.AspNet.Mvc.5.2.3\lib\net45\System.Web.Mvc.dll` oraz `System.Web`, potrzebne do testów filtra w fazie 4.

#### 3. Skrypt weryfikacji

**File**: `verify.ps1` (nowy, w katalogu głównym repo)

**Intent**: Jedna komenda dla człowieka i agenta: restore, build solucji, testy.

**Contract**:
- Znajduje `MSBuild.exe` przez `vswhere`.
- Wykonuje `MSBuild eTMsLomb.sln -restore -p:Configuration=Debug -nologo -v:minimal`, a potem `vstest.console.exe eTMsTESTS\bin\Debug\net48\eTMsTESTS.dll` (ścieżka przez `vswhere`: `Common7\IDE\Extensions\TestPlatform\vstest.console.exe`). Nie `dotnet build`/`dotnet test`: kończą się MSB4019, bo eTMsWEB importuje `Microsoft.WebApplication.targets` z `$(VSToolsPath)`.
- Kończy się kodem ≠ 0 przy pierwszym błędzie.

#### 4. Testy smoke

**File**: `eTMsTESTS/PozyczkaLombardowaBLLTests.cs`, `eTMsTESTS/PozyczkaLombardowaDALUprawnieniaTests.cs` (nowe)

**Intent**: Dowód, że cały łańcuch działa.
- Test czystej logiki: `PozyczkaLombardowaBLL.Wylicz` z progiem minimalnych odsetek (`eTMsBLL/REPOSITORY/PozyczkaLombardowaBLL.cs:46-75`).
- Test charakteryzacyjny obecnej kontroli zapisu: `AkcjaSplataCalkowita` przy braku ról 5/6 zwraca `code=1`, „Nie masz odpowiednich uprawnień!” i nie woła żadnej metody zapisu `IAppDbContext`.

**Contract**: Mock `IAppDbContext` (Moq, `MockBehavior.Loose`) zwraca umowę w statusie 3 i pustą listę uprawnień. Asercja `Verify(…, Times.Never)` obejmuje `PozyczkaLombardowaAkcjaSplataCalkowita`.

### Success Criteria:

#### Automated Verification:

- Solucja buduje się bez ręcznego `TargetFrameworkVersion`/`TargetFrameworkRootPath`, 0 błędów i 0 ostrzeżeń MSB3270: `powershell -File verify.ps1`
- Wszystkie 5 projektów produkcyjnych zachowuje TFM: `git diff --stat -- '*.csproj' '*.sqlproj'` nie pokazuje zmian w plikach produkcyjnych (zmienia się tylko `eTMsLomb.sln` i dochodzą nowe pliki)
- Testy smoke przechodzą (≥ 2 testy, 0 nieudanych) w wyniku `vstest.console.exe` z `verify.ps1`
- `git status --short` nie pokazuje artefaktów buildu (`bin/`, `obj/`, `*.dacpac`, `TestResults/`); w razie potrzeby rozszerzyć `.gitignore`

#### Manual Verification:

- Otwarcie `eTMsLomb.sln` w Visual Studio: projekt `eTMsTESTS` widoczny, testy widoczne w Test Explorer

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie, zanim ruszy faza 2.

---

## Phase 2: Jedna polityka uprawnień w DAL (bez zmiany zachowania)

### Overview

21 kopii kontroli zapisu i 8 kontroli odczytu w `PozyczkaLombardowaDAL` przechodzi na jedną klasę polityki. Zachowanie musi zostać identyczne: te same komunikaty, `return` tylko tam, gdzie jest dziś, i te same źródła danych.

### Changes Required:

#### 1. Klasa polityki

**File**: `eTMsDAL/REPOSITORY/PolitykaUprawnien.cs` (nowy) + `<Compile Include>` w `eTMsDAL/eTMsDAL.csproj`

**Intent**: Jedno miejsce reguł dostępu do placówki dla operacji pożyczek, kasy, remanentu i sprzedaży. Dwie nazwane listy ról zamiast literałów rozsianych po DAL.

**Contract**:
- Konstruktor `(IAppDbContext)`.
- `bool MozeZapisywac(string login, int placowkaId)`: `true` wtedy i tylko wtedy, gdy `UzytkownikUprawnienieLista(login, placowkaId)` zawiera `uprawnienieId` ∈ {5, 6}.
- `bool MozeOdczytywac(string login, int placowkaId)`: `true` wtedy i tylko wtedy, gdy `PozyczkaLombardowaPlacowkaUprawninegoLista(login)` zawiera `placowkaId`. To zapytanie już filtruje role {4, 5, 6} w SQL (`AppDbContext.cs:1162-1183`).
- Listy ról jako nazwane, tylko do odczytu pola klasy.
- Pusty login lub `placowkaId <= 0` → `false`, tak jak dziś: `UzytkownikUprawnienieLista` zwraca wtedy pustą listę (`AppDbContext.cs:932-951`).

#### 2. Przepięcie 21 kontroli zapisu

**File**: `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` (metody z tabeli V1 w research: `UmowaDodaj` `:379` … `UwagaDodaj` `:2431`)

**Intent**: Zastąp każdy blok `var upraw = appDbContext.UzytkownikUprawnienieLista(…).Where(…{5,6}…).ToList(); if (upraw.Count() == 0) {…}` wywołaniem `MozeZapisywac` z tym samym loginem i tym samym `placowkaId`.

**Contract**:
- Identyczny komunikat „Nie masz odpowiednich uprawnień!”, `code = 1` i `return result` w tych samych miejscach.
- Polityka powstaje raz w konstruktorze DAL (pole prywatne), z tym samym `appDbContext`.

#### 3. Przepięcie 8 kontroli odczytu

**File**: `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` (`:140-146`, `:179-185`, `:219-225`, `:2905-2911`, `:2947-2953`, `:2990-2996`, `:3030-3036`, `:3078-3084`)

**Intent**: Zastąp `var placupr = …PlacowkaUprawninegoLista(ktoPyta); if (!placupr.Contains(placowkaId))` wywołaniem `!MozeOdczytywac(ktoPyta, placowkaId)`. W tej fazie **bez** dodawania `return`, żeby zachowanie pozostało identyczne.

**Contract**: Ciało bloku (`code=1`, „Brak uprawnień do umów placówki!”, ustawienie `data`) bez zmian.

#### 4. Testy polityki i charakteryzacja

**File**: `eTMsTESTS/PolitykaUprawnienTests.cs` (nowy), rozszerzenie `eTMsTESTS/PozyczkaLombardowaDALUprawnieniaTests.cs`

**Intent**: Pokryć reguły polityki i zabezpieczyć, że przepięcie nic nie zmieniło.
- Zapis: role {5} → true, {6} → true, {4} → false, {} → false, pusty login → false.
- Odczyt: placówka na liście → true, poza listą → false.
- Charakteryzacja odmowy zapisu dla reprezentatywnych metod: `UmowaDodaj` (placówka z żądania), `ZastawDodaj`, `AkcjaSplataCzesciowa` (`pozyczka1`), `KasaOperacjaDodaj` jeszcze bez kontroli (ma przechodzić do zapisu).
- Charakteryzacja odczytu: `Lista` przy odmowie zwraca `code=1` **i** dane (obecne zachowanie, które faza 3 zmieni).

**Contract**: Mock `IAppDbContext`. Testy nazwane po zachowaniu.
- Test `KasaOperacjaDodaj` (przechodzi do zapisu) wymaga setupu, bo inaczej zatrzyma się na wcześniejszych walidacjach (`DAL:2719-2752`): `kwotaOperacji > 0`, `rozliczenia` sumujące się do kwoty, mock `PozyczkaLombardowaKasaAkcjaOdczytaj` zwracający akcję oraz mock `PozyczkaLombardowaKasaOdczytaj` zwracający kasę z `dataZmiany <= dataOperacji`. Asercja: wołane jest `PozyczkaLombardowaKasaOperacjaDodaj`.

### Success Criteria:

#### Automated Verification:

- `powershell -File verify.ps1` przechodzi (build + wszystkie testy zielone)
- W DAL pożyczek nie ma kopii inline filtra zapisu: `ast-grep run -l csharp -p 'appDbContext.UzytkownikUprawnienieLista($$$A).Where($$$B)' eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` → 0 trafień
- Ani bezpośrednich odczytów listy placówek: `ast-grep run -l csharp -p 'appDbContext.PozyczkaLombardowaPlacowkaUprawninegoLista($$$)' eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` → 0 trafień
- Polityka wołana dokładnie 21× do zapisu i 8× do odczytu: `ast-grep run -l csharp -p '$P.MozeZapisywac($$$)' eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` → 21, `-p '$P.MozeOdczytywac($$$)'` → 8
- Komunikaty bez zmian: `grep -c "Nie masz odpowiednich uprawnień!" eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` → 21, `grep -c "Brak uprawnień do umów placówki!"` → 8

#### Manual Verification:

- Przegląd diffu `PozyczkaLombardowaDAL.cs`: każda z 29 zmian używa tego samego loginu i `placowkaId` co przed zmianą (lista V1/V2 w research)

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie, zanim ruszy faza 3.

---

## Phase 3: Domknięcie luk w DAL

### Overview

Pierwsze zmiany zachowania. Odmowa odczytu przestaje wysyłać dane, a trzy endpointy bez kontroli dostają kontrolę przez politykę z fazy 2.

### Changes Required:

#### 1. `return` w 8 kontrolach odczytu

**File**: `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` (8 bloków z fazy 2.3)

**Intent**: Odmowa kończy metodę z pustymi danymi (`new List<…>()` albo `0`, zgodnie z typem, tak jak dziś w bloku), zanim wykona się zapytanie.

**Contract**:
- `code=1`, komunikat bez zmian, `data` puste, `return result`.
- Ścieżki wydruków w kontrolerze (`throw` przy `code == 1`) bez zmian.
- Znane i zaakceptowane: wcześniejsze bloki „Kto pyta?” i „Która placówka?” nie mają `return` i zostają bez zmian. Przy pustym loginie lub `placowkaId <= 0` odpowiedź kończy się więc komunikatem „Brak uprawnień do umów placówki!”, a `code=1` się nie zmienia (UI patrzy tylko na `code`).

#### 2. Kontrola zapisu w `KasaOperacjaDodaj`

**File**: `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` (`KasaOperacjaDodaj`, `:2715`)

**Intent**: Przed jakimkolwiek odczytem kasy i zapisem (w tym warunkowym `PozyczkaLombardowaKasaDodaj`, `:2773`) wymagaj `MozeZapisywac(operacja.dodal, operacja.placowkaId)`. Kontroler ustawia `dodal` z sesji (`PozyczkaLombardowaController.cs:416-421`).

**Contract**: Odmowa: `code=1`, „Nie masz odpowiednich uprawnień!”, `data=0`, `return`.

#### 3. Kontrola odczytu w `KasaOdczytaj`

**File**: `eTMsDAL/INTERFACES/IPozyczkaLombardowaDAL.cs:126`, `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs:2696`, `eTMsWEB/Controllers/PozyczkaLombardowaController.cs:408-411`

**Intent**: Endpoint dostaje login z sesji i sprawdza odczyt w żądanej placówce.

**Contract**:
- Sygnatura `Result<PozyczkaLombardowaKasa> KasaOdczytaj(string ktoPyta, int placowkaId)` w interfejsie i implementacji.
- Kontroler przekazuje `Session["zalogowanyLogin"] as string`.
- Odmowa: `code=1`, „Brak uprawnień do umów placówki!”, `data=null`, `return`.

#### 4. Kontrola odczytu w `RemanentSyntetyka`

**File**: `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs` (`RemanentSyntetyka`, `:3098`)

**Intent**: Przed `PozyczkaLombardowaRemanentAnalitykaLista` wymagaj `MozeOdczytywac(ktoPyta, placowkaId)`. Ta sama reguła obowiązuje już w siostrzanej `RemanentAnalityka`.

**Contract**: Odmowa: `code=1`, „Brak uprawnień do umów placówki!”, pusta lista, `return`.

#### 5. Testy luk

**File**: `eTMsTESTS/PozyczkaLombardowaDALUprawnieniaTests.cs`

**Intent**:
- Testy charakteryzacyjne z fazy 2, które utrwalały dane przy odmowie i brak kontroli w `KasaOperacjaDodaj`, zmieniają oczekiwanie na nowe zachowanie.
- Nowe testy dla każdego punktu 1–4: odmowa nie woła zapytania danych ani zapisu (`Times.Never`), a uprawniony przechodzi.

**Contract**: Dla `KasaOperacjaDodaj` sprawdzamy, że przy odmowie nie są wołane `PozyczkaLombardowaKasaOdczytaj`, `PozyczkaLombardowaKasaDodaj` ani `PozyczkaLombardowaKasaOperacjaDodaj`.

### Success Criteria:

#### Automated Verification:

- `powershell -File verify.ps1` przechodzi
- Żadna kontrola odczytu nie zostaje bez `return`: reguła ast-grep `kind: if_statement`, `has: {field: condition, regex: 'MozeOdczytywac'}`, `not: {has: {kind: return_statement, stopBy: end}}` na `PozyczkaLombardowaDAL.cs` → 0 trafień
- Liczba wywołań polityki: `$P.MozeZapisywac($$$)` → 22, `$P.MozeOdczytywac($$$)` → 10 (ast-grep na `PozyczkaLombardowaDAL.cs`)
- Sygnatura `KasaOdczytaj(string ktoPyta, int placowkaId)` w interfejsie i implementacji; build przechodzi (kompilator wiąże kontroler)
- Testy odmowy dla `KasaOperacjaDodaj`, `KasaOdczytaj`, `RemanentSyntetyka` i co najmniej jednej z 8 kontroli odczytu przechodzą

#### Manual Verification:

- Na działającej aplikacji LOMBARDZISTA w swojej placówce: lista umów, kasa (odczyt stanu i dodanie operacji), remanent syntetyka działają jak przed zmianą
- Ten sam użytkownik, żądanie z DevTools z `placowkaId` placówki bez uprawnień do `PozyczkaLombardowaPageLista`, `PozyczkaLombardowaKasaOdczytaj`, `PozyczkaLombardowaKasaOperacjaDodaj`, `PozyczkaLombardowaRemanentSyntetyka`: odpowiedź `code=1` bez danych i brak nowej operacji kasowej

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie, zanim ruszy faza 4.

---

## Phase 4: Rola admin na granicy web

### Overview

Administracja (cały `AdminController`, w tym konsola SQL `DBWykonaj`) oraz dodawanie i usuwanie typów zastawów wymagają roli `admin`, egzekwowanej przez serwer.

### Changes Required:

#### 1. Filtr roli

**File**: `eTMsWEB/Filters/WymagaRoliAttribute.cs` (nowy) + `<Compile Include>` w `eTMsWEB/eTMsWEB.csproj`

**Intent**: Filtr akcji przepuszcza żądanie tylko wtedy, gdy `Session["zalogowanyRole"]` (`List<string>`) zawiera którąś z wymaganych ról.

**Contract**:
- `public class WymagaRoliAttribute : ActionFilterAttribute`, konstruktor `(params string[] role)`, logika w `OnActionExecuting`. Domyślny `Order`, żeby biegł po globalnym `CheckSessionAttribute`.
- Odmowa dla `POST`: `filterContext.Result = ContentResult` z JSON `Result { code = 1, message = "Brak uprawnień administratora!" }` serializowanym przez `ETMSCoreBLL.ObjectToJson`.
- Odmowa dla `GET`: `HttpStatusCodeResult(403)`.
- Brak sesji ról traktujemy jako odmowę.

#### 2. Zastosowanie

**File**: `eTMsWEB/Controllers/AdminController.cs`, `eTMsWEB/Controllers/PozyczkaLombardowaController.cs`

**Intent**: Nałóż `[WymagaRoli("admin")]` na klasę `AdminController` oraz na akcje `PozyczkaLombardowaZastawTypDodaj` (`:75`) i `PozyczkaLombardowaZastawTypUsun` (`:84`).

**Contract**:
- `PozyczkaLombardowaZastawTypLista` (`:67`) **bez** atrybutu. Strona `PozyczkaLombardowaZastawTypIndex` leży w `AdminController` (`:326`), więc obejmuje ją atrybut na klasie.
- `HomeController` i `SzablonController` bez zmian.

#### 3. Testy filtra

**File**: `eTMsTESTS/WymagaRoliAttributeTests.cs` (nowy)

**Intent**: Pokryć decyzje filtra na fałszywym `ActionExecutingContext` z mockiem `HttpContextBase`/`HttpSessionStateBase`/`HttpRequestBase`.
- rola `admin` → `Result == null` (przepuszcza);
- role `["5","6"]` przy POST → `ContentResult` z JSON `code == 1`;
- role `["5"]` przy GET → `HttpStatusCodeResult` 403;
- brak `zalogowanyRole` w sesji → odmowa.

**Contract**: Testy nie potrzebują IIS ani bazy.

### Success Criteria:

#### Automated Verification:

- `powershell -File verify.ps1` przechodzi, w tym testy `WymagaRoliAttributeTests`
- `AdminController` ma atrybut na klasie: ast-grep `kind: class_declaration`, `has: {field: name, regex: '^AdminController$'}`, `has: {kind: attribute_list, regex: 'WymagaRoli\("admin"\)'}` na `eTMsWEB/Controllers/AdminController.cs` → 1 trafienie
- W `PozyczkaLombardowaController.cs` atrybut mają dokładnie 2 metody (`PozyczkaLombardowaZastawTypDodaj`, `PozyczkaLombardowaZastawTypUsun`): ast-grep `kind: method_declaration` + `has: {kind: attribute_list, regex: 'WymagaRoli'}` → 2 trafienia; `PozyczkaLombardowaZastawTypLista` wśród nich nie ma

#### Manual Verification:

- Admin po świeżym zalogowaniu (pierwsze żądanie po logowaniu) otwiera UŻYTKOWNICY, PLACÓWKI, PARAMETRY, SZABLONY, TYPY ZASTAWÓW i DBADMIN, a operacje zapisu działają
- LOMBARDZISTA (bez `czyAdministrator`): `GET /Admin/DBIndex` → 403; POST z DevTools na `Admin/DBWykonaj` i `PozyczkaLombardowa/PozyczkaLombardowaZastawTypDodaj` → `code=1` w alercie, brak skutków w bazie
- LOMBARDZISTA: formularz umowy dalej ładuje listę typów zastawów, a zmiana placówki i hasła na stronie głównej działają

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymaj się na ręczne potwierdzenie.

---

## Testing Strategy

### Unit Tests:

- `PozyczkaLombardowaBLL.Wylicz`: próg minimalnych odsetek (smoke, faza 1).
- `PolitykaUprawnien`: obie reguły, puste wejścia.
- `PozyczkaLombardowaDAL`:
  - odmowa zapisu dla reprezentatywnych metod, w tym `UmowaDodaj` z placówką z żądania;
  - odmowa odczytu z pustymi danymi;
  - odmowa dla 3 domkniętych luk;
  - ścieżka uprawniona przechodzi do zapisu lub zapytania.
- `WymagaRoliAttribute`: admin / nie-admin POST / nie-admin GET / brak sesji.

### Integration Tests:

- Brak w tej zmianie. Testy integracyjne SQL wymagają P2, poza zakresem.

### Manual Testing Steps:

1. Uruchom aplikację lokalnie z kopią bazy. Zaloguj się jako admin i przejdź wszystkie pozycje menu ADMIN.
2. Zaloguj się jako LOMBARDZISTA jednej placówki. Sprawdź listę umów, kasę z dodaniem operacji, remanent, sprzedaż i książkę.
3. Jako LOMBARDZISTA wyślij z DevTools żądania do cudzej placówki i do `Admin/*`. Oczekiwane: `code=1` lub 403, brak danych, brak zapisów.
4. Zaloguj się jako WIZYTOR (rola 4). Książka lombardowa i listy odczytowe działają jak przed zmianą.

## Performance Considerations

`MozeOdczytywac` i `MozeZapisywac` wykonują te same zapytania co dziś, po jednym na wywołanie. Dochodzą 3 nowe zapytania w endpointach, które dotąd nie miały kontroli (`KasaOperacjaDodaj`, `KasaOdczytaj`, `RemanentSyntetyka`). Filtr roli czyta tylko sesję.

## Migration Notes

Brak zmian schematu i danych. Wdrożenie to podmiana binariów aplikacji.
- Fazy 1–2 nie zmieniają zachowania.
- Fazy 3–4 mogą odciąć użytkowników, którzy dziś korzystają z endpointów bez wymaganych ról. To zamierzone. Wycofanie to rewert commita fazy.
- `zalogowanyRole` jest wypełniane raz na sesję (`CheckSessionAttribute.cs:40`, gdy `zalogowanyLogin == null`). Nadanie lub odebranie flagi `czyAdministrator` działa w filtrze roli dopiero po ponownym zalogowaniu. To znane, zaakceptowane zachowanie, poza zakresem tej zmiany.

## References

- Research: `context/changes/refactor-opportunities/research.md` (§1 K3, §3.1 K3, §6 #1, weryfikacja V1–V13)
- Analiza źródłowa: `context/changes/post-flow-analysis/research.md` (TD15, §7 S37, S39)
- Wzorzec kontroli zapisu: `eTMsDAL/REPOSITORY/PozyczkaLombardowaDAL.cs:1248` (`AkcjaSplataCalkowita`)
- Filtr sesji i role: `eTMsWEB/Filters/CheckSessionAttribute.cs:36-86`, `eTMsWEB/App_Start/FilterConfig.cs:13`
- Zapytania uprawnień: `eTMsDB/REPOSITORIES/AppDbContext.cs:932-951`, `:1162-1183`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Build na oryginalnym TFM i projekt testowy (P1)

#### Automated

- [ ] 1.1 Solucja buduje się bez ręcznego TargetFrameworkVersion/TargetFrameworkRootPath przez verify.ps1, 0 błędów
- [ ] 1.2 Projekty produkcyjne zachowują TFM: brak zmian w produkcyjnych csproj/sqlproj
- [ ] 1.3 Testy smoke przechodzą (≥ 2 testy, 0 nieudanych)
- [ ] 1.4 git status nie pokazuje artefaktów buildu

#### Manual

- [ ] 1.5 eTMsTESTS widoczny w Visual Studio, testy w Test Explorer

### Phase 2: Jedna polityka uprawnień w DAL (bez zmiany zachowania)

#### Automated

- [ ] 2.1 verify.ps1 przechodzi
- [ ] 2.2 ast-grep: 0 kopii inline UzytkownikUprawnienieLista(...).Where(...) w DAL pożyczek
- [ ] 2.3 ast-grep: 0 bezpośrednich PozyczkaLombardowaPlacowkaUprawninegoLista w DAL pożyczek
- [ ] 2.4 ast-grep: MozeZapisywac 21×, MozeOdczytywac 8×
- [ ] 2.5 Komunikaty bez zmian: 21× i 8×

#### Manual

- [ ] 2.6 Przegląd diffu: te same login i placowkaId w 29 miejscach

### Phase 3: Domknięcie luk w DAL

#### Automated

- [ ] 3.1 verify.ps1 przechodzi
- [ ] 3.2 ast-grep: 0 kontroli MozeOdczytywac bez return
- [ ] 3.3 ast-grep: MozeZapisywac 22×, MozeOdczytywac 10×
- [ ] 3.4 Sygnatura KasaOdczytaj(string ktoPyta, int placowkaId) w interfejsie i implementacji
- [ ] 3.5 Testy odmowy dla KasaOperacjaDodaj, KasaOdczytaj, RemanentSyntetyka i kontroli odczytu przechodzą

#### Manual

- [ ] 3.6 LOMBARDZISTA w swojej placówce: listy, kasa, remanent działają jak przed zmianą
- [ ] 3.7 Żądania do cudzej placówki z DevTools: code=1 bez danych, brak nowej operacji kasowej

### Phase 4: Rola admin na granicy web

#### Automated

- [ ] 4.1 verify.ps1 przechodzi, w tym WymagaRoliAttributeTests
- [ ] 4.2 ast-grep: AdminController ma WymagaRoli("admin") na klasie
- [ ] 4.3 ast-grep: w PozyczkaLombardowaController atrybut mają dokładnie ZastawTypDodaj i ZastawTypUsun

#### Manual

- [ ] 4.4 Admin po świeżym zalogowaniu korzysta ze wszystkich pozycji menu ADMIN
- [ ] 4.5 LOMBARDZISTA: 403 na Admin GET, code=1 na Admin/DBWykonaj i ZastawTypDodaj POST, brak skutków
- [ ] 4.6 LOMBARDZISTA: lista typów zastawów w formularzu umowy, zmiana placówki i hasła działają
