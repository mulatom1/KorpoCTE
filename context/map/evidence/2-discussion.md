# Evidence: discussion

Sources: **git only** (`gh` not installed). Window: whole history 2020-05-06..2022-02-10.

## Buzz per capability

| Capability | PRs | Closed-unmerged | Human review | Reverts | Open bugs | Plans | Debt markers |
| --- | --: | --: | --- | --: | --- | --: | --: |
| all | 0 in subjects | unknown | unknown | 0 | unknown | 0 | 0 |

No `(#n)` or `Merge pull request` subjects. `context/changes/` and `context/archive/` hold only README scaffolds — no plans, ADRs or research.

## Friction

None visible. 0 reverts, 0 second attempts, no hotfix subjects.

## Planning attention

None. README is a single heading (`# eTMsLomb`).

## Debt markers

`git grep -c 'TODO|FIXME|HACK|XXX'` over C#, Razor, own JS and SQL: **0** in every capability (0 per 1000 lines). evidence.

## Mechanical signals

- 2 README commits (one via GitHub web editor) — prose churn, rule 8.

## Unknowns

- Forge: issues, PRs, reviews not read — `gh` not installed (recommend installing GitHub CLI if the GitHub remote holds issues).
- Issue tracker / error tracker: none referenced in the repo. `TB_QUERY_LOG` table and `QueryLog()` in `eTMsDB/REPOSITORIES/AppDbContextBase.cs` log SQL to the DB — the only observability seen.
- Zero TODOs in 26k lines is unusual — may mean markers were stripped or never used; it is not evidence of low debt.

Commands: `git log --format=%s`, `git grep -c -E 'TODO|FIXME|HACK|XXX'`, `ls context/*`, `which gh`.
