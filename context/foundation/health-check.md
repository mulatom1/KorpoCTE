---
project: app01 (APPS — tomsoft1.pl, klient src/client/app01)
checked_at: 2026-09-28T00:05:00Z
health_status: healthy
context_type: brownfield
language_family: multi
stack_assessment_available: true
checks_run:
  - lockfile
  - dependency_audit
  - outdated_deps
  - test_runner
  - ci_cd
  - configuration
audit_findings:
  critical: 0
  high: 0
  moderate: 0
  low: 0
test_runner_detected: true
ci_provider: GitHub Actions
recommended_fixes: 3
---

> Zakres przebiegu: katalog roboczy `src/client/app01` (JS/TS). Audyt NuGet (`dotnet list package --vulnerable/--outdated`) został wykonany dla całej solucji i jest dołączony. Testów serwera (`dotnet test APPS.sln`) w tym przebiegu NIE uruchamiano.

## Dependency Health

### Lockfile

```
Status: present — src/client/app01/package-lock.json; NuGet packages.lock.json w 8/8 projektów
Package manager: npm + dotnet (NuGet)
```

CI wykonuje `npm ci` i `dotnet restore --locked-mode`. `.nvmrc` przypina Node 22 (zgodnie z `actions/setup-node` w CI).

### Security Audit

```
Tool: npm audit --json (src/client/app01)
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: brak podatności ani w bezpośrednich, ani w przechodnich
```

```
Tool: dotnet list APPS.sln package --vulnerable --include-transitive
Summary: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
Direct vs transitive: sprawdzono oba; żaden z 8 projektów nie ma podatnych pakietów
```

**Sekrety:** w kliencie git śledzi wyłącznie `.env.example`; `.env`, `.env.dev`, `.env.prod1`, `.env.prod2` są ignorowane (`.gitignore`: `.env`, `.env.*`, `!.env.example`). Zawartości lokalnych plików `.env*` nie odczytywano.

### Outdated Dependencies

```
Packages with major version gaps: 11 (npm: 10, NuGet: 1)
```

npm (`npm outdated`):

