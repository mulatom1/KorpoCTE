---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T14:00:00+02:00
health_status: needs-attention
context_type: brownfield
language_family: multi
stack_assessment_available: true
checks_run:
  - lockfile
  - dependency_audit
  - outdated_deps
  - test_runner
  - ci_cd
  - configuration
audit_findings:
  critical: 0
  high: 0
  moderate: 0
  low: 0
test_runner_detected: true
ci_provider: GitHub Actions
recommended_fixes: 5
---

## Dependency Health

Projekt ma dwa ekosystemy: serwer .NET 8 (`APPS.sln`, 8 projektów `*.csproj`) i klient JS/TS (`src/client/app01`). To piąty przebieg. Commit `f27c9e9` zamknął dwie poprawki: przypięcie `8.0.*` w projekcie testów i ochronę migracji przed formatowaniem. Commit `7f0d1ef` poprawił środowisko testów w CI. Oba są wypchnięte (`main` = `origin/main`).

### Lockfile

```
Klient:  Status: present (src/client/app01/package-lock.json)
         Package manager: npm
Serwer:  Status: present (packages.lock.json w każdym z 8 projektów, śledzone w git)
         Package manager: dotnet (NuGet)
```

`Directory.Build.props` (`RestorePackagesWithLockFile=true`) wymusza lockfile w każdym nowym projekcie, a CI przywraca pakiety w `--locked-mode`. Wszystkie deklaracje w projekcie testów są już przypięte (`8.0.31`).

### Security Audit

**Klient (npm)**

```
Tool: npm audit --json
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: 0 / 0
```

**Serwer (NuGet)**

```
Tool: dotnet list APPS.sln package --vulnerable --include-transitive
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: 0 / 0 (wszystkie 8 projektów czyste)
```

Łącznie: **0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW.**

### Outdated Dependencies

```
Packages with major version gaps: 26 (npm: 10, NuGet: 16)
```

Lista pochodzi z przebiegu o 13:30 tego samego dnia. Od tego czasu nie zmieniła się żadna deklaracja pakietu ani lockfile.

Większość jest **celowo przypięta** w `CLAUDE.md` (.NET 8 / EF Core 8, MediatR 12, xUnit 2, React Router 7, Vite 7, TypeScript 5.9) i nie należy jej podbijać przy okazji.

- **.NET 8 / ASP.NET Core 8 / EF Core 8**: 8.0.31 → 10.0.12. **Wsparcie .NET 8 LTS kończy się 10.11.2026, czyli za 44 dni.** To jedyna luka, która wymaga decyzji (A3).
- **Nieprzypięte, 2+ majory w tyle**: `Swashbuckle.AspNetCore` 6.6.2 → 10.2.3, `coverlet.collector` 6.0.0 → 10.0.1, `Microsoft.Extensions.ApiDescription.Client` 7.0.2 → 10.0.12 (nieużywany), `eslint-plugin-react-hooks` 5.2.0 → 7.1.1, `@types/node` 24 → 26. Najlepiej zaktualizować je razem z migracją .NET. Nic pilnego.
- `typescript` 5.9 → 7.0 i `react-router` 7 → 8 są przypięte w `CLAUDE.md`, więc ich nie ruszaj.

## Test Suite

**Serwer**

```
Test runner: xUnit
Tests found: 343 tests
Test execution: passing (343/343, ~21 s, `dotnet test APPS.sln`)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3 + Microsoft.AspNetCore.Mvc.Testing 8.0.31 (WebApplicationFactory, środowisko "Test") + EF Core InMemory 8.0.31 + Moq 4.20.72; coverlet.collector 6.0.0
```

Testy endpointów pokrywają `Lotto` i `Portal`. Moduł Flashcards nie ma testów (poza zakresem PRD).

**Klient**

```
Test runner: not detected
Tests found: not applicable
Test execution: not attempted
```

Bramki klienta:
- `tsc -b` (w `npm run build`);
- ESLint: 0 błędów, 3 ostrzeżenia `react-hooks/exhaustive-deps` w `LottoDrawsPage.tsx:132`, `LottoTicketsPage.tsx:182` i `LottoWinningTicketsPage.tsx:126`;
- `prettier --check`: czysto.

