---
date: 2026-10-11T14:23:35+02:00
researcher: Claude (Opus 5.5) dla Tomasz Mularczyk
git_commit: f6700fa
branch: main
repository: 10xDevs4
topic: "Rollout Phase 1 test-plan.md — granica dostępu do treści kursów (ryzyko #1)"
tags: [research, testing, courses, course-content, static-files, authorization, publish-date]
status: complete
last_updated: 2026-10-11
last_updated_by: Claude (Opus 5.5)
---

# Research: granica dostępu do treści kursów (Phase 1, ryzyko #1)

**Date**: 2026-10-11T14:23:35+02:00
**Researcher**: Claude (Opus 5.5) dla Tomasz Mularczyk
**Git Commit**: f6700fa
**Branch**: main
**Repository**: 10xDevs4

## Research Question

Ugruntować rollout Phase 1 z `context/foundation/test-plan.md` dla ryzyka #1: treść kursu dostępna dla niezalogowanego (przez API lub publiczne pliki statyczne) albo dla zalogowanego przed datą publikacji. Zweryfikować, a nie przyjąć na wiarę, intencję odpowiedzi na ryzyko:

- bez JWT żaden endpoint nie zwraca treści kursu,
- kurs z datą publikacji w przyszłości daje 404, także na granicy równości dat (UTC),
- pliku treści kursu nie da się pobrać pod żadnym publicznym adresem hosta.

Do zakwestionowania: „treść poza wwwroot = bezpieczna” oraz „401 = brak wycieku”. Czego unikać: asercji na samym kodzie statusu bez sprawdzenia treści odpowiedzi, treści w katalogu, którego produkcja nie używa, oraz pominięcia granicy dat.

## Summary

1. **API: treść zwraca dokładnie jeden endpoint, chroniony JWT.** W sprawdzonym module Courses treść Markdown (`CourseDocument.Body`) trafia do odpowiedzi tylko przez `GET api/courses/course-content` (`CourseContent/Handler.cs:80`). Endpoint ma `.RequireAuthorization()` i `XTokenFilter` (`CourseContent/Endpoint.cs:31-32`). Drugi konsument czytnika, `CourseTiles`, odczytuje tylko frontmatter (`Description` → `ShortDescription`) i nie zwraca `Body` (`CourseTiles/Contracts.cs`, `CourseTiles/Handler.cs:79`). Oprócz czytnika żaden kod w `src/server/App01` nie czyta plików kursów (grep `ReadAllText|PhysicalFile|Results.File|SendFileAsync`). Ta część ryzyka jest pokryta testami 401 (`EndpointTests.cs:371-399`), ale **asercja dotyczy wyłącznie kodu statusu**. To jest dokładnie anty-wzorzec z test-planu.
2. **Data publikacji: zachowanie zgodne z decyzją wycinka S-04, ale niezgodne co do granularności z literą PRD.** Handler filtruje `c.PublishDate <= now` przy `now = _timeProvider.GetUtcNow().UtcDateTime` (`CourseContent/Handler.cs:56-58`). Testy pokrywają równość (`== Now` → 200, `EndpointTests.cs:244-260`) i +1 minutę (→ 404, `EndpointTests.cs:263-275`). PRD FR-003 mówi o *dacie* („równa aktualnej dacie lub wcześniejsza”, `prd.md:110`, `:191`), a encja i plan S-04 o *dacie i godzinie UTC* (`Course.cs:10`, `archive/2026-10-07-course-content-reading/plan.md:5`). Rozbieżność nie powoduje wcześniejszego udostępnienia: implementacja jest ostrzejsza od PRD. Wyrocznię dla planu trzeba jednak rozstrzygnąć, patrz Open Questions.
3. **Pliki statyczne: założenie „treść poza wwwroot = bezpieczna” jest prawdziwe tylko warunkowo.** `UseStaticFiles` działa przed `UseAuthentication/UseAuthorization` (`Program.cs:84-101`), więc wszystko w `wwwroot` jest publiczne. Domyślny `FileExtensionContentTypeProvider` w ASP.NET Core 10.0.12 mapuje `.md` i `.markdown` na `text/markdown`, a `.txt`, `.pdf` i `.docx` również mają mapowanie. Jedyną obroną w kodzie jest bezpiecznik czytnika (`CourseFrontmatterReader.cs:142-156`). Wyłącza on treść, gdy **`ContentPath`** leży w webroocie, ale **nie wykrywa kopii pliku `.md` w `wwwroot/media/courses/<slug>/`**. Tymczasem lokalne `wwwroot/media/courses/<slug>/` to ręczna kopia katalogu kursu z `my-courses/<slug>/` bez pliku `.md`. Wyciek zależy więc od kroku ręcznego, a decyzja S-04 świadomie nie blokuje `.md` w `UseStaticFiles` (`archive/.../plan.md:46`).
4. **Założenie „404 na adresie pliku = brak wycieku” też jest warunkowe.** `MapFallbackToFile("index.html")` (`Program.cs:111`) zwraca SPA (200, HTML) dla nieznanych tras, gdy `index.html` istnieje w webroocie. Test pliku statycznego nie może więc sprawdzać samego statusu (404 vs 200). Musi sprawdzić, że ciało odpowiedzi nie zawiera znacznika treści kursu.
5. **Najtańsza warstwa to integracja z WebApplicationFactory w tym samym stylu co istniejące `CourseContent/EndpointTests.cs`**: seed InMemory, `FixedTimeProvider`, tymczasowy `ContentPath` i `WebRootKey`. Żadne z tych sprawdzeń nie zależy od semantyki SQL Server, więc InMemory nie kłamie w tej fazie.

