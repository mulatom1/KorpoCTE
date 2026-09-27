<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit — Module 1, Lesson 3

Scaffold the project for the stack you picked in Lesson 2, with the **bootstrap chain**:

```
(/10x-init  →  /10x-shape  →  /10x-prd)  →  /10x-tech-stack-selector  →  /10x-bootstrapper
```

The PRD chain ships from Lesson 1 and the tech-stack-selector ships from Lesson 2 — both re-included in this lesson so you can fix the PRD or swap the stack mid-flight. `/10x-bootstrapper` is the lesson's main topic. The chain ends here in v1; a future Lesson 4 will set up agent context (`CLAUDE.md`, `AGENTS.md`).

### Task Router — Where to start

| Skill | Use it when |
| --- | --- |
| **Bootstrap (lesson focus)** | |
| `/10x-bootstrapper` | You have a hand-off at `context/foundation/tech-stack.md` (written by `/10x-tech-stack-selector`) and you are ready to scaffold the project into the current directory. The skill reads the hand-off, looks up the chosen card in the starter registry, runs its CLI through one of three cwd strategies (scaffold into a temp directory then move files up; scaffold directly into the current directory; clone a starter repo without keeping its git history), preserves `context/` always, sidelines other clashes as `.scaffold` siblings, runs a light pre-scaffold recency check and a deeper post-scaffold audit, and writes a verification log to `context/changes/bootstrap-verification/verification.md`. Use AFTER `/10x-tech-stack-selector`. |
| **Re-run upstream if needed** | |
| `/10x-init` / `/10x-shape` / `/10x-prd` / `/10x-tech-stack-selector` | Bundled so you can fix the PRD or swap the stack mid-flight. If `/10x-bootstrapper` surfaces a registry-drift refusal or you change your mind on the starter, re-run `/10x-tech-stack-selector` to regenerate `tech-stack.md` and re-invoke. |

### How the chain hands off

- `/10x-tech-stack-selector` (Lesson 2) writes `context/foundation/tech-stack.md` with a 4-key frontmatter (`starter_id`, `package_manager`, `project_name`, `hints`) plus a one-paragraph `## Why this stack` body.
- `/10x-bootstrapper` reads that file FULLY (no fallback to conversation history). If it is absent, the skill refuses with a one-sentence redirect to `/10x-tech-stack-selector` and stops — no inline mini-handoff, no standalone-mode in v1.
- The chosen `starter_id` is looked up in `/skills/10x-tech-stack-selector/references/starter-registry.yaml`. The skill consumes that registry; it does not own it. A CI validator (`scripts/validate-starter-registry-sync.mjs`) prevents bootstrapper from referencing a `starter_id` absent from the registry.
- The skill writes `context/changes/bootstrap-verification/verification.md` as the audit-trail log for the run. Schema in `/skills/10x-bootstrapper/references/verification-log-schema.md`.

### What bootstrapper captures (and what it does NOT)

