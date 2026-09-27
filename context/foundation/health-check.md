---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T21:45:00Z
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
recommended_fixes: 4
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

**Sekrety w repozytorium:**

- `appsettings.json` nie jest już śledzony przez git (`git ls-files` zwraca tylko `appsettings.Example.json`). Ignoruje go `src/server/.gitignore` (`appsettings.json`, `appsettings.*.json`, wyjątek `!appsettings.Example.json`), dodany w commicie `493197a`. Według autora plik nie zawierał kluczy, więc rotacja nie jest potrzebna. Zamyka to poprawkę nr 3 z poprzedniego raportu.
- `appsettings.Example.json` dokumentuje wszystkie klucze konfiguracji używane w kodzie (`Jwt:*`, `OpenRouter:*`, `LottoOpenApi:*`, `Tokens:X-TOKEN`, `Swagger:*`, `Workers:*`, `Serilog:*`, `ConnectionStrings:DefaultConnection`). W `.csproj` ma ustawione `CopyToPublishDirectory="Never"`.
- Pliki `.env`, `.env.dev`, `.env.prod1` i `.env.prod2` w `src/client/app01` są ignorowane (`.env`, `.env.*`). Śledzony jest tylko `.env.example` (`VITE_BASE_URL`, `VITE_API_URL`, `VITE_APP_TOKEN`). Wartości `.env*` nie odczytywano.
- Pliki `*.pubxml.user`, w których wciąż jest `net8.0`, są ignorowane i lokalne. Nie wpływają na repozytorium.

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

Te luki służą tylko jako informacja. Wersje są przypięte zgodnie z sekcją „Wersje przypięte" w `CLAUDE.md`, a agent nie może podbijać majorów przy okazji innych zmian.

## Test Suite

```
Test runner: xUnit 2.9.3 (+ Microsoft.AspNetCore.Mvc.Testing / WebApplicationFactory, EF InMemory)
Tests found: 343
Test execution: passing (343/343, 0 pominiętych, ~21 s)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3, Moq, coverlet.collector
```

Testy nie zależą od `appsettings.json`. `TestWebApplicationFactory` i `ConfigurableTestWebApplicationFactory` wywołują `config.Sources.Clear()` i podają konfigurację w pamięci. Usunięcie pliku z repozytorium nie psuje więc testów w CI, gdzie tego pliku nie ma.

Frontend (`src/client/app01`) nie ma test runnera. Jego rolę pełnią `tsc -b` (strict), `eslint --max-warnings 0` i `prettier --check`. Zgodnie z `CLAUDE.md` logika domenowa zostaje po stronie serwera, gdzie pokrywają ją testy endpointów.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push i pull_request na wszystkie gałęzie)
```

| Stage      | Status | Notes                                                                                              |
|------------|--------|----------------------------------------------------------------------------------------------------|
| Lint       | ✓      | `dotnet format --verify-no-changes`; `npx prettier --check`, `npm run lint` (ESLint, 0 ostrzeżeń)  |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator → artefakt HTML)                             |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build`                                                       |
| Type check | ✓      | kompilator C# (nullable) + `tsc -b` w `npm run build`                                              |
| Security   | ✓      | `dotnet list package --vulnerable` (fail na High/Critical); `npm audit --audit-level=high`         |

Lokalne odtworzenie kroków CI (27.09.2026, po commicie `493197a`): `dotnet format --verify-no-changes` → exit 0; `dotnet test` → 343/343; `prettier --check` → exit 0; `npm run lint` → exit 0; `npm run build` → OK. Jedyny komunikat to ostrzeżenie Vite o chunku JS > 500 kB. Stanu przebiegów w GitHub Actions nie sprawdzano.

## Configuration

### High severity

Brak.

### Medium severity

Brak. Formatter (`.prettierrc.json`, `dotnet format` + `.editorconfig`), linter (`eslint.config.js`) oraz `tsconfig.app.json` ze `"strict": true` są na miejscu.