## Detailed Findings

### A. Trasy zwracające treść kursu (API)

- Rejestracja endpointów Courses: 9 wywołań `AddEndpoint` (`ModuleDI.cs:26-34`). Treść (`Body`) zwraca tylko `CourseContent` (`Handler.cs:80`). Pozostałe 8 nie mają w zależnościach `ICourseFrontmatterReader` poza `CourseTiles` (grep po `ICourseFrontmatterReader`: `CourseContent/Handler.cs:26`, `CourseTiles/Handler.cs:28`, `ModuleDI.cs:19`).
- `CourseTiles` jest publiczny (bez JWT) i zwraca `Slug, Title, ShortDescription, Tags, ImageUrl` (`CourseTiles/Contracts.cs`, rekord `CourseTileDto`). Filtruje `PublishDate <= now` (`CourseTiles/Handler.cs:60`). Treść w tym endpoincie to wyłącznie frontmatter, czyli zgodnie z PRD „publiczne pozostają wyłącznie kafelki” (`prd.md:83`, `:187`).
- `CourseContent/Endpoint.cs:31-32`: `.AddEndpointFilter<XTokenFilter>()` + `.RequireAuthorization()`. Autoryzacja endpointu działa w `UseAuthorization` (`Program.cs:101`) przed wykonaniem handlera, więc bez JWT handler (i czytnik pliku) w ogóle się nie uruchamia. To wniosek z kolejności pipeline'u. Rzeczywiste ciało odpowiedzi 401 potwierdzi dopiero test.
- Istniejące testy 401: `CourseContent_WithoutJwtToken_ReturnsUnauthorized` (`EndpointTests.cs:371-383`) i `CourseContent_WithInvalidJwtToken_ReturnsUnauthorized` (`:386-399`). Obie asercje to tylko `Assert.Equal(HttpStatusCode.Unauthorized, ...)`. Nie sprawdzają, że ciało nie zawiera znacznika treści (`# Treść {slug}` z `WriteValidCourse`, `:190-193`). **Luka do domknięcia** (tani, czysto asercyjny dodatek).
- Brak testu: niezalogowany + `X-TOKEN` + kurs przyszły (czy odpowiedź 401 nie różni się od 401 dla opublikowanego, czyli nie zdradza istnienia kursu). To wariant opcjonalny, niski koszt.

### B. Porównanie daty publikacji

