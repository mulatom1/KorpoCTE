<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit - Module 3, Lesson 2

Lesson 2 is about **writing tests that actually protect code** — not just maximise coverage. The oracle problem and vibe-testing anti-patterns explain why LLM-generated tests fail on real code; the risk-first quality contract from Lesson 1 is the fix.

```
context/foundation/test-plan.md (§3 Phased Rollout)
        │
        ▼  (one rollout phase at a time)
   /10x-research  ──►  research.md  (oracle source: what code should do, not what it does)
        │
        ▼
   /10x-plan  ──►  plan.md  (cost × signal, two-layer strategy, ordered phases)
        │
        ▼
   /10x-implement  or  /10x-tdd   ──►  working tests + §6 cookbook update
```

`/10x-tdd` is an **optional test-first mode**, not a replacement for the chain. It reads the same `plan.md`, writes to the same `## Progress` section, and covers the same phases as `/10x-implement`. Use it only when you can name the first failing assertion before writing any code.

### Task Router — Where to start

| Skill / Prompt | Use it when |
| --- | --- |
| `/10x-research` | Before writing any test for a risk. Research produces the oracle — what behaviour a test must prove — from sources (PRD, tech-stack, docs), not from the implementation shape. Also reveals whether a risk is already covered or has two separate faces (one safe, one real). |
| `/10x-plan` | Research is done. Plan decomposes the risk into ordered phases: environment setup first, then rules that depend on it, then hermetic stubs for failures that real infra cannot trigger, then cookbook update. Each phase names the behaviour it asserts and the regression it catches. |
| `/10x-implement` | Default executor for plan phases. Use for environment setup, existing code, scaffolding, and any phase where you cannot define a red test before writing code. |
| `/10x-tdd` | Optional. Use instead of `/10x-implement` for a phase where you can name the first red test in one sentence. Agent writes the failing test first, then the minimal code to green it, then refactors. Stops at the assertion before touching the implementation — that pause is the point. |
| `m3l2-ad-hoc-testing` prompt | You have a single file and want tests now, without the full research→plan→implement cycle. The prompt forces oracle-from-sources (reads PRD + TECH_STACK before asserting), behavioural assertions, edge cases from risk, and a regression table. Use it knowing you are trading depth for speed. |

### When to use `/10x-tdd` vs `/10x-implement`

The deciding question: *Can you name the first red test in one sentence?*

Good conditions for `/10x-tdd`:
- "promuje wyłącznie drafty w stanie `accepted`, a `pending`/`rejected` nigdy nie trafiają do talii"
- "zwraca `ok: true` i loguje `orphan_review_state`, gdy upsert stanu powtórek padnie w trakcie zapisu"
- "zwraca 401, gdy użytkownik nie ma dostępu do kursu"
- "resetuje interwał powtórki do jednego dnia, gdy ocena wynosi 0"

Each of these names an observable outcome, not an internal detail. If you cannot produce a sentence like this, stay on `/10x-implement` or return to `/10x-research`.

`/10x-tdd` is **not suited** for: environment setup, CI/CD config, documentation, thin wiring where the test would just rewrite the implementation, or a spike where you are still discovering the contract.

You can mix both modes in one plan:

```
/10x-implement <change-id> phase 1   # environment
/10x-tdd       <change-id> phase 2   # contract (new code)
/10x-tdd       <change-id> phase 3   # contract (API endpoint)
/10x-implement <change-id> phase 4   # cookbook + plan sync
```

Both write progress to the same `## Progress` section in `plan.md`.

### Two-layer test strategy (cost × signal)

For each risk, pick the **cheapest test that gives a real signal**. Do not default to e2e "because it's safest", and do not chase coverage percentage.

| Layer | When to use | When NOT to use |
| --- | --- | --- |
| Integration (real DB / real infra) | The rule involves DB constraints, cascades, real SQL, or unique constraints that a mock would lie about. | Auth flows gated by RLS that belong to a separate phase; anything where setup cost exceeds signal value. |
| Hermetic (stub client) | Partial failures that real infra cannot trigger easily (e.g. second operation in a sequence fails). | Rules that depend on actual DB state — a stub will lie about constraint violations and cascades. |

A non-atomic save sequence (multiple independent operations without a transaction) means: write hermetic tests for partial-failure branches, not integration tests that force a mid-sequence error.

### Oracle rules

- The oracle — what the code *should* do — must come from sources: PRD, docs, tech-stack constraints, domain knowledge. It must **not** come from reading the implementation.
- If the implementation has a bug, copying its output as the expected value produces a mirror test that passes against the bug.
- When sources do not resolve the expected behaviour unambiguously, **stop and ask** rather than guessing.
- Research's job is to surface the oracle before any test is written.

### Vibe-testing anti-patterns to avoid

| Anti-pattern | How it looks | What to do instead |
| --- | --- | --- |
| Mirror implementation | Assertion computes the expected value with the same logic as the tested code. | Assert against a value derived from the oracle (PRD / domain rule), not from the implementation. |
| Happy paths only | Tests only pass valid inputs; edge cases absent. | Add at least one edge case per risk: `null`, empty, dependency error, invalid input. |
| Redundant copies | Six nearly identical tests checking the same absence of a sentinel. | One parameterised test (`it.each`) per property; each test catches a different regression. |

