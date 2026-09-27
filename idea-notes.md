# Moduł „Kursy i bootcampy” — wersja MVP

## Wizja produktu

Stworzenie nowego modułu w istniejącym portalu, umożliwiającego publikowanie kursów i bootcampów.

## Główny problem, który rozwiązujemy

Wiele kursów jest zbyt statycznych i mało angażujących — zarówno w formie tekstowej, jak i wideo.

Celem nowego modułu jest stworzenie bardziej interaktywnej formy nauki poprzez wprowadzenie elementów grywalizacji. Użytkownik będzie mógł potwierdzać wykonanie zadań poprzez zdobywanie flag oraz śledzić swoje postępy na tle innych uczestników dzięki liście rankingowej.

---

## Stan istniejący

Portal **tomsoft1.pl** posiada obecnie następujące moduły:

- **Home** — ekran powitalny.
- **Apki** — moduł zawierający aplikacje dostępne w ramach tomsoft1.pl (Flashcard, Lotto).
- **Gry** — moduł zawierający gry dostępne w ramach tomsoft1.pl (AgentPLN, Invaders).
- **O mnie** — informacje o właścicielu portalu; część modułu Portal.
- **Kontakt** — dane kontaktowe oraz formularz kontaktowy; część modułu Portal.
- **Users** — lista aktywnych użytkowników portalu, dostępna wyłącznie dla administratora; część modułu Portal.
- **Rejestracja** — formularz rejestracji nowego użytkownika przez administratora, dostępny wyłącznie dla administratora; część modułu Portal.
- **Zaloguj / Wyloguj** — funkcjonalność logowania i wylogowania użytkownika; część modułu Portal.

---

## Podstawowe funkcje nowego modułu „Kursy” — MVP

### Menu główne modułu

Moduł **Kursy** będzie dostępny z głównego menu portalu również dla użytkownika niezalogowanego. Layout modułu istniejącego portalu.

Na stronie modułu dostępne będą:

- **Hangar z trofeami** — dostępny dla zalogowanego użytkownika:
  - formularz aktywacji zdobytej flagi,
  - formularz „Do sprawdzenia”, umożliwiający przesłanie wyniku zadania do weryfikacji przez AI oraz zdobycie nowej flagi,
  - lista wszystkich flag z możliwością rozróżnienia flag:
    - zdobytych,
    - niezdobytych.

- **Lista zasłużonych** — dostępna dla zalogowanego użytkownika:
  - ranking użytkowników na podstawie zdobytych flag i/lub zaliczonych zadań.

- **Dashboard** — dostępny dla zalogowanego użytkownika:
  - prezentacja 5 kluczowych wskaźników KPI na dany dzień:
    - liczba użytkowników,
    - liczba flag możliwych do zdobycia,
    - liczba flag zdobytych przez całą grupę,
    - średnia liczba zdobytych flag na użytkownika,
    - procent zdobytych flag w całej grupie.

### Lista dostępnych kursów

Lista kursów będzie prezentowana w formie kafelków.

- Lista kursów będzie widoczna również dla użytkownika niezalogowanego.
- Dla zalogowanego użytkownika widoczne będą kursy, których data publikacji jest równa lub wcześniejsza od aktualnej daty.
- Dane dotyczące kursów będą pobierane z pliku `courses.json`.

Każdy wpis w pliku `courses.json` będzie zawierał m.in.:

- tytuł kursu,
- krótki opis,
- listę tagów,
- link do grafiki kursu.

Po wybraniu kafelka zalogowany użytkownik będzie mógł przejść do strony szczegółowej danego kursu.

Materiał kursu będzie:

- pobierany z backendu w formacie Markdown,
- konwertowany po stronie klienta z Markdown do HTML,
- wyświetlany użytkownikowi jako sformatowana treść HTML.

### Publikacja kursów

W wersji MVP publikacja nowych materiałów będzie realizowana poprzez FTP.

Publikacja kursu będzie polegała na przesłaniu:

- pliku Markdown z treścią kursu,
- grafiki PNG,
- do odpowiedniego katalogu na serwerze.

### Flagi

Flagi dostępne do zdobycia będą zapisane w bazie danych.

System będzie umożliwiał aktywowanie flag przez użytkownika po wykonaniu odpowiednich zadań.

### Funkcjonalność „Do sprawdzenia”

Dla zadań wymagających automatycznej weryfikacji przez AI w bazie danych będą przechowywane:

- numer zadania,
- system prompt wykorzystywany do weryfikacji danego zadania.

Na tej podstawie system będzie mógł ocenić odpowiedź użytkownika i — w przypadku poprawnego wykonania zadania — przyznać odpowiednią flagę.

---

## Funkcje wyłączone z zakresu MVP

Poniższe funkcjonalności nie wchodzą w zakres pierwszej wersji produktu i mogą zostać rozważone w kolejnych etapach rozwoju:

- import i eksport kursów w sposób inny niż poprzez FTP,
- funkcje społecznościowe, np. współdzielenie wiadomości lub wyników z innymi użytkownikami,
- dedykowana aplikacja mobilna — w pierwszej fazie projekt będzie dostępny wyłącznie jako aplikacja webowa.

---

## Kryteria sukcesu MVP

### Akceptacja modułu

Co najmniej **70% dostępnych flag i zadań zostanie zaliczonych przez użytkowników bootcampu**.

### Szybkość weryfikacji

Weryfikacja odpowiedzi użytkownika przez system będzie trwała nie dłużej niż **5 sekund**.

### Pozytywne opinie użytkowników

Uzyskanie średniej oceny na poziomie co najmniej **4/5** w ankietach satysfakcji przeprowadzanych wśród pierwszych użytkowników.

### Niski wskaźnik błędów

Mniej niż **1% sesji użytkowników** będzie kończyło się niespodziewanym błędem krytycznym.