<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit — Module 1, Lesson 5

Pick a deployment platform and ship to production with the **infra chain**:

```
(/10x-init  →  /10x-shape  →  /10x-prd  →  /10x-tech-stack-selector  →  /10x-bootstrapper  →  /10x-agents-md  →  /10x-rule-review  →  /10x-lesson)  →  /10x-infra-research  →  Plan Mode deploy
```

The full Module 1 chain ships from Lessons 1–4 (re-included so you can fix any earlier contract mid-flight). `/10x-infra-research` is the lesson's main topic; the deploy step itself uses the host's built-in **Plan Mode** rather than a dedicated skill — the artifact (`context/deployment/deploy-plan.md`) is what carries forward.

### Task Router — Where to start

| Skill | Use it when |
| --- | --- |
| **Infrastructure (lesson focus)** | |
| `/10x-infra-research [path-to-tech-stack-or-prd]` | You have a `context/foundation/tech-stack.md` (and ideally a `prd.md`) and need to pick an MVP deployment platform. The skill loads the stack as a hard constraint, runs a 5-question developer interview (persistent connections, cost sensitivity, existing familiarity, global reach, co-location preference), spawns parallel subagent research across six candidate platforms, scores them Pass/Partial/Fail across the five agent-friendly criteria from `references/agent-friendly-criteria.md`, shortlists the top three, and runs a three-lens anti-bias cross-check on the leader (devil's advocate, pre-mortem, unknown unknowns) before writing `context/foundation/infrastructure.md`. Use AFTER `/10x-tech-stack-selector`, BEFORE `/10x-implement`. |
| **Deploy (host built-in, not a skill)** | |
| Plan Mode deploy | You have `infrastructure.md` + `tech-stack.md` and want a read-only plan reviewed before any mutation hits the platform. Activate the host's plan mode (Claude Code: `Shift+Tab` cycles default → auto-accept → plan; IDE: dedicated button) with the prompt "Wykonajmy pierwsze wdrożenie w oparciu o `@infrastructure.md`, zgodnie ze stackiem z `@tech-stack.md`". Read the plan, demand corrections, approve, then let the agent execute. The approved plan persists at `context/deployment/deploy-plan.md` so the next lesson's milestone planning can reference what's already deployed and which secrets are already wired. |
| **Re-run upstream if needed** | |
| `/10x-init` / `/10x-shape` / `/10x-prd` / `/10x-tech-stack-selector` / `/10x-bootstrapper` / `/10x-agents-md` / `/10x-rule-review` / `/10x-lesson` / `/10x-stack-assess` / `/10x-health-check` | Bundled so you can patch any earlier contract mid-flight. If the anti-bias cross-check forces a platform swap that pushes a stack-shaped decision (e.g. "this DB doesn't fit any platform we'd accept"), re-run `/10x-tech-stack-selector` to keep `tech-stack.md` and `infrastructure.md` aligned. |

### How the chain hands off

- `/10x-infra-research` reads `context/foundation/tech-stack.md` (language, framework, runtime, database) as **hard constraints** — platforms that can't run the stack are dropped before scoring. It also reads `context/foundation/prd.md` (scale, latency, uptime expectations) as **soft weights** when scoring. Both inputs are optional but strongly recommended; without them the skill proceeds but warns.
- The skill writes `context/foundation/infrastructure.md` as the third foundation contract: frontmatter (`project`, `researched_at`, `recommended_platform`, `runner_up`, `context_type`, `tech_stack`) plus a body covering recommendation, full platform comparison with scoring matrix, anti-bias findings, operational story (preview / secrets / rollback / approval / logs), and a risk register tying every entry back to the lens that surfaced it. On collision the skill prompts: overwrite, save as `infrastructure-v2.md`, or abort.
- Plan Mode reads `infrastructure.md` and `tech-stack.md` together. The agent emits a step-by-step plan covering automated steps it owns, manual setup gates (account creation, secret configuration), exact deploy commands (Pages vs Workers commands are NOT interchangeable on Cloudflare — the plan must specify), and verification steps. The plan is rejected/edited until it's right; only then does Plan Mode exit and execution begin. The approved plan lands at `context/deployment/deploy-plan.md` and is consumed downstream by milestone-planning skills as ground truth for "what's already deployed".

### What the lesson's skills capture (and what they do NOT)

- **`/10x-infra-research` captures**: platform shortlist scored against five agent-friendly criteria (CLI quality, managed/serverless degree, agent-readable docs, stable/scriptable deploy API, MCP or first-class agent integration), three anti-bias outputs on the leader (numbered weaknesses, 150–200-word failure narrative, 3–5 unknown-unknowns), an operational story with one concrete answer per axis (not categories), and a risk register where every row names its source lens (`Devil's advocate` / `Pre-mortem` / `Unknown unknowns` / `Research finding`). Status of every non-GA feature is captured inline (`beta` / `preview` / `region-limited` / `deprecated`) with the date the status was checked.
- **`/10x-infra-research` does NOT** build Docker images or write Dockerfiles, configure CI/CD pipelines, or plan beyond MVP scope (multi-region HA is explicitly out of scope). It does NOT decide for you — the user accepts, swaps to runner-up, or aborts after the cross-check, and that decision is recorded in the output.
- **Plan Mode** captures: an explicit human gate between "agent has a plan" and "agent mutates production". The artifact (`deploy-plan.md`) is the audit trail for "what was supposed to happen" when the live run goes sideways. Plan Mode does NOT replace `/10x-infra-research` (the platform decision must already be made — Plan Mode plans the deploy, it doesn't pick where to deploy).

### The five agent-friendly criteria (and why they're load-bearing)

The criteria that make `/10x-infra-research`'s scoring matrix are not generic "good platform" axes — they're the specific traits that determine whether an agent can operate this platform from a session without you holding its hand:

1. **CLI-first** — every routine operation has a documented command; the agent doesn't need to click in a panel.
2. **Managed / serverless** — fewer moving pieces means fewer ways the agent (or you) breaks something the platform was supposed to handle.
3. **Agent-readable docs** — markdown / `llms.txt` / GitHub-hosted docs the agent can fetch and parse, not JS-rendered marketing pages.
4. **Stable, scriptable deploy API** — predictable exit codes, structured output, no interactive prompts mid-deploy.
5. **MCP server or first-class agent integration** — bonus, not required. CLI alone is fine for MVP; MCP earns its keep when the agent makes dozens of structured queries against live state.

Hard filters apply before scoring (persistent-connection requirement drops Netlify/Vercel serverless-only; tech-stack runtime mismatch drops the platform entirely). Interview answers reweight criteria after — cost sensitivity penalizes expensive base tiers, familiarity breaks ties, global-reach preference favours edge-native platforms, co-location preference favours integrated databases.

### Anti-bias as a decision discipline (not theatre)

Every research conversation with an LLM has a built-in tilt toward whatever the user already signalled. `/10x-infra-research` runs three structured lenses against the leader BEFORE the file is written, not after:

- **Devil's advocate** — *find the weaknesses, hidden costs, and failure modes specific to deploying `<this stack>` on `<this platform>`*. Output is a numbered list of 3–5 specifics, not categories.
- **Pre-mortem** — *six months later, this decision turned out to be a complete disaster; walk through the assumptions and underestimated risks that led there*. Output is a 150–200-word narrative; narratives surface concrete failure shapes that abstract risk lists hide.
- **Unknown unknowns** — *what's true about this combination that the marketing page and docs don't make obvious?* Output is 3–5 non-obvious risks.

After the cross-check the user has three real options: **proceed with the leader and absorb the risks into the register**, **swap to runner-up** (and re-run the cross-check on the new leader), or **swap to third place**. The third option is rare; if it never happens across many runs, the cross-check has degraded into a ritual and should be rewritten.

Two additional techniques (no skill required, raw prompts) belong in the same toolbox: forcing the model to compare three alternatives in a markdown table (structure beats "the same answer in different words"), and role-rotation (the same decision through a frontend dev's, security person's, and cost owner's eyes — surface the cost each role pays and propose alternatives if any of them flinch).

### CLI vs MCP for live-infra operability

After deploy, the agent needs a way to talk to the running platform. Two paths, complementary not competing:

- **CLI** (`wrangler`, `flyctl`, `vercel`, `gh`) — explicit and auditable, output stays in the terminal, safer defaults for irreversible actions (e.g. `netlify deploy` is draft by default; `--prod` must be passed). Best for MVP: minimal setup, low context cost (no tool schemas pre-loaded), and the agent has to know the command (which is where a per-tool skill helps).
- **MCP** — a dedicated server exposing structured tools with schemas (`pages_deployments_list`, etc.). Each connected MCP server adds tool definitions to the context window, so cost compounds across servers. Earns its keep when the agent makes many discovery-style queries against live state (logs, deployment diffs) and structured JSON beats parsing CLI output.

Sensible default: start with CLI, add MCP when you notice a recurring pattern of `--help` traversal the agent has to do to answer a class of questions. Anthropic's own [building-agents-that-reach-production](https://claude.com/blog/building-agents-that-reach-production-systems-with-mcp) framing is "API, CLI, and MCP are three complementary paths" — pick by task, not by hype.

### Production-access boundary (minimal permissions, human-on-irreversibles)

Both CLI and MCP can give the agent direct access to production. The lesson sets a default posture:

- **Tokens are scoped, not master keys.** On Cloudflare: an API token limited to Pages or Workers for one project, no DNS, no Workers Secrets for unrelated projects, no billing. AWS / GCP equivalent: scoped IAM role with `console-only-user` or read-only on production, full access on staging.
- **Tokens live in env vars, not in `.mcp.json` committed to the repo.** The agent picks them up via the MCP server or CLI's env-discovery, not via plaintext in conversation.
- **Destructive actions are human-only.** Drop a database, rotate a primary secret, delete a project — those are panel-by-hand operations, even if the agent suggests them. Manual click costs 30 seconds; cleanup after an automated mistake costs hours.

This is the MVP posture. As the project matures, the natural evolution is staging gets full agent access, production becomes read-only — covered in later modules.

### Foundation paths used by this lesson

- `context/foundation/tech-stack.md` — input (Lesson 2 hand-off, hard constraints)
- `context/foundation/prd.md` — input (Lesson 1 hand-off, soft weights)
- `context/foundation/infrastructure.md` — output (the third foundation contract)
- `context/deployment/deploy-plan.md` — output of Plan Mode deploy (audit trail of "what was supposed to happen")
- `context/foundation/lessons.md` — recurring rules & pitfalls (use `/10x-lesson` from Lesson 4 if you spot a class of agent failure during research or deploy)
- `docs/reference/contract-surfaces.md` — load-bearing names registry

### Universal language

The shipped skill carries no 10xDevs / cohort / certification references. The candidate platform list (Cloudflare, Vercel, Netlify, Fly.io, Railway, Render) is the starting research lens, not a recommendation set — the scoring + interview + cross-check pipeline is what's load-bearing, and a platform absent from the default list can be added by extending the research step. The five agent-friendly criteria are the artifact's true core; `/10x-infra-research` re-reads them from `references/agent-friendly-criteria.md` so they evolve as platforms do.

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
