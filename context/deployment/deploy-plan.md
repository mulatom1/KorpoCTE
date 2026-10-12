# Wdrożenie App01 (.NET 10) na Azure VM (IIS) przez GitHub Actions + FTPS

## Context

`context/foundation/infrastructure.md` wybrał Azure VM (Windows, IIS + SQL Server 2025 Express) jako platformę MVP. Wdrożenie realizuje workflow `.github/workflows/deploy.yml` („Deploy to Azure IIS via FTPS”). Działa i przeszło pierwsze wdrożenie na VM (2026-10-12).

Model wdrożenia jest dwuetapowy:
1. **GitHub Actions** buduje paczkę i wgrywa ją przez FTPS do katalogu stagingowego na VM (`C:\deploy\staging\tomsoft1`). Na końcu zapisuje znacznik `READY` z SHA commita.
2. **Lokalne zadanie Windows na VM** wykrywa `READY` i aktywuje wydanie w IIS. Workflow **nie** aktywuje wersji w IIS i nie dotyka katalogu aplikacji na żywo.

Zakres: obecna aplikacja (Portal / Lotto / Flashcards / Courses) na .NET 10, jedno środowisko `production`. Aplikacja działa w IIS pod ścieżką `/tomsoft1/` (`VITE_BASE_URL=/tomsoft1/`).

## Ustalenia z kodu, które kształtują wdrożenie

- **Brak auto-migracji** (w kodzie nie ma `Migrate()` ani `EnsureCreated`). Workflow nie uruchamia migracji. Zmiany schematu idą skryptem idempotentnym (`dotnet ef migrations script --idempotent`), który człowiek uruchamia ręcznie na bazie po wykonaniu backupu.
- **`wwwroot` nie ma w repo.** SPA (`src/client/app01/dist`) kopiujemy do `publish/wwwroot/` w trakcie joba.
- **`appsettings*.json` jest gitignorowany** i usuwany z paczki (`rm -f publish/appsettings*.json`). Produkcyjna konfiguracja (JWT, ApiKey, connection string) leży wyłącznie na VM i nigdy nie trafia do artefaktu ani na FTPS.
- **Pliki `.env.*` klienta są gitignorowane.** Job tworzy `src/client/app01/.env.ci` z `VITE_BASE_URL=/tomsoft1/`, `VITE_API_URL` i `VITE_APP_TOKEN` (secrets środowiska `production`) i buduje przez `vite build --mode ci`. `VITE_*` trafia do bundla przeglądarki, więc nie jest sekretem aplikacyjnym — przechowujemy je w secrets tylko dla wygody.
- **`web.config` generuje SDK przy publikacji.** Job przerywa się, jeśli `publish/web.config` nie powstał.
- **`GET api/portal/get-api-version`** zwraca `ApiVersion` z serwerowego `appsettings.json` i nie zmienia się przy wdrożeniu. Dlatego wersję builda identyfikuje statyczny `wwwroot/version.txt` z SHA commita.
- **`mirror -R --delete`** na FTPS czyści katalog stagingowy do zawartości paczki. Jest to bezpieczne tylko dlatego, że root FTP wskazuje **wyłącznie** na `C:\deploy\staging\tomsoft1`. Pliki tylko serwerowe (`Logs/`, `TMP/LOTTO/*`, treść kursów `Courses:ContentPath`, media w `wwwroot`) muszą leżeć poza stagingiem i poza folderem nadpisywanym przy aktywacji.

## Workflow `.github/workflows/deploy.yml`

Uruchamiany ręcznie (`workflow_dispatch`) z parametrami:
- `ref` (domyślnie `main`) — gałąź, tag lub SHA do wdrożenia,
- `self_contained` (bool, domyślnie `false`) — publikacja `win-x64 --self-contained`.

Job `deploy`: `runs-on: ubuntu-latest`, `environment: production`, `concurrency: deploy-azure-iis` (bez anulowania trwającego przebiegu), `permissions: contents: read`.

Kroki:
1. checkout `ref` i zapis rzeczywistego SHA (`git rev-parse HEAD`);
2. setup-dotnet `10.0.x`, setup-node `22` (cache npm);
3. `dotnet restore --locked-mode`;
4. ~~`dotnet test APPS.sln`~~ — **krok zakomentowany** (patrz Ryzyka);
5. zapis `.env.ci` i walidacja, że `VITE_API_URL` i `VITE_APP_TOKEN` są ustawione;
6. klient: `npm ci`, `npx tsc -b`, `npx vite build --mode ci`;
7. serwer: `dotnet publish src/server/App01/App01.Bootstrapper.Api -c Release --no-restore -o publish` albo wariant `-r win-x64 --self-contained true`;
8. złożenie paczki: `dist/` → `publish/wwwroot/`, `wwwroot/version.txt` = SHA, usunięcie `appsettings*.json`, kontrola obecności `web.config`;
9. `actions/upload-artifact` (`azure-tomsoft1-<sha>`, retencja 30 dni) — podstawa rollbacku;
10. instalacja `lftp`, przygotowanie zaufanego certyfikatu z `FTPS_CA_PEM` (walidacja `openssl x509`);
11. FTPS (`ftp:ssl-force`, `ssl-protect-data`, `ssl:verify-certificate yes` z własnym CA, tryb pasywny, port 21):
    - usunięcie starego `/READY`,
    - `mirror -R --delete` paczki do `/` (= `C:\deploy\staging\tomsoft1`),
    - wgranie `/READY` z SHA jako ostatni krok — znacznik, że upload jest kompletny;
