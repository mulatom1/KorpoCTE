---
title: "Destylacja domeny — moduł Kursy (tomsoft1.pl)"
created: 2026-10-15
type: domain-distillation
scope: "App01.Modules.Courses + współdzielone encje Courses; Portal tylko jako kontekst tożsamości"
baseline_commit: 7daf069
---

# Destylacja domeny — moduł Kursy

> Mapa domeny, nie projekt kodu. Identyfikatory (`UL-`, `SD-`, `AG-`, `DR-`, `R-`, `Q-`) są stabilne — kolejne przebiegi powinny się do nich odnosić.

## Krok 0 — Kontekst projektu i źródła

**Dokumenty przeczytane w całości:**

| Dokument | Rola | Uwagi |
| --- | --- | --- |
| `context/foundation/prd.md` | wymagania (v2, 2026-09-23) | język „obserwowalny”, bez mechanizmów |
| `context/foundation/shape-notes.md` | narracja źródłowa PRD | zawiera mechanizmy (FTP, system prompt, `IsAdmin`) i starsze NFR (<5 s) |
| `context/foundation/roadmap.md` | wycinki F-01, S-01…S-07, wszystkie `done` | sekcja Done ma późniejsze sformułowania |
| `context/foundation/lessons.md`, `test-plan.md` | reguły i ryzyka przyjęte | `test-plan.md:145` — ryzyko poufności kodu |
| `context/archive/*/plan.md`, `plan-brief.md` | **historia decyzji** (addenda z datami) | traktowane jako późniejsze decyzje względem PRD |
| `CLAUDE.md` | instrukcje repo, już zaktualizowane po decyzjach z 2026-10-08/10 | |
| `my-courses/korpo-cte-300/korpo-cte-300.md` | prawdziwa treść kursu | najlepsze źródło języka uczestnika |

Brak `tech-stack.md` (projekt brownfield) — stack ustalony z `CLAUDE.md` i kodu.

**Stack i warstwy logiki biznesowej:**

- Serwer: ASP.NET Core 10, modularny monolit, vertical slices (MediatR). Logika domenowa Kursów żyje **w handlerach** `src/server/App01/App01.Modules.Courses/Features/*/Handler.cs` — nie ma warstwy domenowej ani metod na encjach.
- Encje są anemiczne (same właściwości): `src/server/App01/App01.Shared.Application/Entities/Courses/{Course,Flag,UserFlag}.cs`.
- Niezmienniki bazodanowe: `src/server/App01/App01.Shared.Infrastructure/Repositories/Configurations/Courses/*.cs`.
- Treść kursu: pliki Markdown z frontmatterem poza `wwwroot`, czytane przez `App01.Modules.Courses/Content/CourseFrontmatterReader.cs`; grafiki i załączniki w `App01.Bootstrapper.Api/wwwroot/media/courses/<slug>/`.
- Klient: React 19, `src/client/app01/src/pages/courses/*` (Hangar, Terminal, Leaderboard, Courses, CourseDetails) — tylko prezentacja.
- Dane admina (kursy, flagi, kryteria) wprowadzane SQL-em poza aplikacją (FR-010/FR-011) — w repo **nie ma seeda** flag (szukano `HasData`, `INSERT INTO`, `Flags.Add` — jedyne trafienia to testy i `ActivateFlag/Handler.cs`).

**Ograniczenie:** Lotto, Flashcards i większość Portalu poza zakresem — ujęte tylko tam, gdzie dzielą słowa z Kursami.

---

## Krok 1 — Ubiquitous Language

Ścieżki serwera skrócone: `Courses/…` = `src/server/App01/App01.Modules.Courses/…`, `Entities/…` = `src/server/App01/App01.Shared.Application/Entities/…`, `Config/…` = `src/server/App01/App01.Shared.Infrastructure/Repositories/Configurations/Courses/…`, `client/…` = `src/client/app01/src/…`.

