---
project: "Kursy i bootcampy"
version: 2
status: draft
created: 2026-09-23
context_type: brownfield
product_type: web-app
target_scale:
  users: small
  qps: low
  data_volume: small
timeline_budget:
  delivery_weeks: 6
  hard_deadline: 2026-11-04
  after_hours_only: true
---

# PRD: Kursy i bootcampy

## Current System Overview

**Cel systemu:** tomsoft1.pl to wielomodułowy portal webowy prowadzony przez jednego właściciela, udostępniający jego aplikacje, gry i informacje o nim samym.

**Stack techniczny:** architektura = modularny monolit ASP.NET Core 10 (jeden host składa moduły Portal, Lotto, Flashcards, Kursy; moduły nie referencjonują się nawzajem) + React SPA serwowane z tego samego hosta, logowanie oparte o JWT; dwie role — zwykły użytkownik oraz administrator, rozróżniane flagą `IsAdmin` na obiekcie User; wspólny layout i menu główne współdzielone przez wszystkie moduły.


**Obecna baza użytkowników:** zalogowani użytkownicy portalu, administrator będący właścicielem portalu, odwiedzający bez konta. Konta zakłada wyłącznie administrator — samodzielnej rejestracji nie ma.

**Co portal robi dziś:**

- **Home** — ekran powitalny.
- **Apki** — aplikacje portalu: Flashcard, Lotto.
- **Gry** — gry portalu: AgentPLN, Invaders.
- **O mnie** — informacje o właścicielu portalu; część modułu Portal.
- **Kontakt** — dane kontaktowe i formularz kontaktowy; część modułu Portal.
- **Users** — lista aktywnych użytkowników, wyłącznie dla administratora; część modułu Portal.
- **Rejestracja** — formularz rejestracji nowego użytkownika przez administratora, wyłącznie dla administratora; część modułu Portal.
- **Zaloguj / Wyloguj** — logowanie i wylogowanie; część modułu Portal.

Żaden z istniejących modułów nie podejmuje za użytkownika decyzji domenowej — portal serwuje treść i aplikacje, ale nie ocenia tego, co użytkownik zrobił.

## Problem Statement & Motivation

Kursy i bootcampy — zarówno tekstowe, jak i wideo — są zbyt statyczne i mało angażujące. Uczestnik przechodzi przez materiał, ale nic nie potwierdza, że faktycznie wykonał zadanie, i nic nie pokazuje mu, jak wypada na tle pozostałych osób w grupie.

Luka wobec stanu obecnego: portal nie ma dziś żadnej powierzchni do publikowania kursów ani do potwierdzania wykonania zadań. Obejściem jest prowadzenie bootcampu poza portalem, co kosztuje ręczne sprawdzanie każdej odpowiedzi przez prowadzącego i brak jakiegokolwiek wglądu uczestnika we własny postęp.

**Insight, który uzasadnia tę zmianę:** flaga dowodzi wykonania, nie przeczytania. Zadanie trzeba naprawdę wykonać, żeby zdobyć flagę — czytanie i oglądanie materiału tego nie weryfikuje.

**Delta:** portal zyskuje nową pozycję w menu głównym i nowy typ treści — kursy z zadaniami i grywalizacją. Sposób logowania użytkowników oraz działanie pozostałych modułów pozostają bez zmian.

## User & Persona

**Persona główna: uczestnik bootcampu** — znajomy lub współpracownik właściciela portalu, należący do nieformalnej, zamkniętej grupy. Konto zakłada mu administrator; sam się nie rejestruje. Sięga po moduł Kursy, gdy chce przejść materiał bootcampu (np. podstawy AI, podstawy promptowania, techniki zaawansowane) i wykonać powiązane z nim zadania we własnym narzędziu AI. Motywacja ma charakter społeczny — grupa jest mała i uczestnicy znają się nawzajem, więc ranking porównuje ich z konkretnymi osobami, nie z anonimowym tłem.

Dla tej persony zmiana jest w całości nowa: dziś nie ma w portalu niczego, po co by sięgała.

### Secondary persona

**Administrator (właściciel portalu)** — dotąd zakładał konta i utrzymywał portal; po zmianie dodatkowo publikuje materiały kursów oraz definiuje zadania i pulę flag.

