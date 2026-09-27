---
project: "Kursy i bootcampy"
context_type: brownfield
created: 2026-09-23
updated: 2026-09-23
product_type: web-app
target_scale:
  users: small
  qps: low
  data_volume: small
timeline_budget:
  delivery_weeks: 6
  hard_deadline: 2026-11-04
  after_hours_only: true
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  gray_areas_resolved:
    - topic: "typ kontekstu"
      decision: "brownfield — nowy moduł w działającym portalu tomsoft1.pl"
    - topic: "zachowanie, które musi przetrwać"
      decision: "logowanie JWT i sesje; layout i menu portalu; moduły Apki i Gry; admin-only dostęp do Users i Rejestracji"
    - topic: "główna persona"
      decision: "znajomi i współpracownicy — nieformalna grupa rejestrowana przez administratora"
    - topic: "insight"
      decision: "flaga dowodzi wykonania zadania, nie przeczytania materiału"
    - topic: "model uwierzytelniania"
      decision: "bez zmian — JWT, konta zakłada wyłącznie administrator"
    - topic: "role"
      decision: "bez nowych ról — wystarczy flaga IsAdmin na obiekcie User"
    - topic: "zakres publiczny"
      decision: "niezalogowany widzi wyłącznie kafelki: tytuł, opis, tagi, grafika"
    - topic: "koszt czasowy"
      decision: "~6 tygodni przyjęte świadomie zamiast zmniejszania zakresu"
    - topic: "kryterium główne"
      decision: "pełna ścieżka end-to-end na jednym kursie ORAZ ≥70% flag zaliczonych przez grupę"
    - topic: "miejsce wysyłki rozwiązania"
      decision: "hangar — formularz aktywacji flagi i formularz „Do sprawdzenia” w jednym miejscu"
    - topic: "zarządzanie zadaniami i promptami"
      decision: "bezpośrednio w bazie (SQL lub seed); bez ekranu administracyjnego"
    - topic: "edycja i usuwanie"
      decision: "poza zakresem MVP — ani kursy, ani flagi, ani zadania"
    - topic: "nieudana weryfikacja"
      decision: "nielimitowane próby; awaria techniczna komunikowana jako awaria i ponawialna; duplikat zablokowany"
    - topic: "liczba wskaźników na dashboardzie"
      decision: "cztery zamiast pięciu — średnia flag na użytkownika usunięta jako pochodna procentu"
    - topic: "reguła domenowa"
      decision: "ocena otwartej odpowiedzi względem kryteriów zadania i przyznanie flagi przy spełnieniu"
    - topic: "podstawa rankingu"
      decision: "wyłącznie liczba zdobytych flag; wariant „i/lub zaliczone zadania” odrzucony"
  frs_drafted: 14
  quality_check_status: accepted
---

# Shape notes

Źródło wejściowe: `idea-notes.md` (przeczytane w całości).

## Current System

Portal **tomsoft1.pl** — działający, wielomodułowy portal webowy prowadzony przez jednego właściciela.

**Moduły dostępne dziś:**

- **Home** — ekran powitalny.
- **Apki** — aplikacje portalu: Flashcard, Lotto.
- **Gry** — gry portalu: AgentPLN, Invaders.
- **O mnie** — informacje o właścicielu portalu; część modułu Portal.
- **Kontakt** — dane kontaktowe i formularz kontaktowy; część modułu Portal.
- **Users** — lista aktywnych użytkowników, wyłącznie dla administratora; część modułu Portal.
- **Rejestracja** — formularz rejestracji nowego użytkownika przez administratora, wyłącznie dla administratora; część modułu Portal.
- **Zaloguj / Wyloguj** — logowanie i wylogowanie; część modułu Portal.

**Stan techniczny (rzeczywistość, nie wybór):**

- Logowanie oparte o JWT.
- Dwie role: zwykły użytkownik oraz administrator.
- Konta zakłada wyłącznie administrator przez moduł Rejestracja — nie ma samodzielnej rejestracji.
- Wspólny layout i menu główne współdzielone przez wszystkie moduły.

