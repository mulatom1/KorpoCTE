# Granica dostępu do treści kursów — Plan Brief

> Full plan: `context/changes/testing-course-content-access/plan.md`
> Research: `context/changes/testing-course-content-access/research.md`

## What & Why

Rollout Phase 1 test-planu, ryzyko #1: treść kursu nie może trafić do niezalogowanego, do zalogowanego przed chwilą publikacji ani pod publiczny adres pliku statycznego. Istniejące testy sprawdzają głównie kody statusu, a „404 na adresie pliku” albo „401 na endpoincie” nie dowodzi braku wycieku: fallback SPA potrafi zwrócić 200, a test daty zależy od strefy maszyny.

## Starting Point

Treść zwraca tylko `GET api/courses/course-content` (JWT + `X-TOKEN`) z filtrem `PublishDate <= UtcNow`. Pliki `.md` leżą poza webrootem, ale `UseStaticFiles` (przed autoryzacją) serwuje każdy `.md` w `wwwroot` jako `text/markdown`. `CourseContent/EndpointTests.cs` ma gotową fabrykę z tymczasowym `ContentPath` i `webRoot`.

## Desired End State

Testy integracyjne w `CourseContent/EndpointTests.cs` przechodzą na obecnym kodzie i robią się czerwone przy zdjęciu autoryzacji, przesunięciu granicy publikacji o 1 tick lub liczeniu jej w czasie lokalnym, oraz przy pojawieniu się pliku treści pod publicznym adresem. Test-plan i rejestr ryzyk opisują wzorzec i zaakceptowane luki.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Wyrocznia granicy publikacji | Chwila UTC, równość = widoczny (S-04) | Kod nigdy nie pokazuje kursu wcześniej niż PRD; encja to „data i godzina UTC”; PRD nie edytujemy | Plan |
| Kopia `.md` w `wwwroot/media/courses/<slug>/` | Udokumentować jako zaakceptowane ryzyko (§7 + `infrastructure.md`), bez blokady w `Program.cs` | Faza pozostaje czysto testowa; zgodne ze świadomą decyzją S-04 | Plan |
| Media i załączniki `data/` | Publiczne, także przed `PublishDate`; wpis do §7 | Zgodne z S-04; ochrona wymagałaby nowej funkcji | Plan |
| Tryb wykonania | Wszystkie fazy przez `/10x-implement` | Testy charakteryzujące mają być zielone od razu; czerwień dowodzona mutacją | Plan |
| Asercje | Wartownik treści w ciele + kontrola pozytywna; dla plików statycznych bez asercji statusu | Unika anty-wzorca „sam status” i trywialnie przechodzących testów | Research |
| Warstwa | Integracja WebApplicationFactory + InMemory | Żadne sprawdzenie nie zależy od semantyki SQL Server | Research |

## Scope

**In scope:**
- 401 bez wycieku dla JWT: brak, nieprawidłowy, wygasły × kurs opublikowany i przyszły
- Granica publikacji −1 / 0 / +1 tick przy strefie lokalnej ≠ UTC
- Adresy publiczne pliku `.md` w produkcyjnym układzie, z `index.html` SPA w webroocie i bez
- Cookbook §6, §7, §3 test-planu + wiersz w `infrastructure.md`

**Out of scope:**
- Zmiany w kodzie produkcyjnym (w tym blokada `.md`)
- Ochrona mediów i załączników, zmiana granularności daty, testy `CourseTiles`, SQL Server, e2e, Stryker.NET

## Architecture / Approach

Rozszerzamy istniejący `CourseContent/EndpointTests.cs` (jedna fabryka, `FixedTimeProvider`, temp `ContentPath`). Kurs zawiera unikalny wartownik. Testy negatywne sprawdzają jego brak w ciele odpowiedzi, a kontrole pozytywne (JWT → wartownik, miniatura → 200 `image/png`, SPA → `index.html`) dowodzą, że środowisko działa naprawdę. Webroot `<tmp>/wwwroot` i treść `<tmp>/content` są rodzeństwem, jak `ContentPath = ../courses-content` na hoście.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Brak wycieku bez JWT | `[Theory]` 401 × 6 bez wartownika + kontrola pozytywna | Token wygasły generowany z realnym zegarem (ClockSkew = 0) |
| 2. Granica publikacji | `[Theory]` −1/0/+1 tick, strefa lokalna ≠ UTC, seed `Unspecified` | Strefa musi być niezależna od bazy stref systemu |
| 3. Plik nieosiągalny publicznie | 6 adresów × 2 warianty webroota + 3 kontrole pozytywne | `HttpClient` normalizuje `..`, stąd zakodowane `%2e%2e` |
| 4. Dokumentacja | §6.1/6.2/6.5, §7 ×2, §3 `complete`, wiersz ryzyka w `infrastructure.md` | Rozjazd wpisów z Key Decisions |

**Prerequisites:** .NET SDK 10.0.100, `dotnet test APPS.sln` zielone przed startem.
**Estimated effort:** ~1–2 sesje, 4 krótkie fazy.

## Open Risks & Assumptions

- Zakładamy, że `UseSetting(WebHostDefaults.WebRootKey, …)` ustawia provider plików statycznych w TestServer. Kontrola pozytywna z miniaturą to weryfikuje: jeśli padnie, test jest nieważny, a nie zielony.
- Kopia `.md` w `wwwroot/media` pozostaje realną ścieżką wycieku, zależną od dyscypliny publikacji przez FTP.
- PRD FR-003 („data”) i implementacja („chwila UTC”) różnią się granularnością. Rozjazd jest opisany w cookbooku, a PRD pozostaje bez zmian.

## Success Criteria (Summary)

- Zdjęcie autoryzacji, przesunięcie granicy o 1 tick lub czas lokalny zamiast UTC kończą się czerwonym testem na każdej maszynie.
- Plik treści kursu w produkcyjnym układzie nie jest osiągalny pod żadnym z badanych adresów, niezależnie od fallbacku SPA.
- Następna zmiana w Courses znajduje wzorzec i zaakceptowane luki w test-planie.
