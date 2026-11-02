---
title: "Anti-Corruption Layer — dostawca LLM (OpenRouter) za wąskim portem"
created: 2026-10-15
type: refactor-plan
scope: "Shared.Application (port), Shared.Infrastructure (adapter), Modules.Courses (VerifyAnswer), Modules.Flashcards (Generate)"
baseline_commit: 7daf069
depends_on:
  - context/domain/01-domain-distillation.md
  - context/domain/02-invariant-aggregate-refactor.md
status: draft — Phase 5 (Flashcards) czeka na zgodę właściciela
---

# Anti-Corruption Layer — dostawca LLM

> Plan, nie implementacja. Kod produkcyjny nie został zmieniony.
> Skróty ścieżek: `Srv/…` = `src/server/App01/…`, `Courses/…` = `src/server/App01/App01.Modules.Courses/…`, `Tests/…` = `tests/server/App01/App01.Api.Tests/…`, `client/…` = `src/client/app01/src/…`.

## Krok 0 — Kontekst

**Dokumenty bazowe.** Czytane: `context/foundation/prd.md`, `stack-assessment.md`, `test-plan.md`, `infrastructure.md`, `CLAUDE.md`. Brak `tech-stack.md`. Żaden dokument nie mówi wprost „ma być wymienialne”, ale dwa źródła ustawiają intencję:

- PRD opisuje zależność **neutralnie wobec dostawcy**: „Weryfikację wykonuje zewnętrzny model/API, więc przekroczenie limitu czasu jest dopuszczonym ryzykiem” (`context/foundation/prd.md:80`) i „ocena modelem” (`prd.md:120`). Nazwa dostawcy w PRD nie występuje.
- `CLAUDE.md:92` deklaruje, że integracja **jest już odseparowana i współdzielona**: „Weryfikację wykonuje handler … przez istniejący `IOpenRouterService`. NIE zmieniaj `OpenRouterService`, `AddHttpClient()` … — używa ich Flashcards.” To samo stoi w `context/foundation/stack-assessment.md:24` i `:131`. Kod tego nie dotrzymuje (Krok 3).
- `context/foundation/stack-assessment.md:61` już nazywa lukę: „odpowiedź LLM to `Task<string>` … nietypowana granica”.

**Stack i warstwy.** ASP.NET Core 10, modularny monolit. Kierunek zależności: Modules → Shared.Infrastructure → Shared.Application → Shared.Abstractions (`CLAUDE.md`, mapa repozytorium). Wszystkie pakiety NuGet są w jednym manifeście `Srv/App01.Shared.Abstractions/App01.Shared.Abstractions.csproj` i trafiają tranzytywnie do każdej warstwy. Klient: `src/client/app01/package.json` (React 19, react-markdown + remark/rehype, highlight.js, hls.js, mermaid, dayjs).

**Zależności zewnętrzne (manifesty):** BCrypt.Net-Next, FluentValidation, MediatR, JwtBearer, EF Core (+SqlServer, InMemory, Design, Tools), Newtonsoft.Json, Serilog, Swashbuckle, YamlDotNet. Do tego **HTTP API OpenRouter**, które nie jest pakietem: wołamy je ręcznie przez `HttpClient` (konfiguracja `Srv/App01.Bootstrapper.Api/appsettings.Example.json:31-35`).

## Krok 1 — Przeciekające zależności

Liczby plików z `grep -rl "using <pakiet>"` po `src/server` i `tests/server`, bez `obj/`, `bin/` i migracji.

