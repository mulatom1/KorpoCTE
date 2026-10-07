---
project: "Kursy i bootcampy"
version: 1
status: draft
created: 2026-09-29
updated: 2026-10-07
prd_version: 1
main_goal: speed
top_blocker: time
milestone_id: courses-bootcamp-mvp
milestone_seq: 1
milestone_status: open
---

# Roadmap: Kursy i bootcampy

> Wyprowadzona z `context/foundation/prd.md` (v1) + automatycznie zbadanego stanu kodu.
> Edycja w miejscu; archiwizuj, gdy zostanie zastąpiona.
> Wycinki poniżej są w kolejności zależności. Tabela „At a glance” jest indeksem.

## Milestone

**M-1: MVP modułu Kursy** — Status: open

- **Intent:** Uczestnik bootcampu przechodzi w portalu pełną ścieżkę: otwiera kurs, wykonuje zadanie, zdobywa flagę (oceną AI lub aktywacją), widzi ją w hangarze i swoją pozycję na liście zasłużonych — bez regresji istniejących modułów.
- **Source materials:** `context/foundation/prd.md` (v1)
- **Done when:** każdy element F-NN i S-NN poniżej ma status `done`, a pełna ścieżka z kryterium sukcesu „Primary” działa end-to-end na co najmniej jednym kursie.
- **Scope anchors:** US-01; FR-001 – FR-014.

## Vision recap

Kursy i bootcampy są statyczne: nic nie potwierdza, że uczestnik faktycznie wykonał zadanie, i nic nie pokazuje, jak wypada na tle grupy. Moduł Kursy dodaje do portalu kursy z zadaniami, w których flaga dowodzi wykonania, a nie przeczytania — odpowiedź uczestnika ocenia system względem kryteriów zadania. Zdobyte flagi zasilają ranking i wskaźniki postępu małej, zamkniętej grupy znajomych.

## North star

**S-01: Uczestnik wysyła wynik zadania i przy poprawnej odpowiedzi dostaje flagę** — to realizacja jedynej nowej reguły domenowej z PRD i największe ryzyko techniczne, więc przy celu „szybkość dowiezienia” idzie tak wcześnie, jak pozwalają zależności.

> Gwiazda przewodnia (north star) oznacza tu najmniejszy wycinek end-to-end, którego działanie dowodzi, że produkt ma sens — wszystko inne ma znaczenie tylko wtedy, gdy ten wycinek działa.

## At a glance

| ID   | Change ID                     | Outcome (user can …)                                                                 | Prerequisites | PRD refs                          | Status   |
| ---- | ----------------------------- | ------------------------------------------------------------------------------------ | ------------- | --------------------------------- | -------- |
| F-01 | courses-module-skeleton       | (foundation) pusty moduł Kursy jest zarejestrowany w hoście i w testach              | —             | FR-012, FR-013, FR-014            | in-progress |
| S-01 | answer-verification-earns-flag | uczestnik wysyła wynik zadania w hangarze i przy poprawnej odpowiedzi dostaje flagę | F-01          | US-01, FR-005, FR-006, FR-011     | proposed |
| S-02 | hangar-flag-list              | uczestnik widzi w hangarze wszystkie flagi z podziałem na zdobyte i niezdobyte       | S-01          | US-01, FR-007                     | proposed |
| S-03 | public-course-tiles           | odwiedzający wchodzi do Kursów z menu i widzi kafelki kursów                         | F-01          | FR-001, FR-010, FR-013, FR-014    | proposed |
| S-04 | course-content-reading        | zalogowany użytkownik czyta sformatowaną treść opublikowanego kursu                  | S-03          | FR-002, FR-003, FR-010, FR-012    | proposed |
| S-05 | leaderboard                   | zalogowany użytkownik widzi listę zasłużonych według liczby zdobytych flag           | S-01          | US-01, FR-008                     | proposed |
| S-06 | flag-activation               | uczestnik aktywuje zdobytą flagę przez formularz aktywacji w hangarze                | S-01          | FR-004                            | proposed |
| S-07 | group-progress-dashboard      | zalogowany użytkownik widzi dashboard z czterema wskaźnikami postępu grupy           | S-01          | US-01, FR-009                     | proposed |

## Streams

Pomoc w nawigacji — grupuje elementy o wspólnym łańcuchu zależności. Kanoniczna kolejność nadal wynika z grafu zależności poniżej; ta tabela to proponowana kolejność czytania równoległych ścieżek.

