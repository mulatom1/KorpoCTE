---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T21:50:00Z
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
Direct vs transitive: 584 zależności (284 prod, 224 dev, 77 optional); brak podatności
```

Liczba zależności dev wzrosła ze 152 do 224 po dodaniu Vitest 5, jsdom i Testing Library. Nowe pakiety nie wniosły podatności.

**Sekrety w repozytorium:** bez zmian od poprzedniego raportu. `appsettings.json` i `appsettings.*.json` są ignorowane (`src/server/.gitignore`), śledzony jest tylko `appsettings.Example.json`. W kliencie śledzony jest tylko `.env.example`. Wartości lokalnych plików konfiguracyjnych nie odczytywano.

### Outdated Dependencies

```
Packages with major version gaps: 12 (NuGet: 1, npm: 11)
```

NuGet (`dotnet list package --outdated`):

- **MediatR**: 12.5.0 → 14.2.0 (2 majory). **Nie podbijać**: od v13 obowiązuje licencja komercyjna (reguła w `CLAUDE.md`).

npm (`npm outdated`; wszędzie `wanted` = `current`, czyli zakresy w `package.json` celowo zatrzymują się na bieżących majorach):

- **typescript**: 5.9.3 → 7.0.2 (2 majory)
- **@types/node**: 24.19.0 → 26.6.3 (2 majory)
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 majory)
- **vite**: 7.3.6 → 8.3.1; **@vitejs/plugin-react**: 5.2.0 → 6.1.1
- **react-router**: 7.18.4 → 8.4.0
- **mermaid**: 11.17.2 → 12.0.0
- **eslint**: 9.39.5 → 10.11.0; **@eslint/js**: 9.39.5 → 10.0.1; **globals**: 16.5.0 → 17.12.0; **eslint-plugin-react-refresh**: 0.4.26 → 0.5.7

Te luki służą tylko jako informacja. Wersje są przypięte zgodnie z sekcją „Wersje przypięte" w `CLAUDE.md`.

## Test Suite

### Serwer

```
Test runner: xUnit 2.9.3 (+ Microsoft.AspNetCore.Mvc.Testing / WebApplicationFactory, EF InMemory)
Tests found: 343
Test execution: passing (343/343, 0 pominiętych, ~20 s)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3, Moq, coverlet.collector
```

### Klient (nowe od poprzedniego raportu)

```
Test runner: Vitest 5 + jsdom + @testing-library/react + @testing-library/jest-dom
Tests found: 15 (4 pliki)
Test execution: passing (15/15, ~0,8 s)
```

```
Configuration: src/client/app01/vite.config.ts (blok `test`), setup: src/client/app01/src/test/setup.ts
Pliki testów: src/utils/auth.test.ts, src/utils/jwt.test.ts, src/utils/parseFrontmatter.test.ts, src/components/ConfirmModal.test.tsx
```

Agent może teraz automatycznie weryfikować zmiany po obu stronach granicy API.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push i pull_request na wszystkie gałęzie)
```

