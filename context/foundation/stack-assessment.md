---
project: APPS (tomsoft1.pl — App01)
assessed_at: 2026-09-27T09:00:00+02:00
agent_readiness: ready-with-compensation
context_type: brownfield
stack_components:
  language: C# (.NET 8) + TypeScript 5.9
  framework: ASP.NET Core 8 Minimal APIs (modular monolith, MediatR + FluentValidation + EF Core 8) + React 19 SPA (React Router 7, Tailwind 4)
  build_tool: dotnet SDK / MSBuild + Vite 7
  test_runner: xUnit 2.5 (server); frontend — brak
  package_manager: NuGet + npm
  ci_provider: null
  deployment_target: folder publish (FileSystem publish profile), SPA serwowane przez ASP.NET Core
gates_passed: 14
gates_failed: 2
---

## Stack Components

**Język (serwer) — C# na .NET 8.** Wszystkie projekty w `APPS.sln` mają `<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>` i `<ImplicitUsings>enable</ImplicitUsings>`. Typowanie statyczne z włączoną analizą nulli.

**Język (klient) — TypeScript ~5.9.3.** `src/client/app01/tsconfig.app.json` ma `"strict": true`, `noUnusedLocals`, `noUnusedParameters`, `verbatimModuleSyntax`, `erasableSyntaxOnly`. Build (`tsc -b && vite build`) blokuje błędy typów.

**Framework (serwer) — ASP.NET Core 8 Minimal APIs jako modularny monolit.** Odpowiedź na Open Question nr 4 z PRD: architektura to **modularny monolit** — jeden host `App01.Bootstrapper.Api` (Program.cs) składający moduły `App01.Modules.Portal`, `App01.Modules.Lotto`, `App01.Modules.Flashcards` oraz warstwy współdzielone `App01.Shared.Abstractions` (pakiety NuGet), `App01.Shared.Application` (encje, wyjątki, filtry, interfejsy, middleware) i `App01.Shared.Infrastructure` (`AppDbContext`, konfiguracje EF, migracje, serwisy JWT/X-TOKEN/OpenRouter). Każda funkcja to pionowy wycinek `Features/<Nazwa>/{Contracts,Endpoint,Handler,Validator}.cs` oparty o MediatR 12.5 i FluentValidation 12.1. Dane: EF Core 8 na SQL Server (`UseSqlServer(..., sql => sql.UseCompatibilityLevel(110))`), jedna baza i jeden `AppDbContext` dla wszystkich modułów. Logowanie: Serilog. Uwierzytelnianie: JWT Bearer + nagłówek aplikacyjny `X-TOKEN` (`XTokenFilter`). Moduł Lotto i Portal mają hosted workers (wyłączone w środowisku `Test`).

**Framework (klient) — React 19 SPA.** React 19.1, React Router 7.9 w trybie deklaratywnym (`<BrowserRouter>/<Routes>/<Route>` w `src/main.tsx`, import z `"react-router"`), Tailwind CSS 4.1 w konfiguracji CSS-first (`@import "tailwindcss"` w `src/index.css`, plugin `@tailwindcss/vite`, brak `tailwind.config.js`). Markdown przez `react-markdown` + `remark-gfm` + `rehype-highlight` + `rehype-raw` + `remark-frontmatter` (istotne dla FR-002). Build SPA ląduje w `wwwroot` hosta i jest serwowany przez `MapFallbackToFile("index.html")`.

**Build tool.** Serwer: dotnet SDK / MSBuild, publikacja przez `Properties/PublishProfiles/FolderProfile*.pubxml` (`WebPublishMethod=FileSystem`). Klient: Vite 7.1 z trybami `dev`, `prod1`, `prod2` (`.env.*`, `VITE_BASE_URL`).

**Test runner.** Serwer: xUnit 2.5.3 + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) + EF Core InMemory + Moq; testy endpointów w `tests/server/App01/App01.Api.Tests/Features/<Moduł>/<Funkcja>/EndpointTests.cs` (26 plików). Klient: brak runnera testów — jedyne bramki to `tsc -b` i `eslint`.

**CI/CD i wdrożenie.** Brak `.github/workflows` i innych plików CI. Wdrożenie ręczne: folder publish + wgranie plików. Brak Dockerfile.

