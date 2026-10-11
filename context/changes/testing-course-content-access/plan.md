# Granica dostępu do treści kursów — testy (rollout Phase 1, ryzyko #1) Implementation Plan

## Overview

Domykamy ryzyko #1 z `context/foundation/test-plan.md`: treść kursu nie może trafić do niezalogowanego, do zalogowanego przed chwilą publikacji ani pod publiczny adres pliku statycznego. Plan dopisuje testy integracyjne (WebApplicationFactory, EF InMemory) do istniejącego `CourseContent/EndpointTests.cs`. Każda asercja sprawdza treść odpowiedzi (wartownik w pliku kursu), a nie sam kod statusu. Kod produkcyjny się nie zmienia. Zaakceptowane luki operacyjne trafiają do dokumentacji.

## Current State Analysis

- Treść (`Body`) zwraca wyłącznie `GET api/courses/course-content` (`CourseContent/Handler.cs:80`), chroniony `.RequireAuthorization()` i `XTokenFilter` (`CourseContent/Endpoint.cs:31-32`).
- Filtr publikacji: `c.PublishDate <= _timeProvider.GetUtcNow().UtcDateTime` (`CourseContent/Handler.cs:56-58`).
- `UseStaticFiles` działa przed `UseAuthentication/UseAuthorization` (`Program.cs:84-101`), a domyślny `FileExtensionContentTypeProvider` serwuje `.md` jako `text/markdown`. Fallback `MapFallbackToFile("index.html")` (`Program.cs:111`) może zwrócić 200 z HTML SPA dla nieznanego adresu.
- Istniejące testy (`tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`):
  - 401 sprawdza tylko status (`:371-399`),
  - granica daty ma równość i +1 min (`:244-275`), ale `FixedTimeProvider` (`:58-68`) nie ustala `LocalTimeZone`, więc pomyłka „czas lokalny zamiast UTC” jest wykrywana zależnie od strefy maszyny,
  - brak testu adresu pliku statycznego w produkcyjnym układzie katalogów.
- Szczegóły i dowody: `context/changes/testing-course-content-access/research.md`.

## Desired End State

`dotnet test APPS.sln` zawiera testy, które przechodzą na obecnym kodzie i robią się czerwone, gdy:
1. endpoint treści przestanie wymagać JWT (brak / nieprawidłowy / wygasły token),
2. granica publikacji przesunie się o jeden tick w którąkolwiek stronę albo zostanie policzona w czasie lokalnym zamiast UTC,
3. plik `.md` kursu w produkcyjnym układzie (`wwwroot/media/courses/<slug>/` + sąsiedni katalog treści) stanie się osiągalny pod publicznym adresem, niezależnie od tego, czy webroot zawiera `index.html` SPA.

Test-plan §3/§6/§7 i rejestr ryzyk `infrastructure.md` opisują wzorzec oraz świadomie zaakceptowane luki.

### Key Discoveries:

- Wzorzec fabryki z parametrem `webRoot` (`UseSetting(WebHostDefaults.WebRootKey, …)`) i tymczasowym `ContentPath` już istnieje (`EndpointTests.cs:70-130`). Reużywamy go, nie tworzymy drugiej fabryki.
- `ServerDI.cs:65-67`: `ValidateLifetime = true`, `ClockSkew = TimeSpan.Zero`, więc token wygasły choćby sekundę temu jest odrzucany.
- `GenerateJwtToken` (`EndpointTests.cs:142-165`) używa realnego `DateTime.UtcNow`, nie `TimeProvider`. Wariant wygasły musi ustawić `notBefore`/`expires` w przeszłości względem zegara rzeczywistego.
- `_contentPath = <_tempRoot>/content` (`EndpointTests.cs:45-46`). Webroot `<_tempRoot>/wwwroot` daje katalogi-rodzeństwa jak na produkcji (`appsettings.Example.json:37` → `ContentPath = ../courses-content`).
- Stryker.NET nie jest zainstalowany (brak `.config/dotnet-tools.json`), więc dowód czerwieni robimy ręcznymi mutacjami.