| ID | Zależność | Kto ją dziś zna (plik:linia) | Sygnał przecieku |
| --- | --- | --- | --- |
| **L-01** | **OpenRouter chat-completions API** | `Srv/App01.Shared.Infrastructure/Services/OpenRouterService.cs:30-87` (URL, klucz Base64, `messages/role/content`, `choices[0].message.content`); `Srv/App01.Modules.Flashcards/Features/Generate/Handler.cs:59-131` (**pełna kopia**); `Srv/App01.Shared.Application/Interfaces/IOpenRouterService.cs:5-7` (port nazwany od dostawcy, sygnatura w kształcie API czatu: `model`, lista wiadomości); `Srv/App01.Shared.Application/Models/AI/ChatMessage.cs:3` (`Role` jako string protokołu); `Courses/Features/VerifyAnswer/Handler.cs:38`, `:48`, `:122-123` (`"system"`/`"user"`), `:131` (`model: null`), `:177-182` (obejście dziwactwa modelu: ogrodzenie Markdown); `Srv/App01.Shared.Infrastructure/SharedInfrastructureDI.cs:44`; `Tests/Features/Courses/VerifyAnswer/EndpointTests.cs:70`, `:126-127`, `:207-225`, `:376-378`, `:422-426`, `:455-458` (24 linie); `Srv/App01.Shared.Application/Middlewares/ExceptionHandlingMiddleware.cs:163` (komunikat wyjątku dostawcy w `Detail` odpowiedzi 500) | duplikacja rekonstrukcji formatu wire dostawcy w 2 plikach; typy protokołu w porcie aplikacyjnym; nazwa dostawcy w 5 plikach `.cs` + teście; tekst dostawcy w kontrakcie wire 500 |
| L-02 | MediatR 12 | 117 plików; wszystkie rekordy wire `Contracts.Request` implementują `IRequest<…>` (39 plików `Contracts.cs` z `using MediatR`), np. `Courses/Features/VerifyAnswer/Contracts.cs:1`, `:9-12`; endpointy bindują ten typ wprost z body (`Courses/Features/VerifyAnswer/Endpoint.cs:16`) | typ biblioteki w kontrakcie wire |
| L-03 | EF Core | 88 plików; `AppDbContext` wstrzykiwany do każdego handlera (`Courses/Features/ActivateFlag/Handler.cs:23`) | ORM w warstwie aplikacji (świadoma konwencja `CLAUDE.md`, przepis na vertical slice, pkt 3) |
| L-04 | FluentValidation | 78 plików; `ValidationException` rzucany z handlerów i mapowany w middleware (`ExceptionHandlingMiddleware.cs:53`) | wyjątek biblioteki jako kontrakt błędu |
| L-05 | BCrypt.Net-Next | 5 handlerów Portalu wołają `BCrypt.Net.BCrypt` w pełnej kwalifikacji: `Srv/App01.Modules.Portal/Features/UserLogin/Handler.cs:53`, `UserPassChange/Handler.cs:50`, `:62`, `UserPassReset/Handler.cs:59`, `UserRegister/Handler.cs:50`, `UsersRegister/Handler.cs:91` | format hasha persystowany w `User.PasswordHash`, brak portu |
| L-06 | Kształt claimu JWT `isAdmin` | serwer `Srv/App01.Shared.Infrastructure/Services/JwtService.cs:46`, `:84`; klient ręcznie dekoduje payload `client/utils/jwt.ts:11-12` | ta sama wiedza po obu stronach granicy klient/serwer |
| L-07 | YAML frontmatter | serwer: YamlDotNet **tylko** w `Courses/Content/CourseFrontmatterReader.cs:7-8`, `:32-35`; klient: własny parser `client/utils/parseFrontmatter.ts:6-30`, używany wyłącznie przez swój test (`parseFrontmatter.test.ts:3`) | serwer już ma ACL; klient trzyma martwą drugą kopię |
| L-08 | Newtonsoft.Json | zadeklarowany w `App01.Shared.Abstractions.csproj`, **0 użyć** (`grep "Newtonsoft\|JsonConvert"` bez trafień) | martwa zależność |
| — | react-markdown + remark/rehype | jeden komponent `client/components/CourseMarkdown.tsx:1-4`, `:116-119` | **już izolowane** — wzorzec do naśladowania, nie przeciek |

## Krok 2 — Klasyfikacja i wybór #1

| ID | (a) warstwy / pliki | (b) ryzyko i koszt wymiany dziś | (c) intencja w dokumentach vs kod |
| --- | --- | --- | --- |
| **L-01** | **4 warstwy** (port aplikacyjny, adapter infrastruktury, 2 moduły, testy, wire 500) / **7 plików** | **wysokie**: zmiana dostawcy, np. na API z `system` na najwyższym poziomie i odpowiedzią `content[0].text`, wymaga zmiany adaptera **i** handlera Flashcards (osobna kopia), **i** handlera Courses (role jako stringi), **i** 24 linii mocków. Flashcards nie ma testów (`ls Tests/Features/` → tylko `Courses`, `Lotto`, `Portal`), więc regresji FR-013 nic nie złapie | **rozjazd**: PRD mówi o „zewnętrznym modelu/API” (`prd.md:80`), a `CLAUDE.md:92` twierdzi, że Flashcards używa `OpenRouterService`. Nie używa |
| L-02 | 3 warstwy / 117 plików | wysokie (pin v12 przez licencję v13+, `CLAUDE.md`, sekcja „Wersje przypięte”) | brak intencji wymiany: MediatR **jest** konwencją projektu; wymiana to zmiana architektury, nie ACL |
| L-03 | 2–3 / 88 | wysokie | brak intencji: konwencja |
| L-04 | 3 / 78 | średnie | brak intencji: konwencja |
| L-05 | 1 moduł / 6 plików | średnie (migracja hashy) | brak deklaracji; poza zakresem Kursów |
| L-06 | 2 strony granicy / 3 miejsca | niskie | brak |
| L-07 | serwer 1 plik (OK), klient martwy kod | niskie | `CLAUDE.md` (Frontend): „nie dodawaj innego parsera” — klient ma drugi, nieużywany |
| L-08 | 0 | zerowe | — |

**Wybór #1: L-01, dostawca LLM (OpenRouter).**