**Pliki instrukcji.** `CLAUDE.md` w korzeniu opisuje wyłącznie materiał kursowy (łańcuch skilli), nic o samym projekcie. `src/client/app01/README.md` opisuje strukturę frontendu, ale jest częściowo nieaktualny (np. wymienia `pages/home/`, a strony portalu leżą w `pages/portal/*`). Brak `AGENTS.md`.

## Quality Gate Assessment

| Komponent | Typowanie | Konwencje | Dane treningowe | Dokumentacja | Werdykt |
|---|---|---|---|---|---|
| Język serwera — C# / .NET 8 | ✓ | — | — | — | pass |
| Framework serwera — ASP.NET Core 8 Minimal APIs + MediatR/FluentValidation/EF Core | — | ~ | ✓ | ✓ | pass-with-compensation |
| Build serwera — dotnet SDK / MSBuild | — | ✓ | ✓ | ✓ | pass |
| Testy serwera — xUnit + WebApplicationFactory | — | — | ✓ | ✓ | pass |
| Język klienta — TypeScript strict | ✓ | — | — | — | pass |
| Framework klienta — React 19 + React Router 7 (deklaratywny) + Tailwind 4 | — | ~ | ✓ | ✓ | pass-with-compensation |
| Build klienta — Vite 7 | — | ✓ | ✓ | ✓ | pass |
| Testy klienta | — | — | — | — | brak runnera (luka poza kryteriami) |

Legenda: ✓ = spełnia, ✗ = nie spełnia, ~ = częściowo (wymaga uzupełnienia w pliku instrukcji), — = nie dotyczy.

Wynik: 14 z 16 sprawdzonych kryteriów spełnionych w pełni, 2 częściowo (oba dotyczą konwencji).

### Gate Details

**Typowanie**
- C#: ✓ — typowany język; każdy `*.csproj` ma `<Nullable>enable</Nullable>`. Kontrakty API to rekordy (`Contracts.Request : IRequest<Response>`, `Contracts.Response`, DTO), walidacja wejścia przez `AbstractValidator<Contracts.Request>`.
- TypeScript: ✓ — `tsconfig.app.json` → `"strict": true` plus dodatkowe flagi lintujące. Kontrakty API po stronie klienta w `src/services/contracts/*.ts` jako `interface`.
- Uwaga (nie obniża oceny): kontrakty TS są **ręcznie przepisywane** z rekordów C#. `NSwag.ApiDescription.Client` jest w `App01.Shared.Abstractions.csproj`, ale nie generuje klienta TS — ryzyko dryfu kontraktów między serwerem a klientem.

**Konwencje**
- ASP.NET Core: ~ — framework sam w sobie jest opiniowany (DI, konfiguracja, middleware pipeline), ale kształt projektu jest **autorski**: modularny monolit, pionowe wycinki MediatR, ręczna rejestracja każdego endpointu w `ModuleDI.UseModule<X>Endpoints()`, encje wszystkich modułów w `App01.Shared.Application/Entities/<Moduł>/`, jeden `AppDbContext` w `Shared.Infrastructure`, walidacja wywoływana ręcznie w handlerze (nie przez pipeline behavior), admin sprawdzany przez `IJwtService.GetIsAdminFromJwt()` w handlerze. Wzorzec jest bardzo spójny (każda z ~45 funkcji ma te same 4 pliki), ale nigdzie nieopisany — agent musi go wywnioskować z kodu.
- dotnet/MSBuild: ✓ — standardowe `.sln` + `.csproj`, SDK-style.
- React + React Router 7: ~ — Vite + React nie narzuca układu; routing deklaratywny zebrany w jednym `src/main.tsx` (nie file-based). Projekt ma własną, spójną konwencję (`pages/<moduł>/<Nazwa>Page.tsx`, `services/api-<moduł>-service.ts`, `services/contracts/<moduł>-<funkcja>-request|response.ts`, `<RequireAuth/>` jako layout route), częściowo opisaną w `README.md`, ale nieaktualną i nie w pliku instrukcji agenta.
- Vite: ✓ — standardowy `vite.config.ts`, tryby przez `--mode`.

**Dane treningowe (w obrębie rodziny języka)**
- ASP.NET Core, EF Core, MediatR, FluentValidation, xUnit, Serilog: ✓ — mainstream ekosystemu .NET; wzorzec „vertical slice + MediatR" jest szeroko obecny.
- React, Vite, Tailwind, React Router: ✓ — top ekosystemu JS. Uwaga na **dryf wersji**: Tailwind 4 (CSS-first, bez `tailwind.config.js`) i React Router 7 (pakiet `react-router` zamiast `react-router-dom`, trzy tryby: deklaratywny / data / framework) są nowszymi majorami — korpus treningowy jest zdominowany przez Tailwind 3 i React Router 6. Kompensacja poniżej.
- MediatR: w wersji 13+ zmieniła się licencja (komercyjna) — projekt jest na 12.5, co trzeba przypiąć.