To znana, skompensowana luka (A4). Serwer: `dotnet format APPS.sln --verify-no-changes` również przechodzi czysto.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (zacommitowany)
```

| Stage      | Status | Notes                                                                                   |
|------------|--------|-----------------------------------------------------------------------------------------|
| Lint       | ✓      | `npm run lint` + `prettier --check` (klient); brak `dotnet format --verify-no-changes` (serwer) |
| Test       | ✓      | `dotnet test` + pokrycie (coverlet → ReportGenerator → artefakt HTML)                    |
| Build      | ✓      | `dotnet build --no-restore` + `npm run build`                                            |
| Type check | ✓      | `tsc -b` w `npm run build`; kompilator C# w `dotnet build`                               |
| Security   | ✓      | `npm audit --audit-level=high`, `dotnet list package --vulnerable --include-transitive`   |

Wyników uruchomień w GitHub Actions nie sprawdzono, bo lokalnie nie ma `gh` CLI. Nowe w `7f0d1ef`: krok testów ustawia `ASPNETCORE_ENVIRONMENT: Test` i `DOTNET_ENVIRONMENT: Test`, zgodnie z `Program.cs` i z fabrykami testowymi (`UseEnvironment("Test")`). Zniknęła też atrapa connection stringa. Uwaga z poprzedniego raportu jest zamknięta.

## Configuration

### High severity

- **Sekrety w historii publicznego repozytorium.** `appsettings.json` w `HEAD` ma puste klucze. Commity `a5a2a5d` i `3c32e8f` w publicznym `mulatom1/KorpoCTE` nadal zawierają jednak prawdziwe `Jwt.Key`, `Tokens.X-TOKEN` i `LottoOpenApi.ApiKey`. Z repozytorium nie da się sprawdzić, czy wartości na produkcji zostały zrotowane: projekt nadal nie ma `UserSecretsId`, a historia się nie zmieniła. Dodatkowo `src/client/app01/.env` jest **śledzony w git**, bo reguła `.env.*` nie obejmuje samego `.env`. Treści pliku nie czytano; sprawdź, czy nie zawiera starego `VITE_APP_TOKEN`. Fix: A1.

### Medium severity

Brak.

### Low severity

- **`global.json` nie przypina SDK.** Ustawienie `"version": "8.0.400", "rollForward": "latestMajor"` na tej maszynie (jedyne SDK: 10.0.401) wybiera SDK 10, a CI buduje na SDK 8.0.x. Fix: A2.
- **`.env.example` ignorowany przez git.** Plik istnieje lokalnie, ale wyklucza go reguła `.env.*` w `src/client/app01/.gitignore`. Fix: A5.
- **`eslint-config-prettier` zainstalowany, ale niepodpięty** w `eslint.config.js`. Fix: A5.
- **Martwe zależności**: `NSwag.ApiDescription.Client` 13.18.2 i `Microsoft.Extensions.ApiDescription.Client` 7.0.2 są w projekcie, ale nic nie generuje klienta. Fix: A5.
- **Obecne i poprawne**:
  - `.editorconfig` ✓ (z `generated_code = true` dla `**/Migrations/**`);
  - `.prettierrc.json` ✓, `eslint.config.js` ✓, `tsconfig` strict ✓;
  - `.gitignore` ✓ (`appsettings.*.json` ignorowane);
  - `Directory.Build.props` ✓;
  - `AGENTS.md` + `CLAUDE.md` ✓.

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|---|---|---|
| Konwencje serwera `~` (autorski modularny monolit, ręczna rejestracja endpointów, pułapka fallbacku SPA) | `CLAUDE.md` zawiera wszystkie zalecane sekcje, a `AGENTS.md` odsyła do `CLAUDE.md`. 343 testy endpointów łapią błędy rejestracji. `dotnet format` jest czysty, migracje chronione przez `generated_code`. | Mitigated |
| Konwencje klienta `~` (routing deklaratywny, Tailwind 4 / React Router 7 vs korpus v3/v6) | Sekcje „Frontend" i „Wersje przypięte" są w `CLAUDE.md`. `react-router` 7.18.4 mieści się w przypiętym majorze (latest to już 8.x, więc przypięcie ma znaczenie). ESLint i Prettier są egzekwowane w CI. | Mitigated |
| Brak testów frontendu (poza kryteriami) | Nadal brak runnera, ale `tsc`, ESLint i Prettier są egzekwowane w CI. | Mitigated |
| Brak CI (poza kryteriami) | Workflow jest wypchnięty: lint, test, build, type-check i audyt obu ekosystemów. | Mitigated |
| Dryf kontraktów C# ↔ TS | Reguła jest w `CLAUDE.md`. Nieużywane `NSwag` / `ApiDescription.Client` nadal są w zależnościach. | Unchanged |
| Wersje przypięte (MediatR 12, .NET 8) | Przypięcie jest twarde: lockfile'y, wszędzie `8.0.31`, `--locked-mode`. .NET 8 kończy jednak wsparcie 10.11.2026, a `global.json` lokalnie wybiera SDK 10. | Reinforced — wymaga decyzji (A2, A3) |

## Recommended Fixes

### Fix before agent work (Category A)

### A1. Zrotować sekrety opublikowane w publicznej historii i odciąć `.env` od repo

**Impact**: Klucz podpisu JWT z publicznej historii pozwala każdemu wystawić sobie token admina, jeśli produkcja nadal go używa. Moduł Courses dodaje treści zamknięte i ranking, a oba zależą od zaufania do JWT. Agent przeglądający historię (`git log -p`) również wciąga te wartości do kontekstu.
**Severity**: high
**Effort**: moderate (15–30 min)
**Fix**:

1. Wygeneruj nowe wartości (np. `openssl rand -base64 48` dla `Jwt:Key`) i ustaw je lokalnie:
   ```bash
   dotnet user-secrets init --project src/server/App01/App01.Bootstrapper.Api
   dotnet user-secrets set "Jwt:Key" "<nowy-klucz>" --project src/server/App01/App01.Bootstrapper.Api
   dotnet user-secrets set "Tokens:X-TOKEN" "<nowy-token>" --project src/server/App01/App01.Bootstrapper.Api
   dotnet user-secrets set "LottoOpenApi:ApiKey" "<nowy-klucz>" --project src/server/App01/App01.Bootstrapper.Api
   ```
   Alternatywnie ustaw je w `appsettings.Development.json`, który jest już ignorowany.
2. Na produkcji podmień wartości: zmienne środowiskowe `Jwt__Key`, `Tokens__X-TOKEN`, `LottoOpenApi__ApiKey` albo `appsettings.PROD.json` poza repo. Nowy `X-TOKEN` oznacza też nowy `VITE_APP_TOKEN` w `.env.prod1` / `.env.prod2` i przebudowę klienta.
3. Unieważnij stary klucz Lotto API u dostawcy.
4. Wyjmij `.env` z repo, zanim trafi tam nowy token:
   ```bash
   cd src/client/app01
   printf '.env\n!.env.example\n' >> .gitignore
   git rm --cached .env
   git add .gitignore .env.example
   ```
5. Przepisanie historii (`git filter-repo` + force push) jest opcjonalne. Po rotacji stare wartości są bezużyteczne, a publiczne repo mogło już zostać sklonowane.

Jeśli rotacja już się odbyła poza repozytorium, zostaje tylko punkt 4, a projekt spełnia kryteria „healthy".

### A2. Ustalić, którym SDK budujesz

**Impact**: Lokalnie projekt buduje SDK 10.0.401, a CI SDK 8.0.x, więc używają różnych wersji analizatorów i `dotnet format`. Agent weryfikuje zmiany lokalnie, dlatego „przechodzi u mnie" nie gwarantuje zielonego CI.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: wybierz jedno:

- zostań na SDK 8: `winget install Microsoft.DotNet.SDK.8`, a w `global.json` zmień `rollForward` na `latestFeature`;
- albo świadomie przejdź na SDK 10 (target nadal `net8.0`): w `global.json` ustaw `"version": "10.0.100", "rollForward": "latestFeature"`, a w CI podaj `dotnet-version: |` z `8.0.x` i `10.0.x`.

### A3. Zaplanować wyjście z .NET 8 przed 10.11.2026

**Impact**: Za 44 dni .NET 8 przestanie dostawać poprawki bezpieczeństwa. Reguła „nie podbijaj majorów" w `CLAUDE.md` jest słuszna dla agenta, ale bez planu migracji skończy się to brakiem łatek.
**Severity**: medium
**Effort**: significant (> 1 hour)
**Fix**:

1. Zdecyduj o kolejności: najpierw Courses na .NET 8, potem migracja, czy odwrotnie.
2. Przeprowadź migrację jako osobną zmianę (np. `/10x-new dotnet10-upgrade`):
   - `net8.0` → `net10.0`;
   - EF Core, ASP.NET Core i `Mvc.Testing` → 10.0.x;
   - Swashbuckle 10 (albo sam `Microsoft.AspNetCore.OpenApi`), Serilog.AspNetCore 10, coverlet 10;
   - **MediatR zostaje na 12.x** (licencja);
   - sprawdź zachowanie `UseCompatibilityLevel(110)` w EF 10.
3. Po migracji zaktualizuj sekcję „Wersje przypięte" w `CLAUDE.md`, `global.json` i `dotnet-version` w CI.

### A4. Testy frontendu (Vitest): na razie niepotrzebne

**Impact**: Agent weryfikuje UI tylko kompilacją, lintem i formatterem. To, co ważne dla Courses, pokrywają 343 testy serwera i reguła „logika domenowa na serwerze". Dodaj Vitest, gdy w kliencie pojawi się nietrywialna logika (stan quizu, własne przetwarzanie markdownu).
**Severity**: low
**Effort**: moderate (15–30 min), gdy zajdzie potrzeba
**Fix** (gdy zapadnie decyzja):

```bash
cd src/client/app01
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom
```

Następnie:
- w `vite.config.ts` dodaj `test: { environment: "jsdom" }`;
- w `package.json` dodaj skrypt `"test": "vitest run"`;
- w CI dodaj krok `npm test`;
- w `CLAUDE.md` dopisz `npm test` do „Weryfikacji przed zakończeniem zadania".

### A5. Drobne porządki konfiguracji

**Impact**: Usuwają pułapki, na które agent trafi przy pracy nad Courses (nowe zmienne środowiskowe, nowe reguły lintu, mylące zależności).
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**:

- **Podepnij `eslint-config-prettier`**: w `eslint.config.js` dodaj `import eslintConfigPrettier from "eslint-config-prettier";` i wstaw `eslintConfigPrettier` jako ostatni element tablicy w `defineConfig([...])`. Potem sprawdź `npm run lint`.
- **Usuń martwe zależności** `NSwag.ApiDescription.Client` i `Microsoft.Extensions.ApiDescription.Client` z `App01.Shared.Abstractions.csproj`. Potem uruchom `dotnet build APPS.sln && dotnet test APPS.sln` i zacommituj zaktualizowane lockfile'y.
- **`.env.example` do repo**: robi to już punkt 4 w A1.
- **Ostrzeżenia ESLint** w trzech stronach Lotto: moduł Lotto jest objęty wymogiem PRD „bez zmian". Zostaw je jako znane albo popraw świadomie.

### Addressed in upcoming lessons (Category B)

### CI: dopracowanie pipeline'u

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: Dopracujesz pipeline. Etapy są kompletne, do rozważenia zostają:
- `dotnet format APPS.sln --verify-no-changes` jako lint serwera;
- failowanie buildu przy podatnościach NuGet (`<NuGetAudit>true</NuGetAudit>` + `<WarningsAsErrors>NU1903;NU1904</WarningsAsErrors>` w `Directory.Build.props`), bo obecny krok audytu nigdy nie failuje;
- jeden wyzwalacz zamiast `push` + `pull_request` na `'**'`;
- Dependabot i secret scanning (GitHub → Settings → Code security). Secret scanning złapałby problem z A1.

### Pliki instrukcji agenta (`CLAUDE.md`, `AGENTS.md`)

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: Uporządkujesz pliki instrukcji. Oba istnieją i zawierają komplet kompensacji z oceny stacku. Warto dopisać:
- do „Weryfikacji przed zakończeniem zadania": `npx prettier --check "src/**/*.{ts,tsx,css}"` i `dotnet format APPS.sln --verify-no-changes`;
- do zasad: „sekrety tylko w user-secrets / `appsettings.*.json` / niecommitowanym `.env`, nigdy w `appsettings.json`".

### Konfiguracja wdrożenia

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: Zautomatyzujesz wdrożenie; dziś to folder publish `FolderProfile*.pubxml`, bez Dockerfile. Razem z A1 to dobry moment, by sekrety produkcyjne trafiały na serwer wyłącznie jako zmienne środowiskowe.

## Summary

```
Health status: needs-attention
```

Technicznie projekt jest w bardzo dobrym stanie:
- audyt obu ekosystemów jest czysty;
- lockfile'y są przypięte i egzekwowane w CI;
- 343 testy przechodzą;
- `tsc`, ESLint, Prettier i `dotnet format` są czyste, a migracje chronione przed formatowaniem.

Status „needs-attention" wynika tylko z A1. Sekrety nadal są w historii **publicznego** repozytorium i z repo nie da się potwierdzić, że zostały zrotowane. Dodatkowo `src/client/app01/.env` jest śledzony w git. Pozostałe punkty to decyzje (SDK, .NET 8) i drobne porządki.

Next step: zrotuj sekrety i wyjmij `.env` z repo (A1). Po tym projekt spełnia kryteria „healthy" i możesz przejść do agent onboardingu. Migrację .NET 8 → 10 (A3) zaplanuj jako osobną zmianę przed 10.11.2026.
