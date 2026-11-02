---
artifact: contributors
generated: 2026-10-14
head: 375c6fd
area: "Warstwa danych — eTMsDB/REPOSITORIES/AppDbContext.cs (+ AppDbContextBase.cs, IAppDbContext.cs)"
window: "12 months ago..HEAD = 0 commitów → cała historia 2020-05-06..2022-02-10"
sources: git (bez forge — brak `gh`)
evidence: context/map/evidence/4-people.md
---

# Kontekst kontrybutorów — warstwa danych (`AppDbContext.cs`)

## Dlaczego ten obszar

- **Centralny**: fan-out 37, zależą od niego wszystkie 12 capability (artifact-2). `evidence`
- **Jedyny aktywny**: jedyny plik źródłowy zmieniony po imporcie (375c6fd, artifact-1). `evidence`
- **Podejrzany**: całe SQL systemu w stringach z konkatenacją wartości; transakcje tylko w tekście SQL. `evidence`

## Kto pracował przy obszarze

| Osoba | Commity w obszarze | Okres | Tematy |
| --- | --: | --- | --- |
| tomsoft1 | 1 (import 42292b7) | 2020-05-06 | wniesienie całego pliku (3876 linii) — autorstwo sprzed repo nieznane |
| Tomasz Mularczyk | 1 (375c6fd) | 2022-02-10 | **format dat SQL Server**: `set dateformat ymd;` dopisane do ~110 zapytań (moduły, uprawnienia, parametry, klienci i dalej) |

W ostatnich 12 miesiącach: **nikt** (0 commitów w repo).

Tożsamości: `mulatom1` (edycja README przez GitHub) to ta sama osoba co Tomasz Mularczyk — scalone po wspólnym handle'u. Czy `tomsoft1` to też ta sama osoba — `unknown`.

## Powtarzające się tematy

- Jedyny udokumentowany temat zmiany: **zależność od ustawień regionalnych serwera SQL** przy parsowaniu dat (`'yyyy-MM-dd HH:mm:ss'` wklejane do zapytań). Poprawka była cross-cutting — w każdym zapytaniu osobno, nie w jednym miejscu (np. w `AppDbContextBase`). `evidence` z diffu.
- Brak innych tematów — za mało historii.

## Co przeczytać przed zmianą

PR-ów i dyskusji brak (żaden commit nie odwołuje się do PR, brak planów/ADR w `context/`). Zamiast tego:

1. `git show 375c6fd -- eTMsDB/REPOSITORIES/AppDbContext.cs` — jedyny precedens zmiany i wzorzec, jak budowane są zapytania.
2. `eTMsDB/REPOSITORIES/AppDbContextBase.cs` — `SqlConnection`/`SqlCommand`, `ExecuteNonQuery`, `GetDataSet`, `QueryLog` (logowanie zapytań do `TB_QUERY_LOG`).
3. `eTMsDATABASE/scripts/CreateDB.sql` — model danych (28 tabel: `TB_` dane, `SL_` słowniki), do porównania z kolumnami w stringach SQL.
4. Wystąpienia `BEGIN TRAN`/`COMMIT TRAN` w `AppDbContext.cs` (32 linie) — które wieloetapowe operacje kasowe są objęte transakcją.

Edge case'y wart sprawdzenia przed zmianą (`inference`):
- format dat i kultura `pl-PL` (`<globalization culture="pl-PL">` w `PLIKI/Web.config`) vs ustawienia SQL Server;
- wartości wklejane do SQL (apostrofy w nazwiskach/opisach zastawów, SQL injection);
- `GetDataSet(query)` wywoływany z konsoli `AdminController.DBWykonaj`;
- spójność encji (`eTMsDB/ENTITIES`) z kolumnami SQL — brak ORM, brak kontroli w buildzie.

## Skupienie czy rozproszenie wiedzy

- **Skupiona** — 1–2 tożsamości w całej historii; to baseline dla tak małego zespołu (false-signal 6), nie odrębne znalezisko.
- Realne ryzyko to nie bus factor w repo, lecz **brak historii**: wiedza o tym, dlaczego zapytania wyglądają tak, a nie inaczej, jest sprzed importu i poza repo.

## Kogo zapytać

| Pytanie | Osoba |
| --- | --- |
| Dlaczego `set dateformat ymd` w każdym zapytaniu; na jakich serwerach/wdrożeniach był problem | Tomasz Mularczyk |
| Pochodzenie kodu, wcześniejsza historia, wdrożenia per placówka (`Web.config.SUKURS`) | tomsoft1 (jeśli inna osoba) |

## Unknowns

- Autorzy i historia sprzed 2020-05-06; czy jest inne repo z historią.
- Czy system działa produkcyjnie i kto go utrzymuje.
- Formalna własność obszaru; czy tomsoft1 jest osiągalny.
- PR-y/issues na GitHubie — niesprawdzone (brak `gh`).

## Komendy

`git log --format='%an|%ae|%cn' | sort | uniq -c` (e-maile użyte tylko do scalenia tożsamości, nie zapisane), `git log --follow --format=… -- eTMsDB/REPOSITORIES/AppDbContext.cs` (via `.work/git-log.txt`), `git show 375c6fd -- eTMsDB/REPOSITORIES/AppDbContext.cs`, `git grep -c -i 'begin tran|commit tran'`, `git grep -n 'GetDataSet|QueryLog'`.