### Odwiedzający bez konta

Widzi, że moduł Kursy istnieje, i ma podgląd listy kursów. Nie jest personą, którą ta zmiana obsługuje — jest powierzchnią wejścia dla przyszłego uczestnika.

## Success Criteria

### Primary

- Pełna ścieżka działa end-to-end na co najmniej jednym kursie: uczestnik otwiera kurs, wykonuje zadanie, wysyła odpowiedź do weryfikacji lub aktywuje flagę, flaga pojawia się w hangarze, pozycja uczestnika aktualizuje się na liście zasłużonych.
- Co najmniej **70%** dostępnych flag i zadań zostaje zaliczonych przez uczestników bootcampu. Mierzone w trakcie trwania bootcampu wskaźnikiem „Procent zdobytych flag” na dashboardzie postępu grupy (różne flagi zdobyte przez grupę / flagi opublikowanych kursów).

### Secondary

- Średnia ocena co najmniej **4/5** w ankiecie satysfakcji wśród pierwszych uczestników. Ankieta jest przeprowadzana po zakończeniu bootcampu, poza portalem.

### Guardrails

- Mniej niż **1%** sesji użytkowników kończy się nieoczekiwanym błędem krytycznym.
- Uczestnik poznaje werdykt weryfikacji w czasie krótszym niż **5 sekund** od wysłania odpowiedzi.
- Logowanie do portalu, menu główne oraz moduły Apki i Gry działają dokładnie tak jak przed zmianą.
- Flaga raz zdobyta nie znika i nie może zostać policzona drugi raz — bez tego ranking i wskaźniki postępu tracą znaczenie.
- Treść kursów ani odpowiedzi uczestników nie są dostępne dla osób niezalogowanych; publiczne pozostają wyłącznie kafelki.

## User Stories

### US-01: Uczestnik zdobywa flagę za zweryfikowane zadanie

Przed zmianą uczestnik nie miał w portalu żadnego sposobu potwierdzenia, że wykonał zadanie — weryfikacja odbywała się poza portalem albo nie odbywała się wcale.

- **Given** zalogowany uczestnik, który przeczytał treść kursu i wykonał zadanie we własnym narzędziu AI
- **When** wkleja wynik zadania w formularzu „Do sprawdzenia" w hangarze i wysyła go
- **Then** system ocenia odpowiedź; przy poprawnej odpowiedzi flaga zostaje przyznana, pojawia się w hangarze jako zdobyta, a pozycja uczestnika na liście zasłużonych oraz wskaźniki postępu grupy odzwierciedlają nowy stan

#### Acceptance Criteria

- Odpowiedź oceniona jako niepoprawna nie zużywa żadnej puli — uczestnik może poprawić ją i wysłać ponownie bez ograniczeń liczby prób.
- Awaria weryfikacji (błąd albo brak odpowiedzi w założonym czasie) jest komunikowana uczestnikowi jako awaria, nie jako ocena negatywna, i pozwala na ponowienie. Nieudana próba techniczna nie jest liczona jako błędna odpowiedź.
- Ponowne wysłanie zadania, za które flaga została już zdobyta, jest zablokowane z informacją, że flaga jest już w posiadaniu uczestnika — weryfikacja nie jest wtedy w ogóle uruchamiana.
- Przed zdobyciem flagi widnieje ona w hangarze jako niezdobyta; po przyznaniu zmienia status na zdobytą.

## Scope of Change

### Przeglądanie kursów

- [new] FR-001: Odwiedzający bez konta może wejść do modułu Kursy z menu głównego portalu i zobaczyć listę kursów w formie kafelków — tytuł, krótki opis, tagi, grafika. Priority: must-have
  > Socrates: Rozważony kontrargument: „publiczna lista wystawia nazwy materiałów bootcampu osobom spoza grupy, a skoro konta zakłada administrator, nikt postronny i tak nie wejdzie dalej". Zarzut przyjęty jako trafny. Rozstrzygnięcie: FR bez zmian — nazwy kursów pełnią rolę wizytówki modułu, a treść, zadania i ranking pozostają za logowaniem.
