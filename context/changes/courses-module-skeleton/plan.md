# Szkielet modułu Courses — plan implementacji

## Overview

Dodajemy nowy moduł `App01.Modules.Courses` do modularnego monolitu i wpinamy go w host (`App01.Bootstrapper.Api`) oraz w projekt testów. Moduł ma jeden techniczny endpoint `ModuleHello` (`GET api/courses/module-hello`), który zwraca `"Hello from module Courses!"`. Dowodzi on, że rejestracja serwisów i endpointów modułu działa end-to-end, i daje gotowy szablon testów (JWT + X-TOKEN) dla kolejnych wycinków. To foundation F-01 z roadmapy: odblokowuje S-01 i S-03 do pracy równoległej, bez konfliktu dwóch zmian zakładających ten sam moduł.

## Current State Analysis

- Host składa trzy moduły: Portal, Lotto, Flashcards. Rejestracja serwisów jest w `src/server/App01/App01.Bootstrapper.Api/Program.cs:30-32`, endpointów w `Program.cs:102-104`, a fallback SPA (`MapFallbackToFile("index.html")`) w `Program.cs:108`.
- Najmniejszy istniejący moduł to `App01.Modules.Flashcards`: csproj z jedną `ProjectReference` do `App01.Shared.Infrastructure` i `ModuleDI.cs` z `AddModuleFlashcardsServices` (MediatR + FluentValidation z `Assembly.GetExecutingAssembly()`) oraz `UseModuleFlashcardsEndpoints`.
- Projekt testów `tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj` referencjonuje Bootstrapper, Lotto, Portal i Shared.Infrastructure, ale nie Flashcards. Instrukcje repo każą nie powielać tej luki dla Courses.
- Pakiety są przypięte lock-file'ami (`Directory.Build.props`: `RestorePackagesWithLockFile`), a CI robi `dotnet restore --locked-mode`. Każdy projekt ma własny `packages.lock.json`.
- Modułu Courses, jego encji, konfiguracji i kluczy w `appsettings.Example.json` nie ma. Po stronie klienta `Layout.tsx:580` ma już zaczątek pod ścieżkę `/courses`, ale tej zmiany to nie dotyczy.

## Desired End State

- `App01.Modules.Courses` istnieje jako osobny projekt w `APPS.sln` (folder solucji `src/server/App01`) i jest referencjonowany przez host oraz projekt testów.
- `GET api/courses/module-hello` z ważnym JWT i nagłówkiem `X-TOKEN` zwraca `200 OK` z JSON `{ "message": "Hello from module Courses!" }`. Bez JWT zwraca `401`, z JWT ale bez `X-TOKEN` zwraca `403`.
- Portal, Lotto i Flashcards działają bez zmian: wszystkie istniejące testy przechodzą, a trasy SPA nadal zwracają `index.html`.
- Pełny zestaw kroków CI przechodzi lokalnie.

### Key Discoveries:

- Wzorzec modułu: `src/server/App01/App01.Modules.Flashcards/App01.Modules.Flashcards.csproj` i `src/server/App01/App01.Modules.Flashcards/ModuleDI.cs`.
- Wzorzec wycinka (4 pliki, kolejność walidacji, łańcuch metadanych endpointu): `src/server/App01/App01.Modules.Portal/Features/UserList/`.
- Wzorzec testu z JWT + X-TOKEN (`WithWebHostBuilder`, `Tokens:X-TOKEN = "test-x-token"`, `GenerateJwtToken`, usunięcie `IHostedService`, InMemory DB): `tests/server/App01/App01.Api.Tests/Features/Portal/UserList/EndpointTests.cs`.
- Brak/błędny `X-TOKEN` daje `ForbiddenException` (→ 403) w `App01.Bootstrapper.Api/Services/XTokenService.cs`. Brak JWT daje 401 z middleware uwierzytelniania, zanim zadziała filtr.
- Niezarejestrowany endpoint nie zwraca 404, tylko trafia w fallback SPA i zwraca HTML. Test sukcesu musi więc sprawdzać treść JSON, nie tylko status 200.

