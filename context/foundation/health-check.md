---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T20:50:45Z
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
recommended_fixes: 2
---

## Dependency Health

### Lockfile

```
Status: present — NuGet packages.lock.json (8/8 projektów) + src/client/app01/package-lock.json
Package manager: dotnet (NuGet) + npm
```

`Directory.Build.props` włącza `RestorePackagesWithLockFile` i `NuGetAuditMode=all`, a `NU1903`/`NU1904` (podatności high/critical) traktuje jako błędy builda. CI robi `dotnet restore --locked-mode` i `npm ci`.

### Security Audit

```
Tool: dotnet list APPS.sln package --vulnerable --include-transitive
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: sprawdzono oba (--include-transitive); brak podatnych pakietów w żadnym z 8 projektów
```

```
Tool: npm audit --json (src/client/app01)
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: 512 zależności (284 prod, 152 dev); brak podatności
```

### Outdated Dependencies

```
Packages with major version gaps: 12 (NuGet: 1, npm: 11) — bez zmian od poprzedniego przebiegu
```

NuGet:

- **MediatR**: 12.5.0 → 14.2.0 (2 majory). **Nie podbijać**: v13+ ma licencję komercyjną (reguła w `CLAUDE.md`).

npm (wszędzie `wanted` = `current`, czyli wersje są przypięte świadomie, zgodnie z `CLAUDE.md`):

- **typescript**: 5.9.3 → 7.0.2 (2 majory)
- **@types/node**: 24.19.0 → 26.6.3 (2 majory)
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 majory)
- **vite**: 7.3.6 → 8.3.1; **@vitejs/plugin-react**: 5.2.0 → 6.1.1
- **react-router**: 7.18.4 → 8.4.0
- **mermaid**: 11.17.2 → 12.0.0
- **eslint**: 9.39.5 → 10.11.0; **@eslint/js**: 9.39.5 → 10.0.1; **globals**: 16.5.0 → 17.12.0; **eslint-plugin-react-refresh**: 0.4.26 → 0.5.7

Luki majorowe służą tylko jako informacja. Agent nie może podbijać tych wersji przy okazji innych zmian, a `CLAUDE.md` już mu tego zabrania.

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

Build `APPS.sln`: 0 błędów, 0 ostrzeżeń.

Frontend (`src/client/app01`) nie ma testów. Przechodzą za to wszystkie bramki statyczne: `tsc -b` (strict), `prettier --check` i `eslint .`. ESLint zwraca teraz **0 błędów i 0 ostrzeżeń**, bo 3 ostrzeżenia `react-hooks/exhaustive-deps` naprawiono w `9be2f37`. `vite build` przechodzi z ostrzeżeniem o rozmiarze chunka (`index-*.js` ma 680 kB, 185 kB po gzip). To informacja, nie błąd.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push + pull_request, wszystkie gałęzie)
```

| Stage      | Status | Notes |
|------------|--------|-------|
| Lint       | ✓      | backend: `dotnet format --verify-no-changes`; frontend: `npx prettier --check` + `npm run lint` (ESLint, bez `--max-warnings 0`, patrz Poprawka 1) |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator 5.5.11 → artefakt HTML); frontend nie ma testów |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build` |
| Type check | ✓      | kompilator C# (nullable) w buildzie; `tsc -b` w `npm run build` |
| Security   | ✓      | `dotnet list package --vulnerable --include-transitive` (fail na High/Critical); `npm audit --audit-level=high`; NuGetAudit w restore |

Wszystkie kroki CI przechodzą lokalnie: `dotnet format --verify-no-changes`, build, testy, prettier, lint i `npm run build`.

## Configuration

### High severity

Brak luk o wysokiej wadze. `.gitignore` jest obecny (w root, `src/server/` i `src/client/app01/`), a `tsconfig.app.json` ma `strict: true`.

### Medium severity

- **`src/client/app01/package.json` → `"lint": "eslint ."`**: ESLint kończy się kodem 0 nawet przy ostrzeżeniach, więc CI przepuści nowe ostrzeżenia `exhaustive-deps` wprowadzone przez agenta. Fix: Poprawka 1.

### Low severity

- **Brak `appsettings.Example.json`**: serwer nie dokumentuje wymaganych kluczy (`Tokens:X-TOKEN`, `Tokens:Key`, `ConnectionStrings:DefaultConnection`, `ApiKey`) poza pustym `appsettings.json`. Frontend ma `.env.example`. Dotyczy tylko stawiania nowego środowiska. Fix: Poprawka 2.
- **`.nvmrc` = 22, lokalnie Node v24.17.0**: CI używa Node 22 i build przechodzi na obu wersjach. Rozjazd jest tylko informacyjny. Jeśli zobaczysz lokalnie coś, czego CI nie odtwarza, przełącz się poleceniem `nvm use`.
- Profile publikacji `Properties/PublishProfiles/*.pubxml` są ignorowane przez git (`src/server/.gitignore`). Lokalnie oba wskazują na `net10.0` (0 wystąpień `net8.0`). To tylko informacja.

