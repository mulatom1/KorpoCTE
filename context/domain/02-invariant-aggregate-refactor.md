---
title: "Niezmiennik #1 i agregat-strażnik — zdobycie flagi wymaga dowodu wykonania"
created: 2026-10-15
type: refactor-plan
scope: "App01.Modules.Courses — VerifyAnswer, ActivateFlag; encje Courses"
baseline_commit: 7daf069
depends_on: context/domain/01-domain-distillation.md
status: draft — czeka na decyzję właściciela (Phase 0)
---

# Niezmiennik #1 i agregat-strażnik

> Plan, nie implementacja. Kod produkcyjny nie został zmieniony.
> Skróty ścieżek: `Courses/…` = `src/server/App01/App01.Modules.Courses/…`, `Entities/…` = `src/server/App01/App01.Shared.Application/Entities/…`, `Config/…` = `src/server/App01/App01.Shared.Infrastructure/Repositories/Configurations/Courses/…`, `Tests/…` = `tests/server/App01/App01.Api.Tests/…`, `client/…` = `src/client/app01/src/…`.

## Krok 0 — Kontekst

- Wymagania: `context/foundation/prd.md` (insight `:48`, Business Logic `:167-175`, Guardrails `:77-83`, US-01 `:87-100`, FR-004…FR-008 `:115-127`). Decyzje późniejsze: addenda w `context/archive/2026-10-08-answer-verification-earns-flag/plan.md:378-390` i `context/archive/2026-10-10-hangar-flag-list/plan.md:17`. Ryzyka przyjęte: `context/foundation/test-plan.md:145`. Brak `tech-stack.md` (brownfield).
- Stack: ASP.NET Core 10, vertical slices z MediatR; **logika domenowa żyje w handlerach** (`Courses/Features/*/Handler.cs`), encje są anemiczne (`Entities/Courses/*.cs`), niezmienniki bazodanowe w `Config/*.cs`, klient React jest wyłącznie prezentacją.
- Ograniczenia z `CLAUDE.md`: nie zmieniać `ExceptionHandlingMiddleware` (mapuje tylko `ValidationException` → 400, `ForbiddenException` → 403, `NotFoundException` → 404 — `src/server/App01/App01.Shared.Application/Middlewares/ExceptionHandlingMiddleware.cs:53-117`); encje w `Shared.Application`, konfiguracje i migracje w `Shared.Infrastructure`; moduły się nie referencjonują; testy na EF InMemory + opcjonalne testy SQL Server (`Tests/Infrastructure/SqlServer/SqlServerDatabaseFixture.cs:11-24`, zmienna `APP01_SQLSERVER_TESTS`).
- Rejestr kontraktów: **brak w projekcie** (szukano `load-bearing`, `rejestr kontrakt`, `contract registry` w `context/` i w korzeniu — trafienia tylko w plikach skilli i promptów). Rolę rejestru pełnią `CLAUDE.md` i `context/domain/glossary.md`.

## Krok 1 — Niezmienniki biznesowe

| ID | Niezmiennik | Źródło |
| --- | --- | --- |
| I-01 | **Zdobycie flagi powstaje tylko z dowodu wykonania**: dla flagi z kryteriami tym dowodem jest zaliczona weryfikacja *tego samego* użytkownika, dla flagi bez kryteriów jest nim kod wyliczony w zadaniu | `prd.md:48`, `prd.md:171-173`; `CLAUDE.md:98` („uczestnik wylicza go, wykonując zadanie”) |
| I-02 | Flaga zdobyta najwyżej raz na użytkownika | `prd.md:82` |
| I-03 | Flaga raz zdobyta nie znika | `prd.md:82` |
| I-04 | Kurs, jego flagi i zdobycia istnieją dla użytkownika od `PublishDate` włącznie | `prd.md:110`, `prd.md:191` |
| I-05 | Werdykt `Incorrect`/`Unavailable` nic nie zużywa i nie jest liczony | `prd.md:97-98`, `CLAUDE.md` (sekcja weryfikacji) |
| I-06 | Gdy flaga jest już posiadana, model nie jest wołany | `prd.md:99` |
| I-07 | Kod flagi wraca z API tylko przy `Correct` i w hangarze właściciela; kryteria nie wracają nigdy | `CLAUDE.md:98`, `Entities/Courses/Flag.cs:16-17` |
| I-08 | Liczba w rankingu = liczba w hangarze; zdobyte ≤ możliwe | `prd.md:127`, `Courses/Features/GroupProgress/Handler.cs:51` |
| I-09 | Treść kursu niedostępna bez logowania | `prd.md:83` |
| I-10 | Tożsamość zdobywcy wyłącznie z JWT | `CLAUDE.md` (Bezpieczeństwo endpointów) |