**Kto używa portalu dziś:** zalogowani użytkownicy portalu, administrator (właściciel), odwiedzający bez konta.

## Vision & Problem Statement

Kursy i bootcampy — zarówno tekstowe, jak i wideo — są zbyt statyczne i mało angażujące. Uczestnik przechodzi przez materiał, ale nic nie potwierdza, że faktycznie wykonał zadanie, i nic nie pokazuje mu, jak wypada na tle pozostałych osób w grupie.

Nowy moduł **Kursy** dokłada do portalu interaktywną formę nauki opartą o grywalizację: uczestnik potwierdza wykonanie zadania zdobyciem flagi, a lista rankingowa pokazuje postępy na tle innych uczestników.

**Insight:** flaga dowodzi wykonania, nie przeczytania. Zadanie trzeba naprawdę wykonać w narzędziu AI, żeby zdobyć flagę — czytanie i oglądanie materiału tego nie weryfikuje.

**Delta wobec stanu obecnego:** portal zyskuje nową pozycję w menu głównym i nowy typ treści (kursy z zadaniami), ale nie zmienia sposobu, w jaki użytkownicy się logują ani jak działają pozostałe moduły.

## User & Persona

**Persona główna: uczestnik bootcampu** — znajomy lub współpracownik właściciela portalu, należący do nieformalnej, zamkniętej grupy. Konto zakłada mu administrator; sam się nie rejestruje. Sięga po moduł Kursy, gdy chce przejść materiał bootcampu (np. podstawy AI, podstawy promptowania, techniki zaawansowane) i wykonać powiązane z nim zadania we własnym narzędziu AI. Motywacja ma charakter społeczny — grupa jest mała i uczestnicy znają się nawzajem, więc ranking porównuje ich z konkretnymi osobami, nie z anonimowym tłem.

### Secondary persona

**Administrator (właściciel portalu)** — publikuje materiały kursów i zakłada konta uczestnikom.

### Odwiedzający bez konta

Widzi, że moduł Kursy istnieje, i ma podgląd listy kursów. Nie jest personą, którą MVP obsługuje — jest powierzchnią wejścia dla przyszłego uczestnika.

## Success Criteria

### Primary

- Pełna ścieżka działa end-to-end na co najmniej jednym kursie: uczestnik otwiera kurs, wykonuje zadanie, wysyła odpowiedź do weryfikacji lub aktywuje flagę, flaga pojawia się w hangarze, pozycja uczestnika aktualizuje się na liście zasłużonych.
- Co najmniej **70%** dostępnych flag i zadań zostaje zaliczonych przez uczestników bootcampu.

### Secondary

- Średnia ocena co najmniej **4/5** w ankiecie satysfakcji wśród pierwszych uczestników.

### Guardrails

- Mniej niż **1%** sesji użytkowników kończy się nieoczekiwanym błędem krytycznym.
- Logowanie JWT, menu główne oraz moduły Apki i Gry działają dokładnie tak jak przed zmianą.
- Flaga raz zdobyta nie znika i nie może zostać policzona drugi raz — bez tego ranking i KPI tracą znaczenie.
- Treść kursów ani odpowiedzi uczestników nie są dostępne dla osób niezalogowanych; publiczne pozostają wyłącznie kafelki.

## User Stories

### US-01: Uczestnik zdobywa flagę za zweryfikowane zadanie

- **Given** zalogowany uczestnik, który przeczytał treść kursu i wykonał zadanie we własnym narzędziu AI
- **When** wkleja wynik zadania w formularzu „Do sprawdzenia" w hangarze i wysyła go
- **Then** system ocenia odpowiedź; przy poprawnej odpowiedzi flaga zostaje przyznana, pojawia się w hangarze jako zdobyta, a pozycja uczestnika na liście zasłużonych oraz wskaźniki na dashboardzie odzwierciedlają nowy stan