1. To jedyny przeciek, w którym **dokumenty twierdzą, że separacja istnieje, a kod jej przeczy**. `CLAUDE.md:92` i `stack-assessment.md:131` budują regułę „nie zmieniaj `OpenRouterService`, bo używa go Flashcards” na fałszywej przesłance. Agent posłuszny tej regule chroni plik, którego Flashcards w ogóle nie dotyka.
2. Zależność dotyka **rdzenia** (SD-01/SD-02 w `01-domain-distillation.md`: werdykt weryfikacji), a nie tylko infrastruktury. Kształt protokołu czatu (`"system"`, `"user"`, `model`) i dziwactwa modelu (ogrodzenie Markdown) żyją w handlerze domenowym.
3. Wymiana jest realna i tania do przewidzenia. Model jest konfigurowalny (`appsettings.Example.json:34`), PRD przyjmuje awarie dostawcy jako ryzyko (`prd.md:80`), a OpenRouter sam jest pośrednikiem, którego można chcieć pominąć.
4. L-02…L-04 są szersze, ale są **świadomymi konwencjami** opisanymi w `CLAUDE.md`. ACL wokół mediatora czy ORM-u to przebudowa architektury, a nie ochrona domeny. L-05 i L-06 dostają osobne wpisy w „Następne w kolejce”.

## Krok 3 — Diagnoza L-01

### Duplikacja (dwie niezależne rekonstrukcje protokołu dostawcy)

| Wiedza o dostawcy | `OpenRouterService.cs` | `Flashcards/Features/Generate/Handler.cs` |
| --- | --- | --- |
| klucze konfiguracji `OpenRouter:Url/ApiKey/Model` | `:30-32` | `:60-62` |
| walidacja konfiguracji, goły `Exception` | `:34-37` | `:64-67` |
| dekodowanie klucza z Base64 (konwencja projektu, nie wymóg dostawcy) | `:39-50` | `:70-82` |
| ciało żądania `{ model, messages: [{ role, content }] }` | `:52-61` | `:93-104` |
| nagłówek `Authorization: Bearer` na współdzielonym `HttpClient` z fabryki | `:63-64` | `:93`, `:109` |
| mapowanie błędu HTTP → `Exception("OpenRouter API error: …")` | `:69-74` | `:113-119` |
| wyłuskanie `choices[0].message.content` | `:78-83` | `:124-129` |

Te same 7 decyzji w dwóch miejscach. Jest też jedna różnica, która już się rozjechała: Flashcards ignoruje parametr modelu, a Courses przekazuje `null` (`VerifyAnswer/Handler.cs:131`), żeby wymusić model z konfiguracji.

### Przecieki przez granice

