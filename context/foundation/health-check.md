---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T20:30:50Z
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
recommended_fixes: 1
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
Direct vs transitive: 512 zależności — brak podatności
```

`dotnet list package --deprecated`: `xunit` 2.9.3 oznaczony jako *Legacy* (następca `xunit.v3`). Pozostanie na 2.x to świadoma decyzja zapisana w `CLAUDE.md` — nie jest to podatność.

### Outdated Dependencies

```
Packages with major version gaps: 12 (NuGet: 1, npm: 11) — bez zmian od poprzedniego przebiegu
```

NuGet:

- **MediatR**: 12.5.0 → 14.2.0 (2 majory) — **nie podbijać**: v13+ ma licencję komercyjną (reguła w `CLAUDE.md`).

npm (w każdym przypadku `wanted` = `current`, więc to świadome przypięcie wersji z `CLAUDE.md`):

- **typescript**: 5.9.3 → 7.0.2 (2 majory)
- **@types/node**: 24.19.0 → 26.6.3 (2 majory)
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 majory)
- **vite**: 7.3.6 → 8.3.1; **@vitejs/plugin-react**: 5.2.0 → 6.1.1
- **react-router**: 7.18.4 → 8.4.0
- **mermaid**: 11.17.2 → 12.0.0
- **eslint**: 9.39.5 → 10.11.0; **@eslint/js**: 9.39.5 → 10.0.1; **globals**: 16.5.0 → 17.12.0; **eslint-plugin-react-refresh**: 0.4.26 → 0.5.7

Luki majorowe są informacyjne. Agent nie może ich podbijać przy okazji innych zmian — `CLAUDE.md` już tego zabrania.

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

Build `APPS.sln` (`--no-incremental`): 0 błędów, 0 ostrzeżeń.

Frontend (`src/client/app01`) nie ma testów. Działają za to bramki statyczne: `tsc -b` (strict) przechodzi, `prettier --check` przechodzi, a `eslint .` zwraca 0 błędów i 3 ostrzeżenia `react-hooks/exhaustive-deps` (patrz Poprawka 1).

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push + pull_request, wszystkie gałęzie)
```

