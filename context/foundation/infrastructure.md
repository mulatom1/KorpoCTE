---
project: APPS (tomsoft1.pl — App01) / Kursy i bootcampy
researched_at: 2026-09-28
recommended_platform: Webio.pl (hosting ASP.NET, IIS + MSSQL) — status quo
runner_up: Azure App Service (Linux B1, Poland Central) + Azure SQL Database
context_type: mvp
tech_stack:
  language: C# (.NET 10) + TypeScript 5.9
  framework: ASP.NET Core 10 Minimal APIs (modularny monolit, EF Core 10 / SQL Server, compat 110) + React 19 SPA serwowane z tego samego hosta
  runtime: .NET 10 (ASP.NET Core Module na IIS; fallback self-contained win-x64)
---

## Recommendation

**Zostajemy na Webio.pl** — pod warunkiem przejścia bramki weryfikacyjnej (zgodność zapytań EF Core 10 z SQL Server 2012 + praktyczne potwierdzenie .NET 10, deklarowanego we wszystkich planach) na subdomenie testowej przed pierwszym wdrożeniem produkcyjnym wersji z modułem Courses.

Stack ma trzy twarde wymagania, które odsiały większość platform: stały proces (4 `BackgroundService` — Lotto ×3, Portal ×1), trwały system plików (FR-010: treść kursów wgrywana przez FTP) i SQL Server. Webio spełnia je wszystkie, a stabilna praca workerów w nocy i przy braku ruchu jest **potwierdzona produkcyjnie** (odpowiedź dewelopera, 2026-09-28), nie tylko deklarowana. Na kryteriach agent-friendly Webio wypada najsłabiej z kandydatów, którzy przeszli filtry, ale przy terminie 2026-11-04 (6 tygodni po godzinach), małej zamkniętej grupie użytkowników i opłaconym rocznym hostingu migracja na Azure (dane, DNS, strefa czasowa, ścieżki zapisu) wnosi więcej ryzyka niż zysku. Luki operacyjne kompensujemy automatyzacją wdrożenia w GitHub Actions (Web Deploy / FTPS) i odczytem logów Serilog przez FTPS. Azure App Service + Azure SQL jest zapisanym planem wyjścia.

Ścieżka decyzji: wywiad → research 8 platform → pierwotny lider Azure App Service → cross-check Azure → deweloper poprosił o analizę pozostania na Webio → cross-check Webio → **decyzja: Webio, runner-up Azure**.

## Platform Comparison

Wywiad (2026-09-28): stałe procesy — **tak**; priorytet — **DX ponad koszt**; znajomość — **Webio.pl**; zasięg — **jeden region (PL)**; usługi — **preferowana ko-lokacja (DB u tego samego dostawcy)**.

Filtry twarde: wymóg stałego procesu + runtime .NET 10 + SQL Server (EF Core, `UseCompatibilityLevel(110)`) + trwały dysk dla treści z FTP.

| Platforma | Filtr twardy | CLI-first | Managed | Agent-readable docs | Stable deploy API | MCP / integracja | Wynik |
|---|---|---|---|---|---|---|---|
| Azure App Service + Azure SQL | ✓ | Pass | Pass | Pass | Pass | Partial | **9/10** |
| Railway | ✓ (Dockerfile + kontener MSSQL) | Pass | Partial | Pass | Pass | Pass | **9/10** |
| Fly.io | ✓ (Dockerfile + MSSQL na maszynie) | Pass | Partial | Pass | Pass | Partial | **8/10** |
| Render | ✓ (Docker + MSSQL jako private service) | Partial | Partial | Partial | Pass | Pass | 7/10 |
| Webio.pl | ✓ (workery potwierdzone produkcyjnie; .NET 10 deklarowane we wszystkich planach) | Partial | Partial | Fail | Partial | Fail | 4/10 |
| Cloudflare (Workers/Containers) | ✗ | — | — | — | — | — | odrzucona |
| Vercel | ✗ | — | — | — | — | — | odrzucona |
| Netlify | ✗ | — | — | — | — | — | odrzucona |

Punktacja: Pass = 2, Partial = 1, Fail = 0. Wynik to heurystyka, nie bramka — patrz uzasadnienie niżej, dlaczego platforma z najniższym wynikiem po filtrach wygrywa na kontekście.