- [new] FR-002: Zalogowany użytkownik może otworzyć stronę szczegółową kursu i zobaczyć jego treść w postaci sformatowanej do czytania. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „surowy Markdown trafia do przeglądarki, więc pełna treść jest dostępna każdemu zalogowanemu" oraz „konwersja po stronie klienta to zbędna zależność wobec serwowania gotowego HTML". Rozstrzygnięcie: FR zostaje bez zmian.
- [new] FR-003: Zalogowany użytkownik widzi wyłącznie te kursy, których data publikacji jest równa aktualnej dacie lub od niej wcześniejsza. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „publikacja przez FTP już to załatwia — wystarczy nie wgrywać pliku" oraz „sama data nie odróżnia kursu zamkniętego od jeszcze nieotwartego". Rozstrzygnięcie: FR zostaje bez zmian.

### Flagi i weryfikacja

- [new] FR-004: Zalogowany użytkownik może aktywować zdobytą flagę przez formularz aktywacji w hangarze. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „flagę można przekazać koledze, więc ranking przestaje mierzyć wykonanie zadania" oraz „ręczna aktywacja dubluje weryfikację AI". Rozstrzygnięcie: FR zostaje bez zmian.
- [new] FR-005: Zalogowany użytkownik może przesłać wynik zadania przez formularz „Do sprawdzenia" w hangarze. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „hangar jest daleko od treści zadania i wymusza skakanie między ekranami" oraz „jeden formularz na wszystkie zadania myli uczestnika". Rozstrzygnięcie: FR zostaje bez zmian.
- [new] FR-006: System ocenia przesłaną odpowiedź względem kryteriów poprawności zdefiniowanych dla danego zadania i przyznaje flagę, gdy odpowiedź je spełnia. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „ocena modelem jest niedeterministyczna — ta sama odpowiedź raz przejdzie, raz nie" oraz „uczestnik może wkleić polecenie sterujące w treści odpowiedzi i wymusić werdykt pozytywny". Rozstrzygnięcie: FR zostaje bez zmian.
- [new] FR-007: Zalogowany użytkownik może zobaczyć w hangarze listę wszystkich flag z rozróżnieniem flag zdobytych i niezdobytych. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „lista zdradza zawartość kursów, których uczestnik jeszcze nie otworzył" oraz „sam licznik zdobyte/wszystkie dałby ten sam efekt bez pełnego katalogu". Rozstrzygnięcie: FR zostaje bez zmian.

### Postępy grupy

- [new] FR-008: Zalogowany użytkownik może zobaczyć listę zasłużonych — ranking uczestników według liczby zdobytych flag. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „ranking w kilkuosobowej grupie znajomych zniechęca osoby z końca listy” oraz „podstawa rankingu jest dwuznaczna — flagi i zadania dają dwie różne kolejności”. Rozstrzygnięcie: FR doprecyzowany — podstawą kolejności jest wyłącznie liczba zdobytych flag, ta sama liczba, którą uczestnik widzi w hangarze. Wariant „i/lub zaliczone zadania” odrzucony.
- [new] FR-009: Zalogowany użytkownik może zobaczyć dashboard z czterema wskaźnikami na dany dzień: liczba użytkowników, liczba flag możliwych do zdobycia, liczba flag zdobytych przez całą grupę, procent zdobytych flag w całej grupie. Priority: must-have
  > Socrates: Przyjęty kontrargument: „średnia liczba flag na użytkownika i procent zdobytych flag w grupie to ta sama informacja w dwóch postaciach". Rozstrzygnięcie: FR zmieniony — zejście z pięciu wskaźników do czterech, średnia usunięta jako pochodna procentu.

### Publikacja treści

- [new] FR-010: Administrator może opublikować kurs, dostarczając jego treść i grafikę poza interfejsem aplikacji. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „publikacja omija aplikację — brak walidacji pliku i historii zmian, literówka w nazwie katalogu kończy się kursem, który się nie wczytuje" oraz „przy kilku kursach ręczne wgranie wystarczy". Rozstrzygnięcie: FR zostaje bez zmian.
- [new] FR-011: Administrator może zdefiniować pulę flag oraz zadania wraz z kryteriami ich poprawności, poza interfejsem aplikacji. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „zmiana promptu w trakcie bootcampu wymaga SQL-a na bazie produkcyjnej" oraz „moduł nie ma żadnej operacji zarządzania treścią wewnątrz aplikacji — zostaje odczyt i zdobywanie flag". Rozstrzygnięcie: FR zostaje bez zmian. Uwaga: brak zarządzania treścią wewnątrz aplikacji trafia do Open Questions.