Sekrety: w repozytorium nie jest śledzony żaden plik `.env*` poza `.env.example`. Śledzony `appsettings.json` ma wszystkie wrażliwe pola puste (`""`), a `appsettings.Development.json` leży lokalnie i jest ignorowany. `CLAUDE.md` zawiera regułę zakazującą wpisywania sekretów do śledzonych plików.

Rozwiązane od poprzedniego przebiegu: 3 ostrzeżenia `react-hooks/exhaustive-deps` na stronach Lotto (commit `9be2f37`).

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|---|---|---|
| Luka 1: `CLAUDE.md` opisuje .NET 8, kod jest na .NET 10 | `CLAUDE.md` jest zaktualizowany: .NET 10, `net10.0` w przepisie na moduł, zakaz `.WithOpenApi()`, dokumentacja 10.0, nagłówek sekcji danych „EF Core 10, SQL Server 2012” (ostatnia pozostałość „EF Core 8” poprawiona po tym przebiegu). W `src/server` nie ma żadnego wywołania `WithOpenApi`, build nie zgłasza ostrzeżeń, a profile publikacji wskazują na `net10.0`. | Mitigated |
| Luka 2: konwencje Minimal APIs / Vite + React żyją tylko w `CLAUDE.md` | Sekcja „Formatowanie” i pełna lista kroków CI są w `CLAUDE.md`. `dotnet format --verify-no-changes`, `prettier --check` i `eslint` przechodzą bez uwag. | Mitigated |
| Luka 3: brak testów frontendu | Potwierdzone: frontend ma 0 testów. Kompensują to `tsc` strict, ESLint (teraz 0 ostrzeżeń) i Prettier w CI oraz reguła „logika domenowa po stronie serwera” (343 testy endpointów). Weryfikacja UI modułu Kursy pozostaje manualna. | Reinforced (świadomie zaakceptowane) |
| Kompensacja: rekomendowane wpisy do `CLAUDE.md` | Są wszystkie 4 bloki ze stack-assessment oraz reguła o sekretach. `AGENTS.md` istnieje. | Mitigated |

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Zablokuj nowe ostrzeżenia ESLint w CI

**Impact**: lint jest teraz czysty, ale `eslint .` kończy się kodem 0 nawet przy ostrzeżeniach. Agent może więc wprowadzić `useEffect` z niepełnymi zależnościami, a CI tego nie zatrzyma. Z flagą `--max-warnings 0` każde nowe ostrzeżenie staje się twardym sygnałem „zepsułem coś”.
**Severity**: medium
**Effort**: quick (< 5 min)
**Fix**:

W `src/client/app01/package.json` zmień:

```json
"lint": "eslint . --max-warnings 0",
```

Następnie w `src/client/app01` uruchom `npm run lint` (powinno przejść, bo teraz jest 0 ostrzeżeń).

### 2. Dodaj `appsettings.Example.json` z listą wymaganych kluczy

**Impact**: agent (albo nowa osoba) stawiająca środowisko nie wie, które klucze trzeba ustawić w `appsettings.Development.json` lub `dotnet user-secrets`. Może wtedy zgadywać albo wpisać wartość do śledzonego `appsettings.json`.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**:

Skopiuj `src/server/App01/App01.Bootstrapper.Api/appsettings.json` do `appsettings.Example.json` w tym samym katalogu. W wartościach wpisz opisowe placeholdery, np. `"<ustaw w user-secrets>"`, a nie prawdziwe dane. Upewnij się, że plik nie trafia do publikacji (`<Content Update="appsettings.Example.json" CopyToPublishDirectory="Never" />` w `.csproj`).

*Świadomie bez akcji:* majory npm (TypeScript 7, Vite 8, React Router 8, ESLint 10 itd.), MediatR 14 (licencja) i xUnit v3 zostają przypięte zgodnie z `CLAUDE.md`. Ostrzeżenie o rozmiarze chunka (680 kB) można potraktować jako kandydata do code-splittingu (`React.lazy` na stronach modułów), ale nie blokuje ono pracy agenta.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (tylko FileSystem publish, profile poza gitem, brak Dockerfile/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zdefiniujesz docelową infrastrukturę, zrobisz pierwszy deploy i zautomatyzujesz go w CI.

### Pliki instrukcji agenta (`CLAUDE.md` / `AGENTS.md`)

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: uporządkujesz istniejące, już aktualne pliki instrukcji w warstwy reguł i dodasz pętle informacji zwrotnej.

## Summary

Health status: healthy

Projekt jest gotowy do pracy z agentem. Zależności są czyste (0 podatności w NuGet i npm) i przypięte lock-filami. 343 testy przechodzą, build nie ma ostrzeżeń, a lint jest czysty. CI pokrywa lint, typy, build, testy i bezpieczeństwo, a `CLAUDE.md` jest spójny z kodem. Zostały dwie drobne poprawki: bramka `--max-warnings 0`, która utrwali obecny czysty lint, oraz przykładowy plik konfiguracji serwera.

Next step: zrób Poprawkę 1 (minuta), opcjonalnie Poprawkę 2, a potem przejdź do agent onboardingu.
