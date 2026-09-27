---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T20:06:26Z
health_status: healthy
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

### Lockfile

```
Status: present — NuGet packages.lock.json (8/8 projektów) + src/client/app01/package-lock.json
Package manager: dotnet (NuGet) + npm
```

`Directory.Build.props` włącza `RestorePackagesWithLockFile`, `NuGetAuditMode=all` i traktuje `NU1903`/`NU1904` (podatności high/critical) jako błędy builda. CI robi `dotnet restore --locked-mode` i `npm ci`.

### Security Audit

```
Tool: dotnet list APPS.sln package --vulnerable --include-transitive
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: sprawdzono oba (--include-transitive) — brak podatnych pakietów w żadnym z 8 projektów
```

```
Tool: npm audit --json (src/client/app01)
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: 512 zależności (284 prod, 152 dev, 77 optional) — brak podatności
```

`dotnet list package --deprecated`: `xunit` 2.9.3 oznaczony jako *Legacy* (następca `xunit.v3`). Pozostanie na 2.x to świadoma decyzja zapisana w `CLAUDE.md` — nie jest to podatność.

### Outdated Dependencies

```
Packages with major version gaps: 12 (NuGet: 1, npm: 11)
```

NuGet:

- **MediatR**: 12.5.0 → 14.2.0 (2 majory) — **nie podbijać**: v13+ ma licencję komercyjną (reguła w `CLAUDE.md`).

npm (wszystkie rozbieżności są świadomym przypięciem wersji z `CLAUDE.md`; `wanted` = `current`, więc nic nie dryfuje w obrębie semver):

- **typescript**: 5.9.3 → 7.0.2 (2 majory)
- **@types/node**: 24.19.0 → 26.6.3 (2 majory)
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 majory)
- **vite**: 7.3.6 → 8.3.1; **@vitejs/plugin-react**: 5.2.0 → 6.1.1
- **react-router**: 7.18.4 → 8.4.0
- **mermaid**: 11.17.2 → 12.0.0
- **eslint**: 9.39.5 → 10.11.0; **@eslint/js**: 9.39.5 → 10.0.1; **globals**: 16.5.0 → 17.12.0; **eslint-plugin-react-refresh**: 0.4.26 → 0.5.7

Luki majorowe są informacyjne — nie blokują pracy agenta. Ważne jest to, że agent nie może ich podbijać przy okazji innych zmian. `CLAUDE.md` już tego zabrania.

## Test Suite

```
Test runner: xUnit 2.9.3 (+ Microsoft.AspNetCore.Mvc.Testing / WebApplicationFactory, EF InMemory)
Tests found: 343
Test execution: passing (343/343, 0 pominiętych, ~20 s)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3, Moq, coverlet.collector
```

Build `APPS.sln` (`--no-incremental`): 0 błędów, 0 ostrzeżeń (w tym 0 ostrzeżeń `obsolete` po migracji na .NET 10).

Frontend (`src/client/app01`) nie ma testów. Działają za to bramki statyczne: `tsc -b` (strict) przechodzi bez błędów, `prettier --check` przechodzi, a `eslint .` zwraca 0 błędów i 3 ostrzeżenia (`react-hooks/exhaustive-deps`, patrz Poprawka 3).

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push + pull_request, wszystkie gałęzie)
```

| Stage      | Status | Notes |
|------------|--------|-------|
| Lint       | ✓      | `dotnet format --verify-no-changes` (backend); `npx prettier --check` + `npm run lint` (ESLint) (frontend) |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator → artefakt HTML); frontend — brak testów |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build` |
| Type check | ✓      | kompilator C# (nullable) w buildzie; `tsc -b` w `npm run build` |
| Security   | ✓      | `dotnet list package --vulnerable --include-transitive` (fail na High/Critical); `npm audit --audit-level=high`; NuGetAudit w restore |

Wszystkie kroki CI przechodzą lokalnie: locked restore, format, build, test, prettier, lint i tsc.

## Configuration

### High severity

- Brak luk o wysokiej wadze: `.gitignore` jest obecny (root, `src/server/`, `src/client/app01/`), `tsconfig.app.json` ma `strict: true`.

### Medium severity