## What We're NOT Doing

- Żadnej logiki domenowej Kursów: encji, konfiguracji EF, `DbSet`, migracji, kafelków, treści, flag. To zakres S-01 i S-03.
- Żadnych kluczy `Courses:*` w `appsettings.Example.json`. `Courses:ContentPath` dopisze S-03, a `Courses:VerificationTimeoutSeconds` dopisze S-01, każdy razem z kodem, który go czyta.
- Żadnych zmian we frontendzie: menu, trasy i serwis API dla Kursów należą do S-03.
- Nie domykamy luki z brakującą referencją do Flashcards w projekcie testów (osobna zmiana).
- Nie zmieniamy istniejących modułów, tras, `ExceptionHandlingMiddleware`, `OpenRouterService` ani `AddHttpClient()`.
- Nie dodajemy workerów (`AddModuleCoursesWorkers`), bo moduł ich nie potrzebuje.

## Implementation Approach

Kopiujemy kształt modułu Flashcards i wycinka UserList, zmieniając tylko nazwy. Endpoint `ModuleHello` przechodzi przez pełną ścieżkę (MediatR → handler z ręczną walidacją → `Results.Ok`), więc jego test dowodzi naraz rejestracji MediatR, walidatorów i endpointów modułu. Faza 1 wpina moduł w host i sprawdza, że aplikacja startuje bez regresji. Faza 2 dodaje referencję w projekcie testów i testy endpointu.

## Critical Implementation Details

- **Kolejność w `Program.cs`:** `app.UseModuleCoursesEndpoints();` musi stać przed `app.MapFallbackToFile("index.html")`. Inaczej żądanie trafi w SPA i dostanie HTML ze statusem 200, co bez sprawdzenia treści wygląda jak sukces.
- **Lock-file'y:** po dodaniu projektu i referencji `dotnet restore` wygeneruje `src/server/App01/App01.Modules.Courses/packages.lock.json` i zaktualizuje lock-file'y hosta (faza 1) oraz projektu testów (faza 2). Wszystkie trzeba zacommitować, bo inaczej CI (`--locked-mode`) odrzuci zmianę.

## Phase 1: Moduł Courses w hoście

### Overview

Tworzymy projekt modułu z `ModuleDI` i wycinkiem `ModuleHello`, dodajemy go do solucji i wpinamy w host. Na koniec fazy aplikacja startuje z nowym modułem, endpoint odpowiada, a istniejące testy przechodzą.

### Changes Required:

#### 1. Projekt modułu

**File**: `src/server/App01/App01.Modules.Courses/App01.Modules.Courses.csproj`

**Intent**: Nowy projekt biblioteki modułu, kopia `App01.Modules.Flashcards.csproj`.

**Contract**: `Microsoft.NET.Sdk`, `net10.0`, `ImplicitUsings` i `Nullable` włączone, jedna `ProjectReference` do `..\App01.Shared.Infrastructure\App01.Shared.Infrastructure.csproj`. Żadnych nowych `PackageReference` (pakiety idą przez `Shared.Abstractions`).

#### 2. Wpis w solucji

**File**: `APPS.sln`

**Intent**: Dodać projekt do solucji w tym samym folderze co pozostałe moduły.

**Contract**: `dotnet sln APPS.sln add src/server/App01/App01.Modules.Courses/App01.Modules.Courses.csproj --solution-folder src/server/App01`. Wpis ma trafić do istniejącego folderu solucji, nie do nowego.

#### 3. Rejestracja modułu

**File**: `src/server/App01/App01.Modules.Courses/ModuleDI.cs`

**Intent**: Punkt wejścia modułu dla hosta, analogiczny do Flashcards.

