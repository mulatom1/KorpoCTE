---
project: APPS (tomsoft1.pl — App01)
assessed_at: 2026-09-27T18:43:50Z
agent_readiness: ready-with-compensation
context_type: brownfield
stack_components:
  language: C# (.NET 10) + TypeScript 5.9
  framework: ASP.NET Core 10 Minimal APIs (modularny monolit, MediatR 12.5 + FluentValidation 12.1 + EF Core 10 / SQL Server) + React 19 SPA (React Router 7 deklaratywnie, Tailwind 4)
  build_tool: dotnet SDK 10 / MSBuild + Vite 7
  test_runner: xUnit 2.9.3 + WebApplicationFactory (serwer); frontend — brak
  package_manager: NuGet (lock files, --locked-mode) + npm (package-lock.json)
  ci_provider: GitHub Actions
  deployment_target: folder publish (FileSystem publish profile), SPA serwowane przez ASP.NET Core
gates_passed: 16
gates_failed: 0
---

## Stack Components

**Język (serwer) — C# na .NET 10.** Wszystkie 8 projektów w `APPS.sln` ma `<TargetFramework>net10.0</TargetFramework>` i `<Nullable>enable</Nullable>`. `global.json` przypina SDK `10.0.100` z `rollForward: latestMajor`. Migracja z .NET 8 nastąpiła w commicie `c8c50e9` („migrate to dotnet 10"), a kolejne commity (`27193ea`, `dab100f`, `626af8c`) usunęły przestarzałe API i pakiety.

**Język (klient) — TypeScript ~5.9.3.** `src/client/app01/tsconfig.app.json` ma `strict: true`, `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`, `verbatimModuleSyntax`.

**Framework (serwer) — ASP.NET Core 10 Minimal APIs, modularny monolit.** Host `App01.Bootstrapper.Api` składa moduły `Portal`, `Lotto`, `Flashcards`. Funkcje są cięte pionowo (`Features/<Nazwa>/{Contracts,Validator,Handler,Endpoint}.cs`) i obsługiwane przez MediatR 12.5.0 + FluentValidation 12.1.1. Dane: EF Core 10.0.12 (SQL Server, `UseCompatibilityLevel(110)`), testy na EF InMemory. OpenAPI przez Swashbuckle 10.2.3 (`AddServerSwagger()`, `UseSwagger()` warunkowo przez `Swagger:Enable`). Logowanie przez Serilog.AspNetCore 10.0.0.

**Framework (klient) — React 19 + React Router 7 (tryb deklaratywny) + Tailwind 4.** Renderowanie Markdown: `react-markdown` + `remark-gfm` + `rehype-highlight` + `rehype-raw` + `remark-frontmatter`. Poza tym `mermaid`, `hls.js`, `dayjs`, `react-datepicker`.

**Narzędzia budowania — dotnet SDK/MSBuild + Vite 7.** `Directory.Build.props` włącza `RestorePackagesWithLockFile`. Po stronie klienta `npm run build` = `tsc -b && vite build --mode dev`, plus warianty `build:prod1` i `build:prod2`.