- `CourseContent/Handler.cs:55-58`: komentarz „UTC, równość = widoczny”, `now = _timeProvider.GetUtcNow().UtcDateTime`, filtr w zapytaniu EF `c.PublishDate <= now`.
- `TimeProvider` rejestrowany przez `TryAddSingleton(TimeProvider.System)` (`ModuleDI.cs:20`). Testy podmieniają go na `FixedTimeProvider(Now)` z `Now = 2026-06-01 12:00 UTC` (`EndpointTests.cs:39`, `:58-68`, `:126-127`).
- Pokrycie granicy: równość co do ticka → 200 (`:244-260`), +1 minuta → 404 (`:263-275`). Mutacja `<=` → `<` jest zabijana przez test równości. Mutacja usunięcia filtra daty jest zabijana przez test +1 min.
- **Niepokryte:** zamiana `GetUtcNow().UtcDateTime` na czas lokalny (np. `GetLocalNow().DateTime`). `FixedTimeProvider` nie nadpisuje `LocalTimeZone`, więc używa strefy maszyny. Na agencie CI w UTC mutant przeżyje, a na lokalnym Windows (UTC+1/+2) zostanie zabity. Taki test daje inny wynik zależnie od maszyny. Tani fix w teście: `FixedTimeProvider` z jawną strefą różną od UTC (nadpisanie `LocalTimeZone`). Wtedy pomyłka czasu lokalnego deterministycznie przesuwa granicę.
- Kind: seed używa `DateTimeKind.Utc`. Porównanie `DateTime` w .NET i InMemory nie uwzględnia `Kind` (porównuje ticki), a SQL Server porównuje wartości `datetime2`. Lekcja „daty z bazy w DTO jako UTC” (`lessons.md`) dotyczy serializacji DTO, a `CourseContent.Response` nie zawiera daty. Ten endpoint jej więc nie podlega. Seed z `Kind = Unspecified` nie zmieni wyniku porównania, ale można go dodać jako tani wariant zgodny z produkcją.
- Rozbieżność granularności: patrz Open Questions #1.

### C. Pliki statyczne i rozwiązanie ścieżki treści

- Pipeline (`Program.cs`): `UseStaticFiles(new StaticFileOptions { ContentTypeProvider })` w liniach 84-87, a `UseAuthentication` i `UseAuthorization` dopiero w liniach 99-101. Dokumentacja ASP.NET Core (Context7, `/dotnet/aspnetcore.docs`, „Static file authorization”, checked: 2026-10-11): przy takiej kolejności „no authorization checks are performed on the static files … publicly accessible”.
- `ContentTypeProvider` = domyślny `FileExtensionContentTypeProvider` + `.m3u8` i `.ts` (`Program.cs:79-82`). `ServeUnknownFileTypes` nie jest ustawione. W runtime `Microsoft.AspNetCore.App 10.0.12` (heap stringów `Microsoft.AspNetCore.StaticFiles.dll`) obecne są wpisy `.markdown → text/markdown` i `.md` (wpis bezpośrednio po nim, ten sam deduplikowany literał `text/markdown`). Przedostatni plan potwierdził to empirycznie: `GET /media/courses/korpo-cte-300/korpo-cte-300.md` → 200 `text/markdown` (`archive/2026-10-07-course-content-reading/plan.md:14`). **Wniosek: każdy `.md` w webroocie jest publiczny.**
- Rozwiązanie ścieżki: `GetContentRoot()` czyta `Courses:ContentPath` i robi `Path.GetFullPath(configured, AppContext.BaseDirectory)` (`CourseFrontmatterReader.cs:131-140`). Plik to `<root>/<slug>/<slug>.md` po walidacji regexem `^[a-z0-9][a-z0-9_-]*$` i sprawdzeniu prefiksu (`:53-57`, `:79-85`). Slug pochodzi z encji, nie z requestu (`Handler.cs:68-69`).
- Bezpiecznik: gdy `ContentPath` jest równy `WebRootPath` lub leży w nim → `LogError` i `null` (`:142-156`). Pusty `WebRootPath` wyłącza sprawdzenie (`:143-145`). Testy: `CourseContent_WithContentPathInsideWebRoot_ReturnsNotFound` (2 warianty, `EndpointTests.cs:416-433`) i `..._WithWebRootNextToContentPath_ReturnsOk` (`:435-452`). **Te testy sprawdzają endpoint API, nie sprawdzają adresu pliku statycznego.** Przy `ContentPath` w webroocie API daje 404, ale plik nadal jest serwowany przez `UseStaticFiles`. Bezpiecznik chroni tylko przed tym, by aplikacja *działała* przy błędnej konfiguracji, a nie przed samym wyciekiem.
- Układ rzeczywisty (lokalny, ignorowany przez git: `.gitignore:24` `my-courses/`, `src/server/.gitignore:59` `**/wwwroot/media/`):
  - `appsettings.Development.json:31` → `ContentPath = c:/PROJEKTY/10xDevs4/my-courses` (poza webrootem),
  - `my-courses/korpo-cte-300/{korpo-cte-300.md, thumbnail.png, images/}` oraz `my-courses/korpo-cte-301/{korpo-cte-301.md, thumbnail.png, data/}`,
  - `wwwroot/media/courses/korpo-cte-300/{thumbnail.png, images/}` oraz `wwwroot/media/courses/korpo-cte-301/{thumbnail.png, data/*.docx|*.pdf|*.txt}`, czyli kopia katalogu kursu **bez** `.md`.
  - `appsettings.Example.json:37-38` → `ContentPath = ../courses-content`, `MediaUrlBase = /media/courses`.
