---
change_id: post-flow-analysis
title: Analiza przepływu spłaty całkowitej pożyczki lombardowej
status: preparing
created: 2026-10-14
updated: 2026-10-14
archived_at: null
---

## Notes

Spłata całkowita pożyczki lombardowej
Badam przepływ spłaty całkowitej pożyczki od akcji PozyczkaLombardowaAkcjaSplataCalkowita w eTMsWEB/Controllers/PozyczkaLombardowaController.cs:284, przez DAL i BLL, aż do SQL w AppDbContext.cs, bo mapa wskazuje monolit pożyczek jako strefę ryzyka (Watch 2): płyną przez niego      pieniądze, ma najtwardszy coupling (Ca 5 / Ce 8, pożyczki i kasa w tych samych plikach) i nie ma testów.