#### Acceptance Criteria

- Odpowiedź oceniona jako niepoprawna nie zużywa żadnej puli — uczestnik może poprawić ją i wysłać ponownie bez ograniczeń liczby prób.
- Awaria weryfikacji (błąd albo brak odpowiedzi w założonym czasie) jest komunikowana uczestnikowi jako awaria, nie jako ocena negatywna, i pozwala na ponowienie. Nieudana próba techniczna nie jest liczona jako błędna odpowiedź.
- Ponowne wysłanie zadania, za które flaga została już zdobyta, jest zablokowane z informacją, że flaga jest już w posiadaniu uczestnika — weryfikacja nie jest wtedy w ogóle uruchamiana.
- Przed zdobyciem flagi widnieje ona w hangarze jako niezdobyta; po przyznaniu zmienia status na zdobytą.

## Functional Requirements

### Przeglądanie kursów

- FR-001: Odwiedzający bez konta może wejść do modułu Kursy z menu głównego portalu i zobaczyć listę kursów w formie kafelków — tytuł, krótki opis, tagi, grafika. Priority: must-have. Change: new
  > Socrates: Rozważony kontrargument: „publiczna lista wystawia nazwy materiałów bootcampu osobom spoza grupy, a skoro konta zakłada administrator, nikt postronny i tak nie wejdzie dalej". Zarzut przyjęty jako trafny. Rozstrzygnięcie: FR bez zmian — nazwy kursów pełnią rolę wizytówki modułu, a treść, zadania i ranking pozostają za logowaniem.
- FR-002: Zalogowany użytkownik może otworzyć stronę szczegółową kursu i zobaczyć jego treść sformatowaną z Markdown do HTML. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „surowy Markdown trafia do przeglądarki, więc pełna treść jest dostępna każdemu zalogowanemu" oraz „konwersja po stronie klienta to zbędna zależność wobec serwowania gotowego HTML". Rozstrzygnięcie: FR zostaje bez zmian.
- FR-003: Zalogowany użytkownik widzi wyłącznie te kursy, których data publikacji jest równa aktualnej dacie lub od niej wcześniejsza. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „publikacja przez FTP już to załatwia — wystarczy nie wgrywać pliku" oraz „sama data nie odróżnia kursu zamkniętego od jeszcze nieotwartego". Rozstrzygnięcie: FR zostaje bez zmian.

### Flagi i weryfikacja

- FR-004: Zalogowany użytkownik może aktywować zdobytą flagę przez formularz aktywacji w hangarze. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „flagę można przekazać koledze, więc ranking przestaje mierzyć wykonanie zadania" oraz „ręczna aktywacja dubluje weryfikację AI". Rozstrzygnięcie: FR zostaje bez zmian.
- FR-005: Zalogowany użytkownik może przesłać wynik zadania przez formularz „Do sprawdzenia" w hangarze. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „hangar jest daleko od treści zadania i wymusza skakanie między ekranami" oraz „jeden formularz na wszystkie zadania myli uczestnika". Rozstrzygnięcie: FR zostaje bez zmian.
- FR-006: System ocenia przesłaną odpowiedź na podstawie system promptu przypisanego do numeru zadania i przyznaje flagę, gdy odpowiedź jest poprawna. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „ocena modelem jest niedeterministyczna — ta sama odpowiedź raz przejdzie, raz nie" oraz „uczestnik może wkleić polecenie sterujące w treści odpowiedzi i wymusić werdykt pozytywny". Rozstrzygnięcie: FR zostaje bez zmian.
- FR-007: Zalogowany użytkownik może zobaczyć w hangarze listę wszystkich flag z rozróżnieniem flag zdobytych i niezdobytych. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „lista zdradza zawartość kursów, których uczestnik jeszcze nie otworzył" oraz „sam licznik zdobyte/wszystkie dałby ten sam efekt bez pełnego katalogu". Rozstrzygnięcie: FR zostaje bez zmian.