| ID | Pojęcie | Definicja (destylowana) | Źródło w dokumentach | Gdzie żyje w kodzie |
| --- | --- | --- | --- | --- |
| UL-01 | **Kurs** | Jednostka materiału z datą publikacji; tożsamość w bazie (slug), opis i treść w pliku | `prd.md:108-111` (FR-002, FR-003) | `Entities/Courses/Course.cs:3-12` (tylko `Slug`, `PublishDate`); tytuł/opis/tagi: `Courses/Content/CourseFrontmatterReader.cs:14-20` |
| UL-02 | **Data publikacji** | Moment, od którego kurs (i jego flagi) istnieje dla uczestników | `prd.md:110`, `prd.md:191` („równa aktualnej dacie lub wcześniejsza”) | `Entities/Courses/Course.cs:10-11` (UTC z godziną); filtr `PublishDate <=` powielony w 9 miejscach (patrz AG-03) |
| UL-03 | **Kafelek kursu** | Publiczna wizytówka: tytuł, krótki opis, tagi, grafika | `prd.md:106` (FR-001), `prd.md:187` | `Courses/Features/CourseTiles/Contracts.cs:16-22` (`CourseTileDto`) |
| UL-04 | **Treść kursu** | Markdown po frontmatterze, tylko dla zalogowanych | `prd.md:108` (FR-002), `prd.md:83` | `Courses/Features/CourseContent/Contracts.cs:12-18` (`Content`) |
| UL-05 | **Flaga** (trofeum) | Jednostka postępu do zdobycia; element puli; należy do kursu | `prd.md:48`, `prd.md:121` (FR-007) | `Entities/Courses/Flag.cs:3-19` |
| UL-06 | **Kod flagi** | Ciąg `SAD{…}`, który uczestnik wylicza, wykonując zadanie, i którym aktywuje flagę; jednocześnie sekret aktywacji | `CLAUDE.md:98`; `my-courses/korpo-cte-300/korpo-cte-300.md:99-105`, `:230` | `Entities/Courses/Flag.cs:10-11` (`Code`), unikalny: `Config/FlagConfiguration.cs:39-40` |
| UL-07 | **Zadanie** | W PRD: każda czynność uczestnika kończąca się flagą. W kodzie: flaga z niepustymi kryteriami (zdobywalna przez weryfikację AI) | `prd.md:119` (FR-006), `prd.md:135` (FR-011 „pula flag oraz zadania”) | **Brak osobnego bytu.** `Courses/Features/HangarTasks/Contracts.cs:15-21` (`HangarTaskDto` z `FlagId`); filtr `!IsNullOrWhiteSpace(f.Criteria)` w `HangarTasks/Handler.cs:51`, `VerifyAnswer/Handler.cs:84` |
| UL-08 | **Kryteria poprawności** | Tajny opis, względem którego model ocenia odpowiedź | `prd.md:119`, `prd.md:171`; w shape-notes „system prompt” (`shape-notes.md:155`) | `Entities/Courses/Flag.cs:16-18` (`Criteria`) |
| UL-09 | **Odpowiedź / wynik zadania** | Otwarty tekst wklejony przez uczestnika do oceny | `prd.md:92`, `prd.md:117` (FR-005) | `Courses/Features/VerifyAnswer/Contracts.cs:9-12` (`Answer`); nie jest zapisywana |
| UL-10 | **Weryfikacja / werdykt** | Ocena odpowiedzi modelem; wynik: `Correct`, `Incorrect`, `Unavailable`, `AlreadyOwned` | `prd.md:93-99`, `prd.md:80` | `Courses/Features/VerifyAnswer/Contracts.cs:22-28`; werdykt modelu `pass/fail`: `VerifyAnswer/Handler.cs:62` |
| UL-11 | **Awaria weryfikacji** | Błąd/timeout oceny — komunikowany jako awaria, ponawialny, nieliczony | `prd.md:98` | `Statuses.Unavailable`, `VerifyAnswer/Handler.cs:99-102`, `:133-142`; UI „Spróbuj ponownie”: `client/pages/courses/TomoAiTerminalPage.tsx:244` |
| UL-12 | **Aktywacja flagi** | Wpisanie kodu flagi w hangarze; **jedyny moment zdobycia flagi** | `prd.md:115` (FR-004); decyzja `context/archive/2026-10-08-answer-verification-earns-flag/plan.md:378-386` | `Courses/Features/ActivateFlag/Handler.cs:42-112`; statusy `Activated/AlreadyOwned/Invalid`: `ActivateFlag/Contracts.cs:21-26` |
| UL-13 | **Zdobycie flagi / flaga zdobyta** | Trwały fakt: użytkownik posiada flagę od chwili `EarnedAt` | `prd.md:82`, `prd.md:100` | `Entities/Courses/UserFlag.cs:3-14`; unikalność `Config/UserFlagConfiguration.cs:35-37` |
| UL-14 | **Hangar (z trofeami)** | Widok wszystkich flag z podziałem zdobyte/niezdobyte + formularz aktywacji | `prd.md:121` (FR-007), `prd.md:188` | `Courses/Features/HangarFlags/*`; UI `client/pages/courses/HangarPage.tsx:180`, `:347` |
| UL-15 | **Terminal TOMO-AI-001 („Do sprawdzenia”)** | Formularz wysyłki odpowiedzi do weryfikacji | `prd.md:117` (w PRD: „formularz w hangarze”); decyzja `context/archive/2026-10-08-answer-verification-earns-flag/plan.md:388-390` | `client/pages/courses/TomoAiTerminalPage.tsx:115`, `:130`; trasa `client/main.tsx:62` |
| UL-16 | **Lista zasłużonych (ranking)** | Kolejność użytkowników wg liczby zdobytych flag | `prd.md:126-127` (FR-008) | `Courses/Features/Leaderboard/*`; `LeaderboardEntryDto(Rank, DisplayName, FlagCount)`: `Leaderboard/Contracts.cs:24-28` |
| UL-17 | **Postęp grupy (4 wskaźniki)** | Liczba użytkowników, flag możliwych, flag zdobytych, % zdobytych — na dany moment | `prd.md:128` (FR-009), `prd.md:71` | `Courses/Features/GroupProgress/Contracts.cs:16-22` |
| UL-18 | **Uczestnik (bootcampu)** | Persona główna — członek zamkniętej grupy | `prd.md:54` | **BRAK jako byt.** W kodzie `UserId`/`User` (`Entities/Portal/User.cs`); słowo tylko w komentarzach/promptach (`VerifyAnswer/Handler.cs:157`, `client/pages/courses/LeaderboardPage.tsx:9`). Szukano: `uczestnik`, `participant`. |
| UL-19 | **Grupa** | Zamknięta grupa znajomych, tło rankingu i wskaźników | `prd.md:54`, `prd.md:128` | **BRAK jako byt.** „Grupa” = wszyscy z ≥1 flagą (`GroupProgress/Handler.cs:60-64`). Szukano: `group`, `grupa`, `grupy` — tylko nazwa wycinka `GroupProgress`. |
| UL-20 | **Pula flag** | Zbiór flag zdefiniowanych przez admina | `prd.md:60`, `prd.md:135` | **BRAK jako byt.** Tylko komentarz `GroupProgress/Handler.cs:51`. Szukano: `pool`, `pula`, `puli`. |
| UL-21 | **Bootcamp** | Cykl kursów dla grupy, w którym mierzy się 70% | `prd.md:71`, `prd.md:215` (OQ-3) | **BRAK w kodzie.** Szukano: `bootcamp` w `Modules.Courses`, encjach i `client/pages/courses`. |
| UL-22 | **Flaga ukryta** | Flaga widoczna w hangarze, ale nieopisana w treści dnia; podpowiedzią jest tylko tytuł | `my-courses/korpo-cte-300/korpo-cte-300.md:120-122` | **BRAK w kodzie** (powstaje emergentnie). Szukano: `hidden`, `ukryt`. |
| UL-23 | **Dzień** | Uczestnik widzi kurs jako „Dzień N” bootcampu | `my-courses/korpo-cte-300/korpo-cte-300.md:8`, `:116` | **BRAK w kodzie** — to tytuł z frontmattera, nie pojęcie. |
| UL-24 | **Odwiedzający bez konta** | Widzi wyłącznie kafelki | `prd.md:62-64` | „gość”: `client/pages/courses/CoursesPage.tsx:74`; po stronie serwera = endpoint bez `RequireAuthorization` (`CourseTiles/Endpoint.cs:16-27`) |
| UL-25 | **Administrator** | Właściciel; publikuje kursy i flagi poza aplikacją | `prd.md:58-60`, `prd.md:189` | claim `isAdmin` (`Entities/Portal/User.cs:13`); **moduł Kursy nie sprawdza roli nigdzie** — publikacja dzieje się poza aplikacją |

