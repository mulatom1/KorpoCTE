---
project: APPS (tomsoft1.pl — App01)
checked_at: 2026-09-27T19:34:04Z
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
recommended_fixes: 6
---

## Dependency Health

Projekt ma dwa ekosystemy: serwer .NET 10 (`APPS.sln`, 8 projektów `*.csproj`, SDK przypięty w `global.json` na 10.0.100, lokalnie 10.0.401 przez `rollForward: latestMajor`) i klient JS/TS (`src/client/app01`). Przebieg wykonano na czystym drzewie roboczym po commicie `6f37f3a` (poza nieśledzonymi `.10x-cli.json` i `skills-lock.json`). Poprawki z `stack-assessment.md` w `CLAUDE.md` są już zacommitowane.

### Lockfile

```
Status: present
  - NuGet: packages.lock.json w każdym z 8 projektów (RestorePackagesWithLockFile w Directory.Build.props, CI: dotnet restore --locked-mode)
  - npm: src/client/app01/package-lock.json (CI: npm ci)
Package manager: dotnet (NuGet) + npm
```

Oba ekosystemy mają pełne przypięcie wersji i CI wymusza zgodność z lock-filami.

### Security Audit

```
Tool (serwer): dotnet list APPS.sln package --vulnerable --include-transitive
Tool (klient): npm audit --json (w src/client/app01)
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: NuGet — 0 bezpośrednich / 0 przechodnich (flaga --include-transitive); npm — 0 / 0
```

Brak znanych podatności w żadnym z 8 projektów .NET ani w drzewie npm.

**Sekrety i pliki `.env`** (weryfikacja zlecona przez `stack-assessment.md`):

- W repozytorium śledzony jest tylko `src/client/app01/.env.example`. Pliki `.env`, `.env.dev`, `.env.prod1`, `.env.prod2` są ignorowane (`src/client/app01/.gitignore`: `.env`, `.env.dev*`, `.env.prod*`, `!.env.example`).
- Historia: `.env` był śledzony od `a5a2a5d` i w `dfc2caa` został zmieniony (rename 100%) na `.env.example`. Wyciekła więc tylko ta sama zawartość, co w przykładzie (deweloperski `X-TOKEN`). Tokeny produkcyjne z `.env.prod*` nigdy nie trafiły do historii.
- `appsettings.json` (śledzony) ma puste wartości dla `X-TOKEN`, klucza JWT i kluczy API. Connection string i sekrety są dostarczane poza repo.
- Uwaga architektoniczna, nie podatność: `VITE_APP_TOKEN` jest wstrzykiwany do bundla klienta, więc `X-TOKEN` jest z definicji jawny dla każdego użytkownika przeglądarki. To filtr aplikacyjny, a nie sekret. Autoryzacja opiera się na JWT (`RequireAuthorization()`).

### Outdated Dependencies

```
Packages with major version gaps: 9 (npm: 8, NuGet: 1)
```

Pakiety opóźnione o 2 lub więcej wersji głównych:

- **typescript**: 5.9.3 → 7.0.2 (2 wersje główne) — przypięty w `CLAUDE.md` na 5.9. TS 7 to natywny kompilator (Go) i trzeba go wprowadzać świadomie.
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 wersje główne) — v7 dodaje reguły React Compiler, które zmienią wynik `npm run lint`.

Opóźnione o 1 wersję główną: `react-router` 7.18.4 → 8.4.0, `vite` 7.3.6 → 8.3.1, `@vitejs/plugin-react` 5.2.0 → 6.1.1, `eslint` / `@eslint/js` 9.39.5 → 10.x, `mermaid` 11.17.2 → 12.0.0, `globals` 16 → 17, `@types/node` 24 → 26. Po stronie NuGet: **MediatR** 12.5.0 → 14.2.0. Ta aktualizacja jest **celowo zablokowana**, bo v13+ ma licencję komercyjną (`CLAUDE.md`).

Wszystkie pozostałe pakiety NuGet (EF Core 10.0.12, Serilog 10, Swashbuckle 10, FluentValidation 12) są aktualne.

## Test Suite

```
Test runner: xUnit 2.9.3 (serwer) + WebApplicationFactory (Microsoft.AspNetCore.Mvc.Testing 10.0.12); frontend — brak
Tests found: 343
Test execution: passing (343/343, 0 pominiętych, ~22 s, net10.0)
```