| Granica | Co przecieka | Dowód |
| --- | --- | --- |
| Infrastruktura → port aplikacyjny | nazwa dostawcy i kształt API czatu (`model`, `historyMessages`, `prompt`) | `IOpenRouterService.cs:5-7` |
| Port → model aplikacyjny | rola wiadomości jako surowy string protokołu | `ChatMessage.cs:3` |
| Port → domena Courses | handler buduje wiadomości protokołu i zna stringi ról | `VerifyAnswer/Handler.cs:122-123` |
| Dostawca → domena Courses | obejście zachowania modelu (JSON w ogrodzeniu ```json) w parserze werdyktu | `VerifyAnswer/Handler.cs:177-182`, `:209-210` |
| Dostawca → moduł Flashcards | cały protokół HTTP w handlerze funkcji, z pominięciem portu | `Flashcards/Features/Generate/Handler.cs:59-131` |
| Dostawca → kontrakt wire (500) | tekst „OpenRouter API error: …” / „…from OpenRouter response” trafia do `ProblemDetails.Detail` | `Flashcards/…/Handler.cs:118`, `:133`, `:141` → `ExceptionHandlingMiddleware.cs:163`. Klient go nie wyświetla, bo czyta `errorData?.message` zamiast `detail` (`client/services/api-flashcards-service.ts:56-59`), ale dostaje go w surowym JSON |
| Dostawca → testy | 24 linie z `IOpenRouterService`/`ChatMessage`; testy promptu sprawdzają role protokołu | `Tests/Features/Courses/VerifyAnswer/EndpointTests.cs:418-458` |

**Granica klient/serwer:** w bundlu klienta nie ma ani biblioteki, ani nazwy dostawcy (`grep -rli openrouter src/client/app01/src` → 0 trafień; `infrastructure.md:93` potwierdza, że klucz jest wyłącznie na VM). Do przeglądarki nie wycieka pakiet, tylko tekst błędu 500 (wiersz wyżej).

### Deklaracja vs kod

- `CLAUDE.md:92`: „NIE zmieniaj `OpenRouterService` … — używa ich Flashcards”. Kod: Flashcards wstrzykuje `IHttpClientFactory` (`Flashcards/…/Handler.cs:26`, `:34`) i nie odwołuje się do `IOpenRouterService` (`grep IOpenRouterService` → tylko Courses, port, DI i test Courses).
- `stack-assessment.md:24` opisuje `IOpenRouterService` jako „integrację LLM” portalu. W praktyce to integracja jednego z dwóch konsumentów.
- `prd.md:80`, „zewnętrzny model/API”, jest neutralne wobec dostawcy, a port aplikacyjny już nie.

## Krok 4 — Projekt ACL

### Zasada podziału

Dwa poziomy, bo są dwie różne wiedze:

1. **Wiedza o dostawcy** (HTTP, URL, klucz, `messages`, `choices`, `response_format`, kody błędów): **jeden adapter** w `Srv/App01.Shared.Infrastructure/Llm/OpenRouter/`.
2. **Wiedza o kontrakcie odpowiedzi modelu dla danej funkcji** (format werdyktu `{"verdict","reason"}`, format fiszek): **value object w module**. Werdykt jest pojęciem domenowym Kursów (UL-10), a nie infrastrukturą.

Reszta kodu zna tylko **wąski port** `ILlmCompletion`, a handler Kursów zna tylko **port domenowy** `IAnswerAssessor`.

### Port współdzielony (Shared.Application) — neutralny wobec dostawcy

```csharp
namespace App01.Shared.Application.Llm;

// Wejście: instrukcje (zaufane) i dane (niezaufane) rozdzielone typem, nie stringiem roli
public sealed record LlmPrompt(
    string Instructions,                 // reguły zadania — po stronie dostawcy: wiadomość systemowa
    UntrustedText Input,                 // dane od użytkownika — nigdy nie interpretowane jako polecenia
    LlmOutputSchema? ExpectedOutput);    // opcjonalny kontrakt JSON odpowiedzi

public sealed record UntrustedText
{
    public string Value { get; }
    // Jedyne miejsce neutralizacji ograniczników (dziś VerifyAnswer/Handler.cs:164-166, :206-207)
    public static UntrustedText Wrap(string raw, string tag);   // → "<tag>…</tag>" z zneutralizowanymi <tag>/</tag>
}

public sealed record LlmOutputSchema(string Name, string JsonSchema);

// Wyjście: wynik zamiast wyjątku; rodzaje awarii nazwane domenowo
public abstract record LlmResult
{
    public sealed record Completed(string Text) : LlmResult;
    public sealed record Failed(LlmFailure Reason) : LlmResult;
}

public enum LlmFailure { Timeout, RateLimited, ProviderUnavailable, InsufficientCredits, Misconfigured, InvalidResponse, Rejected }

public interface ILlmCompletion
{
    // Nigdy nie rzuca dla awarii dostawcy; OperationCanceledException tylko przy anulowaniu przez wołającego
    Task<LlmResult> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken);
}
```

Port nie ma parametru `model`, listy historii ani stringów ról. Wybór modelu to konfiguracja adaptera. Historia czatu nie jest dziś używana przez żadnego konsumenta (`VerifyAnswer` przekazuje jedną wiadomość systemową, Flashcards żadnej).

### Adapter (Shared.Infrastructure/Llm/OpenRouter) — jedyne miejsce wiedzy o dostawcy

```csharp
namespace App01.Shared.Infrastructure.Llm.OpenRouter;

internal sealed class OpenRouterOptions        // sekcja "OpenRouter" — nazwy kluczy bez zmian (prod VM, infrastructure.md:93)
{ string Url; string ApiKey /* Base64 */; string Model; }

internal sealed class OpenRouterLlmCompletion : ILlmCompletion
{
    public async Task<LlmResult> CompleteAsync(LlmPrompt prompt, CancellationToken ct)
    {
        if (!_options.TryResolve(out var url, out var apiKey, out var model)) return Failed(Misconfigured);   // dziś: goły Exception
        var body = OpenRouterRequest.From(prompt, model);          // messages: [system=Instructions, user=Input.Value]
                                                                   // + response_format json_schema, gdy ExpectedOutput != null
        using var request = new HttpRequestMessage(Post, url) { Content = Json(body) };
        request.Headers.Authorization = new("Bearer", apiKey);     // per-request, nie DefaultRequestHeaders współdzielonego klienta
        HttpResponseMessage response;
        try   { response = await _http.SendAsync(request, ct); }
        catch (HttpRequestException) { return Failed(ProviderUnavailable); }
        // OperationCanceledException przepuszczany — o timeoucie decyduje wołający (patrz IAnswerAssessor)

        if (!response.IsSuccessStatusCode) return Failed(MapStatus(response.StatusCode));   // logowanie treści błędu tu, nie w modułach
        return OpenRouterResponse.TryReadContent(await response.Content.ReadAsStringAsync(ct), out var text)
            ? Completed(text)
            : Failed(InvalidResponse);
    }

    // 402 → InsufficientCredits, 429 → RateLimited, 408 → Timeout, 502/503 → ProviderUnavailable, 400/401/403 → Rejected/Misconfigured
    private static LlmFailure MapStatus(HttpStatusCode code);
}

internal static class OpenRouterResponse
{
    // choices[0].message.content; brak choices / pusta treść / top-level "error" / finish_reason "error" → false
    public static bool TryReadContent(string json, out string text);
}