### Zachowane

- [preserved] FR-012: Użytkownik portalu może zalogować się i wylogować dokładnie tak jak przed zmianą. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „to oczywistość, nie wymaganie modułu" oraz „zbyt ogólne, żeby dało się z tego zrobić test". Rozstrzygnięcie: FR zostaje bez zmian jako FR obronny.
- [preserved] FR-013: Użytkownik portalu może korzystać z modułów Apki i Gry dokładnie tak jak przed zmianą. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „te moduły nie dzielą kodu z Kursami, więc ryzyko regresji jest pozorne" oraz „prawdziwe ryzyko siedzi we współdzielonym layoucie, routingu i bazie". Rozstrzygnięcie: FR zostaje bez zmian jako FR obronny.
- [preserved] FR-014: Wyłącznie administrator ma dostęp do ekranów Users i Rejestracja. Priority: must-have
  > Socrates: Rozważone kontrargumenty: „moduł Kursy w ogóle tych ekranów nie dotyka" oraz „ryzyko dotyczy sposobu sprawdzania flagi IsAdmin, nie dwóch konkretnych ekranów". Rozstrzygnięcie: FR zostaje bez zmian jako FR obronny.

Nic nie jest usuwane ani modyfikowane — moduł dokłada się obok istniejących, a trzy pozycje `[preserved]` nazywają wprost zachowanie, które nie może ulec regresji.

## Constraints & Compatibility

**Zachowanie, które nie może ulec regresji:**

- **Logowanie i sesje użytkowników** — najszerszy promień rażenia; błąd w obsłudze uwierzytelniania lub uprawnień wywala użytkowników ze wszystkich modułów naraz.
- **Layout i menu główne portalu** — nowa pozycja w menu i współdzielony layout dotykają każdej strony portalu.
- **Moduły Apki i Gry** (Flashcard, Lotto, AgentPLN, Invaders) — mają działać dokładnie tak jak dziś.
- **Admin-only dostęp do Users i Rejestracji** — nowe reguły dostępu dla Kursów nie mogą rozszczelnić tej granicy.

**Zgodność wsteczna:**

- Istniejące adresy portalu zachowują obecne zachowanie — linki działające dziś działają tak samo po wdrożeniu.
- Istniejący model użytkownika oraz rozróżnienie uprawnień administratora pozostają nietknięte; dane modułu dokładają się obok.

**Migracja danych:**

- Brak migracji — moduł startuje pusty. Kursy, flagi i wyniki powstają od zera po wdrożeniu, więc nie ma planu backfillu ani planu wycofania danych.

## Business Logic Changes

Ta zmiana **dokłada nową regułę domenową**. Portal dziś nie podejmuje za użytkownika żadnej decyzji tego rodzaju — istniejące moduły serwują treść i aplikacje, ale żaden nie ocenia tego, co użytkownik zrobił.

**Nowa reguła: system ocenia otwartą odpowiedź uczestnika względem kryteriów przypisanych do zadania i przyznaje flagę tylko wtedy, gdy odpowiedź je spełnia.**

Wejściem reguły jest to, co uczestnik przysyła jako wynik wykonanego zadania, oraz kryteria poprawności zdefiniowane dla tego konkretnego zadania. Wyjściem jest rozstrzygnięcie: flaga przyznana albo nie. Uczestnik spotyka się z regułą w momencie wysłania wyniku — dostaje werdykt i, przy spełnieniu kryteriów, flagę, która natychmiast zmienia jego stan w hangarze.

Następstwem reguły, nie regułą samą w sobie, jest punktacja: zdobyte flagi przekładają się na pozycję uczestnika na liście zasłużonych oraz na wskaźniki postępu całej grupy.

## Access Control Changes

**Brak zmian w modelu kontroli dostępu — obecny model zachowany.** Moduł Kursy wpina się w istniejący mechanizm logowania portalu. Konta zakłada wyłącznie administrator; samodzielnej rejestracji nie ma i ta zmiana jej nie wprowadza.

