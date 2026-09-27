<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit — Module 1, Lesson 2

Pick a starter and a stack for the PRD you wrote in Lesson 1, with the **stack chain**:

```
(/10x-init  →  /10x-shape  →  /10x-prd)  →  /10x-tech-stack-selector  →  (bootstrapper)
```

The PRD chain ships from Lesson 1 (re-included in this lesson so you can fix the PRD mid-flight). `/10x-tech-stack-selector` is the lesson's main topic; `/10x-bootstrapper` is the next link, taught in Lesson 3.

### Task Router — Where to start

| Skill | Use it when |
| --- | --- |
| **Stack selection (lesson focus)** | |
| `/10x-tech-stack-selector` | You have a PRD at `context/foundation/prd.md` and need to pick a starter. Opens with an explicit choice (take the recommended default for your `(product_type, language_family)` cell, or design your own), walks the follow-up question set when you design your own, applies four agent-friendly quality gates, reasons over the language-aware starter registry, and writes `context/foundation/tech-stack.md`. Optional `[path-to-prd]` argument lets you point at a non-default PRD location (e.g., `/10x-tech-stack-selector @context/foundation/prd-v2.md`); without it the skill defaults to `context/foundation/prd.md`. Use AFTER `/10x-prd`, BEFORE `/10x-bootstrapper`. |
| **Re-run upstream if needed** | |
| `/10x-init` / `/10x-shape` / `/10x-prd` | Bundled so you can fix the PRD mid-flight. If `/10x-tech-stack-selector` surfaces a gap (e.g., a Functional Requirement that forces a feature your recommended starter doesn't carry), re-run `/10x-prd` to amend the PRD before the stack pick. |

### How the chain hands off

- `/10x-tech-stack-selector` reads `context/foundation/prd.md` frontmatter (`product_type`, `target_scale`, `timeline_budget`) as priors. If the PRD is absent, it refuses with a one-sentence redirect to `/10x-shape` — no inline mini-PRD fallback.
- The skill writes `context/foundation/tech-stack.md` with a 4-key frontmatter (`starter_id`, `package_manager`, `project_name`, `hints`) plus a one-paragraph `## Why this stack` body. The hand-off is intentionally minimal — bootstrapper does not parse rationale, only fields.
- `/10x-bootstrapper` (Lesson 3) reads `tech-stack.md` and the registry to scaffold the project.

### What tech-stack-selector captures (and what it does NOT)

- **Captured**: starter pick (registry-shaped), language family, package manager (open string per ecosystem — `pnpm`, `uv`, `bundle`, `cargo`, etc.), team size, deployment target (drawn from the chosen starter's `deployment_defaults`), CI/CD provider + flow, bootstrapper confidence (`verified | first-class | best-effort`), path taken (standard | custom), self-check answers (custom path), quality override (set when the user proceeds with a starter that failed ≥1 agent-friendly gate), feature flags (auth/payments/realtime/AI/background-jobs).
- **NOT captured (deliberate)**: strategic test plan, strategic deployment plan, strategic implementation decisions. Those are downstream of stack selection — a future technical-roadmap concern, not yet planned. Tech-stack-selector owns *framework-shaped* test/deploy/CI choices because those are inseparable from stack pick; what defers is the *strategic* layer ("we TDD on X surface", "preview environment per PR").

### The opening choice (load-bearing)

The first question is an explicit choice — never silent. The skill names the recommended starter for your `(product_type, language_family)` cell up front and asks for explicit confirmation:

- **Standard path** — accept the recommended default. The skill skips the feature audit, team profile, tech preferences, and framework-variant questions; it asks only the deployment, CI/CD, and project-name questions. The hand-off records `path_taken: standard` under `hints`.
- **Custom path** — design your own. The skill walks the full follow-up set (feature audit, team profile, tech preferences, deployment, CI/CD, framework variant), drills into a testing-runner question only when the chosen starter leaves it ambiguous, and closes with a 5-point readiness self-check (from prework lesson 4.1) before locking in. The hand-off records `path_taken: custom` and populates `self_check_answers`.

The recommended-default-per-cell map is multi-language: web/JS and saas/JS both → 10x-astro-starter (the 10x-branded starter leads whenever it competes in a JS cell); api/JS → hono; api/Python → fastapi; web/Python → django; web/Ruby → rails; api/Go → go; api/Rust → axum; mobile/Dart → flutter; desktop/Rust → tauri; etc. Cells with no vetted default carry `<none>` and force the custom path.

### Quality gates (agent-friendly criteria)

Every starter card carries four booleans the LLM filters against:

1. **Typed** — explicit types/schemas the agent can reason from without running the program.
2. **Convention-based** — strong opinions on layout, routing, configuration.
3. **Popular in training data** — assessed *per language family*, not globally (Django is popular within Python training data; Spring within Java; etc.).
4. **Well-documented** — current, version-pinned, link-able docs.

Candidates failing any gate are excluded from the unprompted recommendation set. If you explicitly name a failing starter as your preference, the skill challenges that pick — surfacing the strongest higher-criteria alternative AND the compensation path (CLAUDE.md instructions that patch the gaps) — and asks you to confirm or pivot. Confirming the known-friction pick records the override on the hand-off so bootstrapper can adjust.

### Bootstrapper confidence

Every recommendation surfaces `bootstrapper_confidence` verbatim — never silently elided:

- **`verified`** — bootstrapper has been run end-to-end on this stack; scaffolding will be smooth.
- **`first-class`** — registered with a valid CLI, expected to work but not battle-tested; expect mostly-smooth scaffolding with occasional manual steps.
- **`best-effort`** — limited support; manual steps likely; expect friction (and bootstrapper's CLAUDE.md generation compensates with extra ecosystem-specific context).

This is the heads-up before running `/10x-bootstrapper` so you know what to expect.

### Foundation paths used by this lesson

- `context/foundation/prd.md` — input (from Lesson 1)
- `context/foundation/tech-stack.md` — output (the chain hand-off)
- `context/foundation/lessons.md` — recurring rules & pitfalls
- `docs/reference/contract-surfaces.md` — load-bearing names registry

### Universal language

The shipped skill carries no 10xDevs / cohort / certification references. The recommended-default registry is multi-language (JS, Python, Ruby, Java, Go, Rust, PHP, .NET, Dart) and the cohort's `10x-astro-starter` is one card in the JS+web cell — not "the" recommended path for everyone.

Skills must not write to `context/archive/`. Archived changes are immutable; if a resolved target path starts with `context/archive/`, abort with: "This change is archived. Open a new change with `/10x-new` instead."

<!-- END @przeprogramowani/10x-cli -->

## Projekt App01 — mapa repozytorium

- `APPS.sln` — solucja. Serwer: `src/server/App01/`. Klient: `src/client/app01/`. Testy: `tests/server/App01/App01.Api.Tests/`.
- Architektura: **modularny monolit** ASP.NET Core 8 (Minimal APIs). Jeden host `App01.Bootstrapper.Api` składa moduły:
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
   - Łańcuch: `.WithName("<Moduł><NazwaFunkcji>")`, `.WithTags("<Moduł>")`, `.Produces<Contracts.Response>(200)` + kody błędów, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()` (pominąć tylko dla endpointów publicznych), `.WithOpenApi()`.
5. **Rejestracja (łatwo zapomnieć):** dopisz `Features.<NazwaFunkcji>.Endpoint.AddEndpoint(app);` w `ModuleDI.UseModule<Moduł>Endpoints()`. Niezarejestrowany endpoint NIE zwraca 404 — trafia w `MapFallbackToFile("index.html")` i zwraca HTML SPA.
6. Test: `tests/server/App01/App01.Api.Tests/Features/<Moduł>/<NazwaFunkcji>/EndpointTests.cs` (wzorzec: `Features/Portal/UserList/EndpointTests.cs`).

## Serwer — przepis na nowy moduł (np. Courses)

1. Utwórz `src/server/App01/App01.Modules.<Moduł>/App01.Modules.<Moduł>.csproj` (kopia `App01.Modules.Portal.csproj`: net8.0, Nullable, ImplicitUsings, `ProjectReference` do `App01.Shared.Infrastructure`).
2. `dotnet sln APPS.sln add src/server/App01/App01.Modules.<Moduł>/App01.Modules.<Moduł>.csproj --solution-folder src/server/App01`
3. `ModuleDI.cs` w namespace `App01.Modules.<Moduł>` z metodami: `AddModule<Moduł>Services(this IServiceCollection)` (MediatR + validators z `Assembly.GetExecutingAssembly()`), `UseModule<Moduł>Endpoints(this WebApplication)`, opcjonalnie `AddModule<Moduł>Workers`.
4. `App01.Bootstrapper.Api.csproj` — dodaj `ProjectReference`; `Program.cs` — `using App01.Modules.<Moduł>;`, `builder.Services.AddModule<Moduł>Services();` obok pozostałych, `app.UseModule<Moduł>Endpoints();` PRZED `app.MapFallbackToFile(...)`; workery tylko w bloku `if (!isTestEnvironment)`.
5. Projekt testów `App01.Bootstrapper.Api.Tests.csproj` — dodaj `ProjectReference` do nowego modułu.
6. Nie zmieniaj istniejących modułów, tras ani `ExceptionHandlingMiddleware` — PRD wymaga, by Portal/Lotto/Flashcards działały bez zmian.

## Dane i migracje (EF Core 8, SQL Server)

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

.NET 8 / ASP.NET Core 8 / EF Core 8 · MediatR 12.x (v13+ ma licencję komercyjną) · FluentValidation 12 · xUnit 2.x (nie v3) · Serilog.AspNetCore 9 · React 19 · React Router 7 · Tailwind CSS 4 · Vite 7 · TypeScript 5.9.
Dokumentacja: learn.microsoft.com/aspnet/core (8.0), learn.microsoft.com/ef/core, docs.fluentvalidation.net, reactrouter.com (sekcja „Declarative Mode"), tailwindcss.com/docs (v4), vite.dev.

## Weryfikacja przed zakończeniem zadania

- Serwer: `dotnet build APPS.sln` i `dotnet test APPS.sln` — muszą przejść.
- Klient (z `src/client/app01`): `npm run build` (zawiera `tsc -b`) i `npm run lint`. Brak testów frontendu — dlatego logikę domenową (weryfikacja odpowiedzi, przyznawanie flag, ranking, wskaźniki) trzymaj po stronie serwera, gdzie jest pokryta testami endpointów.
- Nowy endpoint = nowy `EndpointTests.cs` z przypadkiem: sukces, 400 (walidacja), 401 (brak JWT), 403 (gdy dotyczy), brak `X-TOKEN`.