12. podsumowanie w `GITHUB_STEP_SUMMARY`: SHA wgrany do stagingu, aktywacja po stronie VM.

### Secrets (GitHub Environment `production`)

`FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`, `FTPS_CA_PEM`, `VITE_API_URL`, `VITE_APP_TOKEN`. Haseł nie wklejamy do rozmowy ani do repo.

## Strona VM (poza repo)

- Serwer FTPS na IIS z certyfikatem wystawionym przez CA z `FTPS_CA_PEM`; użytkownik FTP z rootem ograniczonym do `C:\deploy\staging\tomsoft1` (nigdy katalog aplikacji na żywo).
- NSG: port 21 + zakres portów pasywnych FTP — najlepiej ograniczony, bo runnery GitHub nie mają stałych adresów IP.
- Lokalne zadanie Windows (Harmonogram zadań / usługa) konsumujące `READY`: kopiuje staging do katalogu aplikacji (z `app_offline.htm` na czas podmiany), nie nadpisuje `appsettings.json` ani katalogów tylko serwerowych, recykluje pulę i usuwa/archiwizuje `READY`. **Skrypt nie jest w repo** — opisać go tutaj (ścieżka, konto, harmonogram) albo dodać do repo jako `deploy/activate.ps1`.

## Kolejność wdrożenia

1. Człowiek: backup bazy (SQL Express — zadanie `sqlcmd BACKUP DATABASE`), a przy zmianie schematu ręczny skrypt idempotentny migracji.
2. `gh workflow run deploy.yml -f ref=<sha|tag|main>` (opcjonalnie `-f self_contained=true` przy 500.31 — brak runtime), potem `gh run watch`.
3. Zadanie na VM aktywuje wydanie po wykryciu `READY`.
4. Smoke test (ręcznie — workflow go nie robi):
   - `https://<host>/tomsoft1/version.txt` == SHA,
   - `GET /tomsoft1/api/portal/get-api-version` → 200,
   - `/tomsoft1/` → 200 z `index.html`.
5. Weryfikacja: logowanie (JWT), Lotto, Flashcards, Courses; w logu Serilog start 4 workerów i brak wyjątków SQL; następnego dnia — czy workery wykonały zadania o czasie (strefa czasowa VM, patrz `infrastructure.md`).
6. Przy 500.3x: człowiek tymczasowo włącza `stdoutLogEnabled="true"` w serwerowym `web.config`.

## Rollback

- `gh workflow run deploy.yml -f ref=<poprzedni SHA/tag>` — zbudowanie i wgranie poprzedniej wersji tą samą ścieżką.
- Alternatywnie: artefakt `azure-tomsoft1-<sha>` (30 dni) wgrany ręcznie do stagingu + `READY`.
- Migracje bazy nie cofają się automatycznie — przywrócenie backupu sprzed wdrożenia albo ręczny skrypt.

## Uprawnienia (zgodnie z infrastructure.md)

- **Agent może:** edytować i commitować workflow (za zgodą), uruchamiać build, czytać `gh run view --log-failed`, wykonywać smoke testy HTTP (read-only).
- **Tylko człowiek:** uruchomienie wdrożenia produkcyjnego, wgrywanie secrets, konfiguracja FTPS/NSG/zadania aktywującego na VM, SQL na bazie produkcyjnej, zmiany w `web.config` i `appsettings.json` na serwerze.

## Ryzyka i luki obecnego workflow

| Luka | Skutek | Propozycja |
|---|---|---|
| `dotnet test` zakomentowany | Wdrożenie nie jest blokowane przez czerwone testy | Przywrócić krok albo wdrażać tylko SHA z zielonym `pull-request.yml` |
| Brak smoke testu w jobie | Zielony job ≠ działające wydanie (aktywacja jest asynchroniczna na VM) | Krok czekający na `version.txt` == SHA z timeoutem |
| Brak required reviewer potwierdzonego w repo | Każdy z prawem `workflow_dispatch` wdraża produkcję | Sprawdzić/ustawić reviewera w Environment `production` |
| Hasło FTP w secrets zamiast OIDC | Długożyjący sekret; otwarty port 21 + pasywne | Rotacja hasła; docelowo OIDC → Blob → Run Command (`infrastructure.md`) |
| Skrypt aktywujący `READY` poza repo | Brak audytu i odtwarzalności po awarii VM | Przenieść do repo i opisać w tym pliku |
| Zdublowana linia w `Summary` | Kosmetyka | Usunąć jedną z dwóch ostatnich linii |

---

## Status

- Workflow: `.github/workflows/deploy.yml` — zacommitowany, działa (ostatnia zmiana: `7d4cc93 deploy fix and test`).
- Plan zaktualizowany do stanu workflow: 2026-10-12 (poprzednia wersja opisywała Web Deploy na hostingu współdzielonym — nieaktualna).

## Stan po wdrożeniu

| Środowisko | Data | SHA | Wariant runtime | Smoke test | Uwagi |
|---|---|---|---|---|---|
| production (Azure VM) | 2026-10-12 | ? | ? | ? | Wdrożenie przeszło (wg `infrastructure.md`); uzupełnić SHA i wariant |