### Słowa o wielu znaczeniach (granice kontekstów)

| Słowo | Znaczenie A (kontekst) | Znaczenie B (kontekst) | Dowód |
| --- | --- | --- | --- |
| **„flaga”** | trofeum do zdobycia — **Zdobywanie flag** | boolowski znacznik roli `IsAdmin` — **Tożsamość (Portal)**; trzecie znaczenie: ciąg `SAD{…}` (= kod) w **Treści kursu** | `prd.md:24` („rozróżniane flagą `IsAdmin`”), `shape-notes.md:30`; `korpo-cte-300.md:99` vs `Flag.cs:10` |
| **„rejestracja”** | założenie konta przez admina — **Tożsamość (Portal)** | aktywacja flagi kodem — **Zdobywanie flag** | `prd.md:37`, `client/pages/portal/auth/UserRegisterPage.tsx:165` vs `korpo-cte-300.md:126-132` („zarejestrować flagę”) |
| **„zadanie”** | dowolna czynność kończąca się flagą (CTF lub AI) — **Treść kursu / PRD** | flaga z `Criteria` (tylko weryfikowalna AI) — **Weryfikacja** | `prd.md:135` vs `HangarTasks/Handler.cs:48-51` |
| **„zdobycie / przyznanie”** | werdykt `Correct` przyznaje flagę — **PRD (stary model)** | flagę przyznaje dopiero aktywacja kodem — **kod i decyzja 2026-10-08** | `prd.md:93`, `prd.md:119` vs `VerifyAnswer/Handler.cs:110-113`, `ActivateFlag/Handler.cs:79-90` |
| **„hangar”** | jedno miejsce z oboma formularzami — **PRD** | widok trofeów + aktywacja; weryfikacja w osobnym terminalu — **kod** | `prd.md:117`, `shape-notes.md:38` vs `client/main.tsx:62`, `coursesSubMenu.ts:9-14` |
| **„użytkownik / uczestnik”** | uczestnik bootcampu (ranking „uczestników”) — **PRD** | każde konto z ≥1 flagą, łącznie z adminami — **Postęp grupy** | `prd.md:126` vs `GroupProgress/Handler.cs:60`, decyzja `context/archive/2026-10-10-group-progress-dashboard/plan-brief.md:27` |
| **„data publikacji”** | data kalendarzowa („równa aktualnej dacie”) — **PRD** | moment UTC z godziną — **kod** | `prd.md:110` vs `Course.cs:10-11`, decyzja `context/archive/2026-10-07-public-course-tiles/plan-brief.md:30` |
| **nazwa terminala** | `TOMO-AI-001` (kod) | `TOMSO-AI-001` (treść kursu), `TOMO-AI001` (roadmapa) | `TomoAiTerminalPage.tsx:115` vs `korpo-cte-300.md:136`, `roadmap.md:95` |