## Krok 2 — Klasyfikacja i wybór #1

Oś (a): rdzeniowość, 1–3. Oś (b): rozsmarowanie, czyli liczba plików/warstw. Oś (c): stan egzekucji.

| ID | (a) rdzeń | (b) rozsmarowanie | (c) egzekucja | Dowód (c) |
| --- | --- | --- | --- | --- |
| **I-01** | **3** — to insight produktu i jedyna nowa reguła domenowa | **5 miejsc / 4 warstwy**: 2 handlery, encja, log, UI (Krok 3) | **naruszalny** — serwer nie łączy weryfikacji z aktywacją | `Courses/Features/ActivateFlag/Handler.cs:59-62` szuka po samym kodzie; `Courses/Features/VerifyAnswer/Handler.cs:110-113` niczego nie zapisuje |
| I-02 | 3 | 3 (handler, konfiguracja EF, migracja) | **egzekwowany** (baza + wyścig + test SQL) | `Config/UserFlagConfiguration.cs:35-37`; `ActivateFlag/Handler.cs:88-106`; `Tests/Features/Courses/ActivateFlag/ActivateFlagRaceSqlServerTests.cs:32` |
| I-03 | 2 | 2 | naruszalny (cascade przy usunięciu konta) | `Config/UserFlagConfiguration.cs:44-47`; `src/server/App01/App01.Modules.Portal/Features/UserDelete/Handler.cs:63` |
| I-04 | 2 | 9 kopii warunku w 8 handlerach | egzekwowany, ale rozproszony | `context/domain/01-domain-distillation.md` (INV-03a); test spójności `Tests/Features/Courses/FlagCountConsistency/EndpointTests.cs` |
| I-05 | 2 | 1 | egzekwowany | `VerifyAnswer/Handler.cs:99-108` |
| I-06 | 2 | 2 | egzekwowany + test `Times.Never` | `VerifyAnswer/Handler.cs:89-95`; `Tests/Features/Courses/VerifyAnswer/EndpointTests.cs:397` |
| I-07 | 2 | 3 | egzekwowany (testy na surowym JSON) | `HangarFlags/Handler.cs:81-82`; `Tests/Features/Courses/ActivateFlag/EndpointTests.cs:384` |
| I-08 | 2 | 3 | egzekwowany | `FlagCountConsistency/EndpointTests.cs` |
| I-09 | 2 | 3 | egzekwowany dla `.md`, ryzyko przyjęte dla załączników | `01-domain-distillation.md` (DR-05) |
| I-10 | 2 | wszystkie handlery | egzekwowany | `ActivateFlag/Handler.cs:52`, `VerifyAnswer/Handler.cs:74` |

**Wybór #1: I-01 — „zdobycie flagi powstaje tylko z dowodu wykonania”.**

- Jest najbardziej rdzeniowy. PRD buduje na nim cały produkt: „flaga dowodzi wykonania, nie przeczytania” (`prd.md:48`), a ranking i wskaźniki są „następstwem reguły” (`prd.md:175`). Jeśli I-01 nie trzyma, I-02 i I-08 liczą poprawnie liczby bez znaczenia.
- Jest najsłabiej egzekwowany ze wszystkich rdzeniowych. I-02 ma indeks, obsługę wyścigu i test na prawdziwym silniku. I-01 nie ma żadnego mechanizmu, a istniejący test **utrwala naruszenie**: `Tests/Features/Courses/ActivateFlag/EndpointTests.cs:237-251` aktywuje flagę z kryteriami (`:166`, `Criteria = SecretCriteria`) bez żadnej wcześniejszej weryfikacji i oczekuje `Activated`.
- Ryzyko przyjęto przy innym modelu. FR-004 dopuścił przekazanie kodu (`prd.md:116`), kiedy flagę miała przyznawać weryfikacja. Od 2026-10-08 przyznaje ją wyłącznie aktywacja kodem (`context/archive/2026-10-08-answer-verification-earns-flag/plan.md:382-385`), więc spełnił się warunek ponownej oceny z `test-plan.md:145`.
- I-03 jest naruszalny, ale mniej rdzeniowy, a usunięcie konta to rzadka, świadoma operacja admina. Zostaje w kolejce (Q-03 w `01-domain-distillation.md`).

**Warunek wstępny:** wybór I-01 jako reguły do *wymuszenia* wymaga decyzji właściciela (Q-01). Ten plan zakłada odpowiedź „tak, aktywacja flagi z kryteriami wymaga wcześniejszego `Correct` tego samego użytkownika”. Wariant „nie” opisuje Phase 0.

