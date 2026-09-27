---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T19:10:00Z
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
recommended_fixes: 6
---

## Dependency Health

Projekt ma dwa ekosystemy: serwer .NET 10 (`APPS.sln`, 8 projektów `*.csproj`, SDK przypięty w `global.json` na 10.0.100, lokalnie 10.0.401 przez `rollForward: latestMajor`) i klient JS/TS (`src/client/app01`). Przebieg wykonano po migracji na .NET 10 (commity `c8c50e9`, `27193ea`, `dab100f`, `626af8c`). `main` = `origin/main`. W drzewie roboczym są niezacommitowane zmiany w `CLAUDE.md`, `prd.md` i `stack-assessment.md`.

### Lockfile

```
Klient:  Status: present (src/client/app01/package-lock.json)
         Package manager: npm
Serwer:  Status: present (packages.lock.json w każdym z 8 projektów, śledzone w git)
         Package manager: dotnet (NuGet)
```

`Directory.Build.props` (`RestorePackagesWithLockFile=true`) wymusza lockfile w każdym nowym projekcie. `dotnet restore APPS.sln --locked-mode` przechodzi lokalnie, więc lockfile'e są zgodne z deklaracjami po skoku na .NET 10.

### Security Audit

```
Serwer:  Tool: dotnet list APPS.sln package --vulnerable --include-transitive
         Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
         Direct vs transitive: sprawdzono oba poziomy, żaden z 8 projektów nie ma podatnych pakietów
Klient:  Tool: npm audit --json
         Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW (0 info)
         Direct vs transitive: brak podatności na obu poziomach
```

Skan bezpieczeństwa plików środowiskowych: `src/client/app01/.env` jest **śledzony w git** i ma niepuste wartości `VITE_BASE_URL`, `VITE_API_URL` i `VITE_APP_TOKEN`. Zmienne `VITE_*` i tak trafiają do bundla w przeglądarce, więc `X-TOKEN` nie jest sekretem w ścisłym sensie. Mimo to konfiguracja środowiskowa nie powinna leżeć w repo. Warianty `.env.dev`, `.env.prod1` i `.env.prod2` są poprawnie ignorowane. Szczegóły w sekcji Configuration.

### Outdated Dependencies

```
Packages with major version gaps: 10 (1 NuGet, 9 npm)
```

Dwa pakiety są 2 wersje major do tyłu:

- **typescript**: 5.9.3 → 7.0.2 (2 major wstecz). Przypięty celowo (`~5.9.3`, `CLAUDE.md`).
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 major wstecz).

Jedna wersja major wstecz:

- **MediatR** (NuGet): 12.5.0 → 14.2.0. **Nie podbijać**: od v13 licencja komercyjna (reguła w `CLAUDE.md`).
- **react-router**: 7.18.4 → 8.4.0. Przypięty celowo (React Router 7, tryb deklaratywny).
- **vite**: 7.3.6 → 8.3.1 oraz **@vitejs/plugin-react**: 5.1.2 → 6.1.1. Vite 7 przypięty celowo.
- **eslint**: 9.39.2 → 10.11.0, **@eslint/js**: 9.39.2 → 10.0.1, **globals**: 16.5.0 → 17.12.0, **eslint-plugin-react-refresh**: 0.4.26 → 0.5.7.
- **mermaid**: 11.17.2 → 12.0.0, **@types/node**: 24 → 26.

Pozostałe pakiety NuGet (EF Core 10.0.12, Serilog 10, Swashbuckle 10, FluentValidation 12) są aktualne. Pakiety npm poza powyższymi mają tylko podbicia minor/patch w obrębie zakresów (`react` 19.3.0, `tailwindcss` 4.3.3, `typescript-eslint` 8.70.1).

## Test Suite

```
Test runner: xUnit 2.9.3 (+ Microsoft.AspNetCore.Mvc.Testing / WebApplicationFactory, Moq, coverlet)
Tests found: 343 tests
Test execution: passing (343/343, 0 pominiętych, ~24 s; ASPNETCORE_ENVIRONMENT=Test)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3 na net10.0, EF Core InMemory
```

