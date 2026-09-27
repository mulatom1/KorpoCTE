---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T21:30:00Z
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
recommended_fixes: 3
---

## Dependency Health

### Lockfile

```
Status: present — NuGet packages.lock.json (8/8 projektów) + src/client/app01/package-lock.json
Package manager: dotnet (NuGet) + npm
```

`Directory.Build.props` włącza `RestorePackagesWithLockFile` i `NuGetAuditMode=all`, a `NU1903`/`NU1904` (podatności high/critical) traktuje jako błędy builda. CI wykonuje `dotnet restore --locked-mode` oraz `npm ci`.

### Security Audit

```
Tool: dotnet list APPS.sln package --vulnerable --include-transitive
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: sprawdzono oba (--include-transitive); brak podatnych pakietów w żadnym z 8 projektów
```

```
Tool: npm audit --json (src/client/app01)
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: 512 zależności (284 prod, 152 dev, 77 optional); brak podatności
```

Pliki `.env`, `.env.dev`, `.env.prod1` i `.env.prod2` w `src/client/app01` są ignorowane przez `.gitignore` (reguły `.env` i `.env.*`). Do repozytorium trafia tylko `.env.example`. Zawartości plików `.env*` nie odczytywano. `appsettings.json` jest śledzony przez git (obok `appsettings.Example.json`), a jego zawartości też nie sprawdzano pod kątem sekretów. Patrz poprawka nr 3.

### Outdated Dependencies

```
Packages with major version gaps: 12 (NuGet: 1, npm: 11)
```

NuGet (`dotnet list package --outdated`):

- **MediatR**: 12.5.0 → 14.2.0 (2 majory). **Nie podbijać**: od v13 obowiązuje licencja komercyjna (reguła w `CLAUDE.md`).
- Poza tym nic nie wymaga aktualizacji w obrębie tej samej wersji minor (`--highest-minor`: brak wyników).

npm (`npm outdated`; wszędzie `wanted` = `current`, czyli zakresy w `package.json` celowo zatrzymują się na bieżących majorach):

- **typescript**: 5.9.3 → 7.0.2 (2 majory)
- **@types/node**: 24.19.0 → 26.6.3 (2 majory)
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 majory)
- **vite**: 7.3.6 → 8.3.1; **@vitejs/plugin-react**: 5.2.0 → 6.1.1
- **react-router**: 7.18.4 → 8.4.0
- **mermaid**: 11.17.2 → 12.0.0
- **eslint**: 9.39.5 → 10.11.0; **@eslint/js**: 9.39.5 → 10.0.1; **globals**: 16.5.0 → 17.12.0; **eslint-plugin-react-refresh**: 0.4.26 → 0.5.7

Te luki są tylko informacją. Wersje są przypięte zgodnie z sekcją „Wersje przypięte" w `CLAUDE.md`, a agent nie może podbijać majorów przy okazji innych zmian.

## Test Suite

```
Test runner: xUnit 2.9.3 (+ Microsoft.AspNetCore.Mvc.Testing / WebApplicationFactory, EF InMemory)
Tests found: 343
Test execution: passing (343/343, 0 pominiętych, ~14 s)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3, Moq, coverlet.collector
```

Frontend (`src/client/app01`) nie ma test runnera. Jego rolę pełnią `tsc -b` (strict), `eslint --max-warnings 0` i `prettier --check`, które przechodzą lokalnie. Zgodnie z `CLAUDE.md` logika domenowa zostaje po stronie serwera, gdzie pokrywają ją testy endpointów.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push i pull_request na wszystkie gałęzie)
```

| Stage      | Status | Notes                                                                                       |
|------------|--------|---------------------------------------------------------------------------------------------|
| Lint       | ✓      | `dotnet format --verify-no-changes`; `npx prettier --check`, `npm run lint` (ESLint, 0 ostrzeżeń) |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator → artefakt HTML)                      |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build`                                                 |
| Type check | ✓      | kompilator C# (nullable) + `tsc -b` w `npm run build`                                       |
| Security   | ✓      | `dotnet list package --vulnerable` (fail na High/Critical); `npm audit --audit-level=high`    |

Lokalne odtworzenie kroków CI (27.09.2026): `dotnet format --verify-no-changes` → exit 0; `dotnet build` → 0 ostrzeżeń; `dotnet test` → 343/343; `prettier --check` → OK; `npm run lint` → OK; `npm run build` → OK. Jedyny komunikat to ostrzeżenie Vite o chunku JS > 500 kB (680 kB, gzip 185 kB). Stanu przebiegów w GitHub Actions nie sprawdzano, bo `gh` CLI nie jest zainstalowane.

## Configuration

### High severity

Brak.

### Medium severity

Brak. Formatter (`.prettierrc.json`, `dotnet format` + `.editorconfig`), linter (`eslint.config.js`) oraz `tsconfig.app.json` ze `"strict": true` są na miejscu.

### Low severity

- **Chunk JS 680 kB** (`dist/assets/index-*.js`). Nie dotyczy pracy agenta, ale ostrzeżenie Vite pojawia się przy każdym buildzie i łatwo przeoczyć przy nim prawdziwe ostrzeżenie. Poprawka: podział kodu przez `React.lazy(() => import(...))` dla ciężkich stron (np. korzystających z `mermaid` / `hls.js`) albo `build.rollupOptions.output.manualChunks` w `vite.config.ts`.