**Dokumentacja**
- ✓ dla wszystkich: learn.microsoft.com (ASP.NET Core 8, EF Core 8 — wersjonowane), docs.fluentvalidation.net, xunit.net, react.dev, reactrouter.com (osobne sekcje per tryb), tailwindcss.com/docs (v4), vite.dev.

## Gaps & Compensation

**1. Autorska architektura serwera (konwencje ~).** Agent dodający moduł Kursy musi utworzyć nowy projekt, podpiąć go w 4 miejscach (sln, Program.cs ×2–3, projekt testów), dodać encje we współdzielonej warstwie, konfigurację EF, DbSet i migrację — i zrobić to dokładnie tak jak istniejące moduły. Bez opisu łatwo o: brak rejestracji endpointu (wycinek istnieje, a endpoint zwraca fallback SPA `index.html` zamiast 404), brak `XTokenFilter`, encję wewnątrz modułu zamiast w `Shared.Application`, walidator niewywołany w handlerze. **Kompensacja:** sekcje „Architektura serwera", „Przepis na wycinek funkcji", „Przepis na nowy moduł", „Dane i migracje", „Bezpieczeństwo endpointów" poniżej.

**2. Konwencje klienta (konwencje ~).** Routing w jednym pliku, ręcznie pisane kontrakty, serwisy API per moduł, Tailwind 4 i React Router 7 z ryzykiem generowania kodu pod starsze majory. **Kompensacja:** sekcje „Frontend" i „Wersje przypięte".

**3. Brak testów frontendu (poza kryteriami).** Weryfikacja zmian UI to tylko `tsc -b` + `eslint`. Kompensacja minimalna: reguła, że każda zmiana klienta kończy się `npm run build` i `npm run lint`, a logika domenowa (weryfikacja odpowiedzi, przyznawanie flag, ranking, wskaźniki) żyje po stronie serwera, gdzie są testy endpointów. Decyzja o dodaniu Vitest należy do `/10x-health-check` / planu technicznego.

**4. Brak CI (poza kryteriami).** Nic automatycznie nie uruchamia `dotnet test` przed wdrożeniem. Kompensacja: reguła „przed zakończeniem zadania uruchom `dotnet test APPS.sln`" w pliku instrukcji; decyzja o CI poza zakresem tej oceny.

**5. Ryzyko dryfu kontraktów C# ↔ TS.** Kompensacja: reguła aktualizacji obu stron w tej samej zmianie.

### Recommended Instruction File Additions

Poniższe bloki można wkleić bezpośrednio do `CLAUDE.md` (lub `AGENTS.md`) w korzeniu repozytorium — najlepiej jako osobna sekcja „Projekt: App01" pod treścią kursową.

```markdown
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
```

```markdown
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
```

```markdown
## Serwer — przepis na nowy moduł (np. Courses)

1. Utwórz `src/server/App01/App01.Modules.<Moduł>/App01.Modules.<Moduł>.csproj` (kopia `App01.Modules.Portal.csproj`: net8.0, Nullable, ImplicitUsings, `ProjectReference` do `App01.Shared.Infrastructure`).
2. `dotnet sln APPS.sln add src/server/App01/App01.Modules.<Moduł>/App01.Modules.<Moduł>.csproj --solution-folder src/server/App01`
3. `ModuleDI.cs` w namespace `App01.Modules.<Moduł>` z metodami: `AddModule<Moduł>Services(this IServiceCollection)` (MediatR + validators z `Assembly.GetExecutingAssembly()`), `UseModule<Moduł>Endpoints(this WebApplication)`, opcjonalnie `AddModule<Moduł>Workers`.
4. `App01.Bootstrapper.Api.csproj` — dodaj `ProjectReference`; `Program.cs` — `using App01.Modules.<Moduł>;`, `builder.Services.AddModule<Moduł>Services();` obok pozostałych, `app.UseModule<Moduł>Endpoints();` PRZED `app.MapFallbackToFile(...)`; workery tylko w bloku `if (!isTestEnvironment)`.
5. Projekt testów `App01.Bootstrapper.Api.Tests.csproj` — dodaj `ProjectReference` do nowego modułu.
6. Nie zmieniaj istniejących modułów, tras ani `ExceptionHandlingMiddleware` — PRD wymaga, by Portal/Lotto/Flashcards działały bez zmian.
```