Weryfikacja serwera przeszła w całości: `dotnet build APPS.sln` bez błędów i ostrzeżeń (w tym bez ostrzeżeń `obsolete`), `dotnet format --verify-no-changes` z kodem 0.

Frontend nie ma testów (znana luka ze stack-assessment). Dlatego jedyną automatyczną weryfikacją klienta są `tsc -b`, ESLint, Prettier i `vite build`. **Lokalnie żadne z tych narzędzi się teraz nie uruchamia.** Pakiety `prettier`, `eslint`, `vite` i `typescript` w `src/client/app01/node_modules` nie mają katalogów `bin/`, a `npx prettier`, `npm run lint` i `npm run build` kończą się `MODULE_NOT_FOUND` (np. `node_modules\prettier\bin\prettier.cjs`). `npm ls` nie zgłasza braków, bo metadane pakietów są na miejscu. To uszkodzona instalacja lokalna, nie błąd projektu. CI robi świeże `npm ci`, więc go to nie dotyczy.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push i pull_request na wszystkie gałęzie)
```

| Stage      | Status | Notes |
|------------|--------|-------|
| Lint       | ✓      | ESLint (`npm run lint`), Prettier `--check`, `dotnet format --verify-no-changes` |
| Test       | ✓      | `dotnet test` z pokryciem (coverlet → ReportGenerator → artefakt HTML); frontend bez testów |
| Build      | ✓      | `dotnet build --no-restore` (po `restore --locked-mode`), `npm run build` |
| Type check | ✓      | Kompilator C# (nullable) + `tsc -b` w `npm run build` |
| Security   | ✓      | `dotnet list package --vulnerable --include-transitive`, `npm audit --audit-level=high` |

Uwaga: krok `dotnet list package --vulnerable` tylko wypisuje wynik i nie przerywa joba przy znalezisku, bo nie sprawdza wyjścia. `npm audit --audit-level=high` przerywa. Lokalne odpowiedniki kroków backendu przechodzą. Kroków frontendu nie da się lokalnie odtworzyć, dopóki `node_modules` jest uszkodzony.

## Configuration

### High severity

Brak.

### Medium severity

- **`src/client/app01/.env` (śledzony) + `.env.example` (ignorowany)**: wzorzec `.env.*` w `src/client/app01/.gitignore` ignoruje także `.env.example`, więc szablon zmiennych nie trafia do repo. Za to `.env` z konkretnymi URL-ami i tokenem jest commitowany. Agent (i nowa osoba) nie ma w repo udokumentowanej listy zmiennych, a zmiana `.env` wpada do diffów. Fix: patrz Recommended Fixes #3.
- **`CLAUDE.md`, dwa resztkowe fragmenty po .NET 8** (wynik weryfikacji rekomendacji ze stack-assessment):
  - linia 101: na końcu łańcucha endpointu został dopisek `` `Produces`.WithOpenApi()` ``. Reguła mówi „NIE dodawaj `.WithOpenApi()`”, ale tekst kończy się tym wywołaniem, co jest mylącym sygnałem dla agenta;
  - linia 114: nagłówek „Dane i migracje (EF Core 8, SQL Server)”, a projekt jest na EF Core 10.

### Low severity