```
Configuration: tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj
Framework: xUnit 2.9.3, Moq, coverlet.collector, EF Core InMemory
```

`dotnet build APPS.sln` zakończył się z **0 ostrzeżeniami** (w tym 0 ostrzeżeń `obsolete`). To potwierdza, że migracja na .NET 10 jest domknięta.

Frontend nie ma testów. Jest to świadoma decyzja skompensowana regułą w `CLAUDE.md` („logika domenowa po stronie serwera"). Weryfikacja UI modułu Kursy pozostanie manualna. Automatyczną ochroną klienta są jedynie `tsc -b` (strict) i ESLint.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (push i pull_request na każdą gałąź; joby backend-ci i frontend-ci)
```

| Stage      | Status | Notes |
|------------|--------|-------|
| Lint       | ✓      | `dotnet format --verify-no-changes` (serwer); `npx prettier --check` + `npm run lint` / ESLint (klient) |
| Test       | ✓      | `dotnet test` (xUnit) z pokryciem Cobertura i raportem HTML (ReportGenerator) jako artefakt |
| Build      | ✓      | `dotnet build --no-restore`; `npm run build` (Vite) |
| Type check | ✓      | Kompilator C# (nullable) w buildzie; `tsc -b` w `npm run build` |
| Security   | ~      | `npm audit --audit-level=high` blokuje build. `dotnet list package --vulnerable` jedynie **raportuje**: kończy się kodem 0 nawet przy znaleziskach, więc nie blokuje PR |

Wszystkie kroki CI odtworzone lokalnie przechodzą: `dotnet format --verify-no-changes` (exit 0), build (0 ostrzeżeń), testy 343/343, `prettier --check` („All matched files use Prettier code style!”), `npm run lint` (0 błędów, 3 ostrzeżenia), `npm run build` (sukces z ostrzeżeniem o chunku > 500 kB).

## Configuration

Obecne: `.editorconfig` (root, reguły .NET), `.gitignore` (root + `src/client/app01`), `src/client/app01/.prettierrc.json`, `src/client/app01/eslint.config.js`, `tsconfig.app.json` z `"strict": true`, `src/client/app01/.env.example`, `global.json`, `Directory.Build.props`, `CLAUDE.md`, `AGENTS.md`.

### High severity

Brak.

### Medium severity

- **`CLAUDE.md` linia 101 (przepis na wycinek funkcji, krok 4)** — zdanie kończy się urwanym fragmentem: „…zbiera Swashbuckle z `WithName`/`WithTags`/`Produces`.WithOpenApi()`.” Pozostałość po wklejeniu poprawki z `stack-assessment.md`. Sam zakaz jest poprawny, ale ostatni token wygląda jak sugestia łańcucha `.Produces(...).WithOpenApi()`. Agent czyta ten przepis dosłownie. Fix: usunąć `.WithOpenApi()` i zostawić końcówkę „…z `WithName`/`WithTags`/`Produces`.”

### Low severity

- **`src/server/App01/App01.Bootstrapper.Api/Properties/PublishProfiles/FolderProfile1.pubxml`** — `PublishUrl` wciąż wskazuje `bin\Release\net8.0\publish\`. `FolderProfile.pubxml` jest już poprawiony. Pliki `*.pubxml` nie są śledzone w git, więc problem jest lokalny. Fix: zmienić na `bin\Release\net10.0\publish\`.
- **3 ostrzeżenia ESLint `react-hooks/exhaustive-deps`** — `LottoDrawsPage.tsx:132`, `LottoTicketsPage.tsx:182`, `LottoWinningTicketsPage.tsx:126`. Agent tworzący nowe strony (Kursy) może skopiować ten wzorzec. Fix w sekcji Recommended Fixes.

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|------------------|----------------------|--------|
| Luka 1: `CLAUDE.md` opisuje .NET 8 (TFM, `.WithOpenApi()`, dokumentacja 8.0, wersje EF/Serilog) | `CLAUDE.md` ma sekcję „Wersje przypięte” dla .NET 10, `net10.0` w przepisie na moduł i zakaz `.WithOpenApi()`. W `src/server` nie ma żadnego wywołania `WithOpenApi`, build daje 0 ostrzeżeń `obsolete`. Zostały: urwany fragment w linii 101 i lokalny `FolderProfile1.pubxml` z `net8.0` | Mitigated (2 drobne resztki) |
| Luka 2 / convention: ~ (Minimal APIs, Vite + React — konwencje tylko w `CLAUDE.md`) | Dodane sekcje „Formatowanie” i pełna lista kroków CI. Lokalnie `dotnet format` i `prettier --check` przechodzą. CI pilnuje obu | Mitigated |
| Luka 3: brak testów frontendu | Potwierdzone. Brak runnera JS, jedyna ochrona to `tsc -b` + ESLint. Reguła „logika domenowa po stronie serwera” w `CLAUDE.md` + 343 testy endpointów | Accepted (compensated) |
| Rekomendacja: pliki instrukcji z blokami z assessmentu | Wszystkie 4 bloki (wersje, poprawki punktowe, formatowanie, checklista CI) są obecne w `CLAUDE.md`. `AGENTS.md` odsyła do `CLAUDE.md` | Mitigated |

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Urwany fragment `.WithOpenApi()` w przepisie na wycinek funkcji

**Impact**: `CLAUDE.md` ma pierwszeństwo przed kodem. Przepis na slice zostanie użyty dosłownie przy każdym nowym endpoincie modułu Kursy. Końcówka „`Produces`.WithOpenApi()`” jest sprzeczna z zakazem w tym samym zdaniu i może skłonić agenta do dopisania przestarzałego API (ASPDEPR002). Build by to wyłapał, ale kosztem dodatkowej iteracji.
**Severity**: medium
**Effort**: quick (< 5 min)
**Fix**:

W `CLAUDE.md`, linia 101, zamień końcówkę
`…metadane OpenAPI zbiera Swashbuckle z `WithName`/`WithTags`/`Produces`.WithOpenApi()`.`
na
`…metadane OpenAPI zbiera Swashbuckle z `WithName`/`WithTags`/`Produces`.`

### 2. Skan podatności NuGet w CI nie blokuje PR

**Impact**: `dotnet list package --vulnerable` zwraca exit 0 nawet przy znaleziskach. Jeśli agent doda pakiet z podatnością (np. przy module Kursy), CI przejdzie na zielono. Po stronie npm ta sama ochrona działa (`--audit-level=high`).
**Severity**: medium
**Effort**: quick (< 5 min)
**Fix**:

W `Directory.Build.props` zamień restore-time audit NuGet na błędy dla HIGH/CRITICAL (NuGetAudit jest domyślnie włączony w SDK 10, ale zgłasza tylko ostrzeżenia):

```xml
<Project>
  <PropertyGroup>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NuGetAuditMode>all</NuGetAuditMode>
    <WarningsAsErrors>$(WarningsAsErrors);NU1903;NU1904</WarningsAsErrors>
  </PropertyGroup>
</Project>
```

Alternatywnie w `.github/workflows/pull-request.yml`:

```yaml
- name: Check vulnerable NuGet packages
  run: |
    dotnet list package --vulnerable --include-transitive 2>&1 | tee vuln.txt
    ! grep -qE "\b(High|Critical)\b" vuln.txt
```

### 3. Ostrzeżenia `react-hooks/exhaustive-deps` w stronach Lotto

**Impact**: agent buduje nowe strony na wzór istniejących. Wzorzec „funkcja fetch poza `useEffect` + niepełna tablica zależności” rozpropaguje się na moduł Kursy. Ostrzeżenia nie blokują CI, więc nikt ich nie zatrzyma.
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**:

W `src/client/app01/src/pages/lotto/LottoDrawsPage.tsx:132`, `LottoTicketsPage.tsx:182` i `LottoWinningTicketsPage.tsx:126` przenieś funkcję `fetch…` do wnętrza `useEffect` albo owiń ją w `useCallback` z właściwymi zależnościami i dodaj ją do tablicy zależności. Następnie `npm run lint` powinien pokazać 0 ostrzeżeń. Opcjonalnie w CI: `npm run lint -- --max-warnings=0`.

### 4. Zaplanowana (nie natychmiastowa) aktualizacja majorów frontendu

**Impact**: `typescript` (5.9 → 7) i `eslint-plugin-react-hooks` (5 → 7) są 2 wersje główne za bieżącymi. `react-router` 8, `vite` 8 i `eslint` 10 — o jedną. Wersje są celowo przypięte w `CLAUDE.md`, więc agent ich nie podbije. Rośnie jednak rozjazd z dokumentacją i danymi treningowymi, a pojedyncza duża aktualizacja w przyszłości będzie kosztowniejsza. **Nie robić tego przed zakończeniem modułu Kursy.**
**Severity**: low
**Effort**: significant (> 1 hour)
**Fix**:

Osobna zmiana po wdrożeniu Kursów, po jednym majorze na raz, z weryfikacją `npm run lint && npm run build`:

```bash
cd src/client/app01
npm install -D eslint-plugin-react-hooks@7 eslint@10 @eslint/js@10 globals@17
npm install vite@8 @vitejs/plugin-react@6   # potem react-router@8, mermaid@12 — każde osobno
```

Po każdej aktualizacji zaktualizuj sekcję „Wersje przypięte” w `CLAUDE.md`. MediatR zostaje na 12.x (licencja).

### 5. Chunk JS > 500 kB w buildzie klienta

**Impact**: niewielki dla agenta. To ostrzeżenie Vite przy każdym buildzie, więc agent może je błędnie uznać za skutek swojej zmiany. Wpływa też na czas ładowania SPA (prawdopodobnie `mermaid` i `highlight.js` w głównym chunku).
**Severity**: low
**Effort**: moderate (15–30 min)
**Fix**:

Załaduj ciężkie strony leniwie w `src/main.tsx` (`const X = lazy(() => import("./pages/..."))` + `<Suspense>`), a `mermaid` importuj dynamicznie (`await import("mermaid")`) w komponencie, który go renderuje. Jeśli rozmiar jest akceptowalny, ustaw `build.chunkSizeWarningLimit` w `vite.config.ts` i dopisz do `CLAUDE.md`, że to ostrzeżenie jest znane.

### 6. `FolderProfile1.pubxml` wskazuje `net8.0`

**Impact**: publikacja tym profilem trafi do katalogu `net8.0`, co myli przy wdrożeniu. Profil nie jest śledzony w git, więc problem jest tylko lokalny.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**:

W `src/server/App01/App01.Bootstrapper.Api/Properties/PublishProfiles/FolderProfile1.pubxml` zmień `<PublishUrl>bin\Release\net8.0\publish\</PublishUrl>` na `<PublishUrl>bin\Release\net10.0\publish\</PublishUrl>`.

### Addressed in upcoming lessons (Category B)

### Dopracowanie `CLAUDE.md` / `AGENTS.md` i feedback loops

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: uporządkujesz istniejący, bogaty `CLAUDE.md` (i odsyłający do niego `AGENTS.md`) w architekturę pamięci agenta oraz dodasz pętle zwrotne (np. hooki uruchamiające format/lint/test po zmianie).

### Konfiguracja wdrożenia (brak Dockerfile/PaaS, tylko folder publish)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zastąpisz ręczne profile `FolderProfile*.pubxml` powtarzalnym wdrożeniem i dopniesz CI/CD o etap deploy.

## Summary

```
Health status: healthy
```

Projekt jest w dobrej kondycji do pracy z agentem. Zależności nie mają znanych podatności w obu ekosystemach i są przypięte lock-filami wymuszanymi przez CI. 343 testy endpointów przechodzą, a build .NET 10 daje 0 ostrzeżeń. Wszystkie kroki CI (format, lint, type-check, build, testy) przechodzą lokalnie. Luka „`CLAUDE.md` opisuje .NET 8” z assessmentu jest praktycznie zamknięta. Zostały drobiazgi: urwany fragment `.WithOpenApi()` w przepisie na slice, nieblokujący skan NuGet w CI, 3 ostrzeżenia ESLint i majory frontendu do zaplanowanej aktualizacji. Świadomym ograniczeniem pozostaje brak testów frontendu, skompensowany logiką po stronie serwera.

Next step: popraw linię 101 w `CLAUDE.md` i zaostrz audyt NuGet (łącznie ~5 min), a potem przejdź do agent onboarding (M1L4).