## What We're NOT Doing

- Żadnych zmian w kodzie produkcyjnym (`Program.cs`, handler, czytnik). Blokada `.md` w `UseStaticFiles` odrzucona: kopia `.md` w `wwwroot/media/courses/<slug>/` zostaje zaakceptowanym ryzykiem operacyjnym, opisanym w test-planie §7 i w `infrastructure.md`.
- Ochrona mediów i załączników (`wwwroot/media/courses/<slug>/data/*.docx|pdf|txt`): pozostają publiczne zgodnie z S-04, wpis do §7.
- Zmiana granularności daty publikacji na dzień kalendarzowy. Wyrocznią jest chwila UTC z S-04, PRD nie edytujemy.
- Testy `CourseTiles` (publiczne pola frontmattera, nie treść), testy na prawdziwym SQL Server (Phase 2 test-planu), e2e.
- Instalacja Stryker.NET.

## Implementation Approach

Trzy fazy testowe, każda zamyka jedno oblicze ryzyka. Każda używa jednego unikalnego wartownika treści (np. stała `ContentSentinel` wpisywana do treści kursu) zamiast ogólnego `# Treść …`. Każda ma kontrolę pozytywną, czyli dowód, że wartownik naprawdę wraca, gdy dostęp jest dozwolony. Bez tej kontroli asercja „brak wartownika” mogłaby przejść trywialnie. Na koniec każdej fazy jedna ręczna mutacja potwierdza, że test umie być czerwony. Mutację cofamy przed commitem. Wszystkie fazy wykonuje `/10x-implement`.

## Critical Implementation Details

- **Asercje plików statycznych nie mogą opierać się na statusie.** W wariancie z `index.html` w webroocie nieznany adres zwraca 200 (SPA), a bez niego 404. Oba warianty są poprawne. O wycieku decyduje wyłącznie obecność wartownika w ciele i `Content-Type: text/markdown`.
- **`HttpClient` normalizuje segmenty `..` przed wysłaniem.** Próba wyjścia z `media/courses` musi używać zakodowanej postaci (`%2e%2e`), inaczej test sprawdza ten sam adres co `/content/...`.

## Phase 1: Brak wycieku treści bez JWT

### Overview

Zastępuje dwa testy 401, które sprawdzają tylko status, jednym testem parametryzowanym z asercją na treści odpowiedzi.

### Changes Required:

#### 1. Wartownik treści i helper kursu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`

**Intent**: Dodać stałą wartownika (unikalny ciąg niewystępujący w komunikatach API) i helper zapisujący poprawny kurs z wartownikiem w treści Markdown oraz w tytule frontmattera. Asercje „brak treści” zyskują w ten sposób jednoznaczny cel.

**Contract**: `private const string ContentSentinel`; helper `WriteCourseWithSentinel(string slug)` obok `WriteValidCourse` (`:190-193`). Istniejące testy pozostają bez zmian.

#### 2. Test 401 z asercją treści

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`

**Intent**: Usunąć `CourseContent_WithoutJwtToken_ReturnsUnauthorized` i `CourseContent_WithInvalidJwtToken_ReturnsUnauthorized` (`:371-399`) i zastąpić je `[Theory]` po wariancie tokena (brak, nieprawidłowy, wygasły) × kurs (opublikowany, z przyszłą `PublishDate`). Oczekiwane: 401, a ciało nie zawiera wartownika. Dodać kontrolę pozytywną: ten sam seed z ważnym JWT zwraca 200 z wartownikiem w `content`.

**Contract**: `CourseContent_WithoutValidJwt_ReturnsUnauthorizedWithoutContent(string tokenVariant, bool published)` (6 przypadków) + `CourseContent_WithValidJwt_ReturnsSentinelContent`. Token wygasły: podpisany poprawnym kluczem, issuer i audience, z `notBefore`/`expires` w przeszłości (np. −2 h / −1 h względem `DateTime.UtcNow`). Wymaga rozszerzenia `GenerateJwtToken` o opcjonalny parametr wygaśnięcia.

### Success Criteria:

#### Automated Verification:

- Testy fazy przechodzą: `dotnet test APPS.sln --filter "FullyQualifiedName~Courses.CourseContent"`
- Build bez ostrzeżeń w zmienionym projekcie testów: `dotnet build APPS.sln`
- Formatowanie: `dotnet format APPS.sln --verify-no-changes`

#### Manual Verification:

- Mutacja: tymczasowe usunięcie `.RequireAuthorization()` z `CourseContent/Endpoint.cs` daje czerwone przypadki testu 401. Po cofnięciu zmiany `git diff src/` jest pusty.

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na potwierdzenie mutacji przez człowieka przed fazą 2.

---

## Phase 2: Granica publikacji odporna na strefę czasową

### Overview

Granica `PublishDate <= now` (chwila UTC, równość = widoczny) jest testowana co do ticka i deterministycznie, niezależnie od strefy maszyny (lokalny Windows vs CI w UTC).

### Changes Required:

#### 1. `FixedTimeProvider` z jawną strefą lokalną

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`