- **`src/server/App01/App01.Bootstrapper.Api/Properties/PublishProfiles/FolderProfile1.pubxml`**: `PublishUrl` nadal wskazuje `bin\Release\net8.0\publish\`. `FolderProfile.pubxml` jest już poprawiony na `net10.0`.
- **`Microsoft.AspNetCore.OpenApi` 10.0.12** w `App01.Shared.Abstractions.csproj`: w kodzie nie ma `AddOpenApi`/`MapOpenApi` (OpenAPI generuje Swashbuckle), więc pakiet wygląda na martwą zależność po usunięciu `.WithOpenApi()`.

Obecne i poprawne: `.editorconfig` (312 linii reguł .NET), `.gitignore` (root + klient), `eslint.config.js`, `.prettierrc.json`, `tsconfig.app.json` ze `strict: true`, `CLAUDE.md`, `AGENTS.md`.

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|---|---|---|
| Luka 1: `CLAUDE.md` opisuje .NET 8, kod jest na .NET 10 | Większość rekomendacji wdrożona (sekcja wersji, TFM `net10.0` w przepisie na moduł, zakaz `.WithOpenApi()`, dokumentacja 10.0). Zostały: dopisek `.WithOpenApi()` w l. 101, „EF Core 8” w l. 114, `net8.0` w `FolderProfile1.pubxml`. Zmiany w `CLAUDE.md` są **niezacommitowane**. | Partially mitigated |
| Ryzyko wersji: agent proponuje API przestarzałe w .NET 10 | Build z 0 ostrzeżeniami, 0 wywołań `WithOpenApi` w `src/server`. Kod jest czysty, a reguła w `CLAUDE.md` jest. | Mitigated |
| Luka 2: konwencje Minimal APIs / Vite+React tylko w `CLAUDE.md` + formatowanie | Blok „Formatowanie” i pełna checklista CI dodane do `CLAUDE.md`. `dotnet format --verify-no-changes` przechodzi. Prettier/ESLint po stronie klienta nie działają lokalnie (uszkodzony `node_modules`). | Mitigated (serwer) / Reinforced (klient) |
| Luka 3: brak testów frontendu | Jedyną siatką bezpieczeństwa klienta są `tsc -b` + lint + build, a lokalnie się teraz nie uruchamiają. Agent pracujący nad UI modułu Kursy nie zweryfikuje zmian przed pushem. | Reinforced |
| Typed (C# nullable, TS strict) | `strict: true` potwierdzone; type check jest w CI (`tsc -b`, kompilator C#). | Mitigated |

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Napraw lokalną instalację zależności klienta

**Impact**: Agent nie uruchomi `npm run build` (`tsc -b`), `npm run lint` ani `prettier --check`, a przy braku testów frontendu to jedyne automatyczne sprawdzenia klienta. Każda zmiana w UI (moduł Kursy) trafi do CI niezweryfikowana.
**Severity**: high
**Effort**: quick (< 5 min)
**Fix**:

```bash
cd src/client/app01
rm -rf node_modules
npm ci
npx prettier --check "src/**/*.{ts,tsx,css}" && npm run lint && npm run build
```

Jeśli katalogi `bin/` znikną ponownie, sprawdź antywirusa albo narzędzia czyszczące, które mogą usuwać katalogi `bin` w projekcie (np. skrypt „clean bin/obj” dla .NET uruchamiany rekursywnie od korzenia repo).

### 2. Dokończ aktualizację `CLAUDE.md` i zacommituj ją

**Impact**: Instrukcje mają pierwszeństwo przed kodem. Dopisek `.WithOpenApi()` na końcu przepisu na endpoint i „EF Core 8” w nagłówku sekcji danych to sprzeczne sygnały. Dopóki zmiany nie są w commicie, inne klony i sesje ich nie widzą.
**Severity**: medium
**Effort**: quick (< 5 min)
**Fix**:

- `CLAUDE.md:101`: usuń końcowy fragment `` .WithOpenApi()` ``, tak by zdanie kończyło się na „…z `WithName`/`WithTags`/`Produces`.”
- `CLAUDE.md:114`: „## Dane i migracje (EF Core 10, SQL Server)”.
- `git add CLAUDE.md context/foundation && git commit -m "docs: align CLAUDE.md with .NET 10"`

### 3. Uporządkuj pliki `.env` klienta

**Impact**: Szablon zmiennych (`.env.example`) jest ignorowany, więc agent nie wie, jakie zmienne istnieją. Śledzony `.env` z realnymi URL-ami i tokenem wpada do diffów i utrwala konfigurację środowiska w historii.
**Severity**: medium
**Effort**: quick (< 5 min)
**Fix**:

```bash
cd src/client/app01
# w .gitignore, pod linią ".env.*":
printf '!.env.example\n.env\n' >> .gitignore
git rm --cached .env
git add .gitignore .env.example
```