- **`src/client/app01/.env` jest śledzony w git** — `src/client/app01/.gitignore` ignoruje `.env.*`, ale nie sam `.env`. Plik zawiera klucze `VITE_BASE_URL`, `VITE_API_URL` i `VITE_APP_TOKEN`. Śledzony `.env` to prosta droga do przypadkowego commitu prawdziwej wartości. Zmienne `VITE_*` i tak trafiają do bundla przeglądarki, więc nie powinny zawierać sekretów. Fix: Poprawka 1.
- **`src/server/App01/App01.Bootstrapper.Api/appsettings.json` jest śledzony i ma pola `Tokens:Key`, `ConnectionStrings:DefaultConnection` oraz dwa `ApiKey`** — health-check nie odczytywał ich wartości (ani w bieżącym pliku, ani w historii). Trzeba ręcznie potwierdzić, że w repo są puste lub przykładowe. `appsettings.*.json` jest poprawnie ignorowany (`src/server/.gitignore:48`). Fix: Poprawka 1.
- **3 ostrzeżenia ESLint** `react-hooks/exhaustive-deps` w `src/pages/lotto/{LottoDrawsPage,LottoTicketsPage,LottoWinningTicketsPage}.tsx`. Fix: Poprawka 3.

### Low severity

- **`FolderProfile1.pubxml` wciąż publikuje do `bin\Release\net8.0\publish\`** (`FolderProfile.pubxml` jest już poprawiony na `net10.0`). Fix: Poprawka 2.
- **Literówka w `CLAUDE.md:101`** — linia kończy się zbędnym `` `. `` po zdaniu o `.WithOpenApi()`. Fix: Poprawka 4.
- **ReportGenerator przypięty w CI do 5.0.4** (stara wersja z 2021 r.). Fix: Poprawka 5.
- `.editorconfig`, `.prettierrc.json`, `eslint.config.js` i `src/client/app01/.env.example` są obecne. Serwer nie ma pliku `.env.example` / `appsettings.Example.json` dokumentującego wymagane klucze konfiguracji (niska waga, dotyczy tylko nowego środowiska).

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|---|---|---|
| Luka 1: `CLAUDE.md` opisuje .NET 8, kod jest na .NET 10 | `CLAUDE.md` zaktualizowany: .NET 10, `net10.0` w przepisie na moduł, zakaz `.WithOpenApi()`, dokumentacja 10.0. W `src/server` jest 0 wywołań `WithOpenApi`, a build ma 0 ostrzeżeń `obsolete`. Zostały: `FolderProfile1.pubxml` z `net8.0` i drobna literówka w `CLAUDE.md:101`. | Mitigated (prawie w całości) |
| Luka 2: konwencje Minimal APIs / Vite + React żyją tylko w `CLAUDE.md` | Dodano sekcję „Formatowanie" i pełną listę kroków CI. `dotnet format --verify-no-changes` i `prettier --check` przechodzą lokalnie. | Mitigated |
| Luka 3: brak testów frontendu | Potwierdzone: 0 testów frontendu. Kompensują to `tsc` strict, ESLint i Prettier w CI oraz reguła „logika domenowa po stronie serwera" (343 testy endpointów). ESLint zgłasza 3 ostrzeżenia hooków. | Reinforced (świadomie zaakceptowane) |
| Kompensacja: rekomendowane wpisy do `CLAUDE.md` | Wszystkie 4 bloki ze stack-assessment są obecne w `CLAUDE.md`. `AGENTS.md` istnieje. | Mitigated |

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Wyłącz `src/client/app01/.env` ze śledzenia i potwierdź brak sekretów w `appsettings.json`

**Impact**: agent edytujący konfigurację (np. przy dodawaniu modułu Kursy) może wpisać prawdziwą wartość do śledzonego pliku, a ta trafi do commitu. Nie znamy stanu historii tych plików.
**Severity**: medium
**Effort**: moderate (15–30 min)
**Fix**:

```bash
# frontend: .env przestaje być śledzony, zostaje lokalnie
echo ".env" >> src/client/app01/.gitignore
git rm --cached src/client/app01/.env

# ręcznie: sprawdź, czy w historii nie ma prawdziwych wartości
git log -p -- src/client/app01/.env src/server/App01/App01.Bootstrapper.Api/appsettings.json
```