| Stream | Theme           | Chain                                  | Note                                                                                   |
| ------ | --------------- | -------------------------------------- | -------------------------------------------------------------------------------------- |
| A      | Zdobywanie flag | `F-01` → `S-01` → `S-02` → `S-06`      | Ścieżka konieczna z pierwszym dowodem działania na czele — rdzeń celu „szybkość”.      |
| B      | Postępy grupy   | `S-05` → `S-07`                        | Dołącza do strumienia A w `S-01`; ranking domyka kryterium sukcesu „Primary”.          |
| C      | Treść kursów    | `S-03` → `S-04`                        | Dołącza do strumienia A w `F-01`; może iść równolegle z `S-01`.                        |

## Baseline

Stan kodu na `2026-09-29` (zbadany automatycznie + potwierdzony przez użytkownika).
Fundamenty poniżej zakładają, że te warstwy istnieją, i NIE budują ich od nowa.

- **Frontend:** present — SPA z trasami deklaratywnymi i wspólnym layoutem; pakiety renderowania Markdown już zainstalowane; layout ma zaczątek pod ścieżkę `/courses`, brak stron Kursów.
- **Backend / API:** present — modularny monolit z modułami Portal, Lotto, Flashcards; modułu Kursy brak.
- **Data:** present — ORM z migracjami i docelowa baza SQL; brak modelu danych Kursów.
- **Auth:** present — logowanie tokenem z rozróżnieniem administratora, nagłówek aplikacyjny, ochrona tras po stronie klienta.
- **Deploy / infra:** present — zautomatyzowane wdrożenie na hosting docelowy, potwierdzone na środowisku testowym i produkcyjnym (wg użytkownika).
- **Observability:** present — logowanie strukturalne do plików i logowanie żądań; brak zewnętrznego śledzenia błędów.
- **Integracja AI:** present — istniejąca usługa wywołań modelu językowego, używana przez Flashcards.

## Foundations

### F-01: Szkielet modułu Kursy

- **Outcome:** (foundation) pusty moduł Kursy jest zarejestrowany w hoście i w projekcie testów, bez żadnej funkcji i bez zmian w istniejących modułach.
- **Change ID:** courses-module-skeleton
- **PRD refs:** FR-012, FR-013, FR-014 (weryfikacja braku regresji), Constraints & Compatibility
- **Unlocks:** S-01 i S-03 — pozwala uruchomić je równolegle, bez konfliktu dwóch zmian zakładających ten sam moduł; ścieżka weryfikacji: testy endpointów Kursów działają od pierwszego wycinka.
- **Prerequisites:** —
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Najmniejsza możliwa zmiana w hoście, zrobiona raz; jeśli rejestracja nowego modułu zepsuje start aplikacji, wyjdzie to przed jakąkolwiek logiką domenową.
- **Status:** in-progress

## Slices

### S-01: Zweryfikowana odpowiedź daje flagę

- **Outcome:** uczestnik może wkleić wynik zadania w formularzu „Do sprawdzenia” w hangarze i w czasie poniżej 5 s dostać werdykt; przy poprawnej odpowiedzi flaga zostaje przyznana raz na zawsze. Awaria oceny jest komunikowana jako awaria z możliwością ponowienia, a flaga już zdobyta blokuje ponowną ocenę.
- **Change ID:** answer-verification-earns-flag
- **PRD refs:** US-01, FR-005, FR-006, FR-011
- **Prerequisites:** F-01; co najmniej jedno zadanie z kryteriami i flagą zdefiniowane przez administratora poza aplikacją
- **Parallel with:** S-03, S-04
- **Blockers:** —
- **Unknowns:**
  - Jak uczestnik wskazuje w formularzu, którego zadania dotyczy odpowiedź (wybór z listy czy numer zadania)? — Owner: właściciel portalu. Block: no.
  - Treść kryteriów pierwszego prawdziwego zadania do testu end-to-end. — Owner: właściciel portalu. Block: no.
- **Risk:** Ocena modelem jest niedeterministyczna i podatna na wstrzykiwanie poleceń w treści odpowiedzi; stąd pierwsza pozycja — jeśli tu się nie uda, reszta modułu nie ma sensu.
- **Status:** proposed

### S-02: Hangar z listą flag