Sprawdź przedtem, czy build CI (`vite build --mode dev`) nie polega na commitowanym `.env`. Jeśli polega, przenieś wartości do zmiennych/sekretów GitHub Actions albo zostaw `.env` i dodaj tylko `!.env.example`.

### 4. Popraw `PublishUrl` w drugim profilu publikacji

**Impact**: Publikacja przez `FolderProfile1` trafi do katalogu `net8.0`, co myli przy wdrożeniu i przy analizie artefaktów przez agenta.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: w `src/server/App01/App01.Bootstrapper.Api/Properties/PublishProfiles/FolderProfile1.pubxml` zmień `bin\Release\net8.0\publish\` na `bin\Release\net10.0\publish\`.

### 5. Usuń nieużywany pakiet `Microsoft.AspNetCore.OpenApi`

**Impact**: Martwa zależność po usunięciu `.WithOpenApi()`. Agent może uznać ją za wskazówkę, że wbudowane OpenAPI (`AddOpenApi`) jest w użyciu, i mieszać je ze Swashbuckle.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**:

```bash
dotnet remove src/server/App01/App01.Shared.Abstractions package Microsoft.AspNetCore.OpenApi
dotnet restore APPS.sln --force-evaluate   # aktualizuje packages.lock.json
dotnet build APPS.sln && dotnet test APPS.sln
```

Zacommituj zmienione `packages.lock.json` (wymóg `--locked-mode` w CI). Jeśli build zgłosi brak typu, pakiet jednak jest potrzebny i trzeba cofnąć zmianę.

### 6. Przegląd przestarzałych zależności (bez podbijania przypiętych majorów)

**Impact**: Przypięte majory (MediatR 12, React Router 7, Vite 7, TypeScript 5.9) są decyzją projektową i `CLAUDE.md` zabrania ich podbijania. Nieprzypięte dev-zależności lintera (`eslint-plugin-react-hooks` 2 majory wstecz, ESLint 10) będą się rozjeżdżać z dokumentacją, z której korzysta agent.
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**:

```bash
cd src/client/app01
npm update            # podbicia minor/patch w obrębie zakresów (react 19.3, tailwind 4.3, typescript-eslint 8.70)
npm run lint && npm run build
```

Podbicie ESLint 9 → 10 i `eslint-plugin-react-hooks` 5 → 7 zrób jako osobną, świadomą zmianę (nowe reguły hooków mogą zgłosić błędy w istniejącym kodzie). MediatR zostaw na 12.x.

### Addressed in upcoming lessons (Category B)

### Pliki instrukcji agenta — dopracowanie `CLAUDE.md` / `AGENTS.md`

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: Oba pliki już istnieją i są bogate. Na lekcji zbudujesz pętle feedbacku (np. uruchamianie lint/build/test jako weryfikacja agenta) i zdecydujesz, co zostaje w `CLAUDE.md`, a co trafia do reguł per obszar.

### CI: twarde przerwanie na podatnym pakiecie NuGet, wdrożenie

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: Rozbudujesz pipeline o pierwszy deploy (dziś tylko `FolderProfile*.pubxml`, bez kontenera ani PaaS). Przy okazji możesz sprawić, by krok `dotnet list package --vulnerable` przerywał job przy znalezisku, tak jak `npm audit --audit-level=high`.

## Summary

Health status: needs-attention

Stan serwera jest bardzo dobry. Zero podatności w NuGet i npm, lockfile'e zgodne w `--locked-mode`, build .NET 10 bez ostrzeżeń, 343/343 testów zielonych, formatowanie czyste, a pełne CI obejmuje lint, testy, build, type check i skan bezpieczeństwa. Status obniża stan lokalny klienta: uszkodzony `node_modules` blokuje `tsc`, ESLint, Prettier i `vite build`, czyli jedyną weryfikację frontendu, który nie ma testów. Do tego dochodzą drobne resztki migracji z .NET 8 w `CLAUDE.md` i profilu publikacji oraz ignorowany `.env.example`.

Next step: napraw `node_modules` (#1) i dokończ `CLAUDE.md` (#2), razem kilka minut. Wtedy projekt spełnia kryteria `healthy` i można przejść do agent onboarding.
