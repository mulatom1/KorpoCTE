# Evidence: history

Window: requested 12 months has 0 commits → whole history 2020-05-06..2022-02-10 @ 375c6fd. 4 commits, 3 counted (42292b7 initial import of 407 files excluded as mass). Buckets: quarter. No bots, no agent authors.

## Activity per capability

| Capability | Changes (files) | Commits | Note |
| --- | --: | --: | --- |
| shared | 1 | 1 | `eTMsDB/REPOSITORIES/AppDbContext.cs` (+110/−110) — evidence |
| platform | 4 | 1 | `.jfm`, `.dbmdl` (deleted), `*.user` files — IDE/user state, mechanical |
| docs | 2 | 2 | `README.md` (1 line each) |
| every business capability | 0 | 0 | — |

Unmapped: 0. Agent-authored: 0 %.

## Top files in top capabilities

Only one source file ever changed after the import: `eTMsDB/REPOSITORIES/AppDbContext.cs` (3876 lines, all SQL of the system). The change (375c6fd "sql dateformat ymd") prefixed 110 queries with `set dateformat ymd;` — the queries cover modules, permissions, parameters, clients and further capabilities, so the fix is about **every** capability's data access, attributed to `shared` because that is where the SQL lives (inference).

## Trend

| Quarter | Counted commits |
| --- | --: |
| 2020-Q2 | 2 (README) |
| 2022-Q1 | 1 (date format fix) |

Label: **dormant** since 2022-02-10 (no commit for ~32 months). No campaigns.

## Fix pressure

Heuristic `fix|bug|hotfix|revert|regression|broken|incident` matches 0 subjects. 375c6fd is a fix in substance (locale-dependent date parsing on SQL Server) though not by subject — inference. Reverts: 0.

## Co-change between capabilities

Not computable: one non-trivial commit. 375c6fd couples `AppDbContext.cs` with IDE/user files (mechanical).

## Hub files and freshness

`eTMsDB/REPOSITORIES/AppDbContext.cs` exists at HEAD. `eTMsDATABASE/eTMsDATABASE.dbmdl` deleted in 375c6fd (binary cache — rule 11, irrelevant).

## Mechanical signals

- 42292b7 initial import: 407 files — baseline, not activity.
- `*.user`, `*.jfm`, `*.dbmdl` changes in 375c6fd — local IDE state committed by accident (rule 7).

## Unknowns

- Development history before 2020-05-06 (code arrived as one import) — where the real buzz lived is not in this repo.
- Whether development continues elsewhere (another repo/branch, deployments from local copies — `Web.config.SUKURS`, `PLIKI/Web.config` suggest per-site configs).
- Hand-kept pairs that should change together (C# entities ↔ `eTMsDATABASE` DDL ↔ raw SQL in AppDbContext ↔ AngularJS models) — no history to test them.

Commands: `git log --no-merges --name-status -M` (→ .work/git-log.txt), `git log --shortstat`, `git show --name-status 375c6fd e5ab5c4`, `git show 375c6fd -- AppDbContext.cs`, `git cat-file -e`.