- **Outcome:** uczestnik może zobaczyć w hangarze wszystkie flagi z rozróżnieniem zdobytych i niezdobytych; flaga przyznana w S-01 od razu zmienia status na zdobytą.
- **Change ID:** hangar-flag-list
- **PRD refs:** US-01, FR-007
- **Prerequisites:** S-01
- **Parallel with:** S-03, S-04, S-05, S-06, S-07
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Domyka kryteria akceptacji US-01 („przed zdobyciem flaga widnieje jako niezdobyta”); mały zakres, więc tuż po pierwszym dowodzie działania.
- **Status:** proposed

### S-03: Publiczne kafelki kursów w menu

- **Outcome:** odwiedzający bez konta może wejść do modułu Kursy z menu głównego i zobaczyć kafelki kursów (tytuł, krótki opis, tagi, grafika), a administrator publikuje kurs przez wgranie plików poza aplikacją; istniejące pozycje menu, Apki, Gry i ekrany administratora działają bez zmian.
- **Change ID:** public-course-tiles
- **PRD refs:** FR-001, FR-010, FR-013, FR-014
- **Prerequisites:** F-01
- **Parallel with:** S-01, S-02, S-05, S-06, S-07
- **Blockers:** —
- **Unknowns:**
  - Czy publiczna lista kafelków pokazuje także kursy z przyszłą datą publikacji, czy stosuje ten sam filtr co FR-003? — Owner: właściciel portalu. Block: no.
- **Risk:** Zmiana menu i layoutu dotyka każdej strony portalu; ryzyko regresji FR-013/FR-014 trzeba sprawdzić w tym wycinku, a błędny wpis kursu nie może wywalić listy.
- **Status:** proposed

### S-04: Czytanie treści kursu

- **Outcome:** zalogowany użytkownik może otworzyć stronę szczegółową opublikowanego kursu (data publikacji ≤ dziś) i przeczytać jego sformatowaną treść; kurs niepublikowany jest niewidoczny, a niezalogowany trafia do logowania i wraca do kursu.
- **Change ID:** course-content-reading
- **PRD refs:** FR-002, FR-003, FR-010, FR-012
- **Prerequisites:** S-03
- **Parallel with:** S-01, S-02, S-05, S-06, S-07
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Granica prywatności — treść kursu nie może wyciec do niezalogowanych ani przez publiczne pliki statyczne; logowanie musi działać jak wcześniej (FR-012).
- **Status:** proposed

### S-05: Lista zasłużonych

- **Outcome:** zalogowany użytkownik może zobaczyć ranking uczestników według liczby zdobytych flag — tej samej liczby, którą widzi w hangarze.
- **Change ID:** leaderboard
- **PRD refs:** US-01, FR-008
- **Prerequisites:** S-01
- **Parallel with:** S-02, S-03, S-04, S-06, S-07
- **Blockers:** —
- **Unknowns:**
  - Czy ranking obejmuje wszystkich użytkowników portalu, czy tylko uczestników bootcampu (np. bez administratora)? — Owner: właściciel portalu. Block: no.
- **Risk:** Ostatni brakujący element pełnej ścieżki z kryterium sukcesu „Primary”; poprawność zależy od gwarancji „flaga liczona raz” z S-01.
- **Status:** proposed

### S-06: Aktywacja flagi

- **Outcome:** uczestnik może aktywować zdobytą flagę przez formularz aktywacji w hangarze; flaga aktywowana drugi raz nie jest liczona ponownie.
- **Change ID:** flag-activation
- **PRD refs:** FR-004
- **Prerequisites:** S-01
- **Parallel with:** S-02, S-03, S-04, S-05, S-07
- **Blockers:** —
- **Unknowns:**
  - Skąd uczestnik bierze kod flagi do aktywacji i czy każda flaga ma kod, czy tylko flagi niezwiązane z zadaniem ocenianym przez AI? — Owner: właściciel portalu. Block: no.
- **Risk:** Druga droga zdobycia flagi korzysta z tej samej gwarancji jednokrotności co S-01; osobno, żeby nie rozdmuchać pierwszego dowodu działania.
- **Status:** proposed

### S-07: Dashboard postępu grupy