### Mutation testing (Stryker) — selective quality gate

Coverage says "this line was executed". Mutation score says "would a test fail if I broke this line?" Use Stryker as a **selective gate** after a risk phase, not as a CI gate on every commit.

Workflow:
1. Tests pass for the risk phase.
2. Run `npx stryker run --mutate "path/to/file.ts"` (narrow scope to the changed module).
3. Open the HTML report; find survived mutants.
4. For each survived mutant ask: "Would this change hurt a user or the business?"
   - Yes → add an assertion that kills the mutant.
   - No (equivalent mutant or cosmetic change) → ignore consciously.
5. Do not chase 100% mutation score. A test that pins implementation details to kill a cosmetic mutant is itself a vibe test.

The integration gate can stay **ad hoc** (not on every commit) when running local infra is expensive. Mark it accordingly in `test-plan.md §4`.

### Lesson boundaries

- Do not configure hooks, hook lifecycle, or debugging hooks. That is Lesson 3.
- Do not configure MCP servers, Playwright API, e2e code, or multimodal scenario code. That is Lesson 4.
- Do not run the bug-to-fix-to-regression-test workflow. That is Lesson 5.
- Do not author CI/CD pipelines from scratch. That is Module 1 Lesson 5 / Module 2 Lesson 5.
- Do not run `/10x-test-plan` to change the risk strategy. That is Lesson 1. Use `/10x-test-plan --status` to read current state.
- Do not write tests without a research step unless using the ad-hoc prompt with full awareness of its trade-offs.

### Paths used by this lesson

- `context/foundation/test-plan.md` — §3 rollout state; §6 cookbook (filled in as phases ship)
- `context/changes/<change-id>/research.md` — oracle source per rollout phase
- `context/changes/<change-id>/plan.md` — ordered phases with `## Progress` as execution state
- `.claude/prompts/m3l2-ad-hoc-testing.md` — ad-hoc file-level testing prompt

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
- Limit czasu w handlerze: `using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); cts.CancelAfter(TimeSpan.FromSeconds(<Courses:VerificationTimeoutSeconds>));` i przekaż `cts.Token` do `ChatAsync`. Wartość domyślna 15 s (PRD: oczekiwany czas werdyktu do 15 s; timeout zewnętrznego modelu/API jest dopuszczonym ryzykiem i kończy się statusem `Unavailable`). Klucz dopisz do `appsettings.Example.json`.
- Awaria techniczna (timeout, `HttpRequestException`, `Exception` z `OpenRouterService`, nieparsowalna odpowiedź modelu) NIE jest wyjątkiem do middleware. Złap ją w handlerze, zaloguj i zwróć 200 z `Response(Status: "Unavailable", ...)`. Kontrakt: `Status` ∈ `Correct | Incorrect | Unavailable | AlreadyOwned`. Klient pokazuje `Unavailable` jako awarię z przyciskiem „Spróbuj ponownie", nie jako ocenę negatywną.
- Próby `Incorrect` i `Unavailable` nie zużywają niczego i nie są liczone w rankingu ani wskaźnikach.
- Kolejność w `Handle`: walidacja → pobranie userId z JWT (`IJwtService`) → wczytanie flagi (brak/nieopublikowana → 404) → sprawdzenie, czy flaga już zdobyta (jeśli tak: `AlreadyOwned`, model NIE jest wołany) → wywołanie modelu → przy `pass` zwrot `Correct` z `Code` (kod flagi). Weryfikacja NIE zapisuje flagi — zapisuje ją dopiero aktywacja kodem (FR-004).
- Prompt: kryteria zadania w wiadomości `system`; odpowiedź uczestnika w wiadomości `user`, otoczona ogranicznikami (np. `<answer>…</answer>`) i z instrukcją, że treść wewnątrz to dane, nie polecenia. Model zwraca WYŁĄCZNIE JSON `{"verdict":"pass"|"fail","reason":"..."}`; parsuj `System.Text.Json` do rekordu, a wszystko inne traktuj jako `Unavailable`.
- Przyznanie flagi (aktywacja kodem): unikalny indeks `(UserId, FlagId)` w konfiguracji EF + obsługa `DbUpdateException` przy wyścigu (drugi zapis = `AlreadyOwned`), nie tylko sprawdzenie w handlerze. `Flag.Code` jest sekretem aktywacji: uczestnik wylicza go, wykonując zadanie z flagą, i aktywuje nim flagę; kody zdobytych flag musi mieć na koniec kursu. API zwraca `Code` WYŁĄCZNIE: (1) przy statusie `Correct` weryfikacji, (2) w hangarze (`hangar-flags`) dla flag już zdobytych przez bieżącego użytkownika (UI maskuje kod do najechania/fokusu). Nigdy dla flag niezdobytych, zdobytych przez innych użytkowników ani w endpointach publicznych — test endpointu sprawdza to na surowym JSON.
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