public static class OpenRouterRegistration
{
    // Jedyna publiczna nazwa z „OpenRouter” poza katalogiem adaptera nie jest potrzebna:
    public static IServiceCollection AddLlmCompletion(this IServiceCollection s, IConfiguration c);   // rejestruje ILlmCompletion
}
```

`SharedInfrastructureDI.cs:44` zmienia się z `AddScoped<IOpenRouterService, OpenRouterService>()` na `services.AddLlmCompletion(configuration)`.

### Port domenowy i value objecty Kursów

```csharp
namespace App01.Modules.Courses.Verification;

// Value object werdyktu — JEDYNE miejsce wiedzy o kontrakcie odpowiedzi modelu dla weryfikacji
public sealed record AnswerVerdict(bool Passed, string? Reason)
{
    public const string SchemaName = "answer_verdict";
    public const string JsonSchema =
        """{"type":"object","properties":{"verdict":{"type":"string","enum":["pass","fail"]},"reason":{"type":"string"}},
            "required":["verdict","reason"],"additionalProperties":false}""";

    // dziś: VerifyAnswer/Handler.cs:62, :168-198, :209-210
    public static bool TryParse(string modelText, out AnswerVerdict verdict);   // tylko {"verdict":"pass"|"fail"}; reszta → false
}

public sealed record AssessmentCriteria(string Value);         // Flag.Criteria — tajne, nigdy w odpowiedzi API

public abstract record AssessmentOutcome
{
    public sealed record Passed : AssessmentOutcome;
    public sealed record Failed : AssessmentOutcome;
    public sealed record Unavailable(LlmFailure Cause) : AssessmentOutcome;   // timeout, dostawca, nieparsowalny werdykt
}

public interface IAnswerAssessor
{
    Task<AssessmentOutcome> AssessAsync(AssessmentCriteria criteria, string answer, CancellationToken ct);
}