**Azure App Service + Azure SQL** — `az webapp deploy`, `az webapp log tail`, `azd up`, akcja `azure/webapps-deploy` (GA). Jedyny kandydat z **zarządzanym SQL Serverem** w tym samym regionie (Poland Central); compat 110 wspierany. Always On od B1 (brak na F1/Shared), sloty dopiero od S1. `/home` na Linux jest trwały i dostępny przez FTPS. Docs w `MicrosoftDocs/azure-docs` (markdown) + `azure.microsoft.com/llms.txt`. MCP: Azure MCP Server w trakcie migracji repo (`Azure/azure-mcp` → `microsoft/mcp`, status niejasny); Azure DevOps Remote MCP GA 08.2026, ale bez wsparcia Claude Code — Partial. .NET 10: GA ogłoszone na Ignite 11.2025, w części regionów etykieta „(Preview)" do końca walidacji (sprawdzone 2026-09-28).

**Railway** — `railway up`, `railway logs`, rollback do ostatniego udanego wdrożenia, `llms-full.txt` + strony jako `.md`, oficjalny MCP (GA). .NET tylko przez Dockerfile (Railpack nie obsługuje .NET). SQL Server wyłącznie z **community template** (`mcr.microsoft.com/mssql/server`, ≥ 2 GB RAM, edycja Developer nie jest licencjonowana produkcyjnie — trzeba `MSSQL_PID=Express`). Jedyny region EU: Amsterdam. Wolumen = krótki downtime przy każdym redeployu.

**Fly.io** — `fly deploy`, `fly releases` + `fly deploy --image`, `fly logs`, `fly secrets`; region **Warszawa (`waw`)**; docs jako markdown (`fly.io/llms.txt`). SQL Server tylko samodzielnie na maszynie z wolumenem (1 wolumen ↔ 1 maszyna, snapshoty 5 dni, bez HA). MCP wbudowany w flyctl, oznaczony **experimental** (2026-09-28). Brak darmowego limitu od 2024.

**Render** — Docker, Blueprints (`render.yaml`), deploy hooks, oficjalny MCP (GA), region Frankfurt. Rollback tylko przez dashboard/API (brak podkomendy CLI), `llms.txt` niepotwierdzony. Persistent disk wyłącza zero-downtime deploy i skalowanie; SQL Server jako niewspierany private service ≥ Standard ($25/mies.).

**Webio.pl** — hosting ASP.NET na IIS, dedykowana pula aplikacji w każdym planie, MSSQL (2–16 baz, 250 MB – 10,4 GB zależnie od planu), FTP bez limitu, Let's Encrypt, 75–710 zł/rok. Brak CLI/API/MCP, dokumentacja wyłącznie jako HTML KB. Web Deploy: profil publikacji do pobrania z panelu → wdrożenie da się oskryptować (`msdeploy` / `dotnet publish -p:PublishProfile`), choć Webio nie dokumentuje automatyzacji. Strona planów deklaruje „ASP.NET Core 1.0 – 10.x" we wszystkich planach (aktualne źródło); starsza strona `wspierane-rozwiazania.html` (Core 1.x–3.x, PHP do 7.3, SQL Server 2012) jest nieaktualna — plany oferują PHP 8.x (sprawdzone 2026-09-28). Produkcja działa dziś na .NET 8.

**Cloudflare** — Containers GA od 13.04.2026, ale usypiają po bezczynności (`sleepAfter`, wątek w tle nie podtrzymuje instancji), dysk jest efemeryczny, brak SQL Servera (Hyperdrive: tylko Postgres/MySQL), brak `wrangler tail` dla Containers. Odrzucona.

