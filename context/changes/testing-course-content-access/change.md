---
change_id: testing-course-content-access
title: Testy granicy dostępu do treści kursów (rollout Phase 1, ryzyko #1)
status: implemented
created: 2026-10-11
updated: 2026-10-11
archived_at: null
---

## Notes

Open a change folder for rollout Phase 1 of context/foundation/test-plan.md: "Granica dostępu do treści kursów".
  Risks covered: #1 (treść kursu dostępna dla niezalogowanego — przez API lub publiczne pliki statyczne — albo dla zalogowanego przed datą publikacji). Test types planned: integracja (WebApplicationFactory + próba pobrania pliku statycznego).
  Risk response intent: #1 — bez JWT żaden endpoint nie zwraca treści kursu; kurs z datą publikacji w przyszłości daje 404 (także na granicy równości dat, UTC); plik treści kursu nie jest osiągalny pod żadnym publicznym adresem hosta.
  Challenge: „treść poza wwwroot = bezpieczna" oraz „401 = brak wycieku". Avoid: sprawdzanie samego kodu statusu bez treści odpowiedzi, treść w katalogu nieużywanym przez produkcję, pominięcie granicy dat.
  After creating the folder, follow the downstream continuation rule.