### Low severity

- **Brak instrukcji „skopiuj `appsettings.Example.json`"**. Po świeżym klonie `appsettings.json` nie istnieje, a aplikacja rzuca `JWT Key not configured` przy starcie (`ServerDI.cs:70`). Nigdzie w dokumentacji nie opisano tego kroku.
- **Literówka w konfiguracji testów**: fabryki testowe i 26 plików `EndpointTests.cs` ustawiają `Swagger:Enabled`, a `Program.cs` czyta `Swagger:Enable`. Dziś nie ma to skutków, bo domyślna wartość to `false`.
- **Chunk JS > 500 kB** (ostrzeżenie Vite przy każdym buildzie).

Obecne pliki: `.editorconfig`, `.gitignore` (root, `src/server`, klient), `.env.example`, `appsettings.Example.json`, `.nvmrc`, `global.json`, `Directory.Build.props`, `CLAUDE.md`, `AGENTS.md`. Brak `tailwind.config.*` i `postcss.config.*` jest poprawny dla Tailwind 4 (CSS-first).

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap                                                     | Health-Check Finding                                                                                                                                                              | Status     |
|----------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------|
| Luka 1: `CLAUDE.md` opisuje .NET 8, kod jest na .NET 10              | `CLAUDE.md` ma sekcję „Wersje przypięte" dla .NET 10, `net10.0` w przepisie na moduł, zakaz `.WithOpenApi()` i dokumentację 10.0. Śledzone `*.pubxml` mają `net10.0`.           | Mitigated  |
| Luka 2: konwencje Minimal APIs / Vite+React żyją tylko w `CLAUDE.md` | Sekcja „Formatowanie (CI odrzuca niesformatowany kod)" i pełna lista kroków CI są w `CLAUDE.md`. `dotnet format` i `prettier --check` przechodzą.                                | Mitigated  |
| Luka 3: brak testów frontendu                                        | Nadal brak runnera po stronie klienta. Kompensują to reguła „logika domenowa po stronie serwera" oraz `tsc` strict, ESLint i prettier w CI. UI modułu Kursy wymaga weryfikacji manualnej. | Accepted   |
| Typed / Convention / Training data / Documented (16/16 pass)         | Typowanie potwierdzone (`strict: true`, nullable). Przypięte wersje ograniczają ryzyko, że agent sięgnie po API nowszych majorów.                                              | Reinforced |

Wszystkie wpisy kompensacyjne rekomendowane przez stack-assess są obecne w `CLAUDE.md`. Reguła „Nie wpisuj sekretów … do `appsettings.json`" pozostaje aktualna i jest teraz dodatkowo wymuszona przez `.gitignore`.

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Udokumentuj krok `appsettings.Example.json` → `appsettings.json`

**Impact**: agent (albo nowa osoba) po świeżym klonie nie uruchomi API. Start kończy się wyjątkiem `JWT Key not configured`, a z kodu nie wynika, skąd wziąć konfigurację. Agent może wtedy „naprawić" problem wpisaniem klucza do śledzonego pliku.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**:

W `CLAUDE.md`, w sekcji „Bezpieczeństwo endpointów" przy regule o sekretach, dopisz:

```markdown
- `appsettings.json` i `appsettings.*.json` są ignorowane przez git (`src/server/.gitignore`); jedynym śledzonym plikiem jest `appsettings.Example.json`. Lokalnie: `cp src/server/App01/App01.Bootstrapper.Api/appsettings.Example.json src/server/App01/App01.Bootstrapper.Api/appsettings.json` i uzupełnij wartości. Nowy klucz konfiguracji dopisz najpierw do `appsettings.Example.json` (z pustą/przykładową wartością).
```

### 2. Popraw literówkę `Swagger:Enabled` w fabrykach testowych

