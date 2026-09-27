---
project: APPS (tomsoft1.pl — App01)
assessed_at: 2026-09-27T23:10:00Z
agent_readiness: ready-with-compensation
context_type: brownfield
stack_components:
  language: C# (.NET 10) + TypeScript 5.9
  framework: ASP.NET Core 10 Minimal APIs (modularny monolit, MediatR 12.5 + FluentValidation 12.1 + EF Core 10 / SQL Server) + React 19 SPA (React Router 7 deklaratywnie, Tailwind 4)
  build_tool: dotnet SDK 10 / MSBuild + Vite 7
  test_runner: xUnit 2.9.3 + WebApplicationFactory (serwer); Vitest 5 + jsdom + Testing Library (klient)
  package_manager: NuGet (lock files, --locked-mode) + npm (package-lock.json)
  ci_provider: GitHub Actions
  deployment_target: folder publish (FileSystem publish profile), SPA serwowane przez ASP.NET Core
gates_passed: 16
gates_failed: 0
---

## Stack Components

**Język (serwer) — C# na .NET 10.** Wszystkie 8 projektów w `APPS.sln` ma `<TargetFramework>net10.0</TargetFramework>` i `<Nullable>enable</Nullable>`. `global.json` przypina SDK `10.0.100` (`rollForward: latestMajor`). `Directory.Build.props` włącza `RestorePackagesWithLockFile`, `NuGetAuditMode=all` i traktuje podatności NU1903/NU1904 jako błędy.

**Język (klient) — TypeScript ~5.9.3.** `src/client/app01/tsconfig.app.json` ma `strict: true` (plus `noUnusedLocals`, `noUnusedParameters`, `verbatimModuleSyntax`).

**Framework (serwer) — ASP.NET Core 10 Minimal APIs, modularny monolit.** Host `App01.Bootstrapper.Api` składa moduły `Portal`, `Lotto`, `Flashcards`. Funkcje są cięte pionowo (`Features/<Nazwa>/{Contracts,Validator,Handler,Endpoint}.cs`), obsługiwane przez MediatR 12.5.0 + FluentValidation 12.1.1. Dane: EF Core 10.0.12 (SQL Server, `UseCompatibilityLevel(110)`), w testach EF InMemory. OpenAPI przez Swashbuckle 10.2.3, logowanie przez Serilog.AspNetCore 10.0.0. Integracja LLM: `IOpenRouterService` (`App01.Shared.Application/Interfaces/`) z implementacją w `App01.Shared.Infrastructure/Services/OpenRouterService.cs`, zarejestrowana jako scoped; `HttpClient` z domyślnej fabryki (`builder.Services.AddHttpClient()` w `Program.cs`).

**Framework (klient) — React 19 + React Router 7 (tryb deklaratywny) + Tailwind 4.** Markdown: `react-markdown` + `remark-gfm` + `rehype-highlight` + `rehype-raw` + `remark-frontmatter`. Poza tym `mermaid`, `hls.js`, `dayjs`, `react-datepicker`.

**Narzędzia budowania — dotnet SDK/MSBuild + Vite 7.** Po stronie klienta `npm run build` = `tsc -b && vite build --mode dev`, plus warianty `build:prod1` / `build:prod2`.