**Intent**: Nadpisać `LocalTimeZone` w `FixedTimeProvider` (`:58-68`) strefą z dodatnim przesunięciem względem UTC (np. +5 h, utworzoną przez `TimeZoneInfo.CreateCustomTimeZone`, bez zależności od bazy stref systemu). Pomyłka „czas lokalny zamiast UTC” przesuwa wtedy granicę w kierunku wycieku na każdej maszynie.

**Contract**: `FixedTimeProvider.LocalTimeZone` zwraca stałą strefę ≠ UTC. `GetUtcNow()` bez zmian.

#### 2. Test granicy co do ticka

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`

**Intent**: Zastąpić `CourseContent_PublishDateEqualToNow_ReturnsOk` i `CourseContent_PublishDateInFuture_ReturnsNotFound` (`:244-275`) jednym `[Theory]` po przesunięciu `PublishDate` względem `Now`. Seed z `DateTimeKind.Unspecified`, czyli w kształcie, w jakim EF czyta `datetime2`. Oczekiwane: −1 tick → 200 z wartownikiem; 0 → 200 z wartownikiem; +1 tick → 404 z komunikatem `NotFoundMessage` i bez wartownika.

**Contract**: `CourseContent_PublishDateBoundary_VisibleOnlyFromPublishInstantUtc(long offsetTicks, bool expectVisible)` (3 przypadki).

### Success Criteria:

#### Automated Verification:

- Testy fazy przechodzą: `dotnet test APPS.sln --filter "FullyQualifiedName~Courses.CourseContent"`
- Formatowanie: `dotnet format APPS.sln --verify-no-changes`

#### Manual Verification:

- Mutacja A: `GetUtcNow().UtcDateTime` → `GetLocalNow().DateTime` w `CourseContent/Handler.cs:56` daje czerwony przypadek +1 tick. Mutacja B: `<=` → `<` daje czerwony przypadek 0. Po cofnięciu zmian `git diff src/` jest pusty.

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na potwierdzenie mutacji przez człowieka przed fazą 3.

---

## Phase 3: Plik treści nieosiągalny pod publicznym adresem hosta

### Overview

Test w produkcyjnym układzie katalogów dowodzi, że plik `.md` kursu nie jest serwowany przez `UseStaticFiles` ani przez fallback SPA, a webroot faktycznie działa (kontrola pozytywna).

### Changes Required:

#### 1. Helper produkcyjnego układu

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`

**Intent**: Helper budujący pod `_tempRoot` układ jak na hoście: `wwwroot/media/courses/<slug>/thumbnail.png` (kilka bajtów nagłówka PNG), opcjonalnie `wwwroot/index.html` z rozpoznawalnym znacznikiem SPA, oraz kurs z wartownikiem w `_contentPath` (`<_tempRoot>/content/<slug>/<slug>.md`). Zwraca ścieżkę webroota dla `CreateFactory(..., webRoot:)`.

**Contract**: `string CreateProductionLayout(string slug, bool withSpaIndex)`. `Courses:ContentPath` pozostaje kluczem konfiguracji czytanym przez produkcję (`CourseFrontmatterReader.cs:133`).