**Vercel** — brak runtime .NET (tylko kontener w modelu funkcji: scale-to-zero, „background jobs stop between requests"), FS tylko `/tmp`, brak SQL Servera. Odrzucona.

**Netlify** — brak runtime .NET, funkcje max 10 s / background 15 min, brak SQL Servera (Netlify Database = Postgres). Odrzucona.

### Shortlisted Platforms

#### 1. Webio.pl (Recommended — decyzja dewelopera po cross-checku)

Wygrywa na kontekście, nie na kryteriach: workery już działają stabilnie (dowód produkcyjny), baza SQL Server 2012 odpowiada `UseCompatibilityLevel(110)` bez migracji danych, FTP to dokładnie mechanizm publikacji z FR-010, region PL, domena i certyfikat bez zmian, hosting opłacony na rok. Główne luki — brak CLI/rollbacku/logów przez API — są kompensowane bramką weryfikacyjną i workflow w GitHub Actions (patrz Operational Story).

#### 2. Azure App Service + Azure SQL (runner-up, plan wyjścia)

Najlepszy wynik na kryteriach agent-friendly i jedyny zarządzany SQL Server u tego samego dostawcy. Przegrywa kosztem migracji w oknie 5 tygodni: przeniesienie danych z SQL 2012 (bacpac), przepięcie DNS, strefa czasowa (Linux = UTC vs `DateTime.Now` w workerach), zapisy do katalogu aplikacji (`TMP/LOTTO/*`, `Logs/`) przy run-from-package. Wracamy do tej opcji, jeśli bramka .NET 10 / SQL 2012 na Webio nie przejdzie albo po wygaśnięciu rocznego abonamentu.

#### 3. Railway

Najlepsze DX i MCP GA, ale SQL Server jako samodzielnie utrzymywany kontener (patching, backup, licencja Express, ≥ 2 GB RAM ≈ +$20/mies.) i region wyłącznie Amsterdam. Fly.io (Warszawa) był blisko — Railway wyprzedził go dojrzałością MCP (GA vs experimental).

## Anti-Bias Cross-Check: Webio.pl

(Cross-check pierwotnego lidera — Azure App Service — jest zapisany w załączniku na końcu dokumentu.)

### Devil's Advocate — Weaknesses

1. **.NET 10 deklarowany, ale jeszcze nieuruchomiony na tym koncie** — Webio podaje „ASP.NET Core 1.0 – 10.x" we wszystkich planach, lecz produkcja działa na .NET 8; zainstalowany patch runtime starszy niż wymagany przez build dałby `HTTP Error 500.31` dopiero przy wdrożeniu.
2. **SQL Server 2012 (EOL 07.2022) vs EF Core 10** — `UseCompatibilityLevel(110)` wyłącza część tłumaczeń (np. `OPENJSON`), ale każde nowe zapytanie Courses (ranking, wskaźniki) może trafić w składnię nieobsługiwaną przez 2012; testy na EF InMemory tego nie wykryją.
3. **Brak rollbacku i atomowości wdrożenia** — przerwany upload FTP zostawia pół-wdrożoną aplikację; zablokowane DLL wymagają `app_offline.htm`; każdy hotfix = pełny redeploy.
4. **Nieudokumentowane limity CPU/RAM puli na hostingu współdzielonym** — recykling puli restartuje workery; sąsiedzi mogą zjeść budżet werdyktu < 5 s (wywołanie OpenRoutera + zapis flagi).
5. **Poświadczenia Web Deploy/FTP = klucz do całej witryny** — brak scope'owania tokenów wbrew zasadzie minimalnych uprawnień; jedyna kontrola to GitHub Secrets + environment protection.

### Pre-Mortem — How This Could Fail

Zespół został na Webio, bo „działa od lat". Pierwszy deploy .NET 10 poszedł z Visual Studio i skończył się 500.31 — runtime 10 nie był zainstalowany; dwa wieczory zeszły na przejście na publikację self-contained. Potem profil z `DeleteExistingFiles=true` przy każdej publikacji kasował katalog z treścią kursów wgraną przez FTP i archiwum `TMP/LOTTO/ARCHIWUM`, bo leżały w katalogu witryny — kursy znikały po każdym hotfiksie. Zapytanie rankingu działało w testach, a na SQL 2012 rzucało błąd składni, który dało się zdiagnozować tylko z logów pobieranych ręcznie przez FTP. Baza w planie Podstawowy dobiła do 250 MB po imporcie historii Lotto i zapisy flag zaczęły się sypać. Agent nie miał jak sprawdzić stanu produkcji, więc każdy incydent wymagał człowieka przy panelu. Nie zawiódł hosting — zawiódł brak etapu staging i brak automatyzacji wdrożeń.

### Unknown Unknowns

- **`DeleteExistingFiles=true`** w obu `FolderProfile*.pubxml` — czyste wdrożenie usuwa wszystko spoza paczki: `Logs/`, `TMP/LOTTO/*` i przyszłe pliki kursów. `Courses:ContentPath` musi leżeć poza katalogiem wdrożenia albo być wykluczony regułą skip msdeploy.
- **Workery zależą od czasu lokalnego serwera** (`DateTime.Now` w `LottoWorker01`, `PortalWorker01`; `DateTimeKind.Local` w importach). Na Webio działa (polska strefa), ale to ciche uzależnienie — każda przyszła przeprowadzka musi ustawić strefę (`WEBSITE_TIME_ZONE` / `TZ=Europe/Warsaw`).
- **Limit rozmiaru bazy zależy od planu** (250 MB Podstawowy, 1 GB Rozszerzony, 3,2 GB Dla wymagających) — sprawdzić bieżące zajęcie przed dodaniem tabel Courses.
- **Recykling puli IIS** (domyślnie co 29 h) restartuje workery — dziś to przeżywają, ale nowy kod (np. harmonogramy) musi być odporny na restart w dowolnym momencie.
- **Tania weryfikacja istnieje** — wdrożenie na subdomenę (np. `test.tomsoft1.pl`) albo darmowy plan Testowy (14 dni) z kopią bazy rozstrzyga jednocześnie runtime .NET 10 i zgodność zapytań z SQL 2012, zanim ruszymy produkcję.

## Operational Story

- **Preview deploys**: brak natywnych preview URL. Jedna trwała subdomena staging (`test.tomsoft1.pl`, osobna witryna/pula w panelu Webio + kopia bazy) wdrażana z gałęzi `main` przez `workflow_dispatch`; niedostępna publicznie poza znajomością adresu — wszystkie endpointy i tak wymagają `X-TOKEN`, a treść kursów JWT. PR-y z forków nie wdrażają niczego (sekrety niedostępne).
- **Secrets**: produkcyjne `appsettings.json` (JWT key, ApiKey OpenRoutera, connection string MSSQL) leży wyłącznie na serwerze i jest **wykluczone z wdrożenia** (reguła skip msdeploy / pomijane w FTPS) — w repo tylko `appsettings.Example.json`. Poświadczenia Web Deploy/FTPS w GitHub Secrets środowiska `production` (widoczne tylko dla workflow). Rotacja: zmiana hasła FTP/Web Deploy w panelu Webio → aktualizacja GitHub Secret; rotacja klucza JWT/OpenRouter = edycja `appsettings.json` przez FTPS + recykling puli — **ręcznie, przez człowieka**.
- **Rollback**: ponowne uruchomienie workflow deploy z poprzednim tagiem/SHA (`gh workflow run deploy.yml -f ref=<poprzedni-tag>`), ok. 3–5 min. Artefakt publikacji każdego wdrożenia przechowywany jako GitHub Actions artifact (retencja 30 dni). Migracje EF **nie cofają się automatycznie** — wdrażamy je jako idempotentne skrypty (`dotnet ef migrations script --idempotent`), a rollback schematu to świadomy, ręczny skrypt `Down`.
- **Approval**: wdrożenie na produkcję wymaga ręcznej akceptacji w GitHub Environment `production` (required reviewer = właściciel). Agent może bez nadzoru: build, testy, wdrożenie na staging, odczyt logów. Tylko człowiek: publikacja na produkcję, uruchomienie skryptu migracji na bazie produkcyjnej, rotacja sekretów, operacje w panelu Webio (pula, bazy, DNS), usuwanie danych.
- **Logs**: Serilog pisze do `Logs/applog-YYYYMMDD.txt` w katalogu witryny. Agent czyta je read-only przez FTPS, np. `curl --ssl-reqd -u "$WEBIO_FTP_USER:$WEBIO_FTP_PASS" "ftp://<host>/<site>/Logs/applog-$(date +%Y%m%d).txt" | tail -n 200` (dedykowane konto FTP z uprawnieniem tylko do odczytu, jeśli panel na to pozwala). Błędy startu (500.3x): `stdoutLogEnabled="true"` w `web.config` tymczasowo + panel „Błędy witryny". Logi pipeline: `gh run list`, `gh run view <id> --log-failed`.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Runtime .NET 10 na serwerze w wersji niezgodnej z buildem (500.31) — deklarowany we wszystkich planach | Devil's advocate | L | H | Bramka staging przed produkcją; fallback: `dotnet publish -r win-x64 --self-contained`; zapytać support Webio o wersję Hosting Bundle |
| Zapytania EF Core 10 niezgodne z SQL Server 2012 | Devil's advocate | M | H | Uruchomić ścieżkę end-to-end Courses na staging z kopią bazy produkcyjnej; nie polegać tylko na testach InMemory; unikać nowych funkcji SQL (JSON, `STRING_AGG`) |
| `DeleteExistingFiles=true` kasuje treść kursów, `Logs/`, `TMP/LOTTO/*` | Unknown unknowns | H | H | `Courses:ContentPath` poza katalogiem wdrożenia; reguły skip msdeploy (`Logs`, `TMP`, `appsettings.json`, katalog kursów); w FTPS — sync bez usuwania tych ścieżek |
| Przerwany upload FTP = pół-wdrożona aplikacja, brak rollbacku | Devil's advocate | M | M | Preferować Web Deploy (transakcyjny per plik) nad FTP; `app_offline.htm` na czas wdrożenia; rollback przez ponowny workflow z poprzednim tagiem |
| Limit rozmiaru bazy w planie (250 MB / 1 GB) | Unknown unknowns | M | H | Sprawdzić zajęcie bazy teraz; w razie potrzeby podnieść plan przed startem bootcampu |
| Recykling/limity puli restartują workery lub spowalniają werdykt | Devil's advocate | L | M | Workery odporne na restart; timeout LLM ≤ 4 s w handlerze (już w `CLAUDE.md`); monitorować czasy w logach Serilog |
| Poświadczenia Web Deploy/FTP bez scope'owania | Devil's advocate | M | H | Tylko w GitHub Secrets środowiska `production` z required reviewer; osobne konto FTP read-only do logów; nigdy w repo ani w konwersacji |
| Brak natywnych operacji dla agenta (logi/rollback tylko przez FTP/workflow) | Research finding | H | L | Workflow `deploy.yml` + `gh` jako jedyny interfejs agenta; logi Serilog przez FTPS; rozważyć lekki endpoint health (np. istniejący `GetApiVersion`) do weryfikacji po wdrożeniu |
| Ciche uzależnienie od lokalnej strefy czasowej serwera | Unknown unknowns | L | M | Udokumentować w planie wyjścia; przy migracji ustawić `TZ=Europe/Warsaw` / `WEBSITE_TIME_ZONE` |
| SQL Server 2012 bez poprawek bezpieczeństwa (EOL 07.2022) | Research finding | M | M | Zaakceptowane dla MVP (zamknięta grupa); zapytać Webio o plan upgrade; plan wyjścia = Azure SQL |
| Nieaktualne strony w dokumentacji Webio (`wspierane-rozwiazania.html`: Core 1.x–3.x, PHP ≤ 7.3) | Research finding | L | L | Za źródło przyjmować stronę porównania planów (ASP.NET Core 1.0–10.x, sprawdzone 2026-09-28); potwierdzenie praktyczne na staging |
| Treść kursów wgrana przez FTP do `wwwroot` staje się publiczna | Pre-mortem | M | H | Reguła z `CLAUDE.md` (treść poza `wwwroot`, `Courses:ContentPath`); instrukcja dla admina: katalog docelowy FTP poza witryną publiczną |

## Getting Started

Zweryfikowane względem wersji z repo: SDK `10.0.100` (`global.json`), ASP.NET Core 10, Vite 7 (`build:prod1`), profile `FileSystem` w `Properties/PublishProfiles/`.

1. **Bramka weryfikacyjna (przed produkcją)** — w panelu Webio utwórz subdomenę `test.tomsoft1.pl` z pulą „No Managed Code" i kopią bazy produkcyjnej; wgraj `appsettings.json` ze stagingowymi wartościami (poza wdrożeniem).
2. **Zbuduj artefakt lokalnie lub w CI**:
   ```bash
   # klient
   cd src/client/app01 && npm ci && npm run build:prod1
   # serwer — najpierw framework-dependent; jeśli staging zwróci 500.31, dodaj: -r win-x64 --self-contained true
   dotnet publish src/server/App01/App01.Bootstrapper.Api -c Release -o publish
   # skopiuj zawartość src/client/app01/dist do publish/wwwroot
   ```
3. **Wdrożenie przez Web Deploy** (profil z panelu Webio: „Download Publishing Profile"; hasło z GitHub Secrets) — z runnera `windows-latest`:
   ```powershell
   msdeploy.exe -verb:sync -source:contentPath="$PWD\publish" `
     -dest:contentPath="<site-name>",computerName="https://<host>:8172/msdeploy.axd?site=<site-name>",userName="$env:WEBIO_USER",password="$env:WEBIO_PASS",authType="Basic" `
     -skip:objectName=filePath,absolutePath="appsettings\.json$" `
     -skip:objectName=dirPath,absolutePath="\\Logs$" `
     -skip:objectName=dirPath,absolutePath="\\TMP$" `
     -enableRule:AppOffline -allowUntrusted
   ```
   Dokładny `computerName`/`site-name` odczytaj z pobranego `.PublishSettings` (Webio nie dokumentuje formatu publicznie).
4. **Migracje** — wygeneruj skrypt idempotentny i uruchom go ręcznie na bazie staging, potem produkcji:
   ```bash
   dotnet ef migrations script --idempotent -o migrate.sql --project src/server/App01/App01.Shared.Infrastructure --startup-project src/server/App01/App01.Bootstrapper.Api
   ```
5. **Weryfikacja** — `GET https://test.tomsoft1.pl/api/portal/...` (endpoint wersji API) zwraca 200 i numer wersji; przejście ścieżki Courses end-to-end; w `Logs/applog-*.txt` brak wyjątków SQL. Po zielonym stagingu — ten sam workflow na produkcję z akceptacją w GitHub Environment.

## Out of Scope

The following were not evaluated in this research:
- Docker image configuration
- CI/CD pipeline setup (workflow `deploy.yml` zostanie zaplanowany w Plan Mode deploy → `context/deployment/deploy-plan.md`)
- Production-scale architecture (multi-region, HA, DR)

## Załącznik — Cross-check pierwotnego lidera (Azure App Service + Azure SQL)

Zachowany dla audytu decyzji i jako lista kontrolna na wypadek uruchomienia planu wyjścia.

**Devil's advocate**
1. Kod zapisuje do katalogu aplikacji (`AppDomain.BaseDirectory/TMP/LOTTO/*`, Serilog `Logs/`); przy `WEBSITE_RUN_FROM_PACKAGE=1` `wwwroot` jest read-only → `IOException` w `LottoWorker02/03`.
2. Workery liczą czas lokalny (`DateTime.Now`); App Service Linux działa w UTC → przesunięte harmonogramy.
3. B1 bez slotów: rollback = redeploy (minuty), każde wdrożenie restartuje workery; sloty od S1.
4. Azure SQL serverless (darmowa oferta) z auto-pause: wybudzenie do > 1 min łamie werdykt < 5 s; Basic 5 DTU może dławić przy importach Lotto.
5. Migracja danych z Webio SQL 2012 (bacpac) + przepięcie DNS tomsoft1.pl — ręczne kroki w oknie 5 tygodni.

**Pre-mortem (skrót)** — serverless SQL usypiał i poranne weryfikacje kończyły się `Unavailable`; import Lotto uruchamiał się o złej godzinie (UTC); run-from-package cicho psuł zapisy archiwum i logów; hotfix bez slotów restartował aplikację w trakcie zajęć; rachunek 3× wyższy niż Webio. Źródło porażki: założenie „to ten sam IIS, tylko w chmurze".

**Unknown unknowns** — etykieta „.NET 10 (Preview)" w części regionów (sprawdzić `az webapp list-runtimes --os linux`); zerowy limit quota Basic na nowych subskrypcjach; restarty maintenance platformy; „Allow Azure services" w firewallu SQL otwiera bazę dla wszystkich tenantów Azure; `/home` to udział sieciowy (wolniejsze I/O).