**Testy — xUnit 2.9.3** z `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, Moq, `coverlet.collector`; 25 plików `EndpointTests.cs` w `tests/server/App01/App01.Api.Tests/Features/<Moduł>/<Funkcja>/`, fabryki `TestWebApplicationFactory` i `ConfigurableTestWebApplicationFactory` w `Infrastructure/`. Frontend: Vitest 5 + jsdom + Testing Library (4 pliki testów obok kodu, setup `src/test/setup.ts`).

**CI/CD — GitHub Actions** (`.github/workflows/pull-request.yml`, każdy push/PR). Backend: `dotnet restore --locked-mode`, skan `dotnet list package --vulnerable --include-transitive` (fail na High/Critical), `dotnet format --verify-no-changes`, build, testy z pokryciem. Frontend: `npm ci`, `npm audit --audit-level=high`, `prettier --check`, `npm run lint`, `npm test`, `npm run build`.

**Wdrożenie** — profile `FolderProfile.pubxml` / `FolderProfile1.pubxml` (FileSystem, `bin\Release\net10.0\publish\`). SPA i pliki statyczne serwowane przez host (`app.UseStaticFiles(...)`, `app.MapFallbackToFile("index.html")`). Brak Dockerfile i konfiguracji PaaS.

**Pliki instrukcji** — `CLAUDE.md` (mapa repo, przepisy na slice i moduł — z przykładem „Courses", migracje, bezpieczeństwo, frontend, wersje, checklista CI, formatowanie) oraz `AGENTS.md` (odsyła do `CLAUDE.md`). `.editorconfig` z regułami stylu .NET.

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

Wynik: 16 z 16 kryteriów spełnionych, w tym 2 częściowo (dzięki `CLAUDE.md`). Żadne kryterium nie jest oblane. Ocena uwzględnia zakres PRD „Kursy i bootcampy" — dwie nowe luki (4 i 5) dotyczą wzorców, których nowy moduł potrzebuje, a których `CLAUDE.md` jeszcze nie opisuje.

### Gate Details

#### Typed

- C#: ✓ — `<Nullable>enable</Nullable>` we wszystkich 8 `.csproj` (np. `App01.Shared.Abstractions.csproj`, `App01.Bootstrapper.Api.Tests.csproj`). Kontrakty to `record`y w `Contracts.cs`.
- TypeScript: ✓ — `tsconfig.app.json`: `"strict": true`. Kontrakty API to `interface`y w `src/services/contracts/`.
- Uwaga: brak generatora kontraktów C# → TS. `CLAUDE.md` wymaga aktualizacji interfejsu TS w tej samej zmianie co rekord C#.
- Uwaga (zakres PRD): odpowiedź LLM to `Task<string>` (`IOpenRouterService.ChatAsync`) — nietypowana granica. Werdykt weryfikacji trzeba sparsować do typu po stronie handlera (Luka 4).

#### Convention-based

- ASP.NET Core Minimal APIs: ~ — Minimal APIs same nie narzucają układu ani rejestracji tras. Konwencje projektu są twarde i spisane w `CLAUDE.md` („przepis na wycinek funkcji", „przepis na nowy moduł (np. Courses)"); wzorzec referencyjny `App01.Modules.Portal/Features/UserList/` jest z nimi zgodny.
- React + Vite + React Router (deklaratywnie): ~ — brak routingu plikowego; kompensuje sekcja „Frontend" w `CLAUDE.md` (trasy w `src/main.tsx`, strony `src/pages/<moduł>/`, `Api<Moduł>Service` + `apiFetch`, kontrakty).
- dotnet SDK / Vite: ✓ — ustandaryzowany układ (`.csproj`/`.sln`, `vite.config.ts`).

#### Popular in training data (w obrębie rodziny języka)

- ASP.NET Core + MediatR + FluentValidation + EF Core: ✓ — kanoniczny zestaw „vertical slice" w C#.
- React 19 / React Router 7 / Tailwind 4 / Vite 7: ✓ — mainstream w JS/TS. Ryzyko: agent z przyzwyczajenia sięga po `react-router-dom`, `createBrowserRouter`, `tailwind.config.js` — `CLAUDE.md` już tego zabrania.
- xUnit: ✓ — domyślny wybór w .NET.
- Ryzyko wersji: .NET 10 / EF Core 10 słabiej reprezentowane niż .NET 8 (np. przestarzałe `.WithOpenApi()`); kompensuje sekcja „Wersje przypięte" w `CLAUDE.md`.

#### Well-documented

- ASP.NET Core / EF Core: ✓ — learn.microsoft.com, wersjonowane (`?view=aspnetcore-10.0`).
- MediatR (wiki GitHub, API 12.x), FluentValidation (docs.fluentvalidation.net): ✓.
- React / React Router („Declarative Mode") / Tailwind v4 / Vite: ✓.
- xUnit: ✓ — xunit.net, osobne sekcje v2 i v3.

## Gaps & Compensation

### Luka 1 — ZAMKNIĘTA: `CLAUDE.md` opisywał .NET 8

`CLAUDE.md` jest zsynchronizowany z .NET 10 (mapa repo, `net10.0` w przepisie na moduł, brak `.WithOpenApi()`, sekcja „Wersje przypięte"). W `src/server` jest 0 wywołań `WithOpenApi`.

### Luka 2 — SKOMPENSOWANA: Minimal APIs i Vite + React nie niosą konwencji same z siebie

Konwencje żyją w `CLAUDE.md`, nie we frameworku. Ryzyko resztkowe: każda zmiana wzorca (slice referencyjny, łańcuch endpointu, nowy katalog frontendu) wymaga aktualizacji `CLAUDE.md` w tej samej zmianie.

### Luka 3 — ZAMKNIĘTA: brak testów frontendu

Vitest 5 + jsdom + Testing Library działają, krok „Test frontend" jest w CI, reguły pisania testów są w `CLAUDE.md`.

### Luka 4 — NOWA (zakres PRD): brak wzorca weryfikacji odpowiedzi przez LLM

**Co brakuje.** FR-006 i US-01 wprowadzają pierwszą w portalu regułę domenową opartą o model: werdykt musi przyjść w < 5 s, awaria techniczna ma być komunikowana jako awaria (nie ocena negatywna), a próba techniczna nie może być liczona jako błędna. Obecny kod tego nie wspiera:
- `OpenRouterService.ChatAsync` rzuca gołe `Exception` przy błędzie API i zwraca `string`.
- `builder.Services.AddHttpClient()` — domyślny timeout `HttpClient` to 100 s.
- `ExceptionHandlingMiddleware` mapuje nieobsłużone wyjątki (w tym `ApiException`, który nie ma własnej gałęzi) na 500, a w `Detail` zwraca treść wyjątku.
- Żaden test nie podmienia `IOpenRouterService` — testy endpointu weryfikacji wołałyby prawdziwe API albo padały na braku konfiguracji.
- `CLAUDE.md` zabrania zmian w `ExceptionHandlingMiddleware` i istniejących modułach (Flashcards używa tego samego serwisu).

**Dlaczego to ważne dla agenta.** Bez spisanej reguły agent najpewniej zawoła `ChatAsync` bez limitu czasu, porówna string „na oko" i przepuści wyjątek do middleware (500 z treścią błędu) — co łamie trzy kryteria akceptacji US-01 naraz. Druga pokusa to „poprawienie" `OpenRouterService` lub middleware, co narusza zakaz regresji Flashcards.

**Kompensacja.** Reguła w `CLAUDE.md` (niżej): limit czasu przez `CancellationTokenSource.CancelAfter` w handlerze, trójstanowy wynik w `Response` zamiast wyjątku, ustrukturyzowany (JSON) werdykt modelu z odpornym parsowaniem, izolacja odpowiedzi uczestnika w prompcie, blokada ponownej weryfikacji zdobytej flagi przed wywołaniem modelu, stub `IOpenRouterService` w testach.

### Luka 5 — NOWA (zakres PRD): publikacja treści poza aplikacją a publiczne pliki statyczne

**Co brakuje.** FR-010 zakłada wgrywanie treści i grafik kursu poza interfejsem aplikacji. Host ma `app.UseStaticFiles(...)` bez autoryzacji — wszystko, co trafi do `wwwroot`, jest dostępne dla każdego. Guardrail PRD: „treść kursów nie jest dostępna dla niezalogowanych; publiczne pozostają wyłącznie kafelki". `CLAUDE.md` nie mówi, gdzie leżą pliki kursów ani jak są serwowane.

**Dlaczego to ważne dla agenta.** Najprostsza droga dla agenta to wrzucenie `.md` do `wwwroot/courses/` i pobranie go `fetch`em z klienta — działa od razu i po cichu wystawia treść publicznie.

**Kompensacja.** Reguła w `CLAUDE.md` (niżej): treść Markdown poza `wwwroot`, ścieżka z konfiguracji, odczyt wyłącznie przez endpoint z `RequireAuthorization()` i filtrem daty publikacji; publicznie tylko grafiki kafelków.

### Obserwacja poza kryteriami (do `/10x-health-check`)

`ExceptionHandlingMiddleware` zwraca w 500 `Detail` z `exception.Message` i dwoma poziomami `InnerException` — to ujawnia szczegóły wewnętrzne klientowi. Zmiana middleware jest poza zakresem PRD (zakaz modyfikacji), stąd tylko odnotowanie.

### Recommended Instruction File Additions

Zalecenia z wcześniejszej oceny (sekcje „Wersje przypięte", poprawki punktowe, „Formatowanie", pełna lista kroków CI) są już w `CLAUDE.md` ✓.

Nowe — do wklejenia w `CLAUDE.md` po sekcji „Bezpieczeństwo endpointów":

```markdown
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
```

## Summary

**Ogólna gotowość: ready-with-compensation.** Wszystkie 16 kryteriów jest spełnionych, 2 częściowo — konwencje Minimal APIs i Vite + React dostarcza aktualny `CLAUDE.md`.

**Mocne strony:**
- Typowanie end-to-end: C# z nullable i TypeScript strict.
- Mainstreamowe, dobrze udokumentowane technologie w obu ekosystemach.
- Szczegółowy `CLAUDE.md` z przepisami na slice i moduł (z przykładem „Courses"), regułami bezpieczeństwa dla publicznych kafelków i unikalności flag.
- Testy po obu stronach i pełne CI (locked restore, skan podatności, format, lint, build, testy).

**Kluczowe luki w zakresie PRD:**
1. Brak wzorca weryfikacji przez LLM (timeout, awaria ≠ ocena negatywna, prompt injection, stub w testach) — Luka 4.
2. Ryzyko publicznego wystawienia treści kursów przez `UseStaticFiles` — Luka 5.

Obie kompensuje blok „Moduł Courses" do wklejenia w `CLAUDE.md`.

**Następny krok:** `/10x-health-check`.