- **Realna ścieżka awarii (odpowiedź na „treść poza wwwroot = bezpieczna”):** publikacja (FR-010, „poza interfejsem aplikacji”, `prd.md:133`) polega na ręcznym wgraniu dwóch drzew. Skopiowanie całego katalogu kursu (z `.md`) do `wwwroot/media/courses/<slug>/` udostępnia treść każdemu, bez logowania i bez względu na `PublishDate`. Kod nie wykrywa tego przypadku: bezpiecznik patrzy na `ContentPath`, nie na zawartość webroota. Test zgodny z obecnym kodem może tylko udokumentować tę lukę (byłby czerwony). Zamknięcie jej wymaga zmiany w produkcji, której S-04 świadomie nie zrobił. To decyzja, patrz Open Questions #2.
- **Załączniki przed publikacją:** pliki w `wwwroot/media/courses/<slug>/` (obrazy, a dla `korpo-cte-301` także `.docx/.pdf/.txt` z `data/`) są publiczne również dla kursów z `PublishDate` w przyszłości. S-04 uznał media za publiczne (`archive/.../plan.md:48`: „publiczna jest tylko treść tekstowa”). Nie jest jasne, czy materiały zadania w `data/` to „treść kursu” w rozumieniu PRD. Patrz Open Questions #3.
- **SPA fallback:** `MapFallbackToFile("index.html")` (`Program.cs:111`). Lokalnie `wwwroot` nie ma `index.html`, więc nieznany plik daje 404. Jeśli wdrożenie umieszcza build klienta w webroocie hosta (na co wskazuje fallback), ten sam adres da 200 z HTML SPA. Plan S-04 weryfikował brak wycieku przez „`.md` zwraca 404” (`archive/.../plan.md:146`, `:278`), czyli przez status. **Ten werdykt jest poprawny dla lokalnego układu, ale nie przenosi się na host z `index.html`.** Test musi więc sprawdzać brak znacznika treści w ciele i/lub `Content-Type != text/markdown`.
- Środowisko testowe: `WebApplicationFactory<Program>` ustawia content root na katalog `App01.Bootstrapper.Api`. Lokalne `wwwroot` (z `media/`) istnieje tylko u dewelopera, bo jest ignorowane przez git, więc w CI webroot jest inny lub nie istnieje. Test pliku statycznego musi budować własny tymczasowy webroot przez `UseSetting(WebHostDefaults.WebRootKey, ...)`, jak `EndpointTests.cs:77-80`. Żeby uniknąć anty-wzorca „katalog nieużywany przez produkcję”, powinien odtwarzać produkcyjny kształt: `<webroot>/media/courses/<slug>/` z mediami oraz `ContentPath` jako katalog-rodzeństwo z `<slug>/<slug>.md`, konfigurowany kluczem `Courses:ContentPath`, którego czyta produkcja.

### D. Istniejące testy — co już jest, czego brakuje