internal sealed class LlmAnswerAssessor(ILlmCompletion llm, IConfiguration config, ILogger<…> log) : IAnswerAssessor
{
    public async Task<AssessmentOutcome> AssessAsync(AssessmentCriteria criteria, string answer, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds()));          // Courses:VerificationTimeoutSeconds, domyślnie 15 (prd.md:80)
        var prompt = new LlmPrompt(
            Instructions: AssessmentInstructions.Build(criteria),          // dziś: VerifyAnswer/Handler.cs:156-162
            Input: UntrustedText.Wrap(answer, "answer"),
            ExpectedOutput: new(AnswerVerdict.SchemaName, AnswerVerdict.JsonSchema));
        LlmResult result;
        try { result = await llm.CompleteAsync(prompt, cts.Token); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        { return new Unavailable(LlmFailure.Timeout); }

        return result switch
        {
            LlmResult.Completed c when AnswerVerdict.TryParse(c.Text, out var v) => v.Passed ? new Passed() : new Failed(),
            LlmResult.Completed => new Unavailable(LlmFailure.InvalidResponse),
            LlmResult.Failed f  => new Unavailable(f.Reason),
        };
    }
}
```

Handler `VerifyAnswer` przestaje znać `ILlmCompletion`, prompt i JSON. Woła `IAnswerAssessor` i mapuje `Passed` → ścieżka `Correct` (w planie 02: `claim.RecordVerificationPass`), `Failed` → `Incorrect`, `Unavailable` → `Unavailable`. Kontrakt `VerifyAnswer/Contracts.cs:16-28` zostaje bez zmian.

### Value object Flashcards (Phase 5)

```csharp
namespace App01.Modules.Flashcards.Features.Generate;
// jedyne miejsce wiedzy o formacie [{"pytanie":"odpowiedź"}] (dziś Handler.cs:84-90, :136-150)
internal sealed record GeneratedFlashcards(IReadOnlyList<Contracts.FlashcardItem> Items)
{ public static bool TryParse(string modelText, out GeneratedFlashcards cards); }
```

Handler Flashcards: `LlmResult.Failed` → `throw new Exception("Generowanie fiszek jest chwilowo niedostępne")`. Status 500 przez middleware zostaje jak dziś, więc zachowanie FR-013 się nie zmienia, ale z `Detail` znika nazwa dostawcy.

## Krok 5 — Dowód izolacji, before/after, decyzje kontraktowe

### Wymiana dostawcy dotyka tylko adaptera

Scenariusz: OpenRouter → bezpośrednie API innego dostawcy (inne ciało żądania, inne miejsce treści w odpowiedzi, inne kody błędów).

| Element | Zmienia się? | Dlaczego |
| --- | --- | --- |
| `Srv/App01.Shared.Infrastructure/Llm/<Dostawca>/` | **tak**: nowy adapter `ILlmCompletion` + `Add…` | jedyne miejsce wiedzy o protokole |
| `SharedInfrastructureDI.cs` | jedna linia rejestracji | wybór adaptera |
| `appsettings.Example.json` | nowa sekcja konfiguracji | konfiguracja adaptera |
| `ILlmCompletion`, `LlmPrompt`, `LlmResult` | nie | brak typów dostawcy |
| `AnswerVerdict`, `LlmAnswerAssessor`, `GeneratedFlashcards` | nie | kontrakt odpowiedzi to JSON w treści; schema przekazana przez port |
| handlery `VerifyAnswer`, `Generate` | nie | znają tylko porty |
| tabele / migracje | nie | LLM niczego nie persystuje (odpowiedzi nie są zapisywane — `CLAUDE.md`, weryfikacja; plan 02 zapisuje tylko `FlagVerification`) |
| kontrakty API (`VerifyAnswer/Contracts.cs`, Flashcards) i interfejsy TS | nie | statusy domenowe, nie dostawcy |
| UI (`client/pages/courses/TomoAiTerminalPage.tsx`, strony Flashcards) | nie | dostają `Status`/`Message` albo listę fiszek |
| testy endpointów | nie | fake `ILlmCompletion` jest neutralny; testy kontraktowe adaptera są per-dostawca |

### Before / after

| Miejsce | Before | After |
| --- | --- | --- |
| `IOpenRouterService.cs:5-7` | port od nazwy dostawcy, sygnatura czatu | usunięty → `ILlmCompletion.CompleteAsync(LlmPrompt, ct)` |
| `ChatMessage.cs:3` | `Role` jako string protokołu | usunięty; rozdział instrukcja/dane przez typy `LlmPrompt` |
| `OpenRouterService.cs:28-88` | zwraca `string`, rzuca goły `Exception`, ustawia nagłówek na kliencie z fabryki | `OpenRouterLlmCompletion` zwraca `LlmResult`, mapuje statusy HTTP, nagłówek per request, opcjonalny `response_format` |
| `Flashcards/…/Handler.cs:59-131` | druga, pełna kopia protokołu | `ILlmCompletion` + `GeneratedFlashcards.TryParse` |
| `VerifyAnswer/Handler.cs:120-154` (`GetVerdictAsync`, timeout, catch) | prompt, role, timeout, mapowanie wyjątków w handlerze | `IAnswerAssessor.AssessAsync` → `AssessmentOutcome` |
| `VerifyAnswer/Handler.cs:156-166`, `:206-207` | prompt i neutralizacja ograniczników w handlerze | `AssessmentInstructions.Build` + `UntrustedText.Wrap` |
| `VerifyAnswer/Handler.cs:62`, `:168-198`, `:209-210` | parser werdyktu + obejście ogrodzenia Markdown | `AnswerVerdict.TryParse`; ogrodzenie zbędne przy `json_schema` (patrz D-1), ale tolerowane do czasu potwierdzenia D-2 |
| `ExceptionHandlingMiddleware.cs:163` | dostaje komunikaty z nazwą dostawcy | **bez zmian w middleware** (`CLAUDE.md`); komunikaty przestają zawierać dostawcę, bo źródło jest w adapterze, który nie rzuca |
| `Tests/Features/Courses/VerifyAnswer/EndpointTests.cs:70-225`, `:376-458` | mock `IOpenRouterService.ChatAsync` z `ChatMessage` | fake `ILlmCompletion` zwracający `LlmResult`; asercje promptu na `LlmPrompt.Instructions` / `Input` zamiast ról |
| UI | dostaje `Status`/`Message` (`VerifyAnswer/Contracts.cs:16-20`), nigdy `reason` modelu | **bez zmian** — UI już dziś dostaje dane domenowe, nie surowy obiekt dostawcy; ACL tylko utrwala to od strony serwera |

### Pytania zależne od kontraktu dostawcy — rozstrzygnięcia

Źródło: dokumentacja OpenRouter (Context7 `/openrouterteam/docs`, sprawdzone 2026-10-15: `guides/features/structured-outputs.mdx`, `api_reference/overview.mdx`, `api_reference/streaming.mdx`, `snippets/exports/StatusCode.jsx`).

| ID | Pytanie | Rozstrzygnięcie | Gdzie zakodować |
| --- | --- | --- | --- |
| D-1 | Czy da się wymusić JSON werdyktu zamiast „proszenia” w prompcie i tolerowania ogrodzenia Markdown (`VerifyAnswer/Handler.cs:177-182`)? | **Tak.** `response_format: { type: "json_schema", json_schema: { name, strict: true, schema } }`, a treść wraca w `choices[0].message.content`. Tryb `{ type: "json_object" }` daje „poprawny JSON” bez schematu | schema: `AnswerVerdict.JsonSchema` (moduł); mapowanie na `response_format`: wyłącznie `OpenRouterRequest.From` (adapter) |
| D-2 | Czy każdy model wspiera structured outputs? | **Nie.** Dokumentacja odsyła do listy modeli z `supported_parameters=structured_outputs`. Obsługi przez skonfigurowany `google/gemini-3.5-flash-lite` (`appsettings.Example.json:34`) nie potwierdziłem | adapter wysyła `response_format`; `AnswerVerdict.TryParse` zostawia tolerancję ogrodzenia, dopóki właściciel nie potwierdzi modelu (Q-11) |
| D-3 | Jakie awarie dostawcy rozróżniać? | Przed zatwierdzeniem odpowiedzi błąd przychodzi jako JSON z kodem HTTP: 400 (parametry), 401 (klucz), **402 (brak kredytów — odrębne od 429)**, 429 (limit, z `Retry-After`), 502 (błąd providera), 503 (brak providerów) | `OpenRouterLlmCompletion.MapStatus` → `LlmFailure`; domena widzi tylko `Unavailable(Cause)`, a log w adapterze rozróżnia przyczynę (402 wymaga działania admina, 429 nie) |
| D-4 | Czy 200 OK zawsze oznacza sukces? | Dla **streamingu** nie: błąd w trakcie generowania przychodzi przy statusie 200 jako `error` + `finish_reason: "error"`. Nie używamy streamingu. Dla odpowiedzi nie-streamingowej traktujemy defensywnie jako awarię: top-level `error`, brak `choices`, pustą treść i `finish_reason: "error"`. **To założenie ostrożnościowe, nie cytat z dokumentacji** | `OpenRouterResponse.TryReadContent` (adapter) |
| D-5 | Czy dekodowanie klucza z Base64 to wymóg dostawcy? | **Nie.** Dostawca oczekuje `Authorization: Bearer <klucz>`. Base64 to konwencja przechowywania w konfiguracji tego projektu (`OpenRouterService.cs:39-50`) | `OpenRouterOptions` (adapter); nazwa i format klucza na VM bez zmian |
| D-6 | Wybór modelu i ewentualny fallback | OpenRouter ma parametr `models: string[]` z `route: "fallback"` (funkcja tylko tego dostawcy) | jeśli kiedyś potrzebny: wyłącznie `OpenRouterOptions` + `OpenRouterRequest`; port się nie zmienia |

## Krok 6 — Weryfikacja i plan

### Kryterium sukcesu

```
grep -rln "OpenRouter" src/server tests/server --include=*.cs | grep -v -e /obj/ -e /bin/
```

zwraca **wyłącznie** pliki z `src/server/App01/App01.Shared.Infrastructure/Llm/OpenRouter/` i `tests/server/App01/App01.Api.Tests/Llm/OpenRouter/`. Dodatkowo:

- `grep -rn "choices\|\"role\"\|Bearer" src/server --include=*.cs` w kontekście LLM → tylko katalog adaptera (`Bearer` zostaje też w `ServerDI`/JWT — poza zakresem).
- `grep -rn "IHttpClientFactory" src/server/App01/App01.Modules.Flashcards` → 0 trafień.
- `grep -rn "\"system\"\|\"user\"" src/server/App01/App01.Modules.Courses` → 0 trafień.

| Plik | Zna dostawcę dziś | Po refaktorze |
| --- | --- | --- |
| `Srv/App01.Shared.Infrastructure/Services/OpenRouterService.cs` | tak | usunięty → `Llm/OpenRouter/OpenRouterLlmCompletion.cs` (tak, jedyny) |
| `Srv/App01.Shared.Application/Interfaces/IOpenRouterService.cs` | tak | usunięty → `Llm/ILlmCompletion.cs` (nie) |
| `Srv/App01.Shared.Application/Models/AI/ChatMessage.cs` | tak (kształt) | usunięty |
| `Srv/App01.Shared.Infrastructure/SharedInfrastructureDI.cs` | tak (`:44`) | nie (`AddLlmCompletion`) |
| `Courses/Features/VerifyAnswer/Handler.cs` | tak (`:38`, `:48`, `:122-131`) | nie (`IAnswerAssessor`) |
| `Courses/Verification/*` (nowe) | — | nie (znają `ILlmCompletion`) |
| `Srv/App01.Modules.Flashcards/Features/Generate/Handler.cs` | tak (15 linii) | nie (Phase 5) |
| `Tests/Features/Courses/VerifyAnswer/EndpointTests.cs` | tak (24 linie) | nie (fake `ILlmCompletion`) |
| `Tests/Llm/OpenRouter/*` (nowe) | — | tak (testy kontraktowe adaptera) |
| `Srv/App01.Bootstrapper.Api/appsettings.Example.json:31` | tak (sekcja) | tak — konfiguracja adaptera, świadomie poza kryterium `*.cs` |
| `CLAUDE.md:92`, `:94`, `:99`, `stack-assessment.md:24`, `:131` | tak | zaktualizowane do portu `ILlmCompletion` / `IAnswerAssessor` |

### Plan faz (konwencja: folder zmiany `/10x-new llm-anti-corruption-layer` → `/10x-plan` → `/10x-implement` lub `/10x-tdd`)

| Faza | Zakres | Test-first? | Gotowe, gdy… |
| --- | --- | --- | --- |
| 1. Port i value objecty | `ILlmCompletion`, `LlmPrompt`, `UntrustedText`, `LlmResult`, `LlmFailure` w `Shared.Application/Llm/`; `AnswerVerdict` w `Courses/Verification/` | **tak**: unit testy `UntrustedText.Wrap` (neutralizacja `<answer>`, `</ answer >`, wielkość liter) i `AnswerVerdict.TryParse` (pass, fail, inny verdict, brak pola, nie-JSON, ogrodzenie) | testy zielone; projekt `Shared.Application` nie ma `using System.Net.Http` |
| 2. Adapter | `Llm/OpenRouter/*` + `AddLlmCompletion`; stary `OpenRouterService` jeszcze zostaje | **tak**: testy kontraktowe z fake `HttpMessageHandler`: ciało żądania (role, `response_format`, model z konfiguracji), `Bearer` per request, 200 + treść, 200 bez `choices`, 402/429/503 → właściwy `LlmFailure`, `HttpRequestException` → `ProviderUnavailable`, brak konfiguracji → `Misconfigured`, zły Base64 → `Misconfigured` | testy zielone bez sieci |
| 3. Courses na porcie domenowym | `IAnswerAssessor` + `LlmAnswerAssessor`; `VerifyAnswer/Handler.cs` korzysta z assessora; testy endpointu na fake `ILlmCompletion` | **tak**: przepisanie mocków przed zmianą handlera; wszystkie przypadki z `VerifyAnswer/EndpointTests.cs:249-564` przechodzą semantycznie bez zmian (`Correct`, `Incorrect`, `Unavailable` ×4, `AlreadyOwned` + `Times.Never`, prompt w delimiterach, 400/401/brak `X-TOKEN`) | `grep "\"system\"\|\"user\"" Courses` → 0; `dotnet test` zielone |
| 4. Usunięcie starego portu | `IOpenRouterService`, `ChatMessage`, `OpenRouterService` usunięte; DI na `AddLlmCompletion` | — | build bez ostrzeżeń; `grep IOpenRouterService src tests` → 0 |
| 5. Flashcards (**wymaga zgody — Q-10**) | najpierw test charakteryzujący `Generate` (fake `ILlmCompletion` / fake HTTP: sukces, 403 bez e-maila, awaria → 500), potem `GeneratedFlashcards` + port | **tak** — test charakteryzujący przed zmianą, bo moduł nie ma dziś żadnych testów | kryterium sukcesu z grepem spełnione w całości; test charakteryzujący zielony przed i po |
| 6. Dokumenty | `CLAUDE.md:92-99`, `stack-assessment.md:24/:131`, `test-plan.md:75` (Moq → fake `ILlmCompletion`), `glossary.md` | — | `grep -n "IOpenRouterService" CLAUDE.md context/foundation` → 0 |

Bramka każdej fazy (`CLAUDE.md`, „Weryfikacja przed zakończeniem”): `dotnet format --verify-no-changes`, `dotnet build APPS.sln`, `dotnet test APPS.sln`. Klient się nie zmienia.

**Kolejność względem planu 02:** fazy 1–3 tego planu przed fazą 3 planu `02-invariant-aggregate-refactor.md`. Wtedy `VerifyAnswer` przepisuje się raz: assessor + agregat `FlagClaim`.

### Pytania otwarte

| ID | Pytanie | Kto |
| --- | --- | --- |
| Q-10 | Czy wolno zmienić moduł Flashcards (Phase 5)? `CLAUDE.md` (przepis na moduł, pkt 6) każe nie zmieniać istniejących modułów, a FR-013 wymaga identycznego zachowania. Bez Phase 5 kryterium grep spełnia się tylko częściowo (zostaje `Flashcards/…/Handler.cs`) | właściciel portalu |
| Q-11 | Czy skonfigurowany model wspiera `structured_outputs` (D-2)? Jeśli tak, tolerancję ogrodzenia Markdown w `AnswerVerdict.TryParse` można usunąć | osoba utrzymująca konfigurację OpenRouter |
| Q-12 | Czy `402 InsufficientCredits` ma tylko dawać `Unavailable`, czy też alarm dla admina (to jedyna awaria, której ponowienie nie naprawi)? | właściciel portalu |

### Następne w kolejce (poza tym planem)

- **L-05 BCrypt**: port `IPasswordHasher` w `Shared.Application`, 6 wywołań w Portalu. Format hasha zostaje w bazie, więc wymiana wymaga strategii „rehash przy logowaniu”.
- **L-06 claim `isAdmin`**: nazwa claimu jako stała współdzielona przez kontrakt (np. pole `isAdmin` w odpowiedzi logowania — już istnieje w `client/services/contracts/user-login-response.ts`) zamiast dekodowania JWT w `client/utils/jwt.ts:11-12`.
- **L-07 / L-08**: usunąć martwy `client/utils/parseFrontmatter.ts` (+ test) i nieużywany `Newtonsoft.Json` (z aktualizacją `packages.lock.json`).