- **Outcome:** zalogowany użytkownik może zobaczyć dashboard z czterema wskaźnikami na dany dzień: liczba użytkowników, liczba flag możliwych do zdobycia, liczba flag zdobytych przez grupę, procent zdobytych flag w grupie.
- **Change ID:** group-progress-dashboard
- **PRD refs:** US-01, FR-009
- **Prerequisites:** S-01
- **Parallel with:** S-02, S-03, S-04, S-05, S-06
- **Blockers:** —
- **Unknowns:**
  - Kogo liczy wskaźnik „liczba użytkowników” — wszystkie konta portalu czy uczestników bootcampu? (ta sama decyzja co w S-05) — Owner: właściciel portalu. Block: no.
- **Risk:** Nie jest częścią pełnej ścieżki z kryterium „Primary”, więc na końcu; przy braku czasu to pierwszy kandydat do przesunięcia.
- **Status:** proposed

## Backlog Handoff

| Roadmap ID | Change ID                      | Suggested issue title                                          | Ready for `/10x-plan` | Notes                                  |
| ---------- | ------------------------------ | -------------------------------------------------------------- | --------------------- | -------------------------------------- |
| F-01       | courses-module-skeleton        | Kursy: szkielet modułu zarejestrowany w hoście i testach       | yes                   | Run `/10x-plan courses-module-skeleton` |
| S-01       | answer-verification-earns-flag | Kursy: ocena odpowiedzi przez AI przyznaje flagę               | no                    | Czeka na F-01                          |
| S-02       | hangar-flag-list               | Kursy: hangar z listą flag zdobytych i niezdobytych            | no                    | Czeka na S-01                          |
| S-03       | public-course-tiles            | Kursy: publiczne kafelki kursów i pozycja w menu               | no                    | Czeka na F-01                          |
| S-04       | course-content-reading         | Kursy: strona szczegółowa z treścią opublikowanego kursu       | no                    | Czeka na S-03                          |
| S-05       | leaderboard                    | Kursy: lista zasłużonych według liczby flag                    | no                    | Czeka na S-01                          |
| S-06       | flag-activation                | Kursy: aktywacja flagi w hangarze                              | no                    | Czeka na S-01                          |
| S-07       | group-progress-dashboard       | Kursy: dashboard czterech wskaźników postępu grupy             | no                    | Czeka na S-01                          |

## Open Roadmap Questions

1. **W jakim oknie czasowym mierzone jest kryterium ≥70% zaliczonych flag?** (PRD OQ-3) — Owner: właściciel portalu. Block: roadmap-wide (nie blokuje planowania, blokuje ocenę sukcesu).
2. **Czy moduł potrzebuje zarządzania treścią wewnątrz aplikacji?** (PRD OQ-1) — decyzja MVP: nie; do rewizji, jeśli poprawka kryteriów w trakcie bootcampu okaże się potrzebna. Owner: właściciel portalu. Block: —.
3. **Czy lista kursów ma pozostać publiczna?** (PRD OQ-2) — decyzja: tak; do rewizji przed publikacją kursu, którego tytuł ma pozostać zamknięty. Owner: właściciel portalu. Block: S-03 (tylko przy zmianie decyzji).
4. **Kształt architektury portalu** (PRD OQ-4) — rozstrzygnięte poza PRD: modularny monolit (opis w instrukcjach repozytorium i `infrastructure.md`); do przeniesienia do PRD przy jego następnej wersji. Owner: właściciel portalu. Block: —.
5. **Kogo liczą ranking i wskaźnik „liczba użytkowników”** — wszystkie konta czy wyłącznie uczestników bootcampu? Owner: właściciel portalu. Block: S-05, S-07 (nieblokujące — do rozstrzygnięcia przy planowaniu).
6. **Jak zbierana jest ankieta satysfakcji (kryterium „Secondary” 4/5)?** PRD nie deklaruje jej w portalu. Owner: właściciel portalu. Block: —.

## Parked

- **Import i eksport kursów innym kanałem niż przyjęty kanał publikacji** — Why parked: PRD §Non-Goals.
- **Funkcje społecznościowe (komentarze, wiadomości, współdzielenie wyników)** — Why parked: PRD §Non-Goals.
- **Edycja i usuwanie kursów, flag i zadań wewnątrz aplikacji** — Why parked: PRD §Non-Goals, OQ-1.
- **Samodzielna rejestracja uczestnika** — Why parked: PRD §Non-Goals.
- **Dedykowana aplikacja mobilna** — Why parked: PRD §Non-Goals.
- **Ankieta satysfakcji w portalu** — Why parked: brak FR; przy celu „szybkość” zbierana poza portalem, dopóki PRD nie zdecyduje inaczej.

## Milestone History

## Done
