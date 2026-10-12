---
project: APPS (tomsoft1.pl — App01) / Kursy i bootcampy
researched_at: 2026-10-12
recommended_platform: Azure VM (Windows, IIS + SQL Server 2025 na tej samej maszynie) — maszyna już opłacona
runner_up: Azure App Service (Linux B1, Poland Central) + Azure SQL Database
context_type: mvp
tech_stack:
  language: C# (.NET 10) + TypeScript 5.9
  framework: ASP.NET Core 10 Minimal APIs (modularny monolit, EF Core 10 / SQL Server, compat 110) + React 19 SPA serwowane z tego samego hosta
  runtime: .NET 10 (ASP.NET Core Module V2 na IIS, in-process)
---

## Recommendation

**Wdrażamy na Azure VM (Windows, IIS + SQL Server 2025).**

Stack ma cztery twarde wymagania: stały proces (4 `BackgroundService` — Lotto ×3, Portal ×1), trwały system plików (treść kursów poza `wwwroot`, `Logs/`, `TMP/LOTTO/*`), SQL Server i runtime .NET 10. Na kryteriach agent-friendly VM wypada przeciętnie (6/10 — przegrywa na „managed"), ale wygrywa kontekstem z wywiadu: maszyna jest **już opłacona** (koszt krańcowy ≈ 0 vs ok. +$18/mies. za App Service + Azure SQL), deweloper zna **głównie Azure**, baza jest **ko-lokowana** na tej samej maszynie, a SQL Server 2025 zdejmuje największe ryzyko obecnej produkcji — SQL Server 2012 po EOL. Model hostingu (IIS, Windows, `web.config`, lokalna strefa czasowa po `tzutil`) nie wymaga zmian w kodzie aplikacji.

Ścieżka decyzji: wywiad 2026-10-12 (opłacona Azure VM z IIS + SQL Server 2025) → research 2 agentów (Azure VM; App Service / Railway / Fly.io / Render) → lider Azure VM → cross-check → **decyzja dewelopera: Azure VM, ryzyka odnotowane**.

## Platform Comparison

Wywiad (2026-10-12): stałe procesy — **tak**; koszt/DX — **posiadam opłaconą Azure VM z IIS + SQL Server 2025** (koszt infrastruktury już poniesiony); znajomość — **głównie Azure VM / Azure**; zasięg — **jeden region (PL)**; usługi — **preferowana ko-lokacja**.

Filtry twarde: stały proces + runtime .NET 10 + SQL Server (EF Core) + trwały dysk.

| Platforma | Filtr twardy | CLI-first | Managed | Agent-readable docs | Stable deploy API | MCP / integracja | Wynik |
|---|---|---|---|---|---|---|---|
| Azure App Service + Azure SQL | ✓ | Pass | Pass | Pass | Pass | Partial | **9/10** |
| Railway | ✓ (Dockerfile + kontener MSSQL) | Pass | Partial | Pass | Pass | Partial | **8/10** |
| Fly.io | ✓ (Dockerfile + MSSQL na maszynie) | Pass | Partial | Pass | Pass | Partial | **8/10** |
| Render | ✓ (Docker + MSSQL jako private service) | Partial | Partial | Pass | Pass | Partial | 7/10 |
| **Azure VM (IIS + SQL 2025)** | ✓ | Pass | Fail | Pass | Partial | Partial | **6/10** |
| Cloudflare (Workers/Containers) | ✗ | — | — | — | — | — | odrzucona |
| Vercel | ✗ | — | — | — | — | — | odrzucona |
| Netlify | ✗ | — | — | — | — | — | odrzucona |

Punktacja: Pass = 2, Partial = 1, Fail = 0. Wynik to heurystyka, nie bramka. Wagi z wywiadu (koszt już poniesiony, znajomość Azure, ko-lokacja, region PL) przesuwają VM na pierwsze miejsce mimo najniższego wyniku „managed".

**Azure VM (IIS + SQL Server 2025)** — operacje przez `az vm start|restart|deallocate`, `az vm run-command` (Managed Run Command v2: timeouty, długie skrypty, parametry chronione, wynik do append blob; `instanceView` tnie wynik do ostatnich 4 KB), `az network nsg rule ...`. Wdrożenie: OIDC `azure/login` → zip do Blob → Run Command v2 rozpakowuje do `releases/<sha>` i przełącza ścieżkę fizyczną witryny — bez otwartych portów i bez przechowywanych haseł. Managed = **Fail**: łatanie OS, IIS, .NET Hosting Bundle i SQL Server spoczywa na deweloperze (Azure Update Manager, GA). Docs: markdown w repo MicrosoftDocs (`AspNetCore.Docs`, `EntityFramework.Docs`) i `Accept: text/markdown` na learn.microsoft.com; `learn.microsoft.com/llms.txt` zwraca 404; Microsoft Learn MCP (`https://learn.microsoft.com/api/mcp`) publiczny, bez formalnej etykiety GA. MCP: Azure MCP Server 2.0 **GA** (VM start/stop, zapytania Azure Monitor), ale **bez Run Command**; npm `latest` to `3.0.0-beta.50` (**BETA**, 2026-10-06) — przypiąć 2.x. Sprawdzone 2026-10-12.

**Azure App Service + Azure SQL** — `az webapp deploy --type zip`, `az webapp log tail`; B1 Linux ≈ $13/mies., Windows ≈ $55/mies., Azure SQL Basic ≈ $4,90/mies. (Poland Central, Retail Prices API). Always On od B1, sloty od Standard (rollback = redeploy). .NET 10: GA od 11.2025, App Service trzymał etykietę „(Preview)" do walidacji regionów — brak jawnego potwierdzenia GA dla Poland Central (sprawdzić `az webapp list-runtimes`). Linux = UTC i `/home` jako udział sieciowy. Jedyny kandydat z zarządzanym SQL Serverem w regionie PL.

**Railway** — `railway up`, `railway logs`, rollback w dashboardzie; `llms.txt` + `.md`; MCP oficjalny, ale `@railway/mcp-server@0.1.12` (pre-1.0, brak etykiety statusu — obniżone do Partial względem 09.2026). SQL Server tylko jako samodzielny kontener (≥ 2 GB RAM, `MSSQL_PID=Express`) ≈ $20–30/mies. Region EU: Amsterdam. Podwyżka 2026 zgłaszana tylko przez źródło wtórne.

**Fly.io** — `fly deploy`, `fly releases --image`, `fly logs`; `llms.txt`; `fly mcp server` **EXPERIMENTAL**. **Region Warszawa (`waw`) wycofany** w ramach konsolidacji regionów — najbliżej `fra`/`arn`/`ams`. SQL Server samodzielnie (1 wolumen ↔ 1 maszyna) ≈ $20–25/mies.

**Render** — Docker, deploy hooks, rollback tylko dashboard/API; `llms.txt` potwierdzony; MCP hostowany bez etykiety statusu (repo v0.3.0). Dysk wyłącza zero-downtime deploy. SQL Server jako private service ≥ Standard ($25). Region: Frankfurt. 2026-08-26 zmiana nazw planów na „compute plans" bez zmiany cen.

**Cloudflare / Vercel / Netlify** — brak runtime .NET z procesem stałym, brak SQL Servera, dysk efemeryczny. Odrzucone (stan z 2026-09-28, bez zmian istotnych dla filtrów).

### Shortlisted Platforms

#### 1. Azure VM — IIS + SQL Server 2025 (Recommended — decyzja dewelopera po cross-checku)

Wygrywa kontekstem: koszt już poniesiony, znajomość Azure, baza na tej samej maszynie, nowoczesny SQL Server zamiast 2012, ten sam model IIS co dziś (bez zmian w kodzie aplikacji). Pełna kontrola nad pulą aplikacji pozwala skonfigurować workery tak, by nie były recyklowane — czego na hostingu współdzielonym nie da się zagwarantować. Luki (brak „managed", własny skrypt wdrożenia i rollbacku) kompensujemy workflow `deploy.yml` (FTPS do stagingu + lokalna aktywacja na VM; docelowo OIDC + Run Command) i runbookiem utrzymania.

#### 2. Azure App Service (Linux B1) + Azure SQL — runner-up

Najlepszy wynik na kryteriach (9/10) i jedyny zarządzany SQL Server w Poland Central, ≈ $18/mies. Przegrywa dodatkowym kosztem obok już opłaconej VM, UTC na Linuxie (`DateTime.Now` w workerach), brakiem slotów na B1 i ryzykiem zapisów do katalogu aplikacji przy run-from-package. Plan wyjścia, jeśli utrzymanie VM okaże się zbyt kosztowne czasowo.

#### 3. Railway

Wysoki wynik na kryteriach (8/10): `railway up` / `railway logs`, `llms.txt`, oficjalny MCP (pre-1.0). Remis z Fly.io rozstrzyga region (Fly.io wycofał Warszawę) i status MCP (Fly.io: experimental). Przegrywa z VM: SQL Server tylko jako samodzielnie utrzymywany kontener (≈ $20–30/mies. ponad opłaconą VM), brak regionu w Polsce (Amsterdam), a obowiązki administracyjne bazy zostają po stronie dewelopera — bez przewagi nad VM.

## Anti-Bias Cross-Check: Azure VM (IIS + SQL Server 2025)

### Devil's Advocate — Weaknesses

1. **Administracja spada na dewelopera** — łatanie Windows, IIS, .NET Hosting Bundle i SQL Server (Azure Update Manager); restart po łatce zatrzymuje workery i może trafić w czas zajęć. Funkcja „automated patching" rozszerzenia SQL IaaS jest wycofywana (planowo 2027-09-17).
2. **Licencja SQL Server 2025** — edycje Standard/Enterprise Developer nie są licencjonowane produkcyjnie. Express (limit ok. 50 GB na bazę — źródło wtórne) jest legalny, ale **Automated Backup rozszerzenia SQL IaaS nie obsługuje Express** — backup trzeba oskryptować.
3. **Model opłaty VM** — Azure Reservations są 1- lub 3-letnie i pokrywają tylko compute. Deweloper potwierdził (2026-10-12), że opłacona jest 5-letnia subskrypcja korzystania z VM, a nie rezerwacja; ryzyko zamknięte.
4. **Wdrożenie i rollback zbudowane częściowo** — działający `deploy.yml` wgrywa paczkę przez FTPS (hasło w GitHub Secrets, port 21 + porty pasywne otwarte w NSG) do `C:\deploy\staging\tomsoft1` i zostawia znacznik `READY`; aktywację w IIS robi skrypt na VM, którego nie ma w repo. Job nie uruchamia testów (krok zakomentowany) i nie robi smoke testu — zielony job nie oznacza działającego wydania. Docelowa ścieżka bez długożyjącego hasła i otwartych portów: OIDC → Blob → Run Command v2 → przełączenie `releases/<sha>`.
5. **Jedna maszyna = jeden punkt awarii** — portal, baza i workery padają razem; SLA dla pojedynczej VM tylko z dyskami Premium SSD.

### Pre-Mortem — How This Could Fail

Migrację zaczęto trzy tygodnie przed startem bootcampu (2026-11-04). Baza przeszła z SQL 2012 na 2025 przez backup/restore, compat 110 został w kodzie, więc zapytania działały. VM działała jednak w UTC, a workery liczyły czas z `DateTime.Now` — import Lotto ruszał godzinę za wcześnie i pobierał niepełne wyniki. `Logs/` i `TMP/LOTTO` zostały wewnątrz folderu release, więc pierwsze przełączenie wersji „zgubiło" archiwum. Ktoś otworzył RDP 3389 w NSG „na chwilę" i nie zamknął. Certyfikat win-acme nie odnowił się, bo zadanie harmonogramu działało na koncie bez uprawnień do magazynu certyfikatów — w trzecim tygodniu kursu przeglądarki pokazały ostrzeżenie. Update Manager zrestartował VM o 19:00, w trakcie zajęć. Na koniec audyt licencji wykazał, że SQL to edycja Developer. Nie zawiodła chmura — zawiodło założenie „to tylko IIS, jak na hostingu współdzielonym" i brak runbooka dla serwera, za który teraz w całości odpowiada deweloper.

### Unknown Unknowns

- **Workery na IIS żyją tylko przy właściwej konfiguracji**: rola Application Initialization, pula `startMode="AlwaysRunning"`, `idleTimeout=0` (działa tylko **in-process**), witryna `preloadEnabled=true`, `<applicationInitialization doAppInitAfterRestart="true"/>`, wyłączony recykling okresowy (`periodicRestart time=00:00:00`). Klucze Data Protection trzeba utrwalić („Load User Profile"), inaczej znikają przy restarcie i unieważniają ciasteczka/tokeny chronione.
- **EF Core 10 domyślnie tłumaczy `Contains` na wiele parametrów skalarnych** (nie `OPENJSON`), więc compat 110 na SQL 2025 nic nie kosztuje. Podniesienie do ≥ 170 spowoduje mapowanie kolumn JSON na typ `json` i niechciane migracje `ALTER` — zmieniać tylko świadomie, z pustym `migrations add` jako dowodem.
- **Azure Windows VM domyślnie działa w UTC** — `tzutil /s "Central European Standard Time"` albo (docelowo) `TimeZoneInfo`/`DateTime.UtcNow` w workerach.
- **Run Command zwraca tylko ostatnie 4 KB w `instanceView`** — dłuższe logi kierować do append blob (`--output-blob-uri`) albo Azure Monitor Agent „Custom Text Logs" (GA; wymaga DCR, tabeli `_CL`, linii zaczynających się znacznikiem czasu ISO 8601 — pasuje do Serilog).
- **Azure MCP Server: npm `latest` to 3.0 beta** — przypiąć stabilne 2.x; brak narzędzia Run Command, więc agent i tak używa `az`.

## Operational Story

Stan obecny = `.github/workflows/deploy.yml` („Deploy to Azure IIS via FTPS”), szczegóły w `context/deployment/deploy-plan.md`. Elementy oznaczone „docelowo” to plan, nie stan faktyczny.

- **Preview deploys**: brak — jedno środowisko `production`, `workflow_dispatch` z parametrem `ref` (gałąź/tag/SHA). Docelowo druga witryna IIS na tej samej VM (np. `test.tomsoft1.pl`, osobna pula, osobna baza `App01_Test`). Wszystkie endpointy i tak wymagają `X-TOKEN`, treść kursów — JWT. PR-y z forków nie wdrażają niczego (brak dostępu do sekretów środowiska).
- **Secrets**: produkcyjne `appsettings.json` (klucz JWT, ApiKey OpenRoutera, connection string) leży wyłącznie na VM i nigdy nie trafia do paczki (`rm -f publish/appsettings*.json` w jobie). GitHub Environment `production` trzyma `FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`, `FTPS_CA_PEM` oraz `VITE_API_URL` / `VITE_APP_TOKEN` (te dwa trafiają do bundla przeglądarki — nie są sekretami aplikacji). Konto FTP ma root ograniczony do `C:\deploy\staging\tomsoft1`, nigdy do katalogu aplikacji na żywo. Rotacja hasła FTP, klucza JWT/OpenRouter = ręcznie, przez człowieka (edycja na VM + recykling puli). Docelowo **OIDC** (federated credential na `environment:production`, rola VM Contributor + Storage Blob Data Contributor) zamiast hasła FTP.
- **Rollback**: `gh workflow run deploy.yml -f ref=<poprzedni SHA/tag>` — ponowny build i wgranie przez FTPS + aktywacja na VM; alternatywnie artefakt `azure-tomsoft1-<sha>` (retencja 30 dni) wgrany ręcznie do stagingu ze znacznikiem `READY`. Czas = pełny przebieg joba. Docelowo przełączanie `releases/<sha>` (< 1 min, 5 ostatnich wersji). Migracje EF **nie cofają się automatycznie** — skrypt idempotentny w przód, cofnięcie schematu to świadomy ręczny skrypt + backup bazy sprzed wdrożenia.
- **Approval**: wdrożenie uruchamia człowiek (`workflow_dispatch`); job działa w Environment `production` — required reviewer = właściciel (do potwierdzenia w ustawieniach repo). Agent bez nadzoru: build, testy lokalne, odczyt logów pipeline'u i smoke testy HTTP. Tylko człowiek: wdrożenie produkcyjne, migracja bazy produkcyjnej, zmiany NSG, łatki i restart VM, rotacja sekretów, przepięcie DNS, usuwanie danych.
- **Logs**: Serilog pisze do `D:\App01\shared\Logs\applog-YYYYMMDD.txt` (poza `releases/`). Agent czyta read-only: `az vm run-command invoke -g <rg> -n <vm> --command-id RunPowerShellScript --scripts "Get-Content D:\App01\shared\Logs\applog-$(Get-Date -f yyyyMMdd).txt -Tail 100"`; dłuższe okna — Azure Monitor Custom Text Logs + `az monitor log-analytics query`. Logi IIS/stdout przy błędach startu 500.3x: tymczasowo `stdoutLogEnabled="true"`. Pipeline: `gh run list`, `gh run view <id> --log-failed`.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Migracja (dane SQL 2012 → 2025, DNS, certyfikat) nie zdąży przed startem bootcampu 2026-11-04 | Pre-mortem | L | H | **Wdrożenie na VM przeszło bez problemu (2026-10-12).** Rollback przez `deploy.yml -f ref=<poprzedni SHA>` + backup bazy |
| Wdrożenie bez testów i bez smoke testu (`dotnet test` zakomentowany w `deploy.yml`, aktywacja asynchroniczna na VM) | Research finding (2026-10-12) | M | H | Przywrócić `dotnet test` albo wdrażać tylko SHA z zielonym `pull-request.yml`; krok czekający na `version.txt` == SHA |
| Skrypt aktywujący `READY` na VM poza repo | Research finding (2026-10-12) | M | M | Przenieść skrypt do repo i opisać w `deploy-plan.md`; po odtworzeniu VM bez niego wdrożenie nie działa |
| Edycja SQL Server 2025 Developer użyta produkcyjnie | Devil's advocate | — | — | **Zamknięte (2026-10-12):** SQL Server 2025 **Express** 17.0.1000.7 — licencjonowany produkcyjnie |
| Limity edycji Express (rozmiar bazy, pamięć bufora, rdzenie) | Research finding | L | M | Limit rozmiaru bazy w 2025 ok. 50 GB (źródło wtórne) — wystarcza dla MVP; monitorować rozmiar bazy i czasy zapytań rankingu/dashboardu w logach |
| SQL Server 2025 na buildzie RTM bez cumulative updates | Research finding | M | M | Sprawdzić dostępne CU dla SQL Server 2025 i zainstalować w oknie serwisowym (Azure Update Manager) |
| Brak backupu bazy (Automated Backup nie obsługuje Express, Express nie ma SQL Server Agent) | Devil's advocate | H | H | **Potwierdzone (Express).** Harmonogram zadań Windows: `sqlcmd -Q "BACKUP DATABASE ... TO DISK=... WITH COMPRESSION, CHECKSUM"` (lub skrypt Ola Hallengren) + kopia pliku do Blob (`azcopy`), retencja; okresowy test `RESTORE` na bazie testowej; Azure Backup dla całej VM |
| Workery zatrzymywane przez bezczynność/recykling puli IIS | Unknown unknowns | H | M | `AlwaysRunning`, `idleTimeout=0`, `preloadEnabled`, Application Initialization, `periodicRestart=0`, model in-process — w skrypcie provisioningu |
| Strefa czasowa VM = UTC, workery liczą `DateTime.Now` | Unknown unknowns | H | M | **Świadomie odroczone (decyzja dewelopera 2026-10-12)** — do czasu poprawki harmonogramy workerów są przesunięte o 1–2 h względem czasu PL. Docelowo `tzutil /s "Central European Standard Time"` lub `TimeZoneInfo` w kodzie; weryfikacja godziny w logu workera |
| Przełączenie folderu release gubi `Logs/`, `TMP/LOTTO/*`, treść kursów | Pre-mortem | M | H | Wszystkie ścieżki zapisu i `Courses:ContentPath` w `D:\App01\shared\` (konfiguracja), ACL Modify dla tożsamości puli |
| Restart po łatkach w trakcie zajęć | Devil's advocate | M | M | Azure Update Manager z oknem serwisowym poza godzinami bootcampu (np. noc z niedzieli na poniedziałek) |
| Otwarte porty zarządzania (3389, 1433) i FTPS (21 + zakres pasywny) | Pre-mortem | M | H | NSG: 80/443 + FTPS tylko na czas potrzeby lub z wąskim zakresem pasywnym; FTPS wyłącznie z TLS (`ssl-force`, weryfikacja CA — tak jest w `deploy.yml`); RDP przez Bastion lub JIT; docelowo wdrożenie przez Run Command i zamknięcie FTP |
| Certyfikat Let's Encrypt (win-acme) nie odnowiony | Pre-mortem | M | M | Zadanie odnowienia testowane `wacs --renew --force`; alert na wygaśnięcie (np. monitor dostępności z alertem na certyfikat) |
| Zakres opłaconej subskrypcji VM | Research finding | L | L | **Wyjaśnione (2026-10-12):** opłacona jest 5-letnia subskrypcja korzystania z VM (nie Azure Reservation). Przy przedłużaniu sprawdzić, czy licencje Windows/SQL są wliczone |
| Pojedyncza VM — brak HA | Devil's advocate | L | H | Zaakceptowane dla MVP; Azure Backup VM + backup bazy w Blob; runbook odtworzenia; plan wyjścia = App Service + Azure SQL |
| Zapytania EF Core 10 / compat level po zmianie serwera | Unknown unknowns | L | M | Zostawić `UseCompatibilityLevel(110)`; ścieżka Courses end-to-end na witrynie testowej z kopią bazy |
| Klucze Data Protection gubione przy restarcie puli | Unknown unknowns | M | M | „Load User Profile" = true na puli albo `PersistKeysToFileSystem` w katalogu `shared` |
| Kopia `<slug>.md` kursu w `wwwroot/media/courses/<slug>/` = publiczny wyciek treści | Research finding (2026-09-28) | M | H | Do `wwwroot/media` tylko media; `.md` wyłącznie w `Courses:ContentPath` poza witryną; po publikacji sprawdzić, że `/media/courses/<slug>/<slug>.md` nie zwraca treści |
| Azure MCP 3.0 beta zainstalowany zamiast stabilnego | Research finding | M | L | Przypiąć wersję 2.x w konfiguracji MCP |

## Getting Started

Zweryfikowane względem wersji z repo: SDK `10.0.100` (`global.json`), ASP.NET Core 10 (ANCM V2, in-process), EF Core 10, Vite 7.

1. **Inwentaryzacja VM (ręcznie, raz)** — przez Bastion/Run Command sprawdź: edycję SQL (`SELECT SERVERPROPERTY('Edition'), SERVERPROPERTY('ProductVersion')`), strefę czasową (`tzutil /g`), czy zainstalowany jest **ASP.NET Core 10 Hosting Bundle** (`dotnet --list-runtimes`), reguły NSG, typ zakupu w Cost Management. Brakujący Hosting Bundle zainstaluj z dotnet.microsoft.com, potem `iisreset`.
2. **Provisioning IIS** (skrypt PowerShell w repo, uruchamiany przez `az vm run-command invoke`): rola `Web-AppInit`, katalogi `D:\App01\{releases,shared\Logs,shared\TMP,shared\courses}`, pula „No Managed Code" z `startMode=AlwaysRunning`, `processModel.idleTimeout=00:00:00`, `recycling.periodicRestart.time=00:00:00`, `processModel.loadUserProfile=true`; witryna z `preloadEnabled=true`; `tzutil /s "Central European Standard Time"`; ACL Modify dla `IIS AppPool\App01` na `shared`.
3. **Wdrożenie — stan obecny (działa od 2026-10-12)**: `.github/workflows/deploy.yml` na `ubuntu-latest` buduje klienta (`VITE_BASE_URL=/tomsoft1/`) i `dotnet publish` (framework-dependent lub `win-x64 --self-contained`), usuwa `appsettings*.json`, dopisuje `wwwroot/version.txt` z SHA, wgrywa paczkę przez FTPS (`lftp mirror -R --delete`, weryfikacja certyfikatu z `FTPS_CA_PEM`) do `C:\deploy\staging\tomsoft1` i zostawia znacznik `READY`; lokalne zadanie na VM aktywuje wydanie w IIS. Uruchomienie: `gh workflow run deploy.yml -f ref=main`. Szczegóły i luki: `context/deployment/deploy-plan.md`.

   **Docelowo — OIDC dla GitHub Actions** (zamiast hasła FTP i otwartego portu 21):
   ```bash
   az ad app create --display-name app01-deploy
   az ad app federated-credential create --id <appId> --parameters '{"name":"gh-prod","issuer":"https://token.actions.githubusercontent.com","subject":"repo:<owner>/<repo>:environment:production","audiences":["api://AzureADTokenExchange"]}'
   az role assignment create --assignee <appId> --role "Virtual Machine Contributor" --scope <vm-resource-id>
   ```
   Workflow (zastąpi obecną ścieżkę FTPS): `azure/login` (OIDC) → build klienta + `dotnet publish src/server/App01/App01.Bootstrapper.Api -c Release -o publish` (bez `appsettings*.json`) → zip do Blob → `az vm run-command create --name deploy-<sha> --script-uri <blob-sas>` (rozpakowanie do `releases/<sha>`, `app_offline.htm`, przełączenie `physicalPath`) → smoke test `version.txt` == SHA i `GET /api/portal/get-api-version` → 200.
4. **Baza** — backup dotychczasowej bazy produkcyjnej (SQL 2012) → restore na SQL 2025 (baza testowa najpierw); migracje tylko jako skrypt idempotentny uruchamiany ręcznie:
   ```bash
   dotnet ef migrations script --idempotent -o migrate.sql --project src/server/App01/App01.Shared.Infrastructure --startup-project src/server/App01/App01.Bootstrapper.Api
   ```
   Skonfiguruj backup: SQL Express nie ma SQL Server Agent, więc zadanie w Harmonogramie zadań Windows (`sqlcmd` + `BACKUP DATABASE ... TO DISK`, kopia do Blob przez `azcopy`) oraz Azure Backup dla VM.
5. **Bramka przed przepięciem DNS** — witryna testowa na VM przechodzi: workery uruchamiają się o właściwej godzinie (log), ścieżka Courses end-to-end, restart puli nie gubi sesji (Data Protection), certyfikat win-acme odnawia się na sucho. Dopiero wtedy wdrożenie produkcyjne z akceptacją w GitHub Environment i przepięcie DNS `tomsoft1.pl`.

## Out of Scope

The following were not evaluated in this research:
- Docker image configuration
- CI/CD pipeline setup (działający workflow FTPS opisany w `context/deployment/deploy-plan.md`; migracja na OIDC + Run Command do zaplanowania osobno)
- Production-scale architecture (multi-region, HA, DR)
