---
artifact: territory
generated: 2026-10-14
head: 375c6fd
window: "12 months ago..HEAD = 0 commitów → użyto całej historii 2020-05-06..2022-02-10"
sources: git (bez forge — brak `gh`)
evidence: context/map/evidence/1-history.md, context/map/.work/git-log.txt
---

# Terytorium — gdzie projekt żyje

## TL;DR

- Repo jest **uśpione**: ostatni commit 2022-02-10 (~32 miesiące temu), w oknie 12 miesięcy — **0 commitów**. `evidence`
- Cała historia to 4 commity: 1 import całego kodu (407 plików), 2 zmiany README, 1 realna zmiana kodu. `evidence`
- Jedyny plik źródłowy dotknięty po imporcie: `eTMsDB/REPOSITORIES/AppDbContext.cs` — warstwa danych **wszystkich** capability. `evidence`
- Historia nie mówi, gdzie projekt „żył” przed importem — to główny unknown tego artefaktu.

## Historia commitów

| SHA | Data | Autor | Temat | Plików | Klasyfikacja |
| --- | --- | --- | --- | --: | --- |
| 221edd3 | 2020-05-06 | tomsoft1 | first commit | 1 | docs (README) |
| 42292b7 | 2020-05-06 | tomsoft1 | Create project | 407 | **mass — import** (wykluczony z liczenia) |
| e5ab5c4 | 2020-05-06 | mulatom1 | change readme.md | 1 | docs (edycja przez GitHub web) |
| 375c6fd | 2022-02-10 | Tomasz Mularczyk | sql dateformat ymd | 6 | **jedyna zmiana kodu** + szum IDE |

Liczone: 3 commity. Boty: 0. Commity agentów AI: 0. Merge: 0. Shallow clone: nie.

## Stale aktywne obszary

| Obszar | Zmiany po imporcie | Wniosek |
| --- | --: | --- |
| `eTMsDB/REPOSITORIES/AppDbContext.cs` | 1 commit, +110/−110 | jedyny „gorący” plik; poprawka formatu dat SQL Server (`set dateformat ymd;`) dopisana do ~110 zapytań — `evidence` |
| `README.md` | 2 commity, po 1 linii | proza, nie aktywność (false-signal 8) |
| wszystkie capability biznesowe (pożyczki, kasa, klienci, użytkownicy…) | 0 | brak sygnału — nie znaczy „stabilne”, znaczy „bez historii” |

Trend: **dormant**. Kwartały: 2020-Q2 (README), 2022-Q1 (fix dat). Brak kampanii.

## Co zmienia się razem (co-change)

Policzalne tylko dla 375c6fd:

- `AppDbContext.cs` ↔ `eTMsDATABASE/eTMsDATABASE.jfm`, `eTMsDATABASE.dbmdl` (usunięty), `*.sqlproj.user`, `*.csproj.user`, `LOKAL.pubxml.user` — **mechaniczne** (stan IDE wcommitowany przy okazji).
- Sama zmiana w `AppDbContext.cs` jest **cross-cutting**: dotyka zapytań modułów, uprawnień, parametrów, klientów i dalszych obszarów naraz — `inference` z diffu.

Pary, które *powinny* zmieniać się razem, a historia tego nie pokaże (`unknown`): encje C# (`eTMsDB/ENTITIES`) ↔ DDL (`eTMsDATABASE/dbo/Tables`) ↔ stringi SQL w `AppDbContext.cs` ↔ modele AngularJS w `eTMsWEB/Scripts/*`.

## Gdzie aktywność przecina się z wrażliwymi obszarami

| Obszar wrażliwy | Przecięcie z aktywnością | Dowód |
| --- | --- | --- |
| Dane (SQL Server) | **tak** — jedyna zmiana kodu to warstwa danych | 375c6fd |
| Auth / uprawnienia | pośrednio — 375c6fd zmienił zapytania `SL_MODULY`, `SL_UPRAWNIENIA` | diff 375c6fd |
| Pieniądze (pożyczki, kasa, sprzedaż zastawów) | pośrednio — przez wspólny `AppDbContext.cs` | diff + structure |
| Build / deploy | szum: pliki `.user`, profil publikacji `LOKAL` | 375c6fd |
| Publiczne API / integracje | brak aktywności; jedyny ślad integracji: akcja `PozyczkaLombardowaZastawParametrAllegro` | nazwa akcji — `unknown (external)` |

## Szum odfiltrowany

| Kategoria | Co | Ile |
| --- | --- | --: |
| Vendored NuGet | `packages/` (wcommitowane paczki) | 153 pliki (z fontami) |
| Vendored JS/CSS | `eTMsWEB/Scripts/{angular*,jquery*,bootstrap*,moment*}`, `Content/bootstrap*`, `fonts/` | — |
| Media / binaria | png, jpg, fonty, `*.dbmdl`, `*.jfm` | — |
| Stan IDE | `*.csproj.user`, `*.sqlproj.user`, `*.pubxml.user` | 3 w 375c6fd |
| Masowy commit | 42292b7 import (407 plików) | 1 |
| Lockfile / snapshoty / generowane / lokalizacje / boty | brak w repo | 0 |

Razem 186 z 407 śledzonych plików to szum; 221 plików kodu po filtrze.

## Unknowns

- Historia sprzed 2020-05-06 — kod przyszedł jednym importem; prawdziwe hot-spoty są poza repo.
- Czy rozwój trwa gdzie indziej (inne repo, lokalne kopie per placówka — `Web.config.SUKURS`, `PLIKI/Web.config` sugerują konfiguracje per wdrożenie).
- PR-y i issues na GitHubie — niedostępne bez `gh`.

## Komendy

`git log --no-merges --name-status -M --format=…` (→ `.work/git-log.txt`), `git log --shortstat`, `git show --name-status 375c6fd e5ab5c4`, `git show 375c6fd -- eTMsDB/REPOSITORIES/AppDbContext.cs`, `git rev-parse --is-shallow-repository`, `git ls-files`.