**Testy — xUnit 2.9.3** z `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, Moq, `coverlet.collector`. Testy endpointów leżą w `tests/server/App01/App01.Api.Tests/Features/<Moduł>/<Funkcja>/`. Frontend nie ma testów.

**CI/CD — GitHub Actions** (`.github/workflows/pull-request.yml`, na każdy push/PR). Backend: `dotnet restore --locked-mode`, `dotnet list package --vulnerable --include-transitive`, `dotnet format --verify-no-changes`, build, testy z pokryciem i raportem HTML. Frontend: `npm ci`, `npm audit --audit-level=high`, `prettier --check`, `npm run lint`, `npm run build`.

**Wdrożenie** — profile `FolderProfile*.pubxml` (FileSystem). Brak Dockerfile i konfiguracji PaaS.

**Pliki instrukcji** — `CLAUDE.md` (bogaty: mapa repo, przepis na slice/moduł, migracje, bezpieczeństwo, frontend, wersje, checklista) oraz `AGENTS.md` (odsyła do `CLAUDE.md`). `.editorconfig` z regułami stylu .NET.

## Quality Gate Assessment

| Komponent | Typed | Convention | Training Data | Documented | Werdykt |
| --- | --- | --- | --- | --- | --- |
| C# (.NET 10) | ✓ | — | — | — | pass |
| TypeScript 5.9 | ✓ | — | — | — | pass |
| ASP.NET Core 10 Minimal APIs + MediatR + FluentValidation + EF Core | — | ~ | ✓ | ✓ | pass (z uwagą) |
| React 19 + React Router 7 (deklaratywnie) + Tailwind 4 | — | ~ | ✓ | ✓ | pass (z uwagą) |
| dotnet SDK / MSBuild | — | ✓ | ✓ | ✓ | pass |
| Vite 7 | — | ✓ | ✓ | ✓ | pass |
| xUnit 2.9 + WebApplicationFactory | — | — | ✓ | ✓ | pass |

Legenda: ✓ = pass, ✗ = fail, ~ = partial (pass-with-note: konwencje dostarcza `CLAUDE.md`, nie framework), — = nie dotyczy.

Wynik: 16 z 16 kryteriów spełnionych, w tym 2 częściowo (dzięki `CLAUDE.md`). Żadne kryterium nie jest oblane. Jest jednak jedna luka spoza kryteriów, istotna dla agenta: **`CLAUDE.md` rozjechał się ze stanem kodu po migracji na .NET 10** (szczegóły niżej).

### Gate Details

#### Typed

- C#: ✓ — `<Nullable>enable</Nullable>` we wszystkich 8 `.csproj` (np. `src/server/App01/App01.Shared.Abstractions/App01.Shared.Abstractions.csproj`). Kontrakty to `record`y w `Contracts.cs`.
- TypeScript: ✓ — `src/client/app01/tsconfig.app.json`: `"strict": true` i dodatkowe flagi lintujące. Kontrakty API to `interface`y w `src/services/contracts/`.
- Uwaga: między C# a TS nie ma generatora kontraktów. Granica API jest typowana po obu stronach, ale utrzymywana ręcznie. `CLAUDE.md` już to opisuje („zmiana rekordu C# wymaga aktualizacji interfejsu TS w tej samej zmianie").

#### Convention-based

- ASP.NET Core Minimal APIs: ~ — Minimal APIs z definicji nie narzucają układu katalogów, rejestracji tras ani obsługi błędów (w przeciwieństwie do MVC z kontrolerami). Konwencje projektu są jednak twarde i spisane w `CLAUDE.md`: sekcje „przepis na wycinek funkcji" i „przepis na nowy moduł", 4 pliki na slice, nazewnictwo tras `api/<moduł>/<kebab>`, `ModuleDI.UseModule<X>Endpoints()`. Wzorzec referencyjny `App01.Modules.Portal/Features/UserList/` istnieje i jest spójny z opisem (poza `.WithOpenApi()`, patrz Luki).
- React + Vite + React Router (deklaratywnie): ~ — Vite + React bez frameworka nie narzuca struktury, a tryb deklaratywny Routera nie ma routingu plikowego. Kompensację zapewnia `CLAUDE.md`, sekcja „Frontend": trasy w `src/main.tsx`, strony w `src/pages/<moduł>/<Nazwa>Page.tsx`, serwisy `Api<Moduł>Service` + `apiFetch`, kontrakty w `src/services/contracts/`.
- dotnet SDK / Vite: ✓ — oba mają ustandaryzowany układ projektu (`.csproj`/`.sln`, `vite.config.ts`).

#### Popular in training data (liczone w obrębie rodziny języka)

- ASP.NET Core: ✓ — główny framework webowy w C#. Minimal APIs istnieją od .NET 6 i są dobrze reprezentowane. MediatR + FluentValidation + EF Core to kanoniczny zestaw „vertical slice" w .NET.
- React 19 / React Router 7 / Tailwind 4 / Vite 7: ✓ — mainstream w JS/TS. Uwaga: React Router 7 ma dwa tryby (deklaratywny i framework), a Tailwind 4 zmienił konfigurację na CSS-first. Agent z przyzwyczajenia może sięgać po `react-router-dom`, `createBrowserRouter` albo `tailwind.config.js`. `CLAUDE.md` już temu zapobiega.
- xUnit: ✓ — domyślny wybór w ekosystemie .NET.
- Ryzyko specyficzne dla wersji: .NET 10 i EF Core 10 są w danych treningowych słabiej reprezentowane niż .NET 8. Agent może proponować API oznaczone w .NET 10 jako przestarzałe (np. `.WithOpenApi()` — ASPDEPR002). Tym bardziej potrzebne jest poprawne przypięcie wersji w `CLAUDE.md`.

#### Well-documented

- ASP.NET Core / EF Core: ✓ — learn.microsoft.com, wersjonowane (`?view=aspnetcore-10.0`).
- MediatR: ✓ — wiki na GitHubie, stabilne API 12.x. FluentValidation: ✓ — docs.fluentvalidation.net.
- React / React Router (sekcja „Declarative Mode") / Tailwind v4 / Vite: ✓ — oficjalne, wersjonowane dokumentacje.
- xUnit: ✓ — xunit.net, osobne sekcje dla v2 i v3.

## Gaps & Compensation

### Luka 1 (najważniejsza): `CLAUDE.md` opisuje .NET 8, a kod jest na .NET 10

**Co jest nie tak (dowody):**
- `CLAUDE.md` (sekcja „Wersje przypięte"): „.NET 8 / ASP.NET Core 8 / EF Core 8 · … · Serilog.AspNetCore 9". Tymczasem `App01.Shared.Abstractions.csproj` ma `Microsoft.EntityFrameworkCore` 10.0.12, `Serilog.AspNetCore` 10.0.0, a `global.json` przypina SDK 10.0.100.
- `CLAUDE.md` (sekcja „mapa repozytorium"): „modularny monolit ASP.NET Core 8".
- `CLAUDE.md` (sekcja „przepis na nowy moduł", krok 1): „net8.0". Nowy moduł `Courses` utworzony według tego przepisu dostanie `net8.0` i nie zbuduje się z referencją do projektów `net10.0`.
- `CLAUDE.md` (sekcja „przepis na wycinek funkcji", krok 4): łańcuch ma kończyć się na `.WithOpenApi()`. Kod po commicie `27193ea` nie ma już ani jednego wywołania `WithOpenApi` (0 plików w `src/server`), bo w .NET 10 jest ono przestarzałe. Agent trzymający się `CLAUDE.md` przywróci przestarzałe API.
- `CLAUDE.md` (Dokumentacja): „learn.microsoft.com/aspnet/core (8.0)".
- Poza `CLAUDE.md`: oba pliki `Properties/PublishProfiles/FolderProfile*.pubxml` wciąż mają `PublishUrl` = `bin\Release\net8.0\publish\`.

**Dlaczego to ważne dla agenta:** pliki instrukcji mają wyższy priorytet niż to, co agent wyczyta z kodu. Nieaktualna reguła jest gorsza niż jej brak. Agent zbuduje moduł `Courses` na złym TFM, dopisze przestarzałe `.WithOpenApi()` i poszuka odpowiedzi w dokumentacji .NET 8.

**Kompensacja:** zaktualizować wskazane fragmenty `CLAUDE.md` (gotowe bloki niżej) i poprawić `PublishUrl` w profilach publikacji.

### Luka 2: Minimal APIs i Vite + React nie niosą konwencji same z siebie

Oba kryteria są spełnione częściowo, bo konwencje żyją w `CLAUDE.md`, a nie we frameworku. Obecne pokrycie jest dobre. Warto je uzupełnić o dwie reguły, które agent łamie najczęściej, a CI je wyłapie: formatowanie (`dotnet format --verify-no-changes`, `prettier --check`) i kolejność/grupowanie `using` (`.editorconfig`: `dotnet_separate_import_directive_groups = true`).

### Luka 3 (poza kryteriami): brak testów frontendu

Stack tego nie wymaga, a `CLAUDE.md` już to kompensuje (logika domenowa po stronie serwera). Uwaga dla `/10x-health-check`: weryfikacja UI modułu Kursy (FR-001, FR-002, hangar) będzie wyłącznie manualna.

### Recommended Instruction File Additions

**1. Zamiennik sekcji „Wersje przypięte" w `CLAUDE.md`:**

```markdown
## Wersje przypięte — nie podbijaj majorów bez polecenia