| Stage      | Status | Notes |
|------------|--------|-------|
| Lint       | ✓      | `dotnet format --verify-no-changes` (backend); `npx prettier --check` + `npm run lint` (ESLint) (frontend) |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator 5.5.11 → artefakt HTML); frontend — brak testów |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build` |
| Type check | ✓      | kompilator C# (nullable) w buildzie; `tsc -b` w `npm run build` |
| Security   | ✓      | `dotnet list package --vulnerable --include-transitive` (fail na High/Critical); `npm audit --audit-level=high`; NuGetAudit w restore |

Wszystkie kroki CI przechodzą lokalnie: format, build, test, prettier, lint i tsc.

## Configuration

### High severity

- Brak luk o wysokiej wadze: `.gitignore` jest obecny (root, `src/server/`, `src/client/app01/`), `tsconfig.app.json` ma `strict: true`.

### Medium severity

- **3 ostrzeżenia ESLint** `react-hooks/exhaustive-deps` w `src/pages/lotto/{LottoDrawsPage,LottoTicketsPage,LottoWinningTicketsPage}.tsx`. Fix: Poprawka 1.

### Low severity

- Serwer nie ma pliku `appsettings.Example.json` dokumentującego wymagane klucze konfiguracji. Frontend ma `.env.example`. Dotyczy tylko stawiania nowego środowiska.
- Profile publikacji `Properties/PublishProfiles/*.pubxml` są ignorowane przez git (`src/server/.gitignore:52`). Lokalnie oba wskazują już na `net10.0`, ale ta poprawka nie trafi do innych klonów repozytorium. To informacja, nie błąd.

Sekrety: `src/client/app01/.env` był w repozytorium od `a5a2a5d` do `51aaae0`. Od `51aaae0` nie jest śledzony, a `.gitignore` go ignoruje. Śledzony jest nadal `appsettings.json` z polami `Tokens:Key`, `ConnectionStrings:DefaultConnection` i `ApiKey`. Według właściciela projektu wartości w historii obu plików były przykładowe, więc rotacja kluczy nie jest potrzebna. Health-check sam tych wartości nie odczytywał. `CLAUDE.md:132` zawiera teraz regułę zakazującą wpisywania sekretów do śledzonych plików.

Rozwiązane od poprzednich przebiegów: `.env` wyłączony ze śledzenia, weryfikacja historii sekretów (potwierdzona przez właściciela), reguła o sekretach w `CLAUDE.md`, `FolderProfile1.pubxml` → `net10.0`, literówka w `CLAUDE.md:101`, ReportGenerator 5.0.4 → 5.5.11.

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|---|---|---|
| Luka 1: `CLAUDE.md` opisuje .NET 8, kod jest na .NET 10 | `CLAUDE.md` w pełni zaktualizowany (.NET 10, `net10.0`, zakaz `.WithOpenApi()`, dokumentacja 10.0, literówka usunięta). 0 wywołań `WithOpenApi`, 0 ostrzeżeń builda, profile publikacji lokalnie na `net10.0`. | Mitigated |
| Luka 2: konwencje Minimal APIs / Vite + React żyją tylko w `CLAUDE.md` | Sekcja „Formatowanie" i pełna lista kroków CI są w `CLAUDE.md`. `dotnet format --verify-no-changes` i `prettier --check` przechodzą. | Mitigated |
| Luka 3: brak testów frontendu | Potwierdzone: 0 testów frontendu. Kompensują to `tsc` strict, ESLint i Prettier w CI oraz reguła „logika domenowa po stronie serwera" (343 testy endpointów). Nadal są 3 ostrzeżenia hooków. | Reinforced (świadomie zaakceptowane) |
| Kompensacja: rekomendowane wpisy do `CLAUDE.md` | Wszystkie 4 bloki ze stack-assessment są obecne, a do tego reguła o sekretach. `AGENTS.md` istnieje. | Mitigated |

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Usuń 3 ostrzeżenia `react-hooks/exhaustive-deps`

**Impact**: agent traktuje istniejący kod jako wzorzec. Przy nowych stronach modułu Kursy skopiuje `useEffect` z niepełną listą zależności. Czysty lint (0 ostrzeżeń) daje wyraźny sygnał „zepsułem coś" po zmianie agenta.
**Severity**: medium
**Effort**: quick (< 5 min) na plik
**Fix**: w `LottoDrawsPage.tsx:132`, `LottoTicketsPage.tsx:182` i `LottoWinningTicketsPage.tsx:126` owiń `fetchDraws` / `fetchTickets` / `fetchWinningTickets` w `useCallback` z właściwymi zależnościami i dodaj je do tablicy `useEffect`. Potem w `src/client/app01/package.json` zmień `"lint": "eslint ."` na `"lint": "eslint . --max-warnings 0"`, żeby CI blokowało nowe ostrzeżenia.

*Świadomie bez akcji:* majory npm (TypeScript 7, Vite 8, React Router 8, ESLint 10 itd.), MediatR 14 (licencja) i xUnit v3 zostają przypięte zgodnie z `CLAUDE.md`.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (tylko FileSystem publish, profile poza gitem, brak Dockerfile/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zdefiniujesz docelową infrastrukturę i pierwszy deploy, także jego automatyzację w CI.

### Pliki instrukcji agenta (`CLAUDE.md` / `AGENTS.md`)

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: uporządkujesz istniejące, już aktualne pliki instrukcji (warstwy reguł, pętle informacji zwrotnej).

## Summary

Health status: healthy

Projekt jest gotowy do pracy z agentem. Zależności są czyste i przypięte lock-filami, 343 testy przechodzą, a build nie ma ostrzeżeń. CI pokrywa lint, typy, build, testy i bezpieczeństwo. `CLAUDE.md` jest spójny z kodem i ma regułę o sekretach. Sekrety są uporządkowane: `.env` nie jest już śledzony, a według właściciela historia zawierała tylko przykładowe wartości. Zostały jedynie 3 ostrzeżenia ESLint na stronach Lotto.

Next step: zrób Poprawkę 1 (kilka minut), potem przejdź do agent onboardingu.