**Impact**: klucz testowy nie odpowiada kluczowi czytanemu przez `Program.cs` (`Swagger:Enable`). Agent kopiujący wzorzec z testów powieli błędną nazwę.
**Severity**: low
**Effort**: quick (< 5 min)

```bash
grep -rl '"Swagger:Enabled"' tests/server | xargs sed -i 's/"Swagger:Enabled"/"Swagger:Enable"/'
dotnet test APPS.sln
```

Przy okazji rozważ wyciągnięcie powtarzanego słownika konfiguracji testowej do jednej metody pomocniczej w `Infrastructure/`, żeby agent nie kopiował go do każdego nowego `EndpointTests.cs`.

### 3. Brak testów frontendu przy planowanej pracy nad UI modułu Kursy — ✅ ZROBIONE (27.09.2026)

Dodano Vitest 5 + jsdom + Testing Library (`npm test`, blok `test` w `vite.config.ts`, setup w `src/test/setup.ts`), 15 testów w 4 plikach (`auth`, `jwt`, `parseFrontmatter`, `ConfirmModal`), krok „Test frontend" w `.github/workflows/pull-request.yml` oraz reguły w `CLAUDE.md`. Poniżej pierwotny opis.

**Impact**: agent nie zweryfikuje automatycznie zachowania nowych stron (kafelki kursów, widok kursu, hangar). Wykryje tylko błędy typów, lintu i formatowania.
**Severity**: low (świadomie skompensowane regułą w `CLAUDE.md`)
**Effort**: moderate (15–30 min), jeśli się zdecydujesz
**Fix**: opcjonalnie, gdy w module Kursy pojawi się logika po stronie klienta:

```bash
cd src/client/app01
npm install -D vitest@^3 @testing-library/react @testing-library/jest-dom jsdom
# package.json → "scripts": { "test": "vitest run" }
# vite.config.ts → test: { environment: "jsdom" }
```

W przeciwnym razie dopisz do planu zmiany krok manualnej weryfikacji UI.

### 4. Podział bundla JS

**Impact**: niski dla agenta. Stałe ostrzeżenie w logu `npm run build` zagłusza nowe ostrzeżenia, które agent powinien zauważyć.
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**: w `src/main.tsx` zamień importy ciężkich stron na `const XPage = lazy(() => import("./pages/..."));` i owiń `<Routes>` w `<Suspense>`. Alternatywnie wydziel `mermaid`/`hls.js` przez `build.rollupOptions.output.manualChunks` w `vite.config.ts`.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (folder publish, bez kontenera/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zautomatyzujesz wdrożenie i rozszerzysz pipeline GitHub Actions o krok deploy. Skoro `appsettings.json` nie jest już w repozytorium, konfigurację produkcyjną trzeba będzie dostarczyć ze zmiennych środowiskowych lub sekretów pipeline'u.

### Utrzymanie `CLAUDE.md` / `AGENTS.md`

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: pliki już istnieją i są aktualne. Na lekcji dopracujesz je i ustawisz pętle feedbacku, żeby nadążały za kodem.

## Summary

```
Health status: healthy
```

Projekt jest w dobrej kondycji. NuGet i npm nie mają znanych podatności, a wersje są przypięte lock-file'ami. 343 testy serwera przechodzą i nie zależą od `appsettings.json`. Kroki CI (format, lint, build, typy, skan bezpieczeństwa, testy) przechodzą lokalnie. Poprzednia otwarta kwestia, czyli śledzony `appsettings.json`, jest zamknięta: plik usunięto z repozytorium, nie zawierał kluczy, a `.gitignore` nie pozwoli go dodać ponownie. Zostały drobiazgi: udokumentować kopiowanie `appsettings.Example.json`, poprawić literówkę w konfiguracji testów, opcjonalnie testy frontendu i podział bundla.

Next step: dopisz regułę z poprawki nr 1 do `CLAUDE.md`, a potem przejdź do agent onboardingu.
