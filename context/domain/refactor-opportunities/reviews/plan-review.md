<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Autoryzacja — jedna polityka w DAL i rola admin na granicy web

- **Plan**: context/changes/refactor-opportunities/plan.md
- **Mode**: Deep
- **Date**: 2026-10-14
- **Verdict**: REVISE → SOUND (po poprawkach)
- **Findings**: 1 critical, 1 warning, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | WARNING |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | PASS |
| Plan Completeness | FAIL |

## Grounding

10/10 paths ✓, liczności 21/8 ✓, wzorce i reguły ast-grep ✓ (przetestowane na obecnym kodzie), brief↔plan ✓

Potwierdzone w kodzie przez weryfikację (sub-agent, eksperymenty w kopii repo poza repozytorium):
- Kolejność filtrów: `CheckSessionAttribute` zarejestrowany raz (`FilterConfig.cs:13`), bez `Order`, brak kontrolera bazowego.
- `AdminController` wołany tylko przez admina; logowanie, hasło i zmiana placówki są w `HomeController`; wszystkie wywołania AJAX to POST.
- `KasaOdczytaj` i `KasaOperacjaDodaj` mają po jednym wywołującym; `IPozyczkaLombardowaDAL` ma jedną implementację.
- `placowkaId = 0` nie oznacza „wszystkich placówek” (każde zapytanie SQL filtruje `placowkaId = X`).

## Findings

### F1 — `dotnet test` / `dotnet sln add` nie działają, gdy w grafie jest eTMsWEB

- **Severity**: ❌ CRITICAL
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1 §2–3, kryteria 1.1/1.3; Desired End State
- **Detail**: Projekt eTMsWEB importuje `Microsoft.WebApplication.targets` z `$(VSToolsPath)`. W dotnet SDK tego pliku nie ma, więc `dotnet build`/`dotnet test` kończą się błędem MSB4019. Tak samo kończy się `dotnet sln add`. VS MSBuild `-restore` buduje projekt testowy, a `vstest.console.exe` uruchamia testy na zielono.
- **Fix**: Uruchamiać testy przez `vstest.console.exe` (ścieżka przez vswhere) na `eTMsTESTS\bin\Debug\net48\eTMsTESTS.dll`. Dodać `eTMsTESTS` do `.sln` ręczną edycją pliku.
- **Decision**: FIXED

### F2 — Build kompiluje się względem mscorlib 4.8, a nie 4.5.2

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: End-State Alignment
- **Location**: Phase 1 §1 (Directory.Build.targets)
- **Detail**: `Directory.Build.targets` jest importowany (`Microsoft.Common.targets:60`) po wyliczeniu `FrameworkPathOverride` (`Microsoft.Common.CurrentVersion.targets:80-85`). W efekcie csc dostaje `mscorlib.dll` z runtime 4.8, a build zgłasza 5× MSB3270.
- **Fix**: Ustawić też `FrameworkPathOverride` na `$(TargetFrameworkRootPath).NETFramework\$(TargetFrameworkVersion)`. Sprawdzone w kopii repo: 0 ostrzeżeń, 0 błędów, mscorlib z pakietu.
- **Decision**: FIXED

### F3 — Wczesny return w fazie 3 zmienia komunikat istniejących pre-checków; role cache'owane na sesję

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 3 §1, §4; Migration Notes
- **Detail**: Bloki „Kto pyta?” i „Która placówka?” nie mają `return`. Po fazie 3 odpowiedź kończy się komunikatem „Brak uprawnień do umów placówki!”, a `code=1` zostaje bez zmian. `zalogowanyRole` jest wypełniane raz na sesję (`CheckSessionAttribute.cs:40`), więc zmiana roli admin działa dopiero po ponownym zalogowaniu.
- **Fix**: Zapisać oba punkty w planie jako znane, zaakceptowane zachowanie.
- **Decision**: FIXED

### F4 — Test charakteryzacyjny KasaOperacjaDodaj w fazie 2 wymaga setupu

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 2 §4
- **Detail**: Przy mocku Loose `PozyczkaLombardowaKasaAkcjaOdczytaj` zwraca null. Metoda kończy się wtedy komunikatem „Brak podanej AKCJI” (DAL:2747), zanim cokolwiek zapisze.
- **Fix**: Opisać w planie setup testu: kwota > 0, rozliczenia sumujące się do kwoty, mock akcji i mock kasy z `dataZmiany <= dataOperacji`.
- **Decision**: FIXED