### Postępy grupy

- FR-008: Zalogowany użytkownik może zobaczyć listę zasłużonych — ranking uczestników według liczby zdobytych flag. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „ranking w kilkuosobowej grupie znajomych zniechęca osoby z końca listy” oraz „podstawa rankingu jest dwuznaczna — flagi i zadania dają dwie różne kolejności”. Rozstrzygnięcie: FR doprecyzowany — podstawą kolejności jest wyłącznie liczba zdobytych flag, ta sama liczba, którą uczestnik widzi w hangarze. Wariant „i/lub zaliczone zadania” odrzucony.
- FR-009: Zalogowany użytkownik może zobaczyć dashboard z czterema wskaźnikami na dany dzień: liczba użytkowników, liczba flag możliwych do zdobycia, liczba flag zdobytych przez całą grupę, procent zdobytych flag w całej grupie. Priority: must-have. Change: new
  > Socrates: Przyjęty kontrargument: „średnia liczba flag na użytkownika i procent zdobytych flag w grupie to ta sama informacja w dwóch postaciach". Rozstrzygnięcie: FR zmieniony — zejście z pięciu wskaźników do czterech, średnia usunięta jako pochodna procentu.

### Publikacja treści

- FR-010: Administrator może opublikować kurs, wgrywając plik Markdown z treścią oraz grafikę PNG do odpowiedniego katalogu na serwerze przez FTP. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „publikacja omija aplikację — brak walidacji pliku i historii zmian, literówka w nazwie katalogu kończy się kursem, który się nie wczytuje" oraz „przy kilku kursach ręczne wgranie wystarczy". Rozstrzygnięcie: FR zostaje bez zmian.
- FR-011: Administrator może zdefiniować pulę flag oraz numery zadań wraz z system promptami, zapisując je bezpośrednio w bazie danych. Priority: must-have. Change: new
  > Socrates: Rozważone kontrargumenty: „zmiana promptu w trakcie bootcampu wymaga SQL-a na bazie produkcyjnej" oraz „moduł nie ma żadnej operacji zarządzania treścią wewnątrz aplikacji — zostaje odczyt i zdobywanie flag". Rozstrzygnięcie: FR zostaje bez zmian. Uwaga: brak zarządzania treścią wewnątrz aplikacji trafia do Open Questions.

### Zachowane

- FR-012: Użytkownik portalu może zalogować się i wylogować dokładnie tak jak przed zmianą. Priority: must-have. Change: preserved
  > Socrates: Rozważone kontrargumenty: „to oczywistość, nie wymaganie modułu" oraz „zbyt ogólne, żeby dało się z tego zrobić test". Rozstrzygnięcie: FR zostaje bez zmian jako FR obronny.
- FR-013: Użytkownik portalu może korzystać z modułów Apki i Gry dokładnie tak jak przed zmianą. Priority: must-have. Change: preserved
  > Socrates: Rozważone kontrargumenty: „te moduły nie dzielą kodu z Kursami, więc ryzyko regresji jest pozorne" oraz „prawdziwe ryzyko siedzi we współdzielonym layoucie, routingu i bazie". Rozstrzygnięcie: FR zostaje bez zmian jako FR obronny.
- FR-014: Wyłącznie administrator ma dostęp do ekranów Users i Rejestracja. Priority: must-have. Change: preserved
  > Socrates: Rozważone kontrargumenty: „moduł Kursy w ogóle tych ekranów nie dotyka" oraz „ryzyko dotyczy sposobu sprawdzania flagi IsAdmin, nie dwóch konkretnych ekranów". Rozstrzygnięcie: FR zostaje bez zmian jako FR obronny.

## Constraints & Preserved Behavior

Wskazane wprost jako zachowanie, które nie może ulec regresji:

- **Logowanie JWT i sesje** — najszerszy promień rażenia; błąd w obsłudze tokenu lub ról wywala użytkowników ze wszystkich modułów naraz.
- **Layout i menu główne portalu** — nowa pozycja w menu i współdzielony layout dotykają każdej strony portalu.
- **Moduły Apki i Gry** (Flashcard, Lotto, AgentPLN, Invaders) — mają działać dokładnie tak jak dziś.
- **Admin-only dostęp do Users i Rejestracji** — nowe reguły dostępu dla Kursów nie mogą rozszczelnić tej granicy.

**Zgodność wsteczna i dane:**

- Schemat istniejących użytkowników pozostaje bez zmian — nowe tabele modułu dokładają się obok, obiekt User i flaga `IsAdmin` są nietknięte.
- Brak migracji danych — moduł startuje pusty. Kursy, flagi i wyniki powstają od zera po wdrożeniu, więc nie ma planu backfillu ani rollbacku danych.
- Istniejące adresy i endpointy portalu zachowują obecne zachowanie — linki działające dziś działają tak samo po wdrożeniu.

## Business Logic

**System ocenia otwartą odpowiedź uczestnika względem kryteriów przypisanych do zadania i przyznaje flagę tylko wtedy, gdy odpowiedź je spełnia.**

To jest **nowa reguła** — portal dziś nie podejmuje za użytkownika żadnej decyzji tego rodzaju. Istniejące moduły serwują treść i aplikacje; żaden nie ocenia tego, co użytkownik zrobił.

Wejściem reguły jest to, co uczestnik przysyła jako wynik wykonanego zadania, oraz kryteria poprawności zdefiniowane dla tego konkretnego zadania. Wyjściem jest rozstrzygnięcie: flaga przyznana albo nie. Uczestnik spotyka się z regułą w momencie wysłania wyniku — dostaje werdykt i, przy spełnieniu kryteriów, flagę, która natychmiast zmienia jego stan w hangarze.

Następstwem reguły, nie regułą samą w sobie, jest punktacja: zdobyte flagi przekładają się na pozycję uczestnika na liście zasłużonych oraz na wskaźniki postępu całej grupy.

## Non-Functional Requirements

- Uczestnik poznaje werdykt weryfikacji w czasie krótszym niż 5 sekund od wysłania odpowiedzi.

## Access Control

**Bez zmian w modelu uwierzytelniania — obecny model zachowany.** Moduł Kursy wpina się w istniejące logowanie JWT portalu tomsoft1.pl. Konta zakłada wyłącznie administrator przez moduł Rejestracja; samodzielnej rejestracji nie ma i MVP jej nie wprowadza.

**Bez nowych ról.** Rozróżnienie uprawnień opiera się o istniejącą flagę `IsAdmin` na obiekcie User. Moduł Kursy nie dokłada roli autora ani instruktora.

**Matryca dostępu:**

| Kto | Co widzi i może zrobić |
| --- | --- |
| Niezalogowany | Lista kursów w formie kafelków: tytuł, krótki opis, tagi, grafika. Nic poza tym. |
| Zalogowany użytkownik | Szczegóły kursu i jego treść; wysłanie odpowiedzi do weryfikacji; aktywacja zdobytej flagi; hangar z trofeami; lista zasłużonych (ranking); dashboard z 4 KPI. |
| Administrator (`IsAdmin`) | Wszystko powyżej oraz publikacja materiałów kursów, a także istniejące ekrany Users i Rejestracja. |

**Widoczność kursów w czasie:** zalogowany użytkownik widzi kursy, których data publikacji jest równa aktualnej dacie lub wcześniejsza od niej.

## Non-Goals

**Funkcjonalne:**