**Wniosek o kontekstach:** realne granice to (1) **Tożsamość** (Portal: konto, rejestracja, `IsAdmin`), (2) **Katalog i treść kursów** (pliki + data publikacji), (3) **Zdobywanie flag** (weryfikacja, aktywacja, posiadanie), (4) **Postęp grupy** (ranking i wskaźniki jako read-modele nad zdobyciami). Kod dzieli (2)–(4) tylko wycinkami w jednym module; granica (1)↔(3) jest widoczna w słowach „flaga” i „rejestracja”.

---

## Krok 2 — Subdomeny

| ID | Obszar | Kategoria | Uzasadnienie (cele produktu) |
| --- | --- | --- | --- |
| SD-01 | **Zdobywanie flag** — weryfikacja odpowiedzi, aktywacja kodem, jednokrotność zdobycia | **Core** | Insight „flaga dowodzi wykonania, nie przeczytania” (`prd.md:48`); jedyna nowa reguła domenowa (`prd.md:169-173`); north star S-01 (`roadmap.md:36`); guardrail „flaga raz zdobyta nie znika i nie liczy się drugi raz” (`prd.md:82`) |
| SD-02 | **Polityka werdyktu** — `Unavailable ≠ Incorrect`, `AlreadyOwned` bez wołania modelu, ochrona przed wstrzyknięciem poleceń | **Core** (część SD-01) | AC US-01 (`prd.md:97-99`), guardrail 15 s (`prd.md:80`), ryzyko S-01 (`roadmap.md:104`) |
| SD-03 | **Postęp grupy** — lista zasłużonych, 4 wskaźniki | **Supporting** | PRD nazywa punktację „następstwem reguły, nie regułą” (`prd.md:175`); ale wskaźnik % jest miarą kryterium Primary 70% (`prd.md:71`) — dlatego musi liczyć spójnie z SD-01 |
| SD-04 | **Katalog i treść kursów** — kafelki, treść, data publikacji, publikacja plikami | **Supporting** | Konieczne dla ścieżki Primary (`prd.md:70`), ale zarządzanie treścią jest non-goal (`prd.md:199`); publikacja świadomie poza aplikacją (FR-010) |
| SD-05 | **Wywołanie modelu LLM** (`IOpenRouterService`) | **Generic** | Transport do zewnętrznego API, współdzielony z Flashcards (`CLAUDE.md`, sekcja weryfikacji) |
| SD-06 | **Tożsamość i dostęp** — konta, JWT, `X-TOKEN`, `IsAdmin` | **Generic** | „Brak zmian w modelu kontroli dostępu” (`prd.md:179`); FR-012/FR-014 tylko jako obronne |
| SD-07 | **Renderowanie Markdown, media statyczne** | **Generic** | Gotowe pakiety (`react-markdown`…), `UseStaticFiles` |
| SD-08 | Lotto, Flashcards, Gry | poza produktem | `prd.md:208` — nie są zmieniane |

---

## Krok 3 — Kandydaci na agregaty i polityki

Statusy: **egzekwuje** (kod + baza/test), **deklaruje** (komentarz/dokument, brak mechanizmu), **ignoruje**, **przeczy**.

### AG-01 — Zdobycie flagi (`UserFlag`, korzeń: para Użytkownik × Flaga)