| Właściwość | Test | Stan |
|---|---|---|
| 200 dla opublikowanego, treść bez frontmattera, brak `publishDate`/`description` w JSON | `EndpointTests.cs:203-241` | jest, z asercją na treści |
| `PublishDate == now` → 200 | `:244-260` | jest |
| `PublishDate = now + 1 min` → 404 + komunikat | `:263-275` | jest (`AssertNotFound` sprawdza komunikat, `:195-200`, ale nie brak znacznika treści) |
| bez JWT / zły JWT → 401 | `:371-399` | **tylko status** |
| bez `X-TOKEN` → 403 | `:402-413` | tylko status (poza zakresem ryzyka #1) |
| `ContentPath` w webroocie → API 404 | `:416-433` | jest, ale nie sprawdza adresu statycznego |
| plik `.md` nieosiągalny przez `UseStaticFiles` w produkcyjnym układzie | — | **brak** |
| kopia `.md` w `wwwroot/media/courses/<slug>/` | — | **brak** (zachowanie obecnie: wyciek) |
| odporność granicy daty na strefę lokalną maszyny | — | **brak** |

## Code References

- `src/server/App01/App01.Modules.Courses/Features/CourseContent/Endpoint.cs:18-32` — trasa `api/courses/course-content`, `XTokenFilter` + `RequireAuthorization`
- `src/server/App01/App01.Modules.Courses/Features/CourseContent/Handler.cs:55-80` — filtr `PublishDate <= now` (UTC), 404 z jednym komunikatem, ścieżka ze sluga encji, zwrot `Body`
- `src/server/App01/App01.Modules.Courses/Content/CourseFrontmatterReader.cs:79-85` — budowa ścieżki i kontrola prefiksu
- `src/server/App01/App01.Modules.Courses/Content/CourseFrontmatterReader.cs:131-156` — `ContentPath` względem `AppContext.BaseDirectory`, bezpiecznik webroota
- `src/server/App01/App01.Modules.Courses/Features/CourseTiles/Handler.cs:60` — publiczne kafelki też filtrują `PublishDate <= now`
- `src/server/App01/App01.Modules.Courses/ModuleDI.cs:19-20,26-34` — rejestracja czytnika, `TimeProvider.System`, 9 endpointów
- `src/server/App01/App01.Bootstrapper.Api/Program.cs:79-111` — `UseStaticFiles` przed auth, mapy MIME, `MapFallbackToFile("index.html")`
- `src/server/App01/App01.Shared.Application/Entities/Courses/Course.cs:10-11` — „Data i godzina publikacji w UTC”
- `src/server/App01/App01.Bootstrapper.Api/appsettings.Example.json:36-40` — `Courses:ContentPath`, `MediaUrlBase`
- `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs:39,58-68,70-130,190-200,244-275,371-452` — wzorzec fabryki, `FixedTimeProvider`, helpery, testy granicy i 401

## Architecture Insights

- Ochrona treści ma trzy niezależne warstwy, każda z inną słabością: (1) autoryzacja endpointu, solidna i objęta testami tylko co do statusu; (2) filtr daty w zapytaniu EF, solidny i objęty testami granicy; (3) fizyczne położenie pliku poza webrootem, czyli **konwencja operacyjna**, której kod pilnuje tylko dla `ContentPath`, nie dla zawartości webroota.
- Media i treść leżą w dwóch drzewach o tym samym kształcie `<slug>/…`, utrzymywanych ręcznie. To sprzyja kopiowaniu całego katalogu kursu.
- Wzorzec testu do ponownego użycia: `CourseContent/EndpointTests.cs` (temp `ContentPath`, `WebRootKey`, `FixedTimeProvider`, seed InMemory). Faza nie potrzebuje prawdziwego SQL Server: filtr daty to proste porównanie, a ochrona plików nie dotyka bazy.

## Historical Context (from prior changes)

- `context/archive/2026-10-07-course-content-reading/plan.md:14` — wykryty wyciek `.md` przez `wwwroot` przy `ContentPath` w webroocie. **Potwierdzone** (mechanizm `UseStaticFiles` + mapa `.md` nadal obowiązuje).
- `…/plan.md:46` — „Blokowania `.md` w `UseStaticFiles` … nie. Ochronę zapewnia bezpiecznik w czytniku i konfiguracja.” **Częściowo:** bezpiecznik chroni przed `ContentPath` w webroocie, ale nie przed kopią `.md` w `wwwroot/media`.
- `…/plan.md:48` — media publiczne, „publiczna jest tylko treść tekstowa”. Sformułowanie jest niejasne (w kontekście oznacza „chroniona jest tylko treść tekstowa”). **Zgodne z kodem** co do mediów. Status załączników `data/` pozostaje otwarty.
- `…/plan.md:128`, `:270` — granica `PublishDate == teraz` → 200, +1 min → 404. **Potwierdzone** w testach `:244-275`.
- `…/plan.md:146`, `:278` — „`/media/courses/<slug>/<slug>.md` zwraca 404” jako dowód braku wycieku. **Częściowo:** prawdziwe dla lokalnego webroota bez `index.html`. Na hoście z SPA w webroocie fallback zwróci 200 HTML, więc kryterium statusu nie jest przenośne.

## Related Research

- Brak wcześniejszego `research.md` dla tego ryzyka. Powiązane plany: `context/archive/2026-10-07-course-content-reading/plan.md`, `context/archive/2026-10-07-public-course-tiles/plan.md`.

## Rekomendacja warstw testów (dla `/10x-plan`)

Wszystkie poniższe testy to integracja przez WebApplicationFactory w `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/` (lub w nowym pliku dla granicy plików statycznych). Bez prawdziwej bazy, bez e2e.

1. **Wzmocnienie 401 (bez zmian w produkcji):** brak JWT / zły JWT / wygasły JWT (opcjonalnie) → 401 **i** ciało nie zawiera znacznika treści (`# Treść <slug>`) ani tytułu z frontmattera. Najlepiej `it.each`-style `[Theory]` po wariantach JWT oraz po kursie opublikowanym i przyszłym.
2. **Granica daty odporna na strefę:** `FixedTimeProvider` z nadpisanym `LocalTimeZone` (strefa ≠ UTC). Przypadki: `== now` → 200, `now + 1 tick` (lub +1 s) → 404 bez znacznika treści, `now - 1 tick` → 200. Seed `PublishDate` z `Kind = Unspecified` (produkcyjny kształt z EF). Oczekiwana granularność zależy od Open Questions #1.
3. **Plik treści nieosiągalny pod publicznym adresem (produkcyjny układ):** temp webroot `<root>/wwwroot/media/courses/<slug>/thumbnail.png` + `ContentPath = <root>/content` z `<slug>/<slug>.md`. Żądania bez JWT do `/media/courses/<slug>/<slug>.md`, `/<slug>/<slug>.md`, `/content/<slug>/<slug>.md` i do względnej ścieżki `ContentPath`. Asercja: ciało nie zawiera znacznika treści, `Content-Type` nie jest `text/markdown`. **Nie** asercja „status == 404”, bo fallback SPA może dać 200. Kontrola pozytywna w tym samym teście: `/media/courses/<slug>/thumbnail.png` → 200, co dowodzi, że webroot jest naprawdę serwowany i test nie przechodzi trywialnie.
4. **Kopia `.md` w `wwwroot/media/courses/<slug>/`:** w obecnym kodzie test byłby czerwony (wyciek). Wykonanie zależy od decyzji w Open Questions #2.

## Open Questions

1. **Granularność daty publikacji (wyrocznia).** PRD FR-003 / „Widoczność kursów w czasie” mówi „data publikacji równa aktualnej dacie lub wcześniejsza” (`prd.md:110`, `:191`). Implementacja i S-04 używają chwili UTC (`PublishDate <= UtcNow`). Przy `PublishDate = 2026-06-02 18:00` PRD (dzień) pokazuje kurs od początku 2 czerwca, a kod dopiero od 18:00 UTC. Kod nigdy nie pokazuje kursu *wcześniej* niż PRD, więc to nie jest wyciek, ale wyrocznia testu granicy zależy od wyboru. Do potwierdzenia przez właściciela: obowiązuje decyzja S-04 (chwila UTC, „równość = widoczny” co do ticka)?
2. **Kopia `.md` w webroocie.** Czy faza ma (a) tylko udokumentować tę lukę jako zaakceptowane ryzyko operacyjne (test pominięty / checklista wdrożenia), czy (b) dodać obronę w produkcji, np. odmowę serwowania `.md` przez `UseStaticFiles` (`OnPrepareResponse` lub usunięcie mapowania `.md` z `ContentTypeProvider`)? Wariant (b) zmienia `Program.cs`, czego S-04 świadomie unikał, a CLAUDE.md zabrania „zmieniać istniejących modułów”, choć nie zabrania zmiany hosta. Test byłby wtedy pierwszym czerwonym testem (`/10x-tdd`).
3. **Załączniki zadań w `wwwroot/media/courses/<slug>/data/`** (`.docx`, `.pdf`, `.txt`) są publiczne i dostępne przed `PublishDate`. Czy to „treść kursu” chroniona guardrailem PRD (`prd.md:83`), czy media publiczne jak obrazy? Jeśli treść, ryzyko #1 ma drugie, obecnie niechronione oblicze.
4. **Webroot hosta produkcyjnego.** Nie zweryfikowano (brak dostępu do serwera), czy build SPA (`index.html`) leży w webroocie API. Rekomendacja testu z punktu 3 jest odporna na oba warianty, więc to luka bez wpływu na plan.