## Krok 3 — Diagnoza I-01

### Gdzie dziś żyje reguła

| # | Warstwa | Miejsce | Co robi z regułą |
| --- | --- | --- | --- |
| P1 | Persystencja / model | `Entities/Courses/Flag.cs:16-18` | Jedynym znacznikiem „flaga wymaga weryfikacji” jest konwencja `Criteria` null/puste. Nie ma nazwanego pojęcia |
| P2 | Persystencja | `Entities/Courses/UserFlag.cs:3-14`, `Config/UserFlagConfiguration.cs:35-37` | Zapisuje *fakt zdobycia*, nie zapisuje *dowodu*. Model nie ma żadnego bytu „weryfikacja zaliczona” |
| P3 | Serwis (handler weryfikacji) | `Courses/Features/VerifyAnswer/Handler.cs:84` | Sprawdza „flaga weryfikowalna” (`IsNullOrWhiteSpace(flag.Criteria)`) |
| P4 | Serwis (handler weryfikacji) | `Courses/Features/VerifyAnswer/Handler.cs:110-113` | Po `pass` **jedynym śladem dowodu jest linia logu** (`LogInformation(... "accepted - activation code issued")`, `:111`). Kod trafia do klienta, a serwer o fakcie zapomina |
| P5 | Serwis (handler aktywacji) | `Courses/Features/ActivateFlag/Handler.cs:54-62` | Szuka flagi po samym kodzie, celowo „także bez kryteriów” (komentarz `:54`). Nie ma rozróżnienia, czy flaga wymaga weryfikacji |
| P6 | Serwis (handler aktywacji) | `Courses/Features/ActivateFlag/Handler.cs:79-90` | Tworzy `UserFlag` dla każdego pasującego kodu. To tu reguła powinna stanąć i nie staje |
| P7 | Serwis (lista zadań) | `Courses/Features/HangarTasks/Handler.cs:51` | Druga kopia reguły „weryfikowalna = ma kryteria” |
| P8 | UI | `client/pages/courses/TomoAiTerminalPage.tsx:210-218` → `client/pages/courses/HangarPage.tsx:141`, `:152` | Powiązanie „weryfikacja → aktywacja” istnieje **tylko w choreografii UI i w schowku uczestnika**: terminal pokazuje kod, uczestnik przepisuje go do hangaru |
| P9 | Testy | `Tests/Features/Courses/ActivateFlag/EndpointTests.cs:237-251` | Utrwala stan naruszenia jako oczekiwane zachowanie |

### Ustalenia

- **Warstwy, które nie egzekwują reguły:** persystencja (P2: brak bytu dowodu, brak FK „zdobycie ← weryfikacja”) i handler aktywacji (P5, P6). Nie egzekwuje jej żadna warstwa serwera.
- **Niespójna egzekucja:** pojęcie „flaga weryfikowalna” ma dwie kopie (P3, P7), a trzeci handler (P5) celowo je pomija. Ta sama flaga jest więc „zadaniem” dla terminala i zwykłym kodem dla aktywacji.
- **UI jako jedyny strażnik:** P8. Kolejność „najpierw `Correct`, potem aktywacja” wynika wyłącznie z tego, że kod pojawia się w terminalu. Kod z innego źródła (od kolegi, z hangaru kolegi — `HangarFlags/Handler.cs:81-82`, zgadnięty przy braku limitu prób — `ActivateFlag/Handler.cs:65-69`) omija weryfikację. Blokada `disabled={task.isOwned}` (`TomoAiTerminalPage.tsx:160`) dotyczy innej reguły (I-06) i serwer powtarza ją sam (`VerifyAnswer/Handler.cs:89-95`), więc tu UI nie jest jedynym strażnikiem.
- **Błąd „połykany”:** P4. Zaliczenie weryfikacji to fakt domenowy, który kończy jako log na poziomie `Information`. Operacja „wydaj dowód” loguje i jedzie dalej zamiast utrwalić stan. Po stronie aktywacji brak dowodu nie zatrzymuje operacji, tylko przechodzi do zapisu (P6).
- **Czego NIE ruszamy (poprawne zachowanie):** `catch` w `VerifyAnswer/Handler.cs:133-142` zamienia awarię techniczną na `Unavailable` zgodnie z kontraktem (`prd.md:98`), a `catch (DbUpdateException)` w `ActivateFlag/Handler.cs:92-106` rozróżnia wyścig od innego błędu i rzuca dalej (`:101`). To jest fail-fast tam, gdzie trzeba.