| Niezmiennik | Źródło | Status | Dowód |
| --- | --- | --- | --- |
| INV-01a: flaga zdobyta najwyżej raz na użytkownika | `prd.md:82` | **egzekwuje** | unikalny indeks `Config/UserFlagConfiguration.cs:35-37`; obsługa wyścigu `ActivateFlag/Handler.cs:88-106`; test SQL Server `tests/.../Courses/ActivateFlag/ActivateFlagRaceSqlServerTests.cs:32` |
| INV-01b: flaga raz zdobyta nie znika | `prd.md:82` | **przeczy** (częściowo) | `Config/UserFlagConfiguration.cs:44-47` — `OnDelete(Cascade)` od `User`; `Portal/Features/UserDelete/Handler.cs:63` usuwa użytkownika twardo → jego zdobycia znikają z rankingu i wskaźników. Dodatkowo zmiana `Course.PublishDate` w przyszłość (SQL admina, FR-011) chowa zdobyte flagi z hangaru i rankingu (`HangarFlags/Handler.cs:52`, `Leaderboard/Handler.cs:47`) |
| INV-01c: flaga dowodzi wykonania zadania | `prd.md:48`, `shape-notes.md:89` | **ignoruje** dla flag z kryteriami | `ActivateFlag/Handler.cs:59-62` akceptuje sam kod — nie sprawdza, czy ten użytkownik przeszedł weryfikację (`Correct` niczego nie zapisuje: `VerifyAnswer/Handler.cs:110`). Brak limitu prób aktywacji — `Invalid` tylko logowane (`ActivateFlag/Handler.cs:65-69`), a kody bywają z małej przestrzeni (`korpo-cte-300.md:234` — `SAD{n1;n2}`). Ryzyko przekazania kodu przyjęte w FR-004 (`prd.md:116`), ale `test-plan.md:145` każe je ponownie ocenić, „jeśli kod stanie się jedynym dowodem wykonania” — **ten warunek jest już spełniony** |
| INV-01d: zdobycie tylko flagi z opublikowanego kursu | `prd.md:110` | **egzekwuje** | `ActivateFlag/Handler.cs:60` |
| INV-01e: tożsamość zdobywcy wyłącznie z JWT | `CLAUDE.md` (Bezpieczeństwo) | **egzekwuje** | `ActivateFlag/Handler.cs:52`, `VerifyAnswer/Handler.cs:74` |

### AG-02 — Flaga / Zadanie (`Flag`)

| Niezmiennik | Źródło | Status | Dowód |
| --- | --- | --- | --- |
| INV-02a: kod flagi unikalny | `CLAUDE.md:98` | **egzekwuje** (z zastrzeżeniem) | `Config/FlagConfiguration.cs:39-40`; aktywacja porównuje bez wielkości liter (`ActivateFlag/Handler.cs:58-60`), a indeks zależy od kolacji bazy — dwa kody różniące się wielkością liter dałyby niejednoznaczną aktywację (`FirstOrDefault`) |
| INV-02b: kod nigdy nie wycieka poza `Correct` i hangar właściciela | `CLAUDE.md:98` | **egzekwuje** | `HangarFlags/Handler.cs:81-82`; `HangarTasks/Contracts.cs:16-21` bez `Code`; testy na surowym JSON (wg `CLAUDE.md:98`) |
| INV-02c: kryteria nigdy nie trafiają do API | `Flag.cs:16` | **egzekwuje** | projekcje bez `Criteria` we wszystkich DTO; prompt „nie ujawniaj kryteriów” `VerifyAnswer/Handler.cs:161` |
| INV-02d: kod flagi weryfikowalnej AI jest trudny do zgadnięcia | decyzja `context/archive/2026-10-08-answer-verification-earns-flag/plan.md:385` | **deklaruje** | tylko komentarz `Entities/Courses/Flag.cs:10` („nieodgadywalny”); brak walidacji, brak seeda; treść kursu używa formatu `SAD{WYNIK}` dla flag bez AI (`korpo-cte-300.md:230`) — ten sam kanał (FR-011, SQL) definiuje oba rodzaje |
| INV-02e: „zadanie” = flaga z niepustymi kryteriami | `prd.md:119` | **egzekwuje, ale powiela** | ta sama reguła w `HangarTasks/Handler.cs:51` i `VerifyAnswer/Handler.cs:84` — brak nazwanego pojęcia |

### AG-03 — Kurs i jego publikacja (`Course` + plik treści) — polityka widoczności