Obecne pliki: `.editorconfig`, `.gitignore` (root + klient), `.env.example`, `appsettings.Example.json`, `.nvmrc`, `global.json`, `Directory.Build.props`, `CLAUDE.md`, `AGENTS.md`. Brak `tailwind.config.*` i `postcss.config.*`, co jest poprawne dla Tailwind 4 (CSS-first).

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap                                              | Health-Check Finding                                                                                                                                                         | Status    |
|---------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------|
| Luka 1: `CLAUDE.md` opisuje .NET 8, kod jest na .NET 10       | `CLAUDE.md` ma już sekcję „Wersje przypięte" dla .NET 10, `net10.0` w przepisie na moduł, zakaz `.WithOpenApi()` i dokumentację 10.0. W `src/server` nie ma wywołań `WithOpenApi`. Żaden `*.pubxml` nie zawiera już `net8.0`. Build: 0 ostrzeżeń. | Mitigated |
| Luka 2: konwencje Minimal APIs / Vite+React żyją tylko w `CLAUDE.md` | Sekcja „Formatowanie (CI odrzuca niesformatowany kod)" i pełna lista kroków CI są w `CLAUDE.md`. `dotnet format` i `prettier --check` przechodzą. | Mitigated |
| Luka 3: brak testów frontendu                                 | Nadal brak runnera po stronie klienta. Kompensacja: reguła „logika domenowa po stronie serwera" w `CLAUDE.md` plus `tsc` strict, ESLint i prettier w CI. UI modułu Kursy wymaga weryfikacji manualnej. | Accepted  |
| Typed / Convention / Training data / Documented (16/16 pass)  | Typowanie potwierdzone (`strict: true`, nullable, 0 ostrzeżeń builda). Przypięte wersje ograniczają ryzyko sięgania agenta po API nowszych majorów.                           | Reinforced |

Wszystkie wpisy kompensacyjne rekomendowane przez stack-assess są obecne w `CLAUDE.md`.

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Brak testów frontendu przy planowanej pracy nad UI modułu Kursy

**Impact**: agent nie zweryfikuje automatycznie zachowania nowych stron (kafelki kursów, widok kursu, hangar). Wykryje tylko błędy typów, lintu i formatowania. Każdą zmianę UI trzeba sprawdzić ręcznie.
**Severity**: low (świadomie skompensowane regułą w `CLAUDE.md`)
**Effort**: moderate (15–30 min) na konfigurację, jeśli się zdecydujesz
**Fix**:

Opcjonalnie, tylko jeśli w module Kursy pojawi się logika po stronie klienta (np. stan postępu, filtrowanie):

```bash
cd src/client/app01
npm install -D vitest@^3 @testing-library/react @testing-library/jest-dom jsdom
# package.json → "scripts": { "test": "vitest run" }
# vite.config.ts → test: { environment: "jsdom" }
```

W przeciwnym razie zostaw obecną regułę i dopisz do planu zmiany krok manualnej weryfikacji UI.

### 2. Podział bundla JS (680 kB)

**Impact**: niski dla agenta. Stałe ostrzeżenie w logu `npm run build` zagłusza nowe ostrzeżenia, które agent powinien zauważyć.
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**:

W `src/main.tsx` zamień importy ciężkich stron na `const XPage = lazy(() => import("./pages/..."));` i owiń `<Routes>` w `<Suspense>`. Alternatywnie wydziel `mermaid`/`hls.js` przez `build.rollupOptions.output.manualChunks` w `vite.config.ts`.

### 3. Potwierdź, że śledzony `appsettings.json` nie zawiera sekretów

**Impact**: `CLAUDE.md` zabrania trzymania sekretów w `appsettings.json`, ale plik jest w repozytorium. Jeśli trafił do niego klucz JWT, `ApiKey` albo hasło w connection stringu, agent może go powielić lub ujawnić w diffie.
**Severity**: low (nieweryfikowane — health-check nie odczytywał wartości)
**Effort**: quick (< 5 min)
**Fix**:

```bash
git log -p --follow -- src/server/App01/App01.Bootstrapper.Api/appsettings.json | grep -iE "key|secret|password|pwd"
```

Jeśli coś znajdziesz: przenieś wartości do `dotnet user-secrets` / `appsettings.Development.json` i zrotuj ujawnione klucze.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (folder publish, bez kontenera/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zautomatyzujesz wdrożenie i rozszerzysz istniejący pipeline GitHub Actions o krok deploy.

### Utrzymanie `CLAUDE.md` / `AGENTS.md`

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: pliki już istnieją i są aktualne. Na lekcji dopracujesz je i ustawisz pętle feedbacku, żeby nadążały za kodem.

## Summary

```
Health status: healthy
```

Projekt jest w dobrej kondycji. Obie strony (NuGet i npm) mają zero znanych podatności i przypięte lock-file'y. 343 testy serwera przechodzą. Wszystkie kroki CI (format, lint, build, typy, skan bezpieczeństwa, testy) odtwarzają się lokalnie na zielono, a build .NET nie zgłasza ostrzeżeń. Luki wskazane przez stack-assess zostały zamknięte: `CLAUDE.md` opisuje .NET 10 i zasady formatowania, a profile publikacji nie odwołują się już do `net8.0`. Zostają drobiazgi: brak testów frontendu (świadomie skompensowany), duży bundle JS i niezweryfikowana zawartość śledzonego `appsettings.json`.

Next step: opcjonalnie sprawdź `appsettings.json` (poprawka nr 3), a potem przejdź do agent onboardingu.
