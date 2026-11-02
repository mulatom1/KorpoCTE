---
title: "Słownik domeny — moduł Kursy"
created: 2026-10-15
type: glossary
source: context/domain/01-domain-distillation.md
---

# Słownik domeny — moduł Kursy (dla agentów)

| Termin | Co znaczy | Nazwa w kodzie | Jak tego NIE nazywać |
| --- | --- | --- | --- |
| Kurs | Materiał z datą publikacji; tożsamość w bazie, tytuł i treść w pliku Markdown | `Course` (`Slug`, `PublishDate`) | „lekcja”, „moduł” (moduł = projekt .NET) |
| Dzień | Sposób, w jaki uczestnik widzi kurs w bootcampie („Dzień 1”) | brak — tytuł z frontmattera | nie twórz bytu `Day` |
| Data publikacji | Moment UTC (z godziną), od którego kurs i jego flagi są widoczne; granica włączna (`<=`) | `PublishDate` | „data” bez strefy, „data kalendarzowa” |
| Kafelek | Publiczna wizytówka kursu: tytuł, opis, tagi, grafika | `CourseTileDto` | „karta kursu”, „podgląd treści” |
| Treść kursu | Markdown po frontmatterze, tylko dla zalogowanych | `CourseContent`, `Content` | „HTML kursu”, „strona statyczna” |
| Flaga | Trofeum do zdobycia, należy do kursu | `Flag` | „odznaka”, „punkt”; nie myl z `IsAdmin` („flaga admina”) |
| Kod flagi | Sekret `SAD{…}`, który uczestnik wylicza w zadaniu i którym aktywuje flagę | `Flag.Code` | „hasło”, „token”, „identyfikator flagi” (ID to `FlagId`) |
| Zadanie | Flaga z niepustymi kryteriami — zdobywalna przez weryfikację AI | `Flag` z `Criteria`, `HangarTaskDto` | `Task` (koliduje z C#), „ćwiczenie”; nie twórz osobnej encji bez decyzji |
| Kryteria | Tajny opis poprawnej odpowiedzi dla modelu | `Flag.Criteria` | „system prompt”, „klucz odpowiedzi”; nigdy nie zwracaj w API |
| Odpowiedź | Tekst uczestnika wysłany do oceny; nie jest zapisywana | `Answer` | „rozwiązanie zapisane”, „submission” |
| Weryfikacja | Ocena odpowiedzi modelem; wydaje kod, **niczego nie zapisuje** | `VerifyAnswer` | „przyznanie flagi”, „zdobycie” |
| Werdykt | Wynik weryfikacji: `Correct`, `Incorrect`, `Unavailable`, `AlreadyOwned` | `Contracts.Statuses` | „błąd” dla `Unavailable` — to awaria, nie ocena |
| Awaria weryfikacji | Timeout/błąd modelu; ponawialna, nieliczona | `Unavailable` | „odpowiedź niepoprawna”, „odrzucenie” |
| Aktywacja | Wpisanie kodu w hangarze — jedyny moment zdobycia flagi | `ActivateFlag` (`Activated`, `AlreadyOwned`, `Invalid`) | „rejestracja” (to zakładanie konta w Portalu), „weryfikacja” |
| Zdobycie flagi | Trwały fakt: użytkownik posiada flagę od `EarnedAt`; najwyżej raz | `UserFlag` | „próba”, „wynik”, „aktywność” |
| Flaga ukryta | Flaga w hangarze bez opisu w treści dnia; podpowiedzią jest tytuł | brak — powstaje emergentnie | nie dodawaj pola `IsHidden` bez decyzji |
| Hangar (z trofeami) | Widok wszystkich flag (zdobyte/niezdobyte) + formularz aktywacji | `HangarFlags`, `HangarPage` | miejsce weryfikacji odpowiedzi (to terminal) |
| Terminal TOMO-AI-001 | Strona „Do sprawdzenia” — wysyłka odpowiedzi do weryfikacji | `TomoAiTerminalPage`, endpoint `hangar-tasks` + `verify-answer` | „TOMSO-AI-001”, „Hangar → Do sprawdzenia” |
| Lista zasłużonych | Ranking wg liczby zdobytych flag z opublikowanych kursów; admini wliczani, osoby z 0 flag pominięte | `Leaderboard` | „ranking zadań”, „tablica wyników punktowych” |
| Postęp grupy | 4 wskaźniki na moment `AsOf`: użytkownicy z ≥1 flagą, flagi możliwe, różne flagi zdobyte, % | `GroupProgress` | „statystyki użytkownika”, „średnia na osobę” (usunięta z FR-009) |
| Uczestnik | Osoba w bootcampie; w kodzie każde konto | `UserId` (z JWT) | nie twórz `Participant` bez decyzji; nie bierz `UserId` z body |
| Grupa | Wszyscy użytkownicy z ≥1 zdobytą flagą | brak bytu | „zespół”, „klasa” |
| Pula flag | Wszystkie flagi opublikowanych kursów | brak bytu (zapytanie po `PublishDate`) | „katalog nagród” |
| Bootcamp | Cykl kursów, w którym mierzy się 70% | brak w kodzie | nie zakładaj, że istnieje wiele edycji |
| Administrator | Właściciel; publikuje kursy i flagi poza aplikacją | claim `isAdmin` | „instruktor”, „autor” (brak takich ról) |