## Krok 4 — Projekt agregatu-strażnika

### Kształt

**Agregat `FlagClaim`** — „roszczenie użytkownika do flagi”. Tożsamość: para `(UserId, FlagId)`. Jedyne miejsce, które może utworzyć `UserFlag`.

```
FlagClaim (root)
├── UserId, FlagId                       — tożsamość
├── FlagSnapshot { Title, Code, IsVerifiable, IsPublished }   — tylko odczyt, z Flag + Course
├── Verification? { VerifiedAt }         — NOWY fakt: zaliczona weryfikacja (encja FlagVerification)
└── Award?        { EarnedAt }           — istniejący fakt: zdobycie (encja UserFlag)

Stany:  Open ──RecordVerificationPass──▶ Verified ──Activate──▶ Earned
        Open ──Activate (tylko !IsVerifiable)──────────────────▶ Earned
```

- **Nowa encja `FlagVerification`** (`Entities/Courses/FlagVerification.cs`): `Id`, `UserId`, `FlagId`, `VerifiedAt`. Unikalny indeks `(UserId, FlagId)`, FK do `Flag` (Restrict) i `User` (zachowanie jak w `UserFlagConfiguration.cs:44-47`). Nie przechowuje odpowiedzi ani werdyktu (I-05, `CLAUDE.md`: odpowiedzi nie są persystowane).
- **`UserFlag` zostaje bez zmian.** Read-modele (`HangarFlags`, `Leaderboard`, `GroupProgress`) liczą nadal wiersze `UserFlag`, więc I-08 i test spójności się nie zmieniają.
- **Nazwane pojęcie `IsVerifiable`** powstaje w jednym miejscu: wyrażenie `FlagRules.IsVerifiable` (`Expression<Func<Flag,bool>>` = `!string.IsNullOrWhiteSpace(f.Criteria)`). Używają go repozytorium i `HangarTasks` (P3, P7 → jedno miejsce).
- **Umiejscowienie:** `Courses/Domain/FlagClaim.cs`, `Courses/Domain/FlagClaimErrors.cs`, `Courses/Domain/IFlagClaimRepository.cs`, `Courses/Domain/FlagClaimRepository.cs`. Jest precedens katalogu spoza `Features/`: `Courses/Content/`. Aggregat dotyczy tylko Kursów, więc nie idzie do `Shared.*`.

### Błędy domenowe (nazwane, fail-fast)

```csharp
abstract class FlagClaimException(string message) : Exception(message);
sealed class FlagNotAvailableException      : FlagClaimException  // flaga nie istnieje / kurs nieopublikowany (po FlagId)
sealed class InvalidFlagCodeException       : FlagClaimException  // nieznany kod / kurs nieopublikowany (po kodzie)
sealed class FlagNotVerifiableException     : FlagClaimException  // weryfikacja flagi bez kryteriów
sealed class FlagAlreadyEarnedException     : FlagClaimException  // ponowne zdobycie lub weryfikacja po zdobyciu
sealed class VerificationRequiredException  : FlagClaimException  // aktywacja flagi z kryteriami bez wcześniejszego Correct tego użytkownika
```

### Metody domenowe

```csharp
public sealed class FlagClaim
{
    public long UserId { get; }
    public int FlagId { get; }
    public string Title { get; }
    public bool IsVerifiable { get; }
    public DateTime? VerifiedAt { get; private set; }
    public DateTime? EarnedAt { get; private set; }
    public bool IsEarned => EarnedAt.HasValue;
    internal bool IsNewVerification { get; private set; }   // dla repozytorium: co dopisać
    internal bool IsNewAward { get; private set; }
    private readonly string _code;

    // Wołane PRZED modelem — fail-fast, model nie jest wołany przy naruszeniu (I-06)
    public void EnsureCanBeVerified()
    {
        if (!IsVerifiable) throw new FlagNotVerifiableException();
        if (IsEarned)      throw new FlagAlreadyEarnedException();
    }

    // Wołane tylko po werdykcie "pass"; Incorrect/Unavailable NIE wołają niczego (I-05)
    public string RecordVerificationPass(DateTime now)
    {
        EnsureCanBeVerified();
        if (VerifiedAt is null)          // idempotentne: drugi Correct nie tworzy drugiego dowodu
        {
            VerifiedAt = now;
            IsNewVerification = true;
        }
        return _code;                    // kod wydawany wyłącznie stąd (I-07)
    }

    // Jedyna droga do UserFlag
    public void Activate(DateTime now)
    {
        if (IsEarned)                         throw new FlagAlreadyEarnedException();
        if (IsVerifiable && VerifiedAt is null) throw new VerificationRequiredException();   // I-01
        EarnedAt = now;
        IsNewAward = true;
    }
}
```