```markdown
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
```

```markdown
## Bezpieczeństwo endpointów

- Każdy endpoint wymaga nagłówka aplikacyjnego `X-TOKEN` przez `.AddEndpointFilter<XTokenFilter>()` (wyjątki istniejące: `GetApiVersion`, `FileEdit01`, `TransformNumbers` — nie powielaj ich bez powodu).
- Endpointy dla zalogowanych: `.RequireAuthorization()` (JWT Bearer). Endpointy publiczne (np. lista kafelków kursów) — bez `RequireAuthorization()`, ale z `XTokenFilter`, i zwracają WYŁĄCZNIE pola publiczne (DTO bez treści kursu).
- Rola admina = claim `isAdmin` w JWT; sprawdzenie w handlerze przez `IJwtService.GetIsAdminFromJwt()`. Nie dodawaj nowych ról ani policy.
- Id bieżącego użytkownika pobieraj z JWT przez `IJwtService`, nigdy z body requestu.
```

```markdown
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
```

```markdown
## Wersje przypięte — nie podbijaj majorów bez polecenia

.NET 8 / ASP.NET Core 8 / EF Core 8 · MediatR 12.x (v13+ ma licencję komercyjną) · FluentValidation 12 · xUnit 2.x (nie v3) · Serilog.AspNetCore 9 · React 19 · React Router 7 · Tailwind CSS 4 · Vite 7 · TypeScript 5.9.
Dokumentacja: learn.microsoft.com/aspnet/core (8.0), learn.microsoft.com/ef/core, docs.fluentvalidation.net, reactrouter.com (sekcja „Declarative Mode"), tailwindcss.com/docs (v4), vite.dev.
```

```markdown
## Weryfikacja przed zakończeniem zadania

- Serwer: `dotnet build APPS.sln` i `dotnet test APPS.sln` — muszą przejść.
- Klient (z `src/client/app01`): `npm run build` (zawiera `tsc -b`) i `npm run lint`. Brak testów frontendu — dlatego logikę domenową (weryfikacja odpowiedzi, przyznawanie flag, ranking, wskaźniki) trzymaj po stronie serwera, gdzie jest pokryta testami endpointów.
- Nowy endpoint = nowy `EndpointTests.cs` z przypadkiem: sukces, 400 (walidacja), 401 (brak JWT), 403 (gdy dotyczy), brak `X-TOKEN`.
```

## Summary

**Ogólna gotowość dla agenta: gotowy po uzupełnieniu instrukcji.** Stack jest mocny pod pracę z agentem: typowany end-to-end (C# z nullable + TypeScript strict), zbudowany z mainstreamowych i dobrze udokumentowanych składników ekosystemów .NET i JS, z istniejącą siatką testów endpointów po stronie serwera.

**Mocne strony:** wyjątkowo spójny wzorzec pionowych wycinków (Contracts/Endpoint/Handler/Validator) powtórzony w ~45 funkcjach — agent łatwo go skopiuje, jeśli wie, gdzie patrzeć; centralna obsługa błędów przez middleware; testy integracyjne na `WebApplicationFactory` gotowe do rozszerzenia o moduł Kursy.

**Główne luki:** (1) autorska architektura modularnego monolitu nie jest nigdzie opisana dla agenta — w szczególności ręczna rejestracja endpointów i cicha pułapka fallbacku SPA; (2) konwencje frontendu i ryzyko generowania kodu pod Tailwind 3 / React Router 6; (3) ręcznie synchronizowane kontrakty C# ↔ TS; (4) brak testów frontendu i brak CI. Wszystkie pokrywają bloki powyżej — po wklejeniu ich do `CLAUDE.md` agent ma komplet informacji do dodania modułu Kursy bez naruszania modułów zachowanych przez PRD (FR-012–FR-014).

**Przy okazji:** ta ocena rozstrzyga Open Question nr 4 z PRD (architektura = modularny monolit ASP.NET Core 8 + React SPA serwowane z tego samego hosta) — warto przenieść to do sekcji Current System Overview w `prd.md`.

**Następny krok:** `/10x-health-check`.
