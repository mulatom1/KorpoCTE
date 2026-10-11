---
change_id: testing-flag-consistency
title: Spójność flag na prawdziwym silniku (test-plan faza 2, ryzyka #2 i #3)
status: archived
created: 2026-10-11
updated: 2026-10-11
archived_at: 2026-10-11T13:52:38Z
---

## Notes

Open a change folder for rollout Phase 2 of context/foundation/test-plan.md: "Spójność flag na prawdziwym silniku".
  Risks covered: #2 (podwójne zaliczenie tej samej flagi — równoległa aktywacja lub baza bez wymuszonej unikalności), #3 (hangar, ranking i dashboard pokazują różne liczby flag dla tego samego stanu). Test types planned: integracja (SQL Server) + integracja (InMemory).
  Risk response intent:
  - #2: dwie równoległe aktywacje tej samej flagi przez tego samego uczestnika dają dokładnie jeden zapis, druga dostaje `AlreadyOwned`, ranking pokazuje 1 — sprawdzone na prawdziwym silniku SQL Server, nie na InMemory ani SQLite.
  - #3: przy jednym seedzie (kursy opublikowane/nieopublikowane, granica daty, admin, kilku uczestników) liczba flag uczestnika w hangarze = liczba w rankingu, a suma na dashboardzie jest z nimi zgodna; wyrocznia z PRD, nie z obliczeń produkcyjnych.
  After creating the folder, follow the downstream continuation rule.