Agregat nie przyjmuje `userId` z zewnątrz — repozytorium ładuje go dla `userId` z JWT (I-10). Zgodność kodu sprawdza samo ładowanie po kodzie (`LoadByCodeAsync`), więc `Activate` nie potrzebuje parametru `code`.

### Repozytorium (zamiast rozsianych zapytań)

```csharp
public interface IFlagClaimRepository
{
    // Weryfikacja: po FlagId; null-safe → FlagNotAvailableException
    Task<FlagClaim> LoadByFlagIdAsync(long userId, int flagId, DateTime now, CancellationToken ct);
    // Aktywacja: po kodzie (Trim + ToUpperInvariant, jak dziś ActivateFlag/Handler.cs:58-60) → InvalidFlagCodeException
    Task<FlagClaim> LoadByCodeAsync(long userId, string code, DateTime now, CancellationToken ct);
    Task SaveAsync(FlagClaim claim, CancellationToken ct);
}
```

```
LoadBy…Async:
  flag = Flags.Where(match && Course.PublishDate <= now)                         // I-04
              .Select(f => new { f.Id, f.Title, f.Code, IsVerifiable = FlagRules.IsVerifiable(f),
                                 VerifiedAt = FlagVerifications.Where(uid,fid).Select(VerifiedAt).FirstOrDefault(),
                                 EarnedAt   = UserFlags.Where(uid,fid).Select(EarnedAt).FirstOrDefault() })
              .FirstOrDefaultAsync(ct)                                             // JEDNO zapytanie
  if flag is null → throw FlagNotAvailableException / InvalidFlagCodeException
  return FlagClaim.Rehydrate(...)

SaveAsync:                                                                         // JEDNA transakcja = jeden SaveChanges
  if claim.IsNewVerification → FlagVerifications.Add(...)
  if claim.IsNewAward        → UserFlags.Add(...)
  try   await SaveChangesAsync(ct)
  catch DbUpdateException:
        detach dodane wpisy
        if claim.IsNewAward && UserFlags.Any(uid,fid)              → throw FlagAlreadyEarnedException   // wyścig I-02
        if claim.IsNewVerification && FlagVerifications.Any(uid,fid) → return                          // wyścig dwóch Correct: idempotentnie
        throw                                                                                         // inny błąd → middleware (500), jak dziś :101
```

**Atomowość:** odczyt dowodu i zapis zdobycia stanowią jedną decyzję w jednym `SaveChanges`. Okno między odczytem a zapisem nie jest groźne, bo `FlagVerification` nigdy nie jest usuwany (nie ma operacji usuwania, a FK `Flag` ma Restrict). Wyścig dwóch aktywacji zatrzymuje unikalny indeks `UserFlags` (bez zmian), a wyścig dwóch `Correct` — nowy unikalny indeks `FlagVerifications`. Jawna transakcja `BeginTransaction` nie jest potrzebna.

### Cienkie handlery (route bez zmian: `Endpoint.cs` nietknięte)

```csharp
// VerifyAnswer.Handle
validate → userId = jwt
try {
    var claim = await _claims.LoadByFlagIdAsync(userId, req.FlagId, now, ct);
    claim.EnsureCanBeVerified();                                   // przed modelem
    var verdict = await GetVerdictAsync(...);                      // bez zmian: null → Unavailable
    if (verdict is null)   return Unavailable;
    if (verdict != "pass") return Incorrect;
    var code = claim.RecordVerificationPass(now);
    await _claims.SaveAsync(claim, ct);
    return Correct(code);
}
catch (FlagNotAvailableException or FlagNotVerifiableException) { throw new NotFoundException(NotFoundMessage); }   // 404 jak dziś :84-87
catch (FlagAlreadyEarnedException)                               { return AlreadyOwned; }

// ActivateFlag.Handle
validate → userId = jwt
try {
    var claim = await _claims.LoadByCodeAsync(userId, req.Code, now, ct);
    claim.Activate(now);
    await _claims.SaveAsync(claim, ct);
    return Activated(claim.Title);
}
catch (InvalidFlagCodeException)      { log bez kodu; return Invalid; }                  // jak dziś :64-69
catch (FlagAlreadyEarnedException)    { return AlreadyOwned(title); }
catch (VerificationRequiredException) { return VerificationRequired(title); }            // NOWY status
```