**Contract**: `namespace App01.Modules.Courses; public static class ModuleDI` z metodami `AddModuleCoursesServices(this IServiceCollection)` (MediatR + `AddValidatorsFromAssembly` z `Assembly.GetExecutingAssembly()`) i `UseModuleCoursesEndpoints(this WebApplication)`, która wywołuje `Features.ModuleHello.Endpoint.AddEndpoint(app);` i zwraca `app`.

#### 4. Wycinek ModuleHello

**File**: `src/server/App01/App01.Modules.Courses/Features/ModuleHello/{Contracts,Validator,Handler,Endpoint}.cs`

**Intent**: Techniczny endpoint dowodzący rejestracji modułu. Dokładnie cztery pliki według przepisu na wycinek w `CLAUDE.md`.

**Contract**:
- Namespace `App01.Modules.Courses.Features.ModuleHello`.
- `Contracts`: `public record Request() : IRequest<Response>;`, `public record Response(string Message);`.
- `Validator : AbstractValidator<Contracts.Request>` bez reguł, bo request nie ma pól. Plik istnieje dla spójności wzorca i rejestracji `IValidator<Contracts.Request>`.
- `ModuleHelloHandler : IRequestHandler<Contracts.Request, Contracts.Response>` wstrzykuje `ILogger<ModuleHelloHandler>` i `IValidator<Contracts.Request>`. Pierwszy krok `Handle` to ręczna walidacja (`ValidationException` przy błędzie), potem zwraca `new Contracts.Response("Hello from module Courses!")`. Nie wstrzykuje `AppDbContext`, bo nie używa bazy.
- `Endpoint.AddEndpoint`: `app.MapGet("api/courses/module-hello", ...)` → `mediator.Send(new Contracts.Request())` → `Results.Ok(result)`; łańcuch `.WithName("CoursesModuleHello")`, `.WithTags("Courses")`, `.Produces<Contracts.Response>(StatusCodes.Status200OK)`, `.Produces(StatusCodes.Status401Unauthorized)`, `.Produces(StatusCodes.Status403Forbidden)`, `.AddEndpointFilter<XTokenFilter>()`, `.RequireAuthorization()`. Bez `.WithOpenApi()`.

#### 5. Wpięcie w host

**File**: `src/server/App01/App01.Bootstrapper.Api/App01.Bootstrapper.Api.csproj`, `src/server/App01/App01.Bootstrapper.Api/Program.cs`

**Intent**: Host referencjonuje moduł i rejestruje jego serwisy i endpointy obok pozostałych modułów.

**Contract**:
- csproj: `ProjectReference` do `..\App01.Modules.Courses\App01.Modules.Courses.csproj`.
- `Program.cs`: `using App01.Modules.Courses;` w grupie z pozostałymi modułami; `builder.Services.AddModuleCoursesServices();` po `AddModuleFlashcardsServices()`; `app.UseModuleCoursesEndpoints();` po `UseModuleFlashcardsEndpoints()` i przed `MapFallbackToFile`. Blok workerów bez zmian.

#### 6. Lock-file'y

**File**: `src/server/App01/App01.Modules.Courses/packages.lock.json` (nowy), `src/server/App01/App01.Bootstrapper.Api/packages.lock.json`

**Intent**: Zapisać przypięte zależności nowego projektu i zaktualizowany graf hosta.

**Contract**: wygenerowane przez `dotnet restore`; po wygenerowaniu `dotnet restore --locked-mode` przechodzi.

### Success Criteria:

#### Automated Verification:

- Przywracanie w trybie zablokowanym przechodzi: `dotnet restore --locked-mode`
- Build solucji przechodzi bez nowych ostrzeżeń `obsolete`: `dotnet build APPS.sln`
- Wszystkie istniejące testy przechodzą: `dotnet test APPS.sln`
- Formatowanie zgodne z `.editorconfig`: `dotnet format APPS.sln --verify-no-changes`

