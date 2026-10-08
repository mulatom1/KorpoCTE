<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Zweryfikowana odpowiedź daje flagę

- **Plan**: context/changes/answer-verification-earns-flag/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-10-08
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 2 warnings, 7 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Automated criteria (rerun on 2026-10-08):
- `dotnet build`: 0 warnings, 0 errors. Run with `--artifacts-path`, because the `bin` folder is locked by a VS session.
- `dotnet test`: 425/425.
- `dotnet format --verify-no-changes`: OK.
- `npm run build`: OK. `npm run lint`: OK. `npm test`: 52/52.
- `prettier --check`: committed contents pass. Warnings appear only for CRLF line endings in the local working copy (`core.autocrlf`).

Manual criteria: 1.4, 2.4–2.8 and 3.5–3.8 are confirmed by the user in the session, and each Progress row carries a SHA.

## Findings

### F1 — Different definition of a "verifiable task" in the two endpoints

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: src/server/App01/App01.Modules.Courses/Features/HangarTasks/Handler.cs:51
- **Detail**: `HangarTasks` filters with `Criteria != null && Criteria != ""`, while `VerifyAnswer` (Handler.cs:84) uses `string.IsNullOrWhiteSpace`. A flag whose criteria contain only whitespace appears in the terminal list, but verifying it returns 404. The `HangarTasks` test does not cover whitespace-only criteria.
- **Fix**: Change the filter in `HangarTasks` to `!string.IsNullOrWhiteSpace(f.Criteria)` (EF translates it on SQL Server; InMemory evaluates it in memory). Add a whitespace-only flag to the `HangarTasks_SkipsUnpublishedCourseAndFlagsWithoutCriteria` seed.
- **Decision**: FIXED — predykat IsNullOrWhiteSpace w HangarTasks + flaga z białymi znakami w teście

### F2 — Plan example and entity comment suggest a guessable `Flag.Code`

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: context/changes/answer-verification-earns-flag/plan.md:358, src/server/App01/App01.Shared.Application/Entities/Courses/Flag.cs:10
- **Detail**: After Addendum 1, `Flag.Code` is the activation secret. Yet the manual test step in the plan inserts the code `'cte-01'`, and the entity comment says "Krótki kod flagi dla administratora". An admin copying the example would create a guessable secret once S-06 adds activation.
- **Fix**: In plan.md, change the example to a random code (e.g. `LEFT(REPLACE(NEWID(),'-',''),16)`). Change the comment in Flag.cs to: "Sekret aktywacji flagi — nieodgadywalny, unikalny; zwracany wyłącznie przy werdykcie Correct".
- **Decision**: FIXED — losowy kod w przykładzie SQL planu, komentarz Flag.Code opisuje sekret aktywacji

### F3 — No test guarding that `HangarTasks` never returns `Code`

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: tests/server/App01/App01.Api.Tests/Features/Courses/HangarTasks/EndpointTests.cs:225-238
- **Detail**: The test checks the raw JSON for the absence of `Criteria`, but not of `Code`. The Addendum makes "HangarTasks must never return Code" an explicit invariant. The DTO is safe today, but nothing protects against a regression.
- **Fix**: In that test, add `Assert.DoesNotContain(<seeded Code>, json)`.
- **Decision**: FIXED — asercje braku pola code i wartości kodów w HangarTasks_ResponseDoesNotContainCriteria

### F4 — Model's `reason` logged at Debug may echo criteria

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: src/server/App01/App01.Modules.Courses/Features/VerifyAnswer/Handler.cs:151
- **Detail**: The prompt forbids quoting the criteria, but an injected answer can push the model to put them into `reason`, which ends up in the Debug log. Exposure is limited to people with log access. The plan ties this logging to tuning the criteria.
- **Fix**: Accept as a risk (logs are not public), or remove `{Reason}` from the log.
- **Decision**: ACCEPTED — reason w logu Debug potrzebny do dopracowania kryteriów; logi nie są publiczne

### F5 — `Correct` message refers to a "hangar" that does not exist in the UI

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: src/server/App01/App01.Modules.Courses/Features/VerifyAnswer/Handler.cs:27
- **Detail**: "Odpowiedź poprawna! Zapisz kod flagi i aktywuj go w hangarze." After Addendum 2 the UI has no "Hangar". The PRD places activation (S-06) in the hangar, so the text is accurate for the future but confusing today.
- **Fix**: Leave it and update the wording together with S-06 (location of the activation form), or change it now to "…aktywuj go w formularzu aktywacji." (also update the tests).
- **Decision**: FIXED — komunikat „…aktywuj go w formularzu aktywacji.” (handler, testy serwera i klienta, plan)

### F6 — Terminal shows "Brak zadań do sprawdzenia" together with a list-loading error

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: src/client/app01/src/pages/courses/TomoAiTerminalPage.tsx:46-58
- **Detail**: When `getHangarTasks` fails, the page shows the error alongside the empty-list message, which looks like there are genuinely no tasks. `fetchTasks` also does not clear a previous `error`.
- **Fix**: Show the empty state only when there is no error, and clear `error` at the start of `fetchTasks`.
- **Decision**: FIXED — pusty stan tylko bez błędu, czyszczenie błędu w fetchTasks, test (zweryfikowany próbą złamania)

### F7 — Client-cancel branch is dead because the endpoint does not pass the `CancellationToken`

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: src/server/App01/App01.Modules.Courses/Features/VerifyAnswer/Endpoint.cs:18
- **Detail**: The handler distinguishes a timeout from a client cancellation, but `mediator.Send(request)` does not pass `RequestAborted`, so the token is always `None` (consistent with sibling endpoints). Passing it would, on a client abort, produce an `OperationCanceledException` that the middleware turns into a 500.
- **Fix**: No change. Note the requirement for when the token is passed (middleware or endpoint must handle OCE).
- **Decision**: ACCEPTED — zgodne z wzorcem endpointów projektu; przy przekazaniu RequestAborted trzeba obsłużyć OCE

### F8 — Stale names in the plan; CLAUDE.md update not committed

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: context/changes/answer-verification-earns-flag/plan.md:350, plan.md:430; CLAUDE.md (working tree)
- **Detail**: Testing Strategy and Progress 3.3 still refer to `HangarPage`/`HangarPage.test.tsx`, while the file is `TomoAiTerminalPage.test.tsx`. The CLAUDE.md update describing the "Code instead of save" model (required by Addendum 1) exists only in the working tree, mixed with the user's other changes.
- **Fix**: Fix the names in plan.md, keeping the Progress row's SHA. The user commits CLAUDE.md themselves.
- **Decision**: FIXED — nazwy w plan.md (Testing Strategy, Progress 3.3 z zachowanym SHA); CLAUDE.md commituje użytkownik

### F9 — Two error-parsing styles in `ApiCoursesService`

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: src/client/app01/src/services/api-courses-service.ts:131-149
- **Detail**: The new methods use the `getProblemMessage` helper (`detail` → first `errors` entry → `message`), while `getCourseTiles`/`getCourseContent` parse errors inline. The extra `errors` parsing (for 400 ValidationProblemDetails) is not in the plan, but it is justified.
- **Fix**: Leave as is, or move the older methods to the helper in a separate change.
- **Decision**: SKIPPED — ujednolicenie parsowania błędów przy innej zmianie