**Brak nowych ról.** Rozróżnienie uprawnień opiera się o istniejące rozróżnienie między zwykłym użytkownikiem a administratorem. Moduł nie dokłada roli autora ani instruktora.

**Matryca dostępu do nowej powierzchni:**

| Kto | Co widzi i może zrobić |
| --- | --- |
| Niezalogowany | Lista kursów w formie kafelków: tytuł, krótki opis, tagi, grafika. Nic poza tym. |
| Zalogowany użytkownik | Szczegóły kursu i jego treść; wysłanie odpowiedzi do weryfikacji; aktywacja zdobytej flagi; hangar z trofeami; lista zasłużonych; dashboard z czterema wskaźnikami. |
| Administrator | Wszystko powyżej oraz publikacja materiałów kursów; zachowuje dotychczasowy dostęp do ekranów Users i Rejestracja. |

**Widoczność kursów w czasie:** zalogowany użytkownik widzi kursy, których data publikacji jest równa aktualnej dacie lub wcześniejsza od niej.

## Non-Goals

**Funkcjonalne:**

- **Import i eksport kursów innym kanałem niż przyjęty kanał publikacji** — jedna droga wprowadzania treści; żadnych paczek, formatów wymiany ani integracji z zewnętrznymi platformami kursowymi.
- **Funkcje społecznościowe** — ranking pokazuje pozycje, ale uczestnicy nie komentują, nie wysyłają sobie wiadomości ani nie współdzielą wyników poza listą zasłużonych.
- **Edycja i usuwanie kursów, flag i zadań wewnątrz aplikacji** — tworzenie i zmiana treści dzieją się poza interfejsem aplikacji; w module zostaje odczyt i zdobywanie flag.
- **Samodzielna rejestracja uczestnika** — konta zakłada wyłącznie administrator; moduł nie dokłada formularza rejestracji ani zaproszeń.

**Niefunkcjonalne:**

- **Dedykowana aplikacja mobilna** — pierwsza faza wyłącznie jako aplikacja webowa, bez osobnego wydania na urządzenia mobilne.

**Poza zakresem po stronie istniejącego systemu:**

- Moduły Home, O mnie, Kontakt, Apki i Gry nie są zmieniane.
- Model uwierzytelniania i sposób zakładania kont nie są zmieniane.

## Open Questions

1. **Czy moduł potrzebuje operacji zarządzania treścią wewnątrz aplikacji?** — Decyzja MVP: nie. Publikacja kursów oraz definiowanie zadań i kryteriów odbywają się poza interfejsem aplikacji, a edycja i usuwanie są poza zakresem. Konsekwencja przyjęta świadomie: jedyną operacją zapisu wykonywaną przez człowieka przez interfejs jest zdobycie flagi przez uczestnika. Do rewizji, jeśli poprawka kryteriów w trakcie trwającego bootcampu okaże się potrzebna. Owner: właściciel portalu.
2. **Czy lista kursów ma pozostać publiczna?** — Decyzja: tak. Zarzut, że publiczna lista wystawia nazwy materiałów osobom spoza grupy, został uznany za trafny i mimo to lista zostaje otwarta — nazwy pełnią rolę wizytówki modułu. Do rewizji przed publikacją materiału, którego tytuł ma pozostać zamknięty. Owner: właściciel portalu.
3. **W jakim oknie czasowym mierzone jest kryterium ≥70% zaliczonych flag?** — Decyzja (2026-10-10): w trakcie trwania bootcampu. Miarą jest wskaźnik „Procent zdobytych flag” na dashboardzie postępu grupy (S-07), który wystarcza do oceny kryterium. Owner: właściciel portalu.
4. **Jaki jest kształt architektury istniejącego portalu?** — Decyzja (2026-10-10): modularny monolit ASP.NET Core 10 z React SPA serwowanym z tego samego hosta (opis w Current System Overview oraz w instrukcjach repozytorium). Owner: właściciel portalu.
5. **Jak zbierana jest ankieta satysfakcji (kryterium „Secondary” 4/5)?** — Decyzja (2026-10-10): po zakończeniu bootcampu, poza portalem; portal nie dostaje funkcji ankiety. Owner: właściciel portalu.