#### Manual Verification:

- Aplikacja startuje lokalnie z modułem Courses, a `GET /api/courses/module-hello` z JWT i `X-TOKEN` zwraca JSON `{ "message": "Hello from module Courses!" }` (Swagger lub curl)
- Logowanie, Apki (Lotto), Gry i ekrany administratora działają jak przed zmianą, a trasa SPA (np. `/courses`) nadal zwraca `index.html`

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się i poczekaj na potwierdzenie testów ręcznych przed fazą 2.

---

## Phase 2: Testy modułu w projekcie testów

### Overview

Projekt testów referencjonuje moduł Courses, a `EndpointTests.cs` pokrywa endpoint `ModuleHello`. To szablon testów dla S-01 i S-03. Na koniec fazy przechodzi pełny zestaw kroków CI.

### Changes Required:

#### 1. Referencja w projekcie testów

**File**: `tests/server/App01/App01.Api.Tests/App01.Bootstrapper.Api.Tests.csproj`, `tests/server/App01/App01.Api.Tests/packages.lock.json`

**Intent**: Testy mają bezpośredni dostęp do typów modułu Courses (np. `Contracts.Response`), tak jak do Portal i Lotto.

**Contract**: `ProjectReference` do `..\..\..\..\src\server\App01\App01.Modules.Courses\App01.Modules.Courses.csproj`; zaktualizowany lock-file. Referencji do Flashcards nie dodajemy.