#### 2. Test adresów publicznych

**File**: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs`

**Intent**: `[Theory]` po `withSpaIndex` (true/false). Klient bez JWT i bez `X-TOKEN` odpytuje listę kandydackich adresów pliku treści. Dla każdego: ciało nie zawiera wartownika, a `Content-Type` nie jest `text/markdown`. Status nie jest asertowany. Kontrole pozytywne w tym samym teście: `/media/courses/<slug>/thumbnail.png` → 200 `image/png` (webroot serwowany); w wariancie z SPA nieznany adres zwraca znacznik `index.html` (fallback aktywny); żądanie API z JWT i `X-TOKEN` zwraca wartownika (ten sam `ContentPath` jest naprawdę używany).

**Contract**: `CourseContentFile_IsNotReachableUnderPublicUrls(bool withSpaIndex)`. Kandydackie adresy (dla `slug = kurs-a`): `/media/courses/kurs-a/kurs-a.md`, `/kurs-a/kurs-a.md`, `/content/kurs-a/kurs-a.md`, `/courses-content/kurs-a/kurs-a.md`, `/media/courses/kurs-a/`, `/media/courses/%2e%2e/%2e%2e/content/kurs-a/kurs-a.md`. Komunikat asercji wskazuje adres, który zawiódł.

### Success Criteria:

#### Automated Verification:

- Testy fazy przechodzą: `dotnet test APPS.sln --filter "FullyQualifiedName~Courses.CourseContent"`
- Pełny zestaw testów przechodzi: `dotnet test APPS.sln`
- Formatowanie: `dotnet format APPS.sln --verify-no-changes`

#### Manual Verification:

- Mutacja: tymczasowe skopiowanie `<slug>.md` do `wwwroot/media/courses/<slug>/` w helperze (scenariusz zaakceptowanego ryzyka) daje czerwony test dla `/media/courses/kurs-a/kurs-a.md` w obu wariantach. Po cofnięciu zmiany test jest zielony.

**Implementation Note**: Po przejściu weryfikacji automatycznej zatrzymaj się na potwierdzenie mutacji przez człowieka przed fazą 4.

---

## Phase 4: Cookbook, zaakceptowane ryzyka i status fazy

### Overview

Utrwala wzorzec testu i świadome decyzje tej fazy w dokumentach fundamentu.

### Changes Required:

#### 1. Test-plan

**File**: `context/foundation/test-plan.md`

**Intent**: Wypełnić cookbook i zapisać zaakceptowane luki, żeby kolejne zmiany w Courses nie odkrywały ich od nowa.

**Contract**:
- §6.1: zastąpić linię „Wzorzec granicy dostępu … TBD” odnośnikiem do testów z faz 1–3.
- §6.2: wzorzec w 3–5 punktach: wartownik treści + kontrola pozytywna, brak asercji na statusie dla plików statycznych (fallback SPA), produkcyjny układ katalogów-rodzeństw, `FixedTimeProvider` ze strefą ≠ UTC, wyrocznia daty = chwila UTC (S-04), mimo że PRD FR-003 mówi „data”.
- §6.5: 2–3 linie wniosków z fazy.
- §7: dwa wpisy: (a) kopia `.md` kursu w `wwwroot/media/courses/<slug>/` jest serwowana publicznie; zaakceptowane ryzyko operacyjne (decyzja 2026-10-11, bez blokady w `Program.cs`); re-evaluate przy automatyzacji publikacji lub incydencie; (b) media i załączniki `media/courses/<slug>/**` (także `data/*.docx|pdf|txt`) są publiczne, również przed `PublishDate`, zgodnie z S-04.
- §3: wiersz Phase 1 → `complete`; §8 „Last updated”.

#### 2. Rejestr ryzyk infrastruktury

**File**: `context/foundation/infrastructure.md`

**Intent**: Dopisać do tabeli ryzyk (obok wiersza `DeleteExistingFiles`, `:108`) wiersz „kopia `<slug>.md` w `wwwroot/media/courses/<slug>/` = publiczny wyciek treści”. Mitygacja: przy publikacji przez FTP do `wwwroot/media` wgrywać tylko media; `.md` wyłącznie do `Courses:ContentPath` poza katalogiem witryny.

**Contract**: jeden nowy wiersz tabeli w istniejącym formacie (ryzyko | źródło | prawdopodobieństwo | wpływ | mitygacja).

### Success Criteria:

#### Automated Verification:

- W `context/foundation/test-plan.md` nie zostało „TBD — see §3 Phase 1”: `grep -c "TBD — see §3 Phase 1" context/foundation/test-plan.md` zwraca 0

#### Manual Verification:

- Wpisy §7 i wiersz w `infrastructure.md` zgadzają się z decyzjami z `plan-brief.md` (Key Decisions)

---

## Testing Strategy

### Integration Tests:

- Faza 1: 6 przypadków 401 bez wartownika + kontrola pozytywna z JWT.
- Faza 2: granica −1 / 0 / +1 tick przy strefie lokalnej ≠ UTC i seedzie `Unspecified`.
- Faza 3: 6 adresów × 2 warianty webroota bez wartownika + 3 kontrole pozytywne.

### Manual Testing Steps:

1. Mutacje opisane w każdej fazie: każda daje czerwień, a po cofnięciu `git diff src/` jest pusty.

## Performance Considerations

Brak. Dochodzi kilkanaście szybkich testów WebApplicationFactory na InMemory.

## Migration Notes

Brak. Bez zmian w schemacie ani konfiguracji produkcyjnej.

## References

- Research: `context/changes/testing-course-content-access/research.md`
- Test-plan: `context/foundation/test-plan.md` §2 (ryzyko #1), §3 Phase 1, §6
- Decyzje S-04: `context/archive/2026-10-07-course-content-reading/plan.md:5,14,46,48`
- Wzorzec testu: `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs:70-200`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Brak wycieku treści bez JWT

#### Automated

- [x] 1.1 Testy fazy przechodzą: `dotnet test APPS.sln --filter "FullyQualifiedName~Courses.CourseContent"` — 5dc86ca
- [x] 1.2 Build bez ostrzeżeń w zmienionym projekcie testów: `dotnet build APPS.sln` — 5dc86ca
- [x] 1.3 Formatowanie: `dotnet format APPS.sln --verify-no-changes` — 5dc86ca

#### Manual

- [x] 1.4 Mutacja: usunięcie `.RequireAuthorization()` daje czerwone przypadki 401; po cofnięciu `git diff src/` pusty — 5dc86ca

### Phase 2: Granica publikacji odporna na strefę czasową

#### Automated

- [x] 2.1 Testy fazy przechodzą: `dotnet test APPS.sln --filter "FullyQualifiedName~Courses.CourseContent"`
- [x] 2.2 Formatowanie: `dotnet format APPS.sln --verify-no-changes`

#### Manual

- [x] 2.3 Mutacje A (czas lokalny) i B (`<` zamiast `<=`) dają czerwień; po cofnięciu `git diff src/` pusty

### Phase 3: Plik treści nieosiągalny pod publicznym adresem hosta

#### Automated

- [ ] 3.1 Testy fazy przechodzą: `dotnet test APPS.sln --filter "FullyQualifiedName~Courses.CourseContent"`
- [ ] 3.2 Pełny zestaw testów przechodzi: `dotnet test APPS.sln`
- [ ] 3.3 Formatowanie: `dotnet format APPS.sln --verify-no-changes`

#### Manual

- [ ] 3.4 Mutacja: kopia `<slug>.md` w `wwwroot/media/courses/<slug>/` daje czerwień w obu wariantach; po cofnięciu zielono

### Phase 4: Cookbook, zaakceptowane ryzyka i status fazy

#### Automated

- [ ] 4.1 Brak „TBD — see §3 Phase 1” w test-planie

#### Manual

- [ ] 4.2 Wpisy §7 i wiersz w `infrastructure.md` zgodne z Key Decisions