| Niezmiennik | Źródło | Status | Dowód |
| --- | --- | --- | --- |
| INV-03a: kurs i jego flagi istnieją dla użytkowników od `PublishDate` włącznie | `prd.md:110`, `prd.md:191` | **egzekwuje, rozproszone** | `PublishDate <=` w 9 miejscach: `ActivateFlag/Handler.cs:60`, `CourseContent/Handler.cs:58`, `CourseTiles/Handler.cs:60`, `GroupProgress/Handler.cs:48`, `:53`, `HangarFlags/Handler.cs:52`, `HangarTasks/Handler.cs:51`, `Leaderboard/Handler.cs:47`, `VerifyAnswer/Handler.cs:79`; spójność pilnuje test `tests/.../Courses/FlagCountConsistency/EndpointTests.cs` |
| INV-03b: treść kursu niedostępna dla niezalogowanych | `prd.md:83` | **egzekwuje dla `.md`, przeczy dla załączników** | `CourseContent/Endpoint.cs:31-32`; bezpiecznik `wwwroot` `CourseFrontmatterReader.cs:142-155`; ale załączniki zadań leżą publicznie: `App01.Bootstrapper.Api/wwwroot/media/courses/korpo-cte-301/data/*.docx` (+ `.pdf`, `.txt` w `my-courses/korpo-cte-301/data`), także przed `PublishDate` — ryzyko przyjęte w `context/archive/2026-10-11-testing-course-content-access/plan.md:189` |
| INV-03c: błędny plik kursu nie wywraca listy | `prd.md:134` (FR-010) | **egzekwuje** | `CourseFrontmatterReader.cs:122-127`, `CourseTiles/Handler.cs:69-74` |
| INV-03d: ścieżka pliku tylko ze sluga z bazy | `CLAUDE.md` (Treść kursów) | **egzekwuje** | `CourseFrontmatterReader.cs:52-57`, `:79-85` |

### AG-04 — Polityka werdyktu (proces, nie agregat)

| Niezmiennik | Źródło | Status | Dowód |
| --- | --- | --- | --- |
| INV-04a: awaria techniczna ≠ ocena negatywna | `prd.md:98` | **egzekwuje** | `VerifyAnswer/Handler.cs:99-102`, `:133-149` |
| INV-04b: flaga posiadana → model nie jest wołany | `prd.md:99` | **egzekwuje** | `VerifyAnswer/Handler.cs:89-95` |
| INV-04c: próby nielimitowane i nieliczone | `prd.md:97` | **egzekwuje** (brak zapisu prób) | `VerifyAnswer/Handler.cs` nie zapisuje niczego |
| INV-04d: odpowiedź uczestnika to dane, nie polecenia | `prd.md:120` (Socrates) | **egzekwuje (best-effort)** | `VerifyAnswer/Handler.cs:123`, `:164-166` |
| INV-04e: werdykt w ≤15 s | `prd.md:80` | **egzekwuje** | `VerifyAnswer/Handler.cs:22`, `:125-126`, `:200-204` |

### AG-05 — Postęp grupy (read-model, nie agregat)

| Niezmiennik | Źródło | Status | Dowód |
| --- | --- | --- | --- |
| INV-05a: liczba w rankingu = liczba w hangarze | `prd.md:127` (FR-008) | **egzekwuje** | ta sama granica publikacji `Leaderboard/Handler.cs:47` i `HangarFlags/Handler.cs:52`; test `FlagCountConsistency` |
| INV-05b: zdobyte nigdy > możliwe | `GroupProgress/Handler.cs:51` | **egzekwuje** | `GroupProgress/Handler.cs:52-58` (distinct po flagach z puli) |
| INV-05c: % = różne flagi zdobyte przez grupę / flagi opublikowane | `prd.md:71` | **egzekwuje** | `GroupProgress/Handler.cs:55-68` |

---

## Krok 4 — Rozjazdy MODEL vs KOD

