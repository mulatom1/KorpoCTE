# Pierwsze wdrożenie App01 (.NET 10) na Webio.pl przez GitHub Actions + Web Deploy

## Context

`context/foundation/infrastructure.md` wybrał Webio.pl (IIS + MSSQL 2012) jako platformę MVP. Produkcja (tomsoft1.pl) działa dziś na wersji .NET 8, publikowanej ręcznie z Visual Studio (`FolderProfile*.pubxml`, `DeleteExistingFiles=true`). Repo jest już po migracji na .NET 10, ale CI (`.github/workflows/pull-request.yml`) tylko buduje i testuje, niczego nie publikuje.

Cel: zautomatyzowane, powtarzalne wdrożenie z GitHub Actions (Web Deploy, runner Windows):
1. najpierw na **tomsoft1.com.pl** (witryna testowa, bramka „czy .NET 10 działa na Webio”),
2. potem na **tomsoft1.pl** (produkcja, z ręczną akceptacją),

tak żeby nie zniszczyć plików, które istnieją tylko na serwerze.

Decyzje użytkownika: tomsoft1.com.pl pełni rolę testu, mechanizm = Web Deploy, bez zakładania osobnego stagingu z kopią bazy. Zakres = obecna aplikacja (Portal / Lotto / Flashcards) na .NET 10. Modułu Courses jeszcze nie ma w repo.

## Ustalenia z kodu, które kształtują plan

- **Brak auto-migracji** (w kodzie nie ma `Migrate()` ani `EnsureCreated`). Migracje zostały niedawno zsquashowane: `20260927063454_Initial`, `…PortalFirstUserAdmin`, `…LottoDicts`. Identyfikatory w `__EFMigrationsHistory` na produkcji prawie na pewno są inne, więc **w tym wdrożeniu nie uruchamiamy żadnego skryptu migracji**. Jest tylko ręczna kontrola zgodności schematu (bramka G3).
- **`wwwroot` nie ma w repo.** SPA (`src/client/app01/dist`) kopiowano do publikacji ręcznie. Na serwerze w `wwwroot` mogą leżeć pliki tylko serwerowe (np. HLS `.m3u8` / `.ts`, bo `Program.cs` mapuje te typy), a do tego `Logs/` i `TMP/LOTTO/*`. **Wdrożenie nie może niczego kasować**, stąd `-enableRule:DoNotDeleteRule`.
- **`appsettings.json` jest gitignorowany.** Na serwerze leży wersja produkcyjna i musi zostać nietknięta (reguła skip w msdeploy).
- **Pliki `.env.prod1` i `.env.prod2` są gitignorowane** (`src/client/app01/.gitignore: .env.*`). CI musi zbudować bundle ze zmiennych środowiska GitHub: `VITE_BASE_URL=/`, `VITE_API_URL` (`https://tomsoft1.com.pl` albo `https://tomsoft1.pl`) oraz `VITE_APP_TOKEN`.
- **CORS jest `AllowAnyOrigin`.** Bundle testowy zbudowany z `VITE_API_URL` produkcji po cichu uderzałby w API produkcji. Każde środowisko potrzebuje więc własnego `VITE_API_URL`.
- **`web.config` generuje SDK przy publikacji.** Jeśli ten na serwerze ma ręczne zmiany (zmienne środowiskowe, `hostingModel`, stdout), nadpisanie go zmieni zachowanie aplikacji (bramka G2).
- **`GET api/portal/get-api-version`** (bez `X-TOKEN`) zwraca `ApiVersion` z `appsettings.json` na serwerze. Nie zmienia się przy wdrożeniu, więc do weryfikacji builda dokładamy statyczny `wwwroot/version.txt` z SHA commita.

## Bramki ręczne (tylko człowiek, przed uruchomieniem workflow)

- **G1 — dane Web Deploy.** W panelu Webio, dla obu witryn: włącz Web Deploy (jeśli jest wyłączony) i pobierz `.PublishSettings`. Z pliku odczytaj `publishUrl`, `msdeploySite`, `userName` i `userPWD`, a potem dodaj je jako secrets w GitHub Environments:
  - `test` (tomsoft1.com.pl, bez reviewera),
  - `production` (tomsoft1.pl, required reviewer = właściciel).

  Secrets: `WEBIO_MSDEPLOY_URL`, `WEBIO_SITE`, `WEBIO_USER`, `WEBIO_PASS`, `VITE_APP_TOKEN`. Variable: `VITE_API_URL`. Haseł nie wklejamy do rozmowy ani do repo.
