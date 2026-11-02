# tomsoft1 — AI Toolkit Workflow for App Developer

> **Jedna paczka, dwa narzędzia:** Claude Code + GitHub Copilot (VS Code / Copilot CLI).

Wersjonowany zestaw skilli i reguł wspierających AI-assisted Software Development Life Cycle (SDLC). Projekt jest przykładem **Shared AI Registry** (10xDevs M5L4, ścieżka 10xChampion).

- **Repozytorium:** https://github.com/mulatom1/ai-toolkit
- **Paczka:** `@mulatom1/tomsoft1-ai-toolkit`
- **Rejestr:** https://npm.pkg.github.com
- **Wersja:** `0.1.3`
- **Język:** TypeScript (kompilowany przez `tsc` do `dist/`; paczka zawiera gotowy JavaScript)
- **Node.js:** 20+ (zalecane 22)
- **Licencja:** MIT (plik `LICENSE` w repozytorium)

## 1. Co instaluje toolkit?

Jedno polecenie:

```powershell
npx --no-install ai-toolkit install
```

umieszcza **ten sam, źródłowy skill** w dwóch miejscach, a wspólne reguły dodaje jako zarządzany blok w plikach obu narzędzi:

```text
moj-projekt/
├── .claude/
│   └── skills/
│       └── code-review/
│           └── SKILL.md                 # Claude Code
├── .github/
│   ├── skills/
│   │   └── code-review/
│   │       └── SKILL.md                 # GitHub Copilot
│   └── copilot-instructions.md         # reguły Copilota
├── .ai-toolkit/
│   └── manifest.json                   # pliki zarządzane przez toolkit
└── CLAUDE.md                           # reguły Claude Code
```

Nie musisz samodzielnie kopiować skilla ani zakładać dwóch repozytoriów. Oba pliki `SKILL.md` powstają z **jednego źródła**: `skills/code-review/SKILL.md` znajdującego się w paczce. Dzięki temu zachowują identyczną treść.

> **Uwaga o Copilocie:** Nowe wersje Copilota w VS Code / CLI potrafią także odczytywać `.claude/skills`, dlatego w niektórych interfejsach skill `code-review` może być wykryty z obu lokalizacji. Obie kopie mają tę samą treść. Osobne `.github/skills` jest przydatne także do pracy z Copilotem w środowiskach GitHub.

## 2. Instalacja z GitHub Packages w dowolnym projekcie

### Jednorazowe logowanie na komputerze

Rejestr npm GitHuba wymaga uwierzytelnienia do pobierania pakietów. Przygotuj GitHub **PAT (classic)** z uprawnieniem `read:packages` i zaloguj się:

```powershell
npm login --scope=@mulatom1 --auth-type=legacy --registry=https://npm.pkg.github.com
```

- **Username:** `mulatom1`
- **Password:** token PAT (classic)
- **Email:** e-mail przypisany do konta GitHub

Nie umieszczaj PAT w repozytorium, plikach kodu ani w commitowanej `.npmrc`.

### Instalacja do projektu

W katalogu projektu (nie w repozytorium źródłowym paczki) skonfiguruj scope:

```powershell
npm config set "@mulatom1:registry" "https://npm.pkg.github.com" --location=project
```

Powstający `.npmrc` powinien zawierać wyłącznie:

```ini
@mulatom1:registry=https://npm.pkg.github.com
```

Następnie:

```powershell
npm install @mulatom1/tomsoft1-ai-toolkit@0.1.3
npx --no-install ai-toolkit install
```

Wersja `0.1.3` musi najpierw zostać **rzeczywiście opublikowana** w GitHub Packages; przed publikacją wersji sprawdź instrukcję z sekcji 6.

Sprawdź rezultat:

```powershell
Test-Path .claude/skills/code-review/SKILL.md
Test-Path .github/skills/code-review/SKILL.md
Test-Path .github/copilot-instructions.md
Test-Path CLAUDE.md
```

Oczekiwane: cztery razy `True`.

## 3. Jak uruchomić code-review?

### Claude Code

W katalogu projektu uruchom `claude`, a w interaktywnej sesji wpisz:

```text
/code-review
```

Przykładowy prompt:

```text
Użyj skilla code-review do przeglądu zmian względem HEAD.
Oddziel blokery od sugestii i wskaż brakujące testy.
Nie modyfikuj plików.
```

### GitHub Copilot (VS Code)

1. Otwórz katalog projektu w VS Code.
2. Otwórz **Copilot Chat** i wybierz **Agent**.
3. Wpisz prośbę:

```text
Użyj skilla code-review z .github/skills/code-review/SKILL.md.
Przejrzyj aktualny git diff; nie modyfikuj kodu.
```

Zależnie od wersji VS Code/Copilota możesz także wywołać skill z menu umiejętności (`/skills`). Jeśli nie widzisz nowego skilla, przeładuj okno VS Code.

