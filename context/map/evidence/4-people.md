# Evidence: people

Humans in window (whole history): 2 identities — **Tomasz Mularczyk** (also as `mulatom1`, merged by matching handle) and **tomsoft1** (may be the same person; unknown). Baseline: **small team — concentration is the baseline**; no bus-factor language.

## People per capability

| Capability | People | Topic | Period |
| --- | --- | --- | --- |
| all business capabilities (initial import) | tomsoft1 | imported the whole code base | 2020-05-06 |
| shared (SQL data access) | Tomasz Mularczyk | SQL Server date format (`set dateformat ymd`) across all queries | 2022-02-10 |
| docs | tomsoft1, Tomasz Mularczyk | README | 2020-05-06 |

No capability has been changed by anyone after import except shared data access.

## Concentration

Every capability has 1 author (the importer). Baseline for a 1–2 person repo.

## Agent co-authorship

0 % (no `Co-authored-by` trailers).

## Implications

- Whom to ask about any capability: Tomasz Mularczyk (only post-import change; repo owner). tomsoft1 for the original authoring, if a different person.
- No PRs to read.

## Unknowns

- Original authors before the import; whether tomsoft1 is still reachable; formal ownership; whether the system is in production and who operates it.

Commands: `git log --format='%an|%ae|%cn' | sort | uniq -c` (e-mails used only to merge identities), `git log --name-status`.