- **Captured (v1)**: scaffold via the chosen card's `cmd_template` (CLI delegation, not inline file generation), three cwd strategies dispatched from `bootstrapper-config.yaml` (`subdir-then-move`, `native-cwd`, `git-clone`), strict conflict policy producing `.scaffold` siblings + always preserving `context/`, two verification slots (light pre-scaffold recency check + deep post-scaffold language-aware audit), severity-tiered audit summary, full verification log on disk.
- **NOT captured in v1 (deliberate)**: `AGENTS.md` / `CLAUDE.md` generation (deferred to a future Lesson 4 — "Memory Architecture"); per-starter cert-element placement overlays (live with the future agent-context skill, not here); CI workflow files; AI-as-bridge fallback for stacks outside the registry (deferred to v2 — in v1 chain-mode tech-stack-selector already gates on the registry, so the case cannot arise); standalone-mode where the user names a stack inline without a hand-off (deferred to v2); compensation actions for `bootstrapper_confidence: best-effort` or `quality_override: true` (surfaced in conversation but no automated follow-up — that, too, is the future memory-architecture skill's job).

### The conflict policy

When the skill moves files from a temp scaffold directory up into your current working directory, it applies a strict matrix:

- **`context/**`** — anything the scaffold tried to write under `context/` is **dropped**. Your `context/` is the source of truth for the bootstrap chain (PRD, tech-stack hand-off, plans, frames) and is never overwritten.
- **`.gitignore`** — append-merged: your existing lines stay in order, then the scaffold's lines are de-duped against your set and appended with a separator comment. Git's ignore semantics are additive, so combining is safe.
- **`package.json`, `README.md`, `CLAUDE.md`, `AGENTS.md`, root-level `*.md`** — your existing file wins; the scaffold's copy lands as `<filename>.scaffold` sibling. You can `diff README.md README.md.scaffold` to see what the starter shipped vs what you had.
- **Anything else** — moves silently if no conflict, sidelined as `<filename>.scaffold` if there is one. The matrix never deletes user files.

For the `git-clone` strategy (10x-astro-starter and similar): the cloned `.git/` is deleted before move-up, so the upstream starter's history does not leak into your repo. You initialise your own history afterwards (`git init`).

### Verification log

Every run writes `context/changes/bootstrap-verification/verification.md`. Sections:

- **`## Hand-off`** — verbatim copy of the tech-stack.md frontmatter and `## Why this stack` body.
- **`## Pre-scaffold verification`** — recency findings table (npm package version + `time.modified` for JS starters; GitHub `pushed_at` for any starter with a GitHub `docs_url`).
- **`## Scaffold log`** — the resolved CLI invocation, exit code, files moved, conflicts surfaced as `.scaffold` siblings, `.gitignore` handling.
- **`## Post-scaffold audit`** — full per-language audit output (`npm audit --json` for JS, `pip-audit` for Python, `cargo audit` for Rust, etc.). Severity-tiered: CRITICAL and HIGH surfaced inline in chat, MODERATE and LOW log-only. Direct-vs-transitive split where the tool supports it.
- **`## Hints recorded but not acted on`** — every hint from the hand-off bootstrapper read but did not act on in v1. Audit-trail completeness for the future memory-architecture skill.
- **`## Next steps`** — pointer text. v1 names "your project is scaffolded and verified — happy hacking" and flags the future Lesson 4 skill as the next chain link.

The folder (`context/changes/bootstrap-verification/`) deliberately has no `change.md`. Bootstrap runs are one-shot artifacts, not tracked workflow changes — the folder hosts the log and nothing else. Re-runs apply a warn-and-confirm guard before overwriting; the escape hatch is `verification-v2.md` (and so on).

### Foundation paths used by this lesson

- `context/foundation/tech-stack.md` — input (from Lesson 2)
- `context/changes/bootstrap-verification/verification.md` — output (the audit-trail log)
- `context/foundation/lessons.md` — recurring rules & pitfalls
- `docs/reference/contract-surfaces.md` — load-bearing names registry

### Universal language

The shipped skill carries no 10xDevs / cohort / certification references. The post-scaffold audit dispatches by `language_family` against a small lookup table; cohorts whose stack lands in `java`, `php`, `dart`, or a multi-language combination see a "no built-in audit tool for this ecosystem" log line and a recommended external tool, not a fake "0 findings" record.

Skills must not write to `context/archive/`. Archived changes are immutable; if a resolved target path starts with `context/archive/`, abort with: "This change is archived. Open a new change with `/10x-new` instead."

<!-- END @przeprogramowani/10x-cli -->

## Projekt App01 — mapa repozytorium

- `APPS.sln` — solucja. Serwer: `src/server/App01/`. Klient: `src/client/app01/`. Testy: `tests/server/App01/App01.Api.Tests/`.
- Architektura: **modularny monolit** ASP.NET Core 10 (Minimal APIs). Jeden host `App01.Bootstrapper.Api` składa moduły:
  - `App01.Modules.Portal` — użytkownicy, logowanie, poczta z formularza kontaktowego, wersja API
  - `App01.Modules.Lotto` — losowania, kupony, statystyki, workery
  - `App01.Modules.Flashcards` — generowanie fiszek (OpenRouter)
- Warstwy współdzielone (kierunek zależności: Modules → Shared.Infrastructure → Shared.Application → Shared.Abstractions):
  - `App01.Shared.Abstractions` — tylko pakiety NuGet + `FrameworkReference Microsoft.AspNetCore.App`. Nowe pakiety NuGet dodawaj TUTAJ.
  - `App01.Shared.Application` — encje (`Entities/<Moduł>/`), wyjątki (`ApiException`, `ForbiddenException`, `NotFoundException`), `Filters/XTokenFilter`, interfejsy serwisów, `Middlewares/ExceptionHandlingMiddleware`.
  - `App01.Shared.Infrastructure` — `Repositories/AppDbContext.cs`, `Repositories/Configurations/<Moduł>/`, `Migrations/`, implementacje serwisów (`JwtService`, `XTokenService`, `OpenRouterService`, `CacheDataService`).
- Moduły NIE referencjonują się nawzajem. Wspólne rzeczy idą do `Shared.*`.

## Serwer — przepis na wycinek funkcji (vertical slice)

Każda funkcja to katalog `App01.Modules.<Moduł>/Features/<NazwaFunkcji>/` z DOKŁADNIE czterema plikami. Namespace: `App01.Modules.<Moduł>.Features.<NazwaFunkcji>`. Wzorzec referencyjny: `App01.Modules.Portal/Features/UserList/`.

1. `Contracts.cs` — `public class Contracts` z zagnieżdżonymi rekordami:
   `public record Request(...) : IRequest<Response>;`, `public record Response(...);` oraz DTO jako rekordy.
2. `Validator.cs` — `public class Validator : AbstractValidator<Contracts.Request>` (FluentValidation, komunikaty `.WithMessage(...)`).
3. `Handler.cs` — `public class <NazwaFunkcji>Handler : IRequestHandler<Contracts.Request, Contracts.Response>`.
   - Konstruktor wstrzykuje `ILogger<...>`, `IValidator<Contracts.Request>`, `AppDbContext` i potrzebne serwisy (pola `_camelCase`).
   - PIERWSZY krok `Handle`: `var validationResult = await _validator.ValidateAsync(request, cancellationToken); if (!validationResult.IsValid) throw new ValidationException(validationResult.Errors);` — walidacja NIE jest wpięta w pipeline MediatR, trzeba ją wywołać ręcznie.
   - Uprawnienia admina: `if (!await _jwtService.GetIsAdminFromJwt()) throw new ForbiddenException("...");`
   - Błędy zgłaszaj wyjątkami (`ValidationException` → 400, `ForbiddenException` → 403, `NotFoundException` → 404, `ApiException`); mapuje je `ExceptionHandlingMiddleware`. Nie zwracaj `Results.BadRequest` z handlera.
   - Zapytania EF zawsze z `cancellationToken`; projekcja do DTO przez `.Select(...)`.
4. `Endpoint.cs` — `public static class Endpoint { public static void AddEndpoint(this WebApplication app) { ... } }`:
   - Trasa: `api/<moduł-lowercase>/<nazwa-kebab-case>` (np. `api/portal/user-list`), `app.MapGet/MapPost(...)` z `IMediator mediator` → `mediator.Send(request)` → `Results.Ok(result)`.
   - łańcuch: `.WithName("<Moduł><NazwaFunkcji>")`, `.WithTags("<Moduł>")`, `.Produces<Contracts.Response>(StatusCodes.Status200OK)` + `.Produces(StatusCodes.Status4xx…)` dla kodów błędów, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()` (pominąć tylko dla endpointów publicznych). NIE dodawaj `.WithOpenApi()` — przestarzałe w .NET 10; metadane OpenAPI zbiera Swashbuckle z `WithName`/`WithTags`/`Produces`.
5. **Rejestracja (łatwo zapomnieć):** dopisz `Features.<NazwaFunkcji>.Endpoint.AddEndpoint(app);` w `ModuleDI.UseModule<Moduł>Endpoints()`. Niezarejestrowany endpoint NIE zwraca 404 — trafia w `MapFallbackToFile("index.html")` i zwraca HTML SPA.
6. Test: `tests/server/App01/App01.Api.Tests/Features/<Moduł>/<NazwaFunkcji>/EndpointTests.cs` (wzorzec: `Features/Portal/UserList/EndpointTests.cs`).

## Serwer — przepis na nowy moduł (np. Courses)

1. Utwórz `src/server/App01/App01.Modules.<Moduł>/App01.Modules.<Moduł>.csproj` (kopia `App01.Modules.Portal.csproj`: net10.0, Nullable, ImplicitUsings, `ProjectReference` do `App01.Shared.Infrastructure`).
2. `dotnet sln APPS.sln add src/server/App01/App01.Modules.<Moduł>/App01.Modules.<Moduł>.csproj --solution-folder src/server/App01`
3. `ModuleDI.cs` w namespace `App01.Modules.<Moduł>` z metodami: `AddModule<Moduł>Services(this IServiceCollection)` (MediatR + validators z `Assembly.GetExecutingAssembly()`), `UseModule<Moduł>Endpoints(this WebApplication)`, opcjonalnie `AddModule<Moduł>Workers`.
4. `App01.Bootstrapper.Api.csproj` — dodaj `ProjectReference`; `Program.cs` — `using App01.Modules.<Moduł>;`, `builder.Services.AddModule<Moduł>Services();` obok pozostałych, `app.UseModule<Moduł>Endpoints();` PRZED `app.MapFallbackToFile(...)`; workery tylko w bloku `if (!isTestEnvironment)`.
5. Projekt testów `App01.Bootstrapper.Api.Tests.csproj` — dodaj `ProjectReference` do nowego modułu.
6. Nie zmieniaj istniejących modułów, tras ani `ExceptionHandlingMiddleware` — PRD wymaga, by Portal/Lotto/Flashcards działały bez zmian.

## Dane i migracje (EF Core 10, SQL Server 2012)

- Encje: `App01.Shared.Application/Entities/<Moduł>/<Encja>.cs` (NIE w projekcie modułu).
- Konfiguracja: `App01.Shared.Infrastructure/Repositories/Configurations/<Moduł>/<Encja>Configuration.cs` (`IEntityTypeConfiguration<T>`).
- `DbSet<T>` dopisz w `AppDbContext.cs` w sekcji danego modułu, z `= null!;`.
- Migracja (z katalogu repo):
  `dotnet ef migrations add <Moduł><Opis> --project src/server/App01/App01.Shared.Infrastructure --startup-project src/server/App01/App01.Bootstrapper.Api`
  Nazwa migracji z prefiksem modułu (np. `CoursesInitial`). Nigdy nie edytuj istniejących migracji.
- `UseCompatibilityLevel(110)` w `SharedInfrastructureDI.cs` jest celowe (docelowy SQL Server) — nie zmieniaj; przez to EF nie używa `OPENJSON` dla `Contains` na kolekcjach.
- Unikalność (np. „flaga zdobyta raz na użytkownika") wymuszaj indeksem unikalnym w konfiguracji EF, nie tylko sprawdzeniem w handlerze.
- Testy używają EF InMemory — nie polegaj w logice na funkcjach specyficznych dla SQL Server (surowy SQL, `FromSql`), bo testy ich nie wykryją.

## Bezpieczeństwo endpointów

- Każdy endpoint wymaga nagłówka aplikacyjnego `X-TOKEN` przez `.AddEndpointFilter<XTokenFilter>()` (wyjątki istniejące: `GetApiVersion`, `FileEdit01`, `TransformNumbers` — nie powielaj ich bez powodu).
- Endpointy dla zalogowanych: `.RequireAuthorization()` (JWT Bearer). Endpointy publiczne (np. lista kafelków kursów) — bez `RequireAuthorization()`, ale z `XTokenFilter`, i zwracają WYŁĄCZNIE pola publiczne (DTO bez treści kursu).
- Rola admina = claim `isAdmin` w JWT; sprawdzenie w handlerze przez `IJwtService.GetIsAdminFromJwt()`. Nie dodawaj nowych ról ani policy.
- Id bieżącego użytkownika pobieraj z JWT przez `IJwtService`, nigdy z body requestu.
- Nie wpisuj sekretów (klucze JWT, ApiKey, hasła w connection stringach) do `appsettings.json` ani `src/client/app01/.env*` — lokalnie `appsettings.Development.json` (ignorowany) lub `dotnet user-secrets`; zmienne `VITE_*` trafiają do bundla przeglądarki i nigdy nie są sekretne.
- `appsettings.json` i `appsettings.*.json` są ignorowane przez git (`src/server/.gitignore`); jedynym śledzonym plikiem jest `appsettings.Example.json`. Lokalnie: `cp src/server/App01/App01.Bootstrapper.Api/appsettings.Example.json src/server/App01/App01.Bootstrapper.Api/appsettings.json` i uzupełnij wartości. Nowy klucz konfiguracji dopisz najpierw do `appsettings.Example.json` (z pustą/przykładową wartością).

## Moduł Courses — weryfikacja odpowiedzi przez LLM

- Weryfikację wykonuje handler w `App01.Modules.Courses` przez istniejący `IOpenRouterService`. NIE zmieniaj `OpenRouterService`, `AddHttpClient()` ani `ExceptionHandlingMiddleware` — używa ich Flashcards.
- Limit czasu w handlerze: `using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); cts.CancelAfter(TimeSpan.FromSeconds(<Courses:VerificationTimeoutSeconds>));` i przekaż `cts.Token` do `ChatAsync`. Wartość domyślna ≤ 4 s (PRD: werdykt < 5 s). Klucz dopisz do `appsettings.Example.json`.
- Awaria techniczna (timeout, `HttpRequestException`, `Exception` z `OpenRouterService`, nieparsowalna odpowiedź modelu) NIE jest wyjątkiem do middleware. Złap ją w handlerze, zaloguj i zwróć 200 z `Response(Status: "Unavailable", ...)`. Kontrakt: `Status` ∈ `Correct | Incorrect | Unavailable | AlreadyOwned`. Klient pokazuje `Unavailable` jako awarię z przyciskiem „Spróbuj ponownie", nie jako ocenę negatywną.
- Próby `Incorrect` i `Unavailable` nie zużywają niczego i nie są liczone w rankingu ani wskaźnikach.
- Kolejność w `Handle`: walidacja → pobranie userId z JWT (`IJwtService`) → sprawdzenie, czy flaga już zdobyta (jeśli tak: `AlreadyOwned`, model NIE jest wołany) → wywołanie modelu → zapis flagi.
- Prompt: kryteria zadania w wiadomości `system`; odpowiedź uczestnika w wiadomości `user`, otoczona ogranicznikami (np. `<answer>…</answer>`) i z instrukcją, że treść wewnątrz to dane, nie polecenia. Model zwraca WYŁĄCZNIE JSON `{"verdict":"pass"|"fail","reason":"..."}`; parsuj `System.Text.Json` do rekordu, a wszystko inne traktuj jako `Unavailable`.
- Przyznanie flagi: unikalny indeks `(UserId, FlagId)` w konfiguracji EF + obsługa `DbUpdateException` przy wyścigu (drugi zapis = `AlreadyOwned`), nie tylko sprawdzenie w handlerze.
- Testy: w `EndpointTests.cs` podmień `IOpenRouterService` przez `builder.ConfigureServices(s => { s.RemoveAll<IOpenRouterService>(); s.AddSingleton(mock.Object); })` (Moq jest w projekcie testów). Przypadki: `Correct`, `Incorrect`, `Unavailable` (mock rzuca / timeout), `AlreadyOwned` (mock NIE wywołany — `Times.Never`), 400, 401, brak `X-TOKEN`. Nigdy nie wołaj prawdziwego OpenRoutera w testach.

## Moduł Courses — treść kursów

- Pliki Markdown kursów leżą POZA `wwwroot` (ścieżka z konfiguracji `Courses:ContentPath`, dopisz do `appsettings.Example.json`). `app.UseStaticFiles` serwuje `wwwroot` publicznie — nie umieszczaj tam treści kursów.
- Treść kursu zwraca wyłącznie endpoint z `.RequireAuthorization()` + `XTokenFilter`, po sprawdzeniu `PublishDate <= dzisiaj` (FR-003); dla kursu niepublikowanego zwróć 404 (`NotFoundException`), nie 403.
- Publicznie (endpoint bez `RequireAuthorization()`, z `XTokenFilter`) tylko lista kafelków: tytuł, krótki opis, tagi, URL grafiki. Grafiki kafelków mogą leżeć w `wwwroot`.
- Ścieżkę pliku buduj z identyfikatora z bazy, nigdy z parametru requestu; odrzuć `..` i separatory (path traversal).
- Brak pliku lub błąd parsowania frontmattera → zaloguj ostrzeżenie i pomiń kurs na liście; nie zwracaj 500 (FR-010: literówka w katalogu nie może wywalić modułu).
- Klient renderuje treść istniejącym zestawem `react-markdown` + `remark-gfm` + `rehype-highlight` (+ `remark-frontmatter`); nie dodawaj innego parsera.

## Moduł Courses — rejestracja w testach

- Dodaj `ProjectReference` do `App01.Modules.Courses` w `App01.Bootstrapper.Api.Tests.csproj` (projekt testów obecnie referencjonuje Portal i Lotto, ale nie Flashcards — nie powielaj tej luki).

## Frontend (src/client/app01)

- React 19 + TypeScript strict + Vite 7 + Tailwind 4 + React Router 7 w trybie **deklaratywnym**.
- Routing: wszystkie trasy w `src/main.tsx` (`<BrowserRouter>/<Routes>/<Route>`). Importy z `"react-router"` (NIE `react-router-dom`). NIE używaj trybu framework/data (`createBrowserRouter`, loaderów, `@react-router/dev`, `routes.ts`).
- Trasy dla zalogowanych umieszczaj wewnątrz `<Route element={<RequireAuth />}>`. Widoczność elementów tylko dla admina: `getIsAdminFromToken()` z `src/utils/jwt.ts`.
- Strony: `src/pages/<moduł>/<Nazwa>Page.tsx` (default export). Wspólne komponenty: `src/components/` (używaj istniejących `Card`, `ButtonPrimary`, `FormCard`, `ConfirmModal`, `Layout`, `SubMenu` zamiast nowych).
- Menu główne i layout: `src/components/Layout.tsx` — zmiana dotyka każdej strony; dodawaj tylko nową pozycję, nie przestawiaj istniejących.
- API: klasa `Api<Moduł>Service` w `src/services/api-<moduł>-service.ts` (wzorzec: `api-portal-service.ts`), wywołania WYŁĄCZNIE przez `apiFetch` z `src/services/api-fetch.ts` (obsługa 401/wygaśnięcia sesji), nagłówki `X-TOKEN` + `Authorization: Bearer`.
- Kontrakty: `src/services/contracts/<moduł>-<funkcja-kebab>-request.ts` / `-response.ts`, `export interface`, pola camelCase odpowiadające 1:1 rekordom `Contracts` w C#. Nie ma generatora — **zmiana rekordu C# wymaga aktualizacji interfejsu TS w tej samej zmianie.**
- Tailwind 4: konfiguracja CSS-first (`@import "tailwindcss";` w `src/index.css`, plugin `@tailwindcss/vite`). NIE twórz `tailwind.config.js` ani `postcss.config.js`; własne tokeny przez `@theme` w CSS.
- Markdown: `react-markdown` + `remark-gfm` + `rehype-highlight` (+ `rehype-raw`, `remark-frontmatter`) — używaj tych pakietów, nie dodawaj innego parsera.
- Teksty UI i komentarze po polsku (zgodnie z istniejącym kodem).

## Wersje przypięte — nie podbijaj majorów bez polecenia

.NET 10 (SDK przypięty w `global.json`: 10.0.100) / ASP.NET Core 10 / EF Core 10 · MediatR 12.x (v13+ ma licencję komercyjną) · FluentValidation 12 · xUnit 2.x (nie v3) · Serilog.AspNetCore 10 · Swashbuckle.AspNetCore 10 · React 19 · React Router 7 · Tailwind CSS 4 · Vite 7 · TypeScript 5.9 · Vitest 5.
Dokumentacja: learn.microsoft.com/aspnet/core (wersja 10.0 — `?view=aspnetcore-10.0`), learn.microsoft.com/ef/core (EF Core 10), docs.fluentvalidation.net, reactrouter.com (sekcja „Declarative Mode"), tailwindcss.com/docs (v4), vite.dev.
- Projekt jest po migracji z .NET 8 do .NET 10. Nie używaj API oznaczonych w .NET 10 jako przestarzałe (m.in. `.WithOpenApi()` na endpointach — ASPDEPR002). Ostrzeżenia `obsolete` przy buildzie traktuj jak błąd do naprawy, nie do wyciszenia.
- Pakiety NuGet są przypięte lock-filami (`RestorePackagesWithLockFile` w `Directory.Build.props`, CI robi `dotnet restore --locked-mode`). Po dodaniu lub zmianie pakietu zacommituj zaktualizowane `packages.lock.json`.

## Weryfikacja przed zakończeniem zadania

- Serwer: `dotnet build APPS.sln` i `dotnet test APPS.sln` — muszą przejść.
- Klient (z `src/client/app01`): `npm run build` (zawiera `tsc -b`), `npm run lint` i `npm test` (Vitest + jsdom + Testing Library, konfiguracja w bloku `test` w `vite.config.ts`, setup w `src/test/setup.ts`). Testy frontendu leżą obok testowanego pliku jako `<Nazwa>.test.ts(x)`, importy (`describe`/`it`/`expect`/`vi`) jawnie z `"vitest"` (bez globals). Testuj helpery z `src/utils/` i zachowanie komponentów (kliknięcia, warunkowe renderowanie), nie klasy Tailwind. Logikę domenową (weryfikacja odpowiedzi, przyznawanie flag, ranking, wskaźniki) nadal trzymaj po stronie serwera, gdzie jest pokryta testami endpointów.
- Nowy endpoint = nowy `EndpointTests.cs` z przypadkiem: sukces, 400 (walidacja), 401 (brak JWT), 403 (gdy dotyczy), brak `X-TOKEN`.
- Pełna lista kroków CI (`.github/workflows/pull-request.yml`) do odtworzenia lokalnie: `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, `dotnet build APPS.sln`, `dotnet test APPS.sln`; w `src/client/app01`: `npx prettier --check "src/**/*.{ts,tsx,css}"`, `npm run lint`, `npm test`, `npm run build`.

## Formatowanie (CI odrzuca niesformatowany kod)

- Serwer: przed zakończeniem zadania uruchom `dotnet format APPS.sln`. CI wykonuje `dotnet format --verify-no-changes`. Styl wynika z `.editorconfig`: 4 spacje w `.cs`, `using System*` pierwsze, grupy `using` rozdzielone pustą linią (`dotnet_separate_import_directive_groups = true`), namespace file-scoped zgodny z katalogiem.
- Klient (z `src/client/app01`): `npm run format` (prettier). CI wykonuje `npx prettier --check "src/**/*.{ts,tsx,css}"` oraz `npm audit --audit-level=high`.
- Nie wyłączaj reguł `.editorconfig` ani ESLint, żeby przepchnąć zmianę.
