# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top
> (§1–§5); cookbook patterns at the bottom (§6) fill in as phases ship.
> Read before writing any new test.
>
> Refresh: re-run `/10x-test-plan --refresh` when stale (see §8).
>
> Last updated: 2026-10-11

## 1. Strategy

Testy w tym projekcie podlegają trzem nienegocjowalnym zasadom:

1. **Cost × signal.** Wygrywa najtańszy test, który daje realny sygnał dla
   danego ryzyka. Nie promuj testu do e2e, bo „wydaje się bezpieczniejszy”.
   Nie nakładaj modelu wizyjnego na deterministyczny diff, który już łapie
   regresję.
2. **Obawy użytkownika są pełnoprawnym dowodem.** Ryzyka zakotwiczone w
   „zespół obawia się X, a awaria ujawniłaby się gdzieś w <obszar>” mają tę
   samą wagę co linie PRD czy dane o hot-spotach.
3. **Ryzyka to scenariusze, nie miejsca w kodzie.** Ten plan dokumentuje,
   *co może się zepsuć* i *dlaczego uważamy to za prawdopodobne* — na
   podstawie dokumentów, wywiadu i *sygnału* z kodu (churn, struktura, baza
   testów). NIE twierdzi, że wie, która linia odpowiada za awarię. Tę wiedzę
   wytwarza `/10x-research` w każdej fazie wdrażania. Jeśli plan i research
   nie zgadzają się co do miejsca awarii, prawdą jest research.