### GitHub Copilot CLI

W katalogu projektu uruchom `copilot`. W aktywnej sesji możesz sprawdzić dostępne skille przez `/skills list` i odświeżyć je przez `/skills reload` (jeśli Twoja wersja CLI obsługuje te komendy). Następnie poproś Copilota o użycie `code-review` dla bieżących zmian.

**Uwaga:** Skill jest zestawem instrukcji dla AI, a nie samodzielnym procesem CI. Nie wykonuje automatycznie review na GitHub PR, nie edytuje kodu i nie publikuje komentarzy.

## 4. Aktualizacja i deinstalacja

Aby zaktualizować paczkę i ponownie rozdystrybuować artefakty:

```powershell
npm install @mulatom1/tomsoft1-ai-toolkit@0.1.3
npx --no-install ai-toolkit install
```

Instalator:

- działa **idempotentnie** — ponowne wywołanie nie dubluje reguł;
- nie nadpisuje ręcznie zmienionych ani cudzych skilli;
- zachowuje istniejącą treść `CLAUDE.md` i `.github/copilot-instructions.md` poza blokami toolkitu;
- zapisuje sumy kontrolne zarządzanych plików w `.ai-toolkit/manifest.json`;
- obsługuje migrację instalacji z `0.1.0` (tylko Claude Code), o ile pliki toolkitu nie były ręcznie modyfikowane.

Reguły są oznaczane komentarzami:

```markdown
<!-- ai-toolkit:start -->
... zarządzane reguły ...
<!-- ai-toolkit:end -->
```

Odinstalowanie artefaktów z projektu:

```powershell
npx --no-install ai-toolkit uninstall
```

Deinstalator usuwa obie zarządzane kopie skilla i oba bloki reguł, **nie usuwając prywatnych instrukcji użytkownika**. Jeśli wykryje ręczną modyfikację zarządzanego skilla lub bloku reguł, bezpiecznie przerywa operację bez kasowania plików.

Aby następnie usunąć także zależność npm (opcjonalnie):

```powershell
npm uninstall @mulatom1/tomsoft1-ai-toolkit
```

Najpierw uruchom `ai-toolkit uninstall`, ponieważ samo `npm uninstall` usuwa paczkę, ale nie wycofuje automatycznie reguł i skilli z projektu.

## 5. Struktura repozytorium źródłowego

```text
ai-toolkit/
├── .github/workflows/publish-ai-toolkit.yml
├── bin/ai-toolkit.ts         (CLI, kompilowane do dist/bin)
├── src/manager.ts           (logika, kompilowana do dist/src)
├── skills/code-review/SKILL.md
├── rules/CLAUDE.md
├── test/manager.test.ts
├── package.json
├── tsconfig.json
└── README.md
```

Kod źródłowy jest napisany w **TypeScript**. `npm run build` (`tsc -p .`) kompiluje go do katalogu `dist/`, a skrypt `prepack` robi to automatycznie przed `npm pack` i `npm publish`. Do opublikowanej paczki trafia wyłącznie skompilowany JavaScript, więc konsument nie potrzebuje TypeScriptu.

Właścicielem źródła prawdy jest repozytorium paczki. Pliki w projektach-konsumentach instaluje i usuwa polecenie CLI.

## 6. Testy i publikacja nowej wersji `0.1.3`

W katalogu repozytorium źródłowego `mulatom1/ai-toolkit`:

```powershell
npm ci
npm test
npm pack --dry-run
```

Następnie:

```powershell
git add .
git commit -m "feat: support Claude Code and GitHub Copilot"
git push origin main
```

Otwórz zakładkę [GitHub Actions](https://github.com/mulatom1/ai-toolkit/actions), wybierz workflow **Publish AI toolkit to GitHub Packages** i uruchom **Run workflow** na `main`. Po jego zakończeniu oczekiwany zapis w logach:

```text
+ @mulatom1/tomsoft1-ai-toolkit@0.1.3
```

Nie uruchamiaj publikacji ponownie dla tej samej wersji: GitHub Packages nie przyjmuje nadpisania już wydanego numeru. Jeżeli `0.1.3` jest już opublikowana, podnieś wersję na kolejną wolną.

Workflow ma `packages: write` i korzysta z `GITHUB_TOKEN` (nie wymaga PAT do **publikacji**). PAT classic jest używany lokalnie jedynie przy **pobieraniu** paczki.

## 7. Bezpieczeństwo i licencja

Nie commituj tokenów, nie kopiuj prywatnych instrukcji firmy bez uprawnień i testuj instalator na oddzielnym projekcie. Materiały przykładowe w paczce są demonstracyjne.

Repozytorium jest udostępniane na licencji MIT (patrz `LICENSE`).