| Stage      | Status | Notes                                                                                              |
|------------|--------|----------------------------------------------------------------------------------------------------|
| Lint       | ✓      | `dotnet format --verify-no-changes`; `npx prettier --check`, `npm run lint` (ESLint, 0 ostrzeżeń)  |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator → artefakt HTML); `npm test` (Vitest)        |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build`                                                       |
| Type check | ✓      | kompilator C# (nullable) + `tsc -b` w `npm run build`                                              |
| Security   | ✓      | `dotnet list package --vulnerable` (fail na High/Critical); `npm audit --audit-level=high`         |

Lokalne odtworzenie kroków CI (27.09.2026, po commicie `1a6d86d`): `dotnet format --verify-no-changes` → exit 0; `dotnet test` → 343/343; `prettier --check` → exit 0; `npm run lint` → exit 0; `npm test` → 15/15; `npm run build` → OK (jedynie ostrzeżenie Vite o chunku JS > 500 kB). Stanu przebiegów w GitHub Actions nie sprawdzano.

## Configuration

### High severity

Brak.

### Medium severity

Brak. Formatter (`.prettierrc.json`, `dotnet format` + `.editorconfig`), linter (`eslint.config.js`) oraz `tsconfig.app.json` ze `"strict": true` są na miejscu.

### Low severity

- **`appsettings.Example.json` ma nieaktualny klucz `Swagger:Enable`**. Literówkę rozwiązano, zmieniając `Program.cs:64` na `Swagger:Enabled` (tak jak w testach i w lokalnych `appsettings*.json`). Szablon `appsettings.Example.json:19` wciąż ma jednak `"Enable": false`. Kto skopiuje szablon zgodnie z instrukcją w `CLAUDE.md` i ustawi `Enable: true`, nie zobaczy Swaggera i nie dostanie żadnego błędu.
- **Chunk JS > 500 kB** (ostrzeżenie Vite przy każdym buildzie).

Obecne pliki: `.editorconfig`, `.gitignore` (root, `src/server`, klient), `.env.example`, `appsettings.Example.json`, `.nvmrc`, `global.json`, `Directory.Build.props`, `CLAUDE.md`, `AGENTS.md`. Brak `tailwind.config.*` i `postcss.config.*` jest poprawny dla Tailwind 4 (CSS-first).

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap                                                     | Health-Check Finding                                                                                                                                                    | Status     |
|----------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------|
| Luka 1 (zamknięta): nieaktualny opis .NET 8 w `CLAUDE.md`           | `CLAUDE.md` ma sekcję „Wersje przypięte" dla .NET 10, `net10.0` w przepisie na moduł, zakaz `.WithOpenApi()` i dokumentację 10.0. Lokalne (ignorowane) `*.pubxml` mają `net10.0`. | Mitigated  |
| Luka 2: konwencje Minimal APIs / Vite+React żyją tylko w `CLAUDE.md` | Sekcja „Formatowanie (CI odrzuca niesformatowany kod)" i pełna lista kroków CI (z `npm test`) są w `CLAUDE.md`. `dotnet format` i `prettier --check` przechodzą.         | Mitigated  |
| Luka 3: brak testów frontendu                                        | Rozwiązane: Vitest 5 + Testing Library, 15 testów, krok „Test frontend" w CI, reguły pisania testów w `CLAUDE.md`.                                                      | Mitigated  |
| Typed / Convention / Training data / Documented (16/16 pass)         | Typowanie potwierdzone (`strict: true`, nullable). Przypięte wersje ograniczają ryzyko, że agent sięgnie po API nowszych majorów.                                      | Reinforced |

Wszystkie wpisy kompensacyjne rekomendowane przez stack-assess są obecne w `CLAUDE.md`, łącznie z instrukcją kopiowania `appsettings.Example.json` (poprawka nr 1 z poprzedniego raportu).

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Ujednolić klucz `Swagger:Enabled` w `appsettings.Example.json`

**Impact**: szablon konfiguracji jest wzorcem, który agent i nowe osoby kopiują dosłownie. Klucz niezgodny z kodem daje „cichą" awarię, czyli ustawienie, które nic nie robi. Agent może wtedy szukać przyczyny w kodzie zamiast w szablonie.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**:

```bash
sed -i 's/"Enable": false/"Enabled": false/' src/server/App01/App01.Bootstrapper.Api/appsettings.Example.json
```

### 2. Podział bundla JS

**Impact**: niski dla agenta. Stałe ostrzeżenie w logu `npm run build` zagłusza nowe ostrzeżenia, które agent powinien zauważyć.
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**: w `src/main.tsx` zamień importy ciężkich stron na `const XPage = lazy(() => import("./pages/..."));` i owiń `<Routes>` w `<Suspense>`. Alternatywnie wydziel `mermaid`/`hls.js` przez `build.rollupOptions.output.manualChunks` w `vite.config.ts`.

### 3. Wspólny helper konfiguracji testowej (opcjonalnie)

**Impact**: słownik konfiguracji w pamięci powtarza się w 26 plikach `EndpointTests.cs`. Agent tworzący testy dla modułu Kursy skopiuje go po raz kolejny. Przy zmianie klucza (jak `Swagger:Enabled`) trzeba poprawiać wszystkie kopie.
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**: wyciągnij słownik do statycznej metody w `tests/server/App01/App01.Api.Tests/Infrastructure/` (np. `TestConfiguration.Default()`), użyj jej w fabrykach i w nowych testach, a w `CLAUDE.md` wskaż ją jako wzorzec. Na koniec uruchom `dotnet test APPS.sln`.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (folder publish, bez kontenera/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zautomatyzujesz wdrożenie i rozszerzysz pipeline GitHub Actions o krok deploy. Konfigurację produkcyjną trzeba będzie dostarczyć ze zmiennych środowiskowych lub sekretów pipeline'u, bo `appsettings.json` nie jest w repozytorium.

### Utrzymanie `CLAUDE.md` / `AGENTS.md`

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: pliki już istnieją i są aktualne. Na lekcji dopracujesz je i ustawisz pętle feedbacku, żeby nadążały za kodem.

## Summary

```
Health status: healthy
```

Projekt jest w dobrej kondycji. NuGet i npm nie mają znanych podatności, a wersje są przypięte lock-file'ami. 343 testy serwera i 15 nowych testów klienta przechodzą, a CI uruchamia teraz oba zestawy. Wszystkie kroki CI przechodzą lokalnie. Od poprzedniego raportu zamknięto trzy poprawki: instrukcję `appsettings.Example.json` w `CLAUDE.md`, literówkę `Swagger:Enable(d)` w kodzie i brak testów frontendu. Zostały drobiazgi: jeden nieaktualny klucz w szablonie konfiguracji, ostrzeżenie o rozmiarze bundla i opcjonalne uporządkowanie konfiguracji testów.

Next step: popraw klucz w `appsettings.Example.json` (poprawka nr 1), a potem przejdź do agent onboardingu.