- **Import i eksport kursów inaczej niż przez FTP** — jeden kanał publikacji; żadnych paczek, formatów wymiany ani integracji z zewnętrznymi platformami kursowymi.
- **Funkcje społecznościowe** — ranking pokazuje pozycje, ale uczestnicy nie komentują, nie wysyłają sobie wiadomości ani nie współdzielą wyników poza listą zasłużonych.
- **Edycja i usuwanie kursów, flag i zadań wewnątrz aplikacji** — tworzenie i zmiana treści dzieją się przez FTP oraz bezpośrednio w bazie; w module zostaje odczyt i zdobywanie flag.
- **Samodzielna rejestracja uczestnika** — konta zakłada wyłącznie administrator; moduł nie dokłada formularza rejestracji ani zaproszeń.

**Niefunkcjonalne:**

- **Dedykowana aplikacja mobilna** — pierwsza faza wyłącznie jako aplikacja webowa, bez osobnego wydania na urządzenia mobilne.

## Open Questions

1. **Czy moduł potrzebuje operacji zarządzania treścią wewnątrz aplikacji?** — Decyzja MVP: nie. Kursy publikowane przez FTP, zadania i system prompty wprowadzane bezpośrednio do bazy, edycja i usuwanie poza zakresem. Konsekwencja przyjęta świadomie: jedyną operacją zapisu wykonywaną przez człowieka przez interfejs jest zdobycie flagi przez uczestnika. Do rewizji, jeśli poprawka promptu w trakcie trwającego bootcampu okaże się potrzebna. Owner: właściciel portalu.
2. **Czy lista kursów ma pozostać publiczna?** — Decyzja: tak. Zarzut, że publiczna lista wystawia nazwy materiałów osobom spoza grupy, został uznany za trafny i mimo to lista zostaje otwarta — nazwy pełnią rolę wizytówki modułu. Do rewizji przed publikacją materiału, którego tytuł ma pozostać zamknięty. Owner: właściciel portalu.
3. **W jakim oknie czasowym mierzone jest kryterium ≥70% zaliczonych flag?** — Nierozstrzygnięte. Kryterium dotyczy uczestników bootcampu, ale moment pomiaru (koniec bootcampu, konkretna data, stan po N tygodniach od publikacji) nie został ustalony. Owner: właściciel portalu.
## Timeline acknowledgment

Acknowledged on 2026-09-23: 6-week delivery requires sustained dedication; user accepted.

Deklarowana dostępność: ~10 h tygodniowo, praca po godzinach. Zakres przekracza trzytygodniowy benchmark; koszt został nazwany (weryfikacja AI, dashboard 4 KPI, pełny katalog flag w hangarze, publikacja FTP + `courses.json` + renderer Markdown) i przyjęty bez zmniejszania zakresu.

## Forward: tech-stack

Treści zebrane w trakcie sesji, które opisują sposób wykonania, a nie kształt produktu. Nie należą do PRD — zostają tu dla kolejnego kroku łańcucha:

- Portal opiera się o logowanie JWT z flagą `IsAdmin` na obiekcie User.
- Lista kursów pobierana z pliku `courses.json`; każdy wpis zawiera tytuł, krótki opis, listę tagów i link do grafiki.
- Treść kursu pobierana z backendu w formacie Markdown i konwertowana do HTML po stronie klienta.
- Publikacja materiałów przez FTP: plik Markdown z treścią oraz grafika PNG wgrywane do katalogu na serwerze.
- Numer zadania i system prompt do weryfikacji przechowywane w bazie danych, wprowadzane SQL-em lub seedem.


**Odpowiedniki sformułowań uogólnionych w PRD (2026-09-23):** PRD opisuje te same wymagania od strony obserwowalnej — „treść kursu w postaci sformatowanej do czytania" (FR-002), „kryteria poprawności zdefiniowane dla danego zadania" (FR-006), „dostarczając treść i grafikę poza interfejsem aplikacji" (FR-010), „poza interfejsem aplikacji" (FR-011), „istniejący mechanizm logowania portalu" i „istniejące rozróżnienie między użytkownikiem a administratorem" (Access Control Changes). Konkretne mechanizmy stojące za tymi sformułowaniami są wypisane powyżej i należą do kroku oceny stacku.