- Mapowanie błędu domenowego na odpowiedź siedzi w handlerze, bo middleware'u zmieniać nie wolno, a kontrakt Kursów już niesie statusy biznesowe w 200 (`ActivateFlag/Contracts.cs:21-26`, `VerifyAnswer/Contracts.cs:22-28`).
- **Nowy status kontraktu:** `ActivateFlag.Contracts.Statuses.VerificationRequired = "VerificationRequired"`, komunikat stały, np. „Tę flagę zdobywasz w Terminalu TOMO-AI-001 — najpierw zalicz zadanie.”, `FlagTitle` wypełniony, `Code` nigdy. Świadomie potwierdza, że kod jest prawidłowy. To akceptowalne, bo sam kod nic już nie daje bez weryfikacji, a uczestnik dostaje jasną drogę dalej.
- **Egzekucja przechodzi z klienta na serwer:** UI zachowuje dzisiejszą choreografię (P8), ale przestaje być strażnikiem. `HangarPage` jedynie wyświetla nowy status.

## Krok 5 — Before / after, plan faz, testy

### Before / after

| # | Miejsce | Before | After |
| --- | --- | --- | --- |
| P1 | `Entities/Courses/Flag.cs:16-18` | „weryfikowalna” = konwencja null/puste, czytana ad hoc | bez zmian w encji; znaczenie nazwane raz w `FlagRules.IsVerifiable` |
| P2 | `Entities/Courses/UserFlag.cs`, `Config/UserFlagConfiguration.cs` | jedyny fakt w modelu | bez zmian; obok nowa `FlagVerification` + `FlagVerificationConfiguration` (unikalny `(UserId, FlagId)`), migracja `CoursesFlagVerifications` |
| P3 | `VerifyAnswer/Handler.cs:78-87` | zapytanie EF + `IsNullOrWhiteSpace` + `NotFoundException` | `LoadByFlagIdAsync` + `EnsureCanBeVerified()`; mapowanie na 404 |
| — | `VerifyAnswer/Handler.cs:89-95` | osobne `AnyAsync` na `UserFlags` | stan `IsEarned` agregatu (`FlagAlreadyEarnedException` → `AlreadyOwned`, model nie jest wołany) |
| P4 | `VerifyAnswer/Handler.cs:110-113` | log `Information` i zwrot kodu | `RecordVerificationPass` + `SaveAsync`; log zostaje jako obserwowalność, nie jako jedyny ślad |
| P5 | `ActivateFlag/Handler.cs:54-62` | zapytanie po kodzie „także bez kryteriów” | `LoadByCodeAsync` (normalizacja kodu w repozytorium) |
| — | `ActivateFlag/Handler.cs:72-77` | osobne `AnyAsync` | stan agregatu |
| P6 | `ActivateFlag/Handler.cs:79-106` | `new UserFlag` + `SaveChanges` + `catch DbUpdateException` | `claim.Activate(now)` (precondition I-01) + `SaveAsync` (tłumaczenie wyścigu przeniesione do repozytorium) |
| P7 | `HangarTasks/Handler.cs:51` | druga kopia `!IsNullOrWhiteSpace(f.Criteria)` | `FlagRules.IsVerifiable`; opcjonalnie DTO `IsVerified` jako podpowiedź UI (poza zakresem minimalnym) |
| P8 | `client/pages/courses/HangarPage.tsx:152` + `client/services/contracts/courses-activate-flag-response.ts` | trzy statusy | czwarty status `VerificationRequired` z komunikatem; interfejs TS zaktualizowany w tej samej zmianie (`CLAUDE.md`, Frontend) |
| P9 | `Tests/Features/Courses/ActivateFlag/EndpointTests.cs:237-251` | `Activated` dla flagi z kryteriami bez weryfikacji | seed `FlagVerification` dla bieżącego użytkownika → `Activated`; nowy test bez seeda → `VerificationRequired` |
| Doc | `CLAUDE.md:96`, `CLAUDE.md:98`, `context/domain/glossary.md`, `prd.md:116` | „Weryfikacja NIE zapisuje flagi”; przekazanie kodu dopuszczone | „Weryfikacja zapisuje dowód (`FlagVerification`), nie flagę; aktywacja flagi z kryteriami wymaga dowodu tego użytkownika” |

### Plan faz