| ID | Dokument mówi | Kod robi | Dowód | Co naprawić |
| --- | --- | --- | --- | --- |
| DR-01 | Przy poprawnej odpowiedzi „flaga zostaje przyznana” (US-01, FR-006, Business Logic) | `Correct` zwraca tylko kod; flagę przyznaje dopiero aktywacja | `prd.md:93`, `:119`, `:171-173` vs `VerifyAnswer/Handler.cs:110-113`, `ActivateFlag/Handler.cs:79-90` | **Dokument** — decyzja `context/archive/2026-10-08-answer-verification-earns-flag/plan.md:378-386`; to samo w `roadmap.md:223` (sekcja Done) |
| DR-02 | Formularz „Do sprawdzenia” jest w hangarze (FR-005, US-01) | Osobna strona „Terminal TOMO-AI-001” w podmenu kursów | `prd.md:117`, `:92` vs `client/main.tsx:62`, `client/pages/courses/coursesSubMenu.ts:13-14` | **Dokument** — decyzja `…/2026-10-08-answer-verification-earns-flag/plan.md:388-390`. Treść kursu też mówi „Hangar z trofeami → Do sprawdzenia” (`korpo-cte-300.md:136`) → poprawić treść kursu |
| DR-03 | „Flaga dowodzi wykonania, nie przeczytania” | Dla flag z kryteriami wystarczy znać kod — bez śladu weryfikacji, bez limitu prób | `prd.md:48` vs `ActivateFlag/Handler.cs:59-69` | **Decyzja właściciela** — FR-004 przyjął ryzyko przekazania (`prd.md:116`), ale `test-plan.md:145` uzależnia to od warunku, który jest już spełniony |
| DR-04 | „Flaga raz zdobyta nie znika” | Usunięcie konta kasuje zdobycia (cascade); przesunięcie `PublishDate` chowa je | `prd.md:82` vs `Config/UserFlagConfiguration.cs:44-47`, `Portal/Features/UserDelete/Handler.cs:63` | **Decyzja właściciela** — czy guardrail obejmuje usunięcie konta |
| DR-05 | Treść kursów niedostępna dla niezalogowanych; publiczne tylko kafelki | Załączniki i grafiki zadań (`data/*.docx`, `images/*`) publiczne, także przed publikacją | `prd.md:83`, `:187` vs `App01.Bootstrapper.Api/wwwroot/media/courses/korpo-cte-301/data/` | **Dokument** (ryzyko przyjęte 2026-10-11: `…/2026-10-11-testing-course-content-access/plan.md:189`) — dopisać wyjątek do guardrailu PRD; albo kod, jeśli decyzja się zmieni |
| DR-06 | Data publikacji „równa aktualnej dacie lub wcześniejsza” (data kalendarzowa) | Moment UTC z godziną | `prd.md:110`, `:191` vs `Course.cs:10-11` | **Dokument** — decyzja `…/2026-10-07-public-course-tiles/plan-brief.md:30` |
| DR-07 | Ranking „uczestników”, wskaźnik „liczba użytkowników” | Każde konto z ≥1 flagą, łącznie z adminami; osoby z 0 flag niewidoczne | `prd.md:126`, `:128` vs `GroupProgress/Handler.cs:60-64`, `Leaderboard/Handler.cs:46-61` | **Dokument** — decyzje `…/2026-10-10-leaderboard/plan-brief.md` (wiersz „Kto jest na liście”) i `…/2026-10-10-group-progress-dashboard/plan-brief.md:27` |
| DR-08 | Komentarz encji: kod „nieodgadywalny”, „zwracany wyłącznie przy werdykcie Correct” | Kod zwracany też w hangarze; w treści kursu kod = wyliczalny wynik `SAD{…}` | `Entities/Courses/Flag.cs:10` vs `HangarFlags/Handler.cs:81-82`, `korpo-cte-300.md:230` | **Kod (komentarz)** — decyzja z 2026-10-10 (`…/2026-10-10-hangar-flag-list/plan.md:17`) zawęziła regułę; znaczenie „nieodgadywalny” → patrz Q-02 |
| DR-09 | Admin definiuje „pulę flag oraz zadania” jako dwa pojęcia (FR-011), „numer zadania” | Jeden byt `Flag`; zadanie = flaga z `Criteria`; uczestnik wybiera zadanie z listy po `FlagId` | `prd.md:135`, `shape-notes.md:171`, `korpo-cte-300.md:136` („wpisać numer zadania”) vs `HangarTasks/Contracts.cs:16-21` | **Decyzja właściciela** — czy 1 zadanie = 1 flaga to reguła docelowa (wtedy dokument), czy uproszczenie MVP |
| DR-10 | Werdykt w < 5 s (NFR) | Limit 15 s | `shape-notes.md:210` vs `VerifyAnswer/Handler.cs:22` | **Dokument** — PRD (późniejszy) mówi 15 s (`prd.md:80`); shape-notes do oznaczenia jako nieaktualne |
| DR-11 | Nazwa terminala | `TOMO-AI-001` w kodzie, `TOMSO-AI-001` w treści kursu, `TOMO-AI001` w roadmapie | `TomoAiTerminalPage.tsx:115` vs `korpo-cte-300.md:136`, `roadmap.md:95` | **Dokument / treść kursu** — kod jest spójny z decyzją `…/2026-10-08…/plan.md:390` |
| DR-12 | Kryterium 70% mierzone „w trakcie trwania bootcampu” | Bootcamp nie istnieje w modelu; `AsOf` to dowolny moment, granic bootcampu brak | `prd.md:215` (OQ-3) vs `GroupProgress/Contracts.cs:10-12` | **Decyzja właściciela** — wystarcza, dopóki jest jeden bootcamp; przy drugim wskaźnik zmiesza pule |

---

## Krok 5 — Ranking refaktoru