- **typescript**: 5.9.3 → 7.0.2 (2 majory) — przypięte w `CLAUDE.md` („TypeScript 5.9")
- **eslint-plugin-react-hooks**: 5.2.0 → 7.1.1 (2 majory)
- **@types/node**: 24.19.0 → 26.6.3 (2 majory)
- **react-router**: 7.18.4 → 8.4.0 — przypięte w `CLAUDE.md` („React Router 7")
- **vite**: 7.3.6 → 8.3.1 — przypięte („Vite 7")
- **@vitejs/plugin-react**: 5.2.0 → 6.1.1 (sprzężony z Vite 8)
- **eslint** / **@eslint/js**: 9.39.5 → 10.x
- **globals**: 16.5.0 → 17.12.0
- **mermaid**: 11.17.2 → 12.0.0

NuGet: **MediatR** 12.5.0 → 14.2.0 — celowo przypięte (`CLAUDE.md`: „v13+ ma licencję komercyjną").

Wszystkie pakiety są na najnowszej wersji w obrębie swojego majora („wanted" = „current"). Luki majorowe to w większości świadome przypięcia z `CLAUDE.md`, nie zaniedbanie.

## Test Suite

```
Test runner: Vitest 5.0.2 (+ jsdom, Testing Library)
Tests found: 15 testów w 4 plikach
Test execution: passing (npx vitest run — 4/4 plików, 15/15 testów, 0,9 s)
```

```
Configuration: src/client/app01/vite.config.ts (blok `test`), setup: src/test/setup.ts
Framework: Vitest 5.0.2
```

Pliki testów: `src/utils/auth.test.ts`, `src/utils/jwt.test.ts`, `src/utils/parseFrontmatter.test.ts`, `src/components/ConfirmModal.test.tsx`.

Pozostałe lokalne kontrole (wszystkie zielone): `npm run lint` (ESLint, `--max-warnings 0`), `npx prettier --check "src/**/*.{ts,tsx,css}"`, `npx tsc -b` (exit 0).

Serwer: xUnit 2.9.3 + `WebApplicationFactory`, 25 plików `EndpointTests.cs` — wykryty, ale nie uruchamiany w tym przebiegu.

## CI/CD

```
Provider: GitHub Actions
Configuration: .github/workflows/pull-request.yml (każdy push i PR)
```

| Stage      | Status | Notes |
|------------|--------|-------|
| Lint       | ✓      | `npm run lint` (ESLint 9, `--max-warnings 0`), `npx prettier --check`; serwer: `dotnet format --verify-no-changes` |
| Test       | ✓      | `npm test` (Vitest); serwer: `dotnet test` z pokryciem + raport HTML |
| Build      | ✓      | `npm run build`; serwer: `dotnet build --no-restore` |
| Type check | ✓      | `tsc -b` w ramach `npm run build`; C# typowany przez kompilator |
| Security   | ✓      | `npm audit --audit-level=high`; `dotnet list package --vulnerable --include-transitive` (fail na High/Critical) |

## Configuration

All expected configuration files present. No gaps detected.

- `.editorconfig` (root), `.prettierrc.json`, `eslint.config.js`, `.gitignore`, `.env.example`, `.nvmrc` — obecne.
- `tsconfig.app.json`: `"strict": true`.
- `CLAUDE.md` + `AGENTS.md` — obecne (root).

## Stack Assessment Cross-Reference

```
Stack assessment: context/foundation/stack-assessment.md
Agent readiness (from stack-assess): ready-with-compensation
```

| Quality Gate Gap | Health-Check Finding | Status |
|---|---|---|
| convention_based: partial (Vite + React bez routingu plikowego) | Sekcja „Frontend" w `CLAUDE.md`; lint + prettier + `tsc -b` przechodzą lokalnie i w CI | Mitigated |
| convention_based: partial (Minimal APIs) | Przepisy na slice/moduł w `CLAUDE.md`; `dotnet format --verify-no-changes` w CI | Mitigated |
| Luka 4 — wzorzec weryfikacji przez LLM (zakres PRD) | Blok „Moduł Courses — weryfikacja odpowiedzi przez LLM" jest już w `CLAUDE.md`. Po stronie klienta `apiFetch` nie ma limitu czasu ani rozróżnienia awarii — patrz Fix 1 | Mitigated (serwer) / Open (klient) |
| Luka 5 — treść kursów vs `UseStaticFiles` (zakres PRD) | Blok „Moduł Courses — treść kursów" jest w `CLAUDE.md` | Mitigated |
| Obserwacja: 500 zwraca `exception.Message` w `Detail` | Middleware nie do zmiany (PRD); klient nie powinien wyświetlać `detail` z 500 — patrz Fix 2 | Open |
| Wersje przypięte (.NET 10, TS 5.9, Vite 7, RR 7, MediatR 12) | 11 luk majorowych w `outdated` — w większości objęte przypięciem | Reinforced (celowo) |

## Recommended Fixes

### Fix before agent work (Category A)

### 1. Klient bez limitu czasu i bez stanu „awaria" dla weryfikacji odpowiedzi

**Impact**: PRD (US-01) wymaga werdyktu w < 5 s i komunikatu o awarii odróżnionego od oceny negatywnej. `apiFetch` (`src/services/api-fetch.ts`) woła `fetch` bez `signal` — przy zawieszonym połączeniu formularz „Do sprawdzenia" czeka bez końca, a agent budujący `ApiCoursesService` nie ma wzorca, który to obsługuje.
**Severity**: medium
**Effort**: moderate (15–30 min)
**Fix**:

W `ApiCoursesService` (nie w `apiFetch`, żeby nie zmieniać zachowania innych modułów) przekaż limit czasu i mapuj błąd na stan awarii:

```ts
const response = await apiFetch(url, {
  method: "POST",
  headers,
  body: JSON.stringify(request),
  signal: AbortSignal.timeout(6000), // serwer ucina model po ≤ 4 s
});
```

W komponencie: `TimeoutError`/`TypeError` (sieć) oraz `Status === "Unavailable"` → komunikat „Weryfikacja chwilowo niedostępna" + przycisk „Spróbuj ponownie"; `Incorrect` → komunikat o błędnej odpowiedzi. Dodaj test `<Komponent>.test.tsx` z `vi.fn()` zwracającym każdy z czterech statusów.

### 2. Nie wyświetlaj `detail` z odpowiedzi 500

**Impact**: `ExceptionHandlingMiddleware` zwraca w 500 treść wyjątku (z dwoma poziomami `InnerException`). Middleware jest poza zakresem zmian (PRD), więc jedyną warstwą, która może nie pokazywać tego użytkownikowi, jest klient. Agent kopiujący istniejące wzorce łatwo wstawi `problem.detail` do UI.
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: dopisz do sekcji „Frontend" w `CLAUDE.md`:

```markdown
- Dla odpowiedzi 5xx pokazuj ogólny komunikat („Wystąpił błąd serwera, spróbuj ponownie"); nie wyświetlaj pola `detail` z ProblemDetails. `detail` wolno pokazać tylko dla 400 (walidacja).
```

### 3. Udokumentuj przypięte majory, żeby agent ich nie „aktualizował"

**Impact**: 11 pakietów ma nowsze majory. `CLAUDE.md` przypina TS/Vite/React Router/MediatR, ale nie ESLint 9, `eslint-plugin-react-hooks` 5, `mermaid` 11 ani `@types/node` 24 — agent poproszony o „odświeżenie zależności" może podbić je razem z łamiącymi zmianami (flat-config ESLint 10, nowe reguły react-hooks 7).
**Severity**: low
**Effort**: quick (< 5 min)
**Fix**: dopisz do sekcji „Wersje przypięte" w `CLAUDE.md`: `ESLint 9 (+ eslint-plugin-react-hooks 5) · mermaid 11 · @types/node 24 (zgodnie z Node 22 z .nvmrc)`. Aktualizacje w obrębie majora: `npm update` + commit `package-lock.json`.

### Addressed in upcoming lessons (Category B)

### Konfiguracja wdrożenia (tylko lokalny FolderProfile, brak Dockerfile/PaaS)

**Lesson**: [Sprint Zero z Agentem: infrastruktura, walking skeleton i pierwszy deploy (M1L5)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l5)
**What you'll do there**: zautomatyzujesz wdrożenie i pierwszy deploy z pipeline'u — dziś CI buduje i testuje, ale nie publikuje.

### Dopracowanie plików instrukcji dla agenta

**Lesson**: [Agent Onboarding: Agents.md, AI Rules i feedback loops (M1L4)](https://platforma.przeprogramowani.pl/external/10xdevs-3/m1-l4)
**What you'll do there**: uporządkujesz `CLAUDE.md` / `AGENTS.md` (już bogate) i dodasz pętle feedbacku — tam trafią też reguły z Fix 2 i Fix 3, jeśli nie dopiszesz ich wcześniej.

## Summary

Health status: healthy

Klient jest w dobrej kondycji: 0 podatności (npm i NuGet), lockfile'y po obu stronach, Vitest z 15 zielonymi testami, czysty lint, prettier i `tsc -b`, a CI pokrywa lint, testy, build, type-check i skan bezpieczeństwa. Kompensacje z oceny stacku (wzorzec weryfikacji LLM i ochrona treści kursów) są już w `CLAUDE.md`; otwarte zostają dwie drobne luki po stronie klienta związane z PRD — limit czasu/stan awarii przy weryfikacji i niewyświetlanie `detail` z 500. Luki majorowe w zależnościach są w większości świadomym przypięciem.

Next step: przed pracą nad modułem Courses uruchom lokalnie `dotnet test APPS.sln` (nie wykonano w tym przebiegu), dopisz reguły z Fix 2 i 3 do `CLAUDE.md`, a Fix 1 zrealizuj razem z pierwszym wycinkiem „Do sprawdzenia"; potem agent onboarding.