| Faza | Zakres | Test-first? | Gotowe, gdy… |
| --- | --- | --- | --- |
| **0. Decyzja** | Właściciel rozstrzyga Q-01 (wymuszać I-01?) i Q-08 (co z uczestnikami, którzy dostali `Correct` przed wdrożeniem, a jeszcze nie aktywowali — dziś nie ma po nich śladu w bazie, tylko w logach) | — | decyzja zapisana jako addendum w nowym folderze zmiany (`/10x-new flag-claim-aggregate`). **Wariant „nie wymuszać”:** zostają fazy 1 i 4 bez `VerificationRequired` (konsolidacja reguły w agregacie, bez zmiany zachowania), a do PRD trafia jawne zdanie, że flaga AI dowodzi znajomości kodu |
| 1. Agregat w izolacji | `FlagClaim`, błędy domenowe, `FlagRules` — czysta klasa bez EF | **tak** (`/10x-tdd`) — testy jednostkowe A1–A10 | `dotnet test --filter FullyQualifiedName~FlagClaimTests` zielone; agregat nie ma referencji do `AppDbContext` |
| 2. Persystencja | `FlagVerification`, `FlagVerificationConfiguration`, `DbSet` w `AppDbContext.cs` (sekcja Courses, obok `:32-36`), migracja `CoursesFlagVerifications`, repozytorium | **tak** dla indeksu (S1–S3, `[SqlServerFact]`) | migracja zawiera `IX_FlagVerifications_UserId_FlagId` unikalny; S1–S3 zielone przy `APP01_SQLSERVER_TESTS` |
| 3. Weryfikacja przez agregat | `VerifyAnswer/Handler.cs` na repozytorium | **tak** — E1–E3 przed zmianą handlera; istniejące testy `VerifyAnswer/EndpointTests.cs` bez zmian | wszystkie testy `VerifyAnswer` zielone, w tym `:397` (`Times.Never`) |
| 4. Aktywacja przez agregat | `ActivateFlag/Handler.cs`, nowy status w `Contracts.cs`, `HangarTasks` na `FlagRules` | **tak** — E4–E8 przed zmianą; P9 przepisany | `grep -rn "UserFlags.Add" src/server/App01` znajduje **jedno** miejsce (`FlagClaimRepository`); `grep -rn "IsNullOrWhiteSpace(f.Criteria)\|IsNullOrWhiteSpace(flag.Criteria)" src/server/App01/App01.Modules.Courses` → 0 trafień |
| 5. Klient | interfejs TS, komunikat w `HangarPage`, test Vitest dla `VerificationRequired` | **tak** (Vitest, `HangarPage.test.tsx`) | `npm test`, `npm run lint`, `npm run build` zielone |
| 6. Dokumenty | `CLAUDE.md:96-98`, `glossary.md`, `prd.md` (FR-004 Socrates, US-01) | — | `grep -n "Weryfikacja NIE zapisuje flagi" CLAUDE.md` → 0 trafień |

Każda faza kończy się pełną bramką CI z `CLAUDE.md`: `dotnet format --verify-no-changes`, `dotnet build APPS.sln`, `dotnet test APPS.sln`, a dla fazy 5 dodatkowo prettier (`--end-of-line auto`, `lessons.md`), lint, test i build klienta.

### Przypadki testowe niezmiennika

**Agregat — `Tests/Domain/Courses/FlagClaimTests.cs` (nowy, czysty xUnit)**

| ID | Stan wejściowy | Operacja | Oczekiwane | Rodzaj |
| --- | --- | --- | --- | --- |
| A1 | weryfikowalna, Open | `RecordVerificationPass(t)` | `VerifiedAt = t`, zwraca kod, `IsNewVerification` | legalne |
| A2 | weryfikowalna, Verified | `RecordVerificationPass(t2)` | `VerifiedAt` bez zmian, brak nowego zapisu | legalne (idempotencja) |
| A3 | weryfikowalna, Verified | `Activate(t)` | `EarnedAt = t`, `IsNewAward` | legalne |
| A4 | nieweryfikowalna, Open | `Activate(t)` | `EarnedAt = t` | legalne (flaga CTF) |
| A5 | **weryfikowalna, Open** | `Activate(t)` | **`VerificationRequiredException`**, `EarnedAt` null, brak `IsNewAward` | **nielegalne — I-01** |
| A6 | Earned | `Activate(t)` | `FlagAlreadyEarnedException` | nielegalne — I-02 |
| A7 | Earned | `EnsureCanBeVerified()` | `FlagAlreadyEarnedException` | nielegalne — I-06 |
| A8 | nieweryfikowalna | `EnsureCanBeVerified()` / `RecordVerificationPass` | `FlagNotVerifiableException` | nielegalne |
| A9 | weryfikowalna, Open | brak wywołania po `Incorrect` | stan bez zmian | legalne — I-05 |
| A10 | wyjątek w `Activate` | — | żaden stan nie zmienia się częściowo | fail-fast |

**Endpointy — `Tests/Features/Courses/VerifyAnswer/EndpointTests.cs`, `…/ActivateFlag/EndpointTests.cs`**