- **G2 — kopia zapasowa i `web.config`.** Przez FTP pobierz pełną kopię katalogu obu witryn (to jest **rollback pierwszego wdrożenia**, bo poprzedniej wersji nie ma w pipeline) i zrób backup bazy z panelu Webio. Sprawdź, czy serwerowy `web.config` ma ręczne zmiany. Jeśli tak, przed wdrożeniem trafia do projektu (patrz Zmiany w repo, krok 3).
- **G3 — zgodność schematu.** Na bazie produkcyjnej: `SELECT MigrationId FROM __EFMigrationsHistory` oraz porównanie tabel i kolumn z `AppDbContextModelSnapshot.cs`. Jeśli schemat odpowiada modelowi, wdrażamy bez migracji. Wpisanie nowych ID do historii („baseline”) to osobna, ręczna decyzja na później. Jeśli schemat się różni, **stop**: przed produkcją trzeba przygotować skrypt różnicowy. Sprawdź też, z której bazy korzysta tomsoft1.com.pl.
- **G4 — rozmiar bazy.** Sprawdź w panelu zajęcie bazy względem limitu planu.

## Zmiany w repo

1. **Nowy plik `.github/workflows/deploy.yml`**, uruchamiany przez `workflow_dispatch` z parametrami `target` (`test` | `production`), `ref` (domyślnie `main`) i `self_contained` (bool, domyślnie `false`).
   - Job `deploy`, `runs-on: windows-latest`, `environment: ${{ inputs.target }}`, `concurrency: deploy-${{ inputs.target }}`.
   - Kroki:
     - checkout `ref`;
     - setup-dotnet `10.0.x`, setup-node `22` (cache npm, jak w `pull-request.yml`);
     - `dotnet restore --locked-mode`, potem `dotnet test APPS.sln` (`ASPNETCORE_ENVIRONMENT=Test`, jak w CI);
     - klient: zapis `src/client/app01/.env.ci` z `VITE_BASE_URL=/`, `VITE_API_URL=${{ vars.VITE_API_URL }}` i `VITE_APP_TOKEN=${{ secrets.VITE_APP_TOKEN }}`, potem `npm ci`, `npx tsc -b`, `npx vite build --mode ci`;
     - serwer: `dotnet publish src/server/App01/App01.Bootstrapper.Api -c Release -o publish`, a przy `self_contained: true` dodatkowo `-r win-x64 --self-contained true`;
     - skopiuj `src/client/app01/dist/*` do `publish/wwwroot/` i zapisz `publish/wwwroot/version.txt` z `${{ github.sha }}`;
     - usuń z paczki `publish/appsettings*.json` (zostaje serwerowy);
     - `actions/upload-artifact` paczki `publish` (retencja 30 dni, to podstawa rollbacku dla kolejnych wdrożeń);
     - msdeploy (`C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe`, obecny na `windows-latest`; w razie braku zainstalować go przez `choco install webdeploy`):
       ```
       -verb:sync -source:contentPath="$PWD\publish"
       -dest:contentPath="$env:WEBIO_SITE",computerName="https://$env:WEBIO_MSDEPLOY_URL/msdeploy.axd?site=$env:WEBIO_SITE",userName=…,password=…,authType=Basic
       -enableRule:AppOffline -enableRule:DoNotDeleteRule
       -skip:objectName=filePath,absolutePath="appsettings.*\.json$"
       -allowUntrusted -retryAttempts:3
       ```
     - smoke test (`curl`, fail = czerwony job):
       - `https://<host>/version.txt` == SHA;
       - `https://<host>/api/portal/get-api-version` → 200;
       - `https://<host>/` → 200 z `index.html`.