| Poz. | Kandydat | Wartość | Ryzyko (słabość egzekucji) | Gotowe, gdy… |
| --- | --- | --- | --- | --- |
| **R-1** | **AG-01 Zdobycie flagi — pochodzenie zdobycia (INV-01c) i trwałość (INV-01b)** | najwyższa: to jest insight produktu i podstawa rankingu, % i kryterium 70% | wysokie: jednokrotność jest w bazie, ale „dowód wykonania” dla flag AI nie jest nigdzie wymuszony, brak limitu prób, cascade kasuje zdobycia | …test endpointu `activate-flag` dla flagi z `Criteria` i użytkownika, który nie otrzymał `Correct`, kończy się zgodnie z decyzją Q-01 (np. nie `Activated`), **oraz** `grep -rn "DeleteBehavior.Cascade" Config/UserFlagConfiguration.cs` zwraca wynik zgodny z decyzją Q-03 |
| R-2 | AG-02 Flaga/Zadanie — nazwane pojęcie „zadanie weryfikowalne” i sekretność kodu (INV-02d, INV-02e) | wysoka: od niej zależy, czy weryfikacja AI ma sens | średnie: reguła powielona w 2 miejscach, „trudny do zgadnięcia” tylko w komentarzu | …wyszukiwanie `IsNullOrWhiteSpace(f.Criteria)\|IsNullOrWhiteSpace(flag.Criteria)` w `App01.Modules.Courses` znajduje **jedno** miejsce, a zapytanie `SELECT Code FROM Courses.Flags WHERE Criteria IS NOT NULL AND Code LIKE 'SAD{%'` na produkcji zwraca 0 wierszy (lub wynik zgodny z Q-02) |
| R-3 | AG-03 Polityka widoczności kursu (INV-03a) | średnia: spójność hangaru, rankingu i wskaźników | niskie dziś (test spójności istnieje), ale 9 kopii warunku to pułapka dla każdego nowego wycinka | …`grep -rn "PublishDate <=" App01.Modules.Courses` znajduje **jedno** miejsce definicji, a `FlagCountConsistency/EndpointTests.cs` przechodzi bez zmian |
| R-4 | AG-03 Prywatność załączników (INV-03b) | średnia | średnie: ryzyko przyjęte, ale guardrail PRD mówi co innego | …PRD `prd.md:83` wymienia wyjątek dla mediów **albo** `curl -s -o /dev/null -w "%{http_code}" <host>/media/courses/korpo-cte-301/data/sad_slack_export.txt` bez JWT zwraca 401/404 |
| R-5 | Aktualizacja PRD do decyzji (DR-01, -02, -06, -07, -10, -11) | niska dla kodu, wysoka dla agentów (PRD wprowadza w błąd) | — | …`grep -n "flaga zostaje przyznana\|formularzu „Do sprawdzenia\" w hangarze" context/foundation/prd.md` nic nie zwraca |
| R-6 | AG-04 Polityka werdyktu, AG-05 Postęp grupy | wysoka / średnia | niskie — egzekwowane i przetestowane | utrzymać: testy `VerifyAnswer/EndpointTests.cs` i `GroupProgress/EndpointTests.cs` przechodzą |

**#1 do refaktoru: R-1 (Zdobycie flagi).** To jedyny rdzeniowy niezmiennik, który kod dziś *ignoruje*, a nie tylko rozprasza. Model zmienił się 2026-10-08 (zdobycie przeszło z weryfikacji na aktywację kodem) i od tego dnia kod flagi jest **jedynym** dowodem wykonania. To dokładnie ten warunek, przy którym `test-plan.md:145` każe wrócić do ryzyka. Zanim ktokolwiek ruszy kod, właściciel musi rozstrzygnąć Q-01 i Q-03 — refaktor bez tej decyzji tylko przeniósłby problem w inne miejsce.

### Pytania otwarte (rozstrzygalne w kilka minut)

| ID | Pytanie | Kto zna odpowiedź |
| --- | --- | --- |
| Q-01 | Czy aktywacja flagi **z kryteriami** ma wymagać wcześniejszego `Correct` tego samego użytkownika (wtedy trzeba zapisać fakt „zweryfikowano”), czy przekazanie kodu koledze jest nadal akceptowane po zmianie z 2026-10-08? | właściciel portalu (product owner) |
| Q-02 | Czy kody flag weryfikowanych AI są losowe, czy w formacie `SAD{…}` wyliczalnym z treści? (Jeśli wyliczalne, weryfikację AI da się obejść samą aktywacją.) | administrator wprowadzający dane SQL-em |
| Q-03 | Czy usunięcie konta ma kasować zdobyte flagi (dziś: tak, cascade), czy guardrail „flaga nie znika” obejmuje też ten przypadek? | właściciel portalu |
| Q-04 | Czy limit prób aktywacji (np. na minutę) jest potrzebny przy kodach typu `SAD{n1;n2}`? | właściciel portalu + osoba utrzymująca serwer |
| Q-05 | Czy „bootcamp” stanie się bytem (kilka edycji, osobne pule i rankingi), czy zawsze jest jeden? Od tego zależy DR-12. | właściciel portalu |
| Q-06 | Czy jedno zadanie może kiedyś dawać kilka flag (lub flaga wymagać kilku zadań)? Od tego zależy DR-09. | autor kursów (właściciel portalu) |
| Q-07 | Która nazwa terminala jest kanoniczna: `TOMO-AI-001` czy `TOMSO-AI-001`? | autor kursów |