Hot-spot scope used for likelihood weighting: `src/server/App01/`, `src/client/app01/src/`, `tests/server/App01/` (bez migracji, lockfile'ów, `bin/obj/dist`). Zakres testów: wyłącznie moduł Courses (wywiad Q5).

## 2. Risk Map

Główne scenariusze awarii, uporządkowane według impact × likelihood. Kolumna
Source cytuje *dowód, który podniósł ryzyko* — nigdy konkretny plik jako
„miejsce awarii” (patrz §1 zasada #3).

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence — not anchor) |
|---|---|---|---|---|
| 1 | Treść kursu jest dostępna dla osoby niezalogowanej (przez API lub publiczne pliki statyczne) albo dla zalogowanego przed datą publikacji | High | Medium | PRD §Success Criteria/Guardrails („treść kursów … niedostępne dla niezalogowanych”), FR-002, FR-003; archive/2026-10-07-course-content-reading/plan.md (wykryty wyciek `.md` przez `wwwroot` przy konfiguracji lokalnej); hot-spot dir `src/server/App01/App01.Modules.Courses/` (11 commits/30d) |
| 2 | Ta sama flaga zostaje zaliczona uczestnikowi dwa razy (równoległa aktywacja lub baza bez wymuszonej unikalności), przez co ranking i wskaźniki tracą znaczenie | High | Medium | PRD §Guardrails („flaga … nie może zostać policzona drugi raz”), FR-004; archive/2026-10-10-flag-activation/plan.md (gałąź wyścigu niepokryta — InMemory nie wymusza indeksów); tech-stack: testy na EF InMemory, odradzanym przez dokumentację EF Core (Context7, checked: 2026-10-11) |
| 3 | Hangar, lista zasłużonych i dashboard postępu pokazują różne liczby flag dla tego samego stanu (granica daty publikacji, `asOf`, administratorzy, UTC) | Medium | High | PRD FR-008 („ta sama liczba, którą uczestnik widzi w hangarze”), FR-009; archive/2026-10-10-leaderboard/plan.md i archive/2026-10-10-group-progress-dashboard/plan.md (trzy niezależne miejsca liczenia); `lessons.md` (daty UTC, InMemory maskuje); interview Q3 (moduł Courses zmieniany bez pewności); hot-spot dir `tests/server/App01/App01.Api.Tests/Features/Courses/` (11 commits/30d) |

Mapa ma 3 wiersze — poniżej zalecanego minimum 5. To świadoma decyzja
użytkownika podczas przeglądu briefu (2026-10-11), nie luka w analizie: ryzyka
„wyciek kodu flagi”, „awaria/latencja modelu” i „wstrzyknięcie polecenia w
odpowiedź” zostały uznane za nieistotne (patrz §7). Lens nadużyć jest pokryty
przez #1 (autoryzacja/dostęp).

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | Bez JWT żaden endpoint nie zwraca treści kursu; kurs z datą publikacji w przyszłości daje 404 (także na granicy równości dat); plik treści kursu nie jest osiągalny pod żadnym publicznym adresem hosta | „Treść leży poza `wwwroot`, więc jest bezpieczna” oraz „401 na endpoincie oznacza brak wycieku” | Wszystkie trasy zwracające treść, sposób rozwiązania ścieżki treści z konfiguracji, co serwuje middleware plików statycznych, porównanie daty publikacji (UTC, `TimeProvider`) | integracja (WebApplicationFactory + próba pobrania pliku statycznego) | Sprawdzanie samego kodu statusu bez treści odpowiedzi; test z treścią w katalogu, którego produkcja nigdy nie używa; pominięcie granicy dat |
| #2 | Dwie równoległe aktywacje tej samej flagi przez tego samego uczestnika dają dokładnie jeden zapis; druga dostaje `AlreadyOwned`; ranking pokazuje 1 | „Unikalny indeks jest w konfiguracji EF, więc baza go wymusza” oraz „test na InMemory dowodzi jednokrotności” | Czy indeks istnieje w migracji, obsługa błędu zapisu przy konflikcie, możliwość uruchomienia testów na prawdziwym SQL Server (kontener / LocalDB) w lokalnym i CI, porównanie kodu bez rozróżniania wielkości liter na prawdziwym silniku | integracja na prawdziwym silniku SQL Server | SQLite jako zamiennik (inna semantyka porównań tekstu); symulowanie wyścigu przez mock zamiast realnych równoległych żądań |
| #3 | Przy jednym zestawie danych (kursy opublikowane/nieopublikowane, granica daty, admin, kilku uczestników) liczba flag uczestnika w hangarze = jego liczba w rankingu, a suma na dashboardzie jest z nimi zgodna | „Każdy endpoint ma swój test, więc są spójne” | Reguły liczenia w hangarze, rankingu i dashboardzie; zachowanie `asOf` z przyszłości; oznaczanie dat jako UTC na prawdziwym silniku | integracja (jeden seed, trzy endpointy, porównanie krzyżowe) | Wyrocznia przepisana z obliczeń produkcyjnych; trzy osobne seedy dla trzech endpointów |

## 3. Phased Rollout

Każdy wiersz to osobna faza wdrażania, która otwiera własny folder zmiany
przez `/10x-new`. Status przesuwa się od lewej do prawej; orchestrator
aktualizuje go, gdy artefakty pojawiają się na dysku.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|---|---|---|---|---|---|
| 1 | Granica dostępu do treści kursów | Udowodnić, że treść kursu nie trafia do niezalogowanych, przed publikacją ani przez pliki statyczne | #1 | integracja | complete | testing-course-content-access |
| 2 | Spójność flag na prawdziwym silniku | Udowodnić, że flaga liczy się raz na prawdziwej bazie, a hangar, ranking i dashboard pokazują tę samą liczbę | #2, #3 | integracja (SQL Server) + integracja (InMemory) | change opened | testing-flag-consistency |

## 4. Stack

| Layer | Tool | Version | Notes |
|---|---|---|---|
| unit + integration (serwer) | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + Moq | 2.9.x / 10.0.x | Testy endpointów w `tests/server/App01/App01.Api.Tests/Features/<Moduł>/<Funkcja>/`; baza EF InMemory |
| integration na prawdziwej bazie | none yet — see Phase 2 | — | SQL Server (kontener lub LocalDB) — wybór należy do research fazy 2; nie SQLite |
| unit + component (klient) | Vitest + jsdom + Testing Library | 5.x | Testy obok pliku `<Nazwa>.test.ts(x)` |
| API mocking | Moq (podmiana `IOpenRouterService`) | — | Nigdy prawdziwy OpenRouter w testach |
| e2e | none — deliberately (§7) | — | — |

**Stack grounding tools (current session):**
- Docs: Context7 — sprawdzono strategię testów EF Core (InMemory „highly discouraged”: brak transakcji i wymuszania ograniczeń, inna semantyka zapytań; SQLite różni się porównaniem tekstu od SQL Server); checked: 2026-10-11
- Search: WebSearch — dostępny, nie użyty; checked: 2026-10-11
- Runtime/browser: Playwright MCP — not available in current session; claude-in-chrome dostępny, nie używany (brak fazy e2e); checked: 2026-10-11
- Provider/platform: GitHub (`gh` CLI) — możliwy odczyt wyników CI dla bramki fazy 2; checked: 2026-10-11

## 5. Quality Gates

| Gate | Where | Required? | Catches |
|---|---|---|---|
| format (`dotnet format --verify-no-changes`, prettier) | local + CI | required | dryf stylu |
| lint + build/typecheck (`dotnet build`, `npm run lint`, `npm run build`) | local + CI | required | błędy składni i typów |
| unit + integration (`dotnet test`, `npm test`) | local + CI | required | regresje logiki i kontraktów endpointów |
| integration na prawdziwym SQL Server | local + CI | required after §3 Phase 2 | wyścigi zapisu, ograniczenia bazy, różnice InMemory vs SQL Server |
| dependency audit (`npm audit`, `dotnet list package --vulnerable`) | CI | required | podatne pakiety |

## 6. Cookbook Patterns

Jak dodawać nowe testy w tym projekcie. Każda podsekcja wypełnia się, gdy
odpowiednia faza wdrażania zostanie dostarczona; wcześniej brzmi
„TBD — see §3 Phase <N>.”

### 6.1 Test endpointu Courses (integracja, InMemory)

- **Location**: `tests/server/App01/App01.Api.Tests/Features/Courses/<Funkcja>/EndpointTests.cs`.
- **Reference test**: `tests/server/App01/App01.Api.Tests/Features/Courses/HangarFlags/EndpointTests.cs` (seed InMemory, `FixedTimeProvider`, surowy JSON).
- **Run locally**: `dotnet test APPS.sln`.
- Wzorzec granicy dostępu do treści (niezalogowany, przed publikacją, pliki statyczne): `tests/server/App01/App01.Api.Tests/Features/Courses/CourseContent/EndpointTests.cs` — `CourseContent_WithoutValidJwt_ReturnsUnauthorizedWithoutContent`, `CourseContent_PublishDateBoundary_VisibleOnlyFromPublishInstantUtc`, `CourseContentFile_IsNotReachableUnderPublicUrls` (patrz §6.2).

### 6.2 Test granicy dostępu i plików statycznych

- **Wartownik + kontrola pozytywna.** Kurs zawiera unikalny `ContentSentinel` (tytuł i treść); test negatywny sprawdza jego brak w ciele odpowiedzi, a osobna kontrola pozytywna (ważny JWT → wartownik jest) dowodzi, że brak nie jest trywialny. Sam kod statusu nie jest dowodem braku wycieku.
- **Pliki statyczne bez asercji statusu.** `MapFallbackToFile("index.html")` zwraca 200 z SPA, gdy `index.html` jest w webroocie — o wycieku decyduje ciało (wartownik) i `Content-Type: text/markdown`. Test działa w obu wariantach webroota i potwierdza, że webroot jest serwowany (miniatura → 200 `image/png`).
- **Produkcyjny układ katalogów.** Webroot `<tmp>/wwwroot/media/courses/<slug>/` i treść jako katalog-rodzeństwo `<tmp>/content/<slug>/<slug>.md` przez klucz `Courses:ContentPath` (`UseSetting(WebHostDefaults.WebRootKey, …)` działa w TestServer).
- **Granica publikacji.** Wyrocznia = chwila UTC, równość = widoczny (decyzja S-04; PRD FR-003 mówi „data” — implementacja jest ostrzejsza, nigdy nie pokazuje wcześniej). Testuj −1/0/+1 tick, seed z `Kind = Unspecified`, a `FixedTimeProvider` z jawną `LocalTimeZone` ≠ UTC, żeby pomyłka „czas lokalny” była czerwona także na CI w UTC.
- **Dowód czerwieni** (Stryker.NET niezainstalowany): ręczna mutacja per test — zdjęcie `.RequireAuthorization()`, `GetLocalNow()` / `<` w filtrze daty, kopia `.md` do `wwwroot/media`.

### 6.3 Test na prawdziwym SQL Server

- TBD — see §3 Phase 2 (wzorzec „flaga liczona raz przy równoległych aktywacjach”).

### 6.4 Test spójności liczników między endpointami

- TBD — see §3 Phase 2 (wzorzec „jeden seed, hangar = ranking = dashboard”).

### 6.5 Per-rollout-phase notes

(Po każdej fazie `/10x-implement` dopisuje tu 2–3 linie o tym, czego faza nauczyła.)

- **Phase 1 (testing-course-content-access, 2026-10-11):** API i filtr daty były szczelne; luka siedziała w testach (401 bez asercji ciała, granica zależna od strefy maszyny). Realna ścieżka wycieku jest operacyjna: `.md` w `wwwroot` jest serwowany jako `text/markdown` (potwierdzone testem z mutacją), a kod pilnuje tylko położenia `ContentPath` — zaakceptowane w §7.

## 7. What We Deliberately Don't Test

- **Moduły Portal, Lotto, Flashcards i strony statyczne** — poza zakresem budżetu testów. Istniejące testy zostają w CI jako ochrona FR-012..FR-014; nie dopisujemy nowych. Re-evaluate, jeśli zmiana w Courses dotknie współdzielonego layoutu, logowania lub `Shared.*`. (Source: Phase 2 interview Q5.)
- **Poufność kodu flagi** — uczestnik wylicza kod poza aplikacją (np. w zewnętrznym narzędziu AI), a PRD FR-004 akceptuje przekazywanie kodu. Re-evaluate, jeśli kod stanie się jedynym dowodem wykonania zadania. (Source: przegląd briefu, 2026-10-11.)
- **Czas odpowiedzi i awarie modelu weryfikującego** — PRD Guardrails i `CLAUDE.md` (2026-10-11) przyjmują oczekiwany czas do 15 s i dopuszczają timeout zewnętrznego modelu/API jako ryzyko. Re-evaluate przy skargach uczestników. (Source: przegląd briefu, 2026-10-11.)
- **Odporność promptu weryfikacji na wstrzyknięcie polecenia** — modele się zmieniają, więc wynik oceny szybko się dezaktualizuje. Re-evaluate, jeśli flaga zdobyta oszustwem zostanie zauważona w rankingu. (Source: przegląd briefu, 2026-10-11.)
- **Kopia pliku `.md` kursu w `wwwroot/media/courses/<slug>/`** — taki plik jest serwowany publicznie (`text/markdown`, bez logowania i bez względu na `PublishDate`), a kod tego nie blokuje: bezpiecznik czytnika sprawdza tylko, czy `Courses:ContentPath` nie leży w webroocie. Zaakceptowane ryzyko operacyjne, bez blokady `.md` w `Program.cs`; mitygacja w rejestrze ryzyk `infrastructure.md`. Re-evaluate przy automatyzacji publikacji treści lub po incydencie wycieku. (Source: decyzja planu testing-course-content-access, 2026-10-11.)
- **Media i załączniki kursów (`wwwroot/media/courses/<slug>/**`, także `data/*.docx|pdf|txt`)** — publiczne, również przed `PublishDate`, zgodnie z decyzją S-04 (chroniona jest tylko treść tekstowa `.md`). Re-evaluate, jeśli załącznik zadania zacznie zawierać rozwiązanie lub treść, która ma pozostać zamknięta. (Source: decyzja planu testing-course-content-access, 2026-10-11.)
- **e2e w przeglądarce** — pełna ścieżka została sprawdzona ręcznie przy zamknięciu M-1, a pozostałe ryzyka łapie taniej integracja. Re-evaluate przy nowym milestonie zmieniającym przepływ uczestnika.

## 8. Freshness Ledger

- Strategy (§1–§5) last reviewed: 2026-10-11
- Stack versions last verified: 2026-10-11
- AI-native tool references last verified: 2026-10-11 (brak warstwy AI-native)

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive,
- a recommended tool's `checked:` date is older than three months,
- the project's tech stack changes (new framework, new test runner),
- §7 negative-space no longer matches what the team believes.