2. **`pull-request.yml` bez zmian.** Deploy sam uruchamia testy, więc nie ma zależności między workflowami.
3. **Tylko jeśli G2 wykaże ręczne zmiany:** dodaj `src/server/App01/App01.Bootstrapper.Api/web.config` z tymi ustawieniami (SDK połączy go przy publikacji). Bez sekretów w pliku.
4. **Nowy plik `context/deployment/deploy-plan.md`:** zatwierdzona kopia tego planu (audyt „co miało się wydarzyć”, wymóg lekcji) plus sekcja „Stan po wdrożeniu” uzupełniana po wykonaniu: SHA, data, wynik smoke testów, który wariant runtime zadziałał.
5. **Nie ruszamy:** kodu aplikacji, migracji, `FolderProfile*.pubxml` (zostają jako ręczny fallback), `appsettings.Example.json`.

## Kolejność wykonania

1. Człowiek: G1–G4.
2. Agent: commit `deploy.yml` i `deploy-plan.md` na gałęzi, PR, zielone CI, merge (za zgodą użytkownika).
3. `gh workflow run deploy.yml -f target=test`, potem `gh run watch`.
   - Jeśli smoke test zwróci 500.3x, człowiek włącza tymczasowo `stdoutLogEnabled="true"` w `web.config` na serwerze. Agent czyta log przez FTPS, a przy 500.31 (brak runtime) ponawia z `-f self_contained=true`.
4. Weryfikacja ręczna na tomsoft1.com.pl: logowanie, Lotto, Flashcards (OpenRouter), a w `Logs/applog-*.txt` start 4 workerów i brak wyjątków SQL.
5. `gh workflow run deploy.yml -f target=production -f self_contained=<wariant z kroku 3>`. Czeka na akceptację właściciela w GitHub Environment.
6. Weryfikacja produkcji jak w kroku 4 i następnego dnia: czy workery Lotto i Portal wykonały zadania o czasie.
7. Rollback pierwszego wdrożenia: człowiek wgrywa przez FTP kopię z G2 (wersja .NET 8). Kolejne rollbacki: `gh workflow run deploy.yml -f ref=<poprzedni SHA/tag>`.

## Uprawnienia (zgodnie z infrastructure.md)

- **Agent może:** pisać i commitować workflow, uruchamiać `target=test`, czytać `gh run view --log-failed`, czytać logi przez FTPS (read-only).
- **Tylko człowiek:** akceptacja `production`, operacje w panelu Webio, wgrywanie secrets, SQL na bazie produkcyjnej, zmiany w `web.config` i `appsettings.json` na serwerze, rollback z kopii FTP.

## Weryfikacja końcowa

- Lokalnie przed PR: `dotnet build APPS.sln` i `dotnet test APPS.sln`. YAML sprawdzony przez `actionlint` (jeśli dostępny) albo przez pierwszy przebieg na `target=test`.
- Job deploy zielony dla `test` i `production`, a smoke testy `version.txt` == SHA oraz `get-api-version` 200 przechodzą w logu joba.
- Na obu hostach: SPA ładuje się, logowanie (JWT) działa, istniejące pliki tylko serwerowe (media w `wwwroot`, `Logs/`, `TMP/LOTTO/ARCHIWUM`) nadal są na miejscu (porównanie z listą z kopii G2).
- `context/deployment/deploy-plan.md` uzupełniony o stan po wdrożeniu.

---

## Status planu

- Zatwierdzony: 2026-09-28 (Plan Mode, właściciel).
- Workflow: `.github/workflows/deploy.yml` (utworzony, jeszcze niezacommitowany / nieuruchomiony).

## Bramki ręczne — stan

| Bramka | Stan | Notatki |
|---|---|---|
| G1 — Web Deploy + GitHub Environments `test` / `production` | ☐ | `WEBIO_MSDEPLOY_URL` = `publishUrl` z `.PublishSettings` (host:port, bez `https://`) |
| G2 — kopia FTP obu witryn + backup bazy + przegląd `web.config` | ☐ | |
| G3 — zgodność schematu z `AppDbContextModelSnapshot.cs` | ☐ | baza używana przez tomsoft1.com.pl: ? |
| G4 — zajęcie bazy vs limit planu | ☐ | |

## Stan po wdrożeniu

| Środowisko | Data | SHA | Wariant runtime | Smoke test | Uwagi |
|---|---|---|---|---|---|
| test (tomsoft1.com.pl) | | | | | |
| production (tomsoft1.pl) | | | | | |