.NET 10 (SDK przypięty w `global.json`: 10.0.100) / ASP.NET Core 10 / EF Core 10 · MediatR 12.x (v13+ ma licencję komercyjną) · FluentValidation 12 · xUnit 2.x (nie v3) · Serilog.AspNetCore 10 · Swashbuckle.AspNetCore 10 · React 19 · React Router 7 · Tailwind CSS 4 · Vite 7 · TypeScript 5.9.
Dokumentacja: learn.microsoft.com/aspnet/core (wersja 10.0 — `?view=aspnetcore-10.0`), learn.microsoft.com/ef/core (EF Core 10), docs.fluentvalidation.net, reactrouter.com (sekcja „Declarative Mode"), tailwindcss.com/docs (v4), vite.dev.
- Projekt jest po migracji z .NET 8. Nie używaj API oznaczonych w .NET 10 jako przestarzałe (m.in. `.WithOpenApi()` na endpointach — ASPDEPR002). Ostrzeżenia `obsolete` przy buildzie traktuj jak błąd do naprawy, nie do wyciszenia.
- Pakiety NuGet są przypięte lock-filami (`RestorePackagesWithLockFile` w `Directory.Build.props`, CI robi `dotnet restore --locked-mode`). Po dodaniu lub zmianie pakietu zacommituj zaktualizowane `packages.lock.json`.
```

**2. Poprawki punktowe w innych sekcjach `CLAUDE.md`:**

```markdown
- Mapa repozytorium: „modularny monolit ASP.NET Core 10 (Minimal APIs)".
- Przepis na nowy moduł, krok 1: „(kopia `App01.Modules.Portal.csproj`: net10.0, Nullable, ImplicitUsings, `ProjectReference` do `App01.Shared.Infrastructure`)".
- Przepis na wycinek funkcji, krok 4 — łańcuch: `.WithName("<Moduł><NazwaFunkcji>")`, `.WithTags("<Moduł>")`, `.Produces<Contracts.Response>(StatusCodes.Status200OK)` + `.Produces(StatusCodes.Status4xx…)` dla kodów błędów, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()` (pominąć tylko dla endpointów publicznych). NIE dodawaj `.WithOpenApi()` — przestarzałe w .NET 10; metadane OpenAPI zbiera Swashbuckle z `WithName`/`WithTags`/`Produces`.
```

**3. Nowy blok — formatowanie pilnowane przez CI:**

```markdown
## Formatowanie (CI odrzuca niesformatowany kod)

- Serwer: przed zakończeniem zadania uruchom `dotnet format APPS.sln`. CI wykonuje `dotnet format --verify-no-changes`. Styl wynika z `.editorconfig`: 4 spacje w `.cs`, `using System*` pierwsze, grupy `using` rozdzielone pustą linią (`dotnet_separate_import_directive_groups = true`), namespace file-scoped zgodny z katalogiem.
- Klient (z `src/client/app01`): `npm run format` (prettier). CI wykonuje `npx prettier --check "src/**/*.{ts,tsx,css}"` oraz `npm audit --audit-level=high`.
- Nie wyłączaj reguł `.editorconfig` ani ESLint, żeby przepchnąć zmianę.
```

**4. Uzupełnienie checklisty „Weryfikacja przed zakończeniem zadania":**

```markdown
- Pełna lista kroków CI (`.github/workflows/pull-request.yml`) do odtworzenia lokalnie: `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, `dotnet build APPS.sln`, `dotnet test APPS.sln`; w `src/client/app01`: `npx prettier --check "src/**/*.{ts,tsx,css}"`, `npm run lint`, `npm run build`.
```

**Poza plikami instrukcji (do zrobienia ręcznie):** w `src/server/App01/App01.Bootstrapper.Api/Properties/PublishProfiles/FolderProfile.pubxml` i `FolderProfile1.pubxml` zmienić `bin\Release\net8.0\publish\` na `bin\Release\net10.0\publish\`.

## Summary

**Ogólna gotowość: ready-with-compensation.** Wszystkie 16 kryteriów jest spełnionych, 2 z nich częściowo dzięki `CLAUDE.md`.

**Mocne strony:**
- Typowanie end-to-end: C# z nullable i TypeScript strict.
- Mainstreamowe, dobrze udokumentowane technologie w obu ekosystemach.
- Bardzo szczegółowy `CLAUDE.md` z przepisami krok po kroku, wzorcem referencyjnym i checklistą.
- Testy endpointów na `WebApplicationFactory`.
- Od niedawna pełne CI w GitHub Actions: locked restore, skan podatności, format, lint, build, testy z pokryciem.

**Kluczowe luki:**
1. `CLAUDE.md` nie nadąża za migracją na .NET 10: zły TFM w przepisie na moduł, przestarzałe `.WithOpenApi()` w przepisie na slice, dokumentacja 8.0, złe wersje EF/Serilog. To trzeba naprawić **przed** rozpoczęciem pracy agenta nad modułem Kursy, bo przepis na nowy moduł zostanie użyty dosłownie.
2. Konwencje Minimal APIs i Vite + React żyją wyłącznie w `CLAUDE.md` — trzeba je utrzymywać przy każdej zmianie wzorca.
3. Brak testów frontendu (skompensowany regułą „logika domenowa po stronie serwera").

**Następny krok:** `/10x-health-check` — audyt zależności (NuGet po skoku na .NET 10, npm), skan bezpieczeństwa (m.in. pliki `.env*` w `src/client/app01`) i weryfikacja, czy CI przechodzi na zielono.