#### 2. Testy endpointu ModuleHello

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/ModuleHello/EndpointTests.cs`

**Intent**: Udowodnić, że endpoint modułu jest zarejestrowany i chroniony jak każdy endpoint dla zalogowanych.

**Contract**: namespace `App01.Bootstrapper.Api.Tests.Features.Courses.ModuleHello`, klasa `EndpointTests : IClassFixture<WebApplicationFactory<Program>>` na wzór `Features/Portal/UserList/EndpointTests.cs` (konfiguracja JWT + `Tokens:X-TOKEN`, usunięcie hosted services, InMemory DB, helper `GenerateJwtToken`). Przypadki:
- `ModuleHello_WithJwtAndXToken_ReturnsOkWithMessage`: 200, a deserializowany `Contracts.Response.Message` równa się `"Hello from module Courses!"` (sprawdzenie treści odróżnia endpoint od fallbacku SPA).
- `ModuleHello_WithoutJwtToken_ReturnsUnauthorized`: 401.
- `ModuleHello_WithInvalidJwtToken_ReturnsUnauthorized`: 401.
- `ModuleHello_WithoutXToken_ReturnsForbidden`: ważny JWT, brak `X-TOKEN`, wynik 403.
- Przypadek 400 nie dotyczy: request nie ma pól, więc walidacja nie ma czego odrzucić.

### Success Criteria:

#### Automated Verification:

- Przywracanie w trybie zablokowanym przechodzi: `dotnet restore --locked-mode`
- Build solucji przechodzi: `dotnet build APPS.sln`
- Wszystkie testy przechodzą, w tym 4 nowe testy `ModuleHello`: `dotnet test APPS.sln`
- Formatowanie zgodne z `.editorconfig`: `dotnet format APPS.sln --verify-no-changes`
- Kroki CI klienta przechodzą bez zmian (z `src/client/app01`): `npx prettier --check "src/**/*.{ts,tsx,css}"`, `npm run lint`, `npm test`, `npm run build`

#### Manual Verification:

- W wynikach `dotnet test` widać 4 testy z `Features.Courses.ModuleHello.EndpointTests` i żaden nie jest pominięty

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się i poczekaj na potwierdzenie przed zamknięciem zmiany.

---

## Testing Strategy

### Unit Tests:

- Brak. Handler nie ma logiki wartej osobnego testu jednostkowego.

### Integration Tests:

- `EndpointTests.cs` dla `ModuleHello` przez `WebApplicationFactory<Program>`: sukces z treścią, 401 bez JWT, 401 z błędnym JWT, 403 bez `X-TOKEN`.
- Istniejący zestaw testów Portal/Lotto jako test regresji FR-012, FR-013, FR-014.

### Manual Testing Steps:

1. Uruchom host lokalnie, zaloguj się i wywołaj `GET /api/courses/module-hello` z `Authorization: Bearer <jwt>` i `X-TOKEN`. Oczekiwany wynik: JSON z wiadomością.
2. Wywołaj ten sam endpoint bez `X-TOKEN` (403) i bez JWT (401).
3. Przejdź przez logowanie, Apki, Gry i ekrany Users/Rejestracja jako admin i jako zwykły użytkownik. Zachowanie ma być takie jak przed zmianą.
4. Otwórz w przeglądarce `/courses`. SPA ładuje się jak wcześniej.

## Performance Considerations

Brak. Jeden dodatkowy skan assembly przez MediatR/FluentValidation przy starcie, bez wpływu na czas odpowiedzi.

## Migration Notes

Brak zmian w bazie i migracji. Wdrożenie: nowy projekt jest publikowany razem z hostem przez `ProjectReference`. Wycofanie to revert commitów zmiany.

## References

- Roadmapa: `context/foundation/roadmap.md` (F-01)
- PRD: `context/foundation/prd.md` (FR-012, FR-013, FR-014, Constraints & Compatibility)
- Wzorzec modułu: `src/server/App01/App01.Modules.Flashcards/ModuleDI.cs`
- Wzorzec wycinka: `src/server/App01/App01.Modules.Portal/Features/UserList/`
- Wzorzec testu: `tests/server/App01/App01.Api.Tests/Features/Portal/UserList/EndpointTests.cs`
- Rejestracja w hoście: `src/server/App01/App01.Bootstrapper.Api/Program.cs:30-32`, `:102-108`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Moduł Courses w hoście

#### Automated

- [x] 1.1 Przywracanie w trybie zablokowanym przechodzi: `dotnet restore --locked-mode`
- [x] 1.2 Build solucji przechodzi bez nowych ostrzeżeń `obsolete`: `dotnet build APPS.sln`
- [x] 1.3 Wszystkie istniejące testy przechodzą: `dotnet test APPS.sln`
- [x] 1.4 Formatowanie zgodne z `.editorconfig`: `dotnet format APPS.sln --verify-no-changes`

#### Manual

- [x] 1.5 Aplikacja startuje lokalnie z modułem Courses, a `GET /api/courses/module-hello` z JWT i `X-TOKEN` zwraca JSON `{ "message": "Hello from module Courses!" }` (Swagger lub curl)
- [x] 1.6 Logowanie, Apki (Lotto), Gry i ekrany administratora działają jak przed zmianą, a trasa SPA (np. `/courses`) nadal zwraca `index.html`

### Phase 2: Testy modułu w projekcie testów

#### Automated

- [ ] 2.1 Przywracanie w trybie zablokowanym przechodzi: `dotnet restore --locked-mode`
- [ ] 2.2 Build solucji przechodzi: `dotnet build APPS.sln`
- [ ] 2.3 Wszystkie testy przechodzą, w tym 4 nowe testy `ModuleHello`: `dotnet test APPS.sln`
- [ ] 2.4 Formatowanie zgodne z `.editorconfig`: `dotnet format APPS.sln --verify-no-changes`
- [ ] 2.5 Kroki CI klienta przechodzą bez zmian (z `src/client/app01`): `npx prettier --check "src/**/*.{ts,tsx,css}"`, `npm run lint`, `npm test`, `npm run build`

#### Manual

- [ ] 2.6 W wynikach `dotnet test` widać 4 testy z `Features.Courses.ModuleHello.EndpointTests` i żaden nie jest pominięty