Jeśli w historii pojawił się prawdziwy klucz (JWT `Tokens:Key`, `ApiKey` OpenRoutera, hasło w connection stringu), **zrotuj go**. Nie wystarczy przepisać historii. Sekrety lokalne trzymaj w `appsettings.Development.json` (ignorowany) albo w `dotnet user-secrets`. Dopisz też do `CLAUDE.md` regułę: „Nie wpisuj sekretów do `appsettings.json` ani `.env` — tylko `appsettings.*.json` / user-secrets / zmienne środowiskowe".

### 2. Popraw `PublishUrl` w `FolderProfile1.pubxml`

**Impact**: publikacja profilem `FolderProfile1` trafia do katalogu `net8.0`, co myli agenta i skrypty wdrożeniowe po migracji.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: w `src/server/App01/App01.Bootstrapper.Api/Properties/PublishProfiles/FolderProfile1.pubxml` zamień `bin\Release\net8.0\publish\` na `bin\Release\net10.0\publish\`.

### 3. Usuń 3 ostrzeżenia `react-hooks/exhaustive-deps`

**Impact**: agent traktuje istniejący kod jako wzorzec. Przy nowych stronach (moduł Kursy) skopiuje `useEffect` z niepełną listą zależności. Czysty lint (0 ostrzeżeń) daje też wyraźny sygnał „zepsułem coś" po zmianie agenta.
**Severity**: medium
**Effort**: quick (< 5 min) na plik
**Fix**: w `LottoDrawsPage.tsx:132`, `LottoTicketsPage.tsx:182` i `LottoWinningTicketsPage.tsx:126` owiń `fetchDraws` / `fetchTickets` / `fetchWinningTickets` w `useCallback` z właściwymi zależnościami i dodaj je do tablicy `useEffect`. Opcjonalnie w `package.json` ustaw `"lint": "eslint . --max-warnings 0"`, żeby CI blokowało nowe ostrzeżenia.

### 4. Usuń literówkę w `CLAUDE.md:101`

**Impact**: kosmetyka, ale to linia przepisu na endpoint, którą agent czyta dosłownie.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: usuń końcowe `` `. `` po „…zbiera Swashbuckle z `WithName`/`WithTags`/`Produces`.".

### 5. Zaktualizuj ReportGenerator w CI

**Impact**: stara wersja 5.0.4 narzędzia globalnego. Nie jest ryzykiem dla kodu produkcyjnego, ale może przestać działać z nowszym runtime'em runnera.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: w `.github/workflows/pull-request.yml` zmień `--version 5.0.4` na aktualną wersję 5.x `dotnet-reportgenerator-globaltool`, np. sprawdzając ją przez `dotnet tool search dotnet-reportgenerator-globaltool`.

*Świadomie bez akcji:* majory npm (TypeScript 7, Vite 8, React Router 8, ESLint 10 itd.), MediatR 14 (licencja) i xUnit v3 zostają przypięte zgodnie z `CLAUDE.md`. Aktualizuj je osobnymi, celowymi zmianami, nie przy okazji pracy nad funkcjami.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (tylko FileSystem publish, brak Dockerfile/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zdefiniujesz docelową infrastrukturę i pierwszy deploy, także jego automatyzację w CI.

### Pliki instrukcji agenta (`CLAUDE.md` / `AGENTS.md`)

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: uporządkujesz już istniejące pliki instrukcji (warstwy reguł, pętle informacji zwrotnej). Masz tu przewagę, bo `CLAUDE.md` jest już szczegółowy i aktualny.

## Summary

Health status: healthy

Projekt jest w dobrym stanie do pracy z agentem. Zależności NuGet i npm są przypięte lock-filami i nie mają podatności. 343 testy xUnit przechodzą, a build ma 0 ostrzeżeń po migracji na .NET 10. CI w GitHub Actions pokrywa lint/format, typy, build, testy i skan bezpieczeństwa, a lokalnie wszystkie te kroki przechodzą. Luka z oceny stacku (nieaktualny `CLAUDE.md`) jest prawie w całości zamknięta. Do zrobienia zostały drobiazgi: wyłączenie `.env` ze śledzenia i ręczne potwierdzenie braku sekretów w historii, jeden profil publikacji z `net8.0` oraz 3 ostrzeżenia ESLint. Świadome ograniczenie to brak testów frontendu, więc UI modułu Kursy będzie weryfikowane ręcznie.

Next step: zrób Poprawkę 1 (sekrety) i szybkie poprawki 2–4, potem przejdź do agent onboardingu.