| ID | Scenariusz | Oczekiwane |
| --- | --- | --- |
| E1 | model `pass` | `Correct` z kodem; 1 wiersz `FlagVerifications`, 0 wierszy `UserFlags` |
| E2 | model `fail` / wyjątek / timeout | `Incorrect` / `Unavailable`; 0 wierszy w obu tabelach |
| E3 | dwa `Correct` po kolei | 1 wiersz `FlagVerifications` |
| E4 | **aktywacja flagi z kryteriami bez `Correct`** | **`VerificationRequired`**, `flagTitle` wypełniony, brak `code` w surowym JSON, 0 wierszy `UserFlags` |
| E5 | `Correct` → aktywacja tym samym kontem | `Activated`, 1 wiersz `UserFlags` |
| E6 | **użytkownik A ma `Correct`, użytkownik B aktywuje kodem A** | **B: `VerificationRequired`**; A może aktywować |
| E7 | flaga bez kryteriów, aktywacja bez weryfikacji | `Activated` (regresja: `EndpointTests.cs:270`) |
| E8 | flaga posiadana, aktywacja / weryfikacja | `AlreadyOwned`; przy weryfikacji model nie jest wołany |
| E9 | nieznany kod / kurs nieopublikowany | `Invalid` (regresja `:346`, `:364`) |
| E10 | błąd zapisu bez konkurenta | 500 (regresja `:326`) |
| E11 | 400 / 401 / brak `X-TOKEN` | bez zmian (`:406`, `:424`, `:437`) |

**SQL Server — `[SqlServerFact]`**

| ID | Scenariusz | Oczekiwane |
| --- | --- | --- |
| S1 | migracje zastosowane | unikalny indeks `(UserId, FlagId)` na `Courses.FlagVerifications` |
| S2 | 10 równoległych `Correct` (mock modelu) tego samego użytkownika | 1 wiersz `FlagVerifications`, wszystkie odpowiedzi `Correct` |
| S3 | 10 równoległych aktywacji po `Correct` | 1 wiersz `UserFlags`, jedno `Activated` (wzór `ActivateFlagRaceSqlServerTests.cs:32`) |

Bez zmian i nadal zielone: `Tests/Features/Courses/FlagCountConsistency/EndpointTests.cs`, `HangarFlags`, `Leaderboard`, `GroupProgress`. Read-modele nie są ruszane.

### Nowe nazwy „load-bearing” do zarejestrowania

Projekt nie ma rejestru kontraktów. Nazwy trzeba wpisać do `context/domain/glossary.md` i do `CLAUDE.md` (sekcja „Moduł Courses — weryfikacja odpowiedzi przez LLM”), bo zmiana którejkolwiek łamie bazę, kontrakt API albo testy:

| Nazwa | Rodzaj | Dlaczego nośna |
| --- | --- | --- |
| `FlagClaim` | agregat | jedyne miejsce tworzenia `UserFlag` |
| `IFlagClaimRepository` / `FlagClaimRepository` | port / adapter | jedyne miejsce zapytań i zapisu dla I-01/I-02 |
| `FlagRules.IsVerifiable` | reguła | jedyna definicja „zadania” |
| `FlagVerification`, `VerifiedAt` | encja / pole | dowód wykonania |
| `Courses.FlagVerifications`, `IX_FlagVerifications_UserId_FlagId` | tabela / indeks | wymuszenie w bazie; nazwa sprawdzana w S1 |
| `CoursesFlagVerifications` | migracja | nie edytować po wdrożeniu |
| `VerificationRequired` | status API (C# + TS) | kontrakt klienta, `courses-activate-flag-response.ts` |
| `VerificationRequiredException`, `FlagAlreadyEarnedException`, `InvalidFlagCodeException`, `FlagNotAvailableException`, `FlagNotVerifiableException` | błędy domenowe | mapowanie na odpowiedzi w handlerach |

### Pytania do właściciela (blokują Phase 0)

| ID | Pytanie | Kto |
| --- | --- | --- |
| Q-01 | Czy aktywacja flagi z kryteriami ma wymagać wcześniejszego `Correct` tego samego użytkownika? | właściciel portalu |
| Q-08 | Uczestnicy, którzy dostali `Correct` przed wdrożeniem i jeszcze nie aktywowali: mają zweryfikować ponownie (jedno wywołanie modelu) czy dostać backfill z logów? | właściciel portalu + osoba z dostępem do logów Serilog |
| Q-04 | Limit prób aktywacji (brute force kodów CTF) — osobna zmiana, poza tym planem? | właściciel portalu |
