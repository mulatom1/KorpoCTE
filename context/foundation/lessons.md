# Lessons Learned

> Append-only register of recurring rules and patterns. Re-read at start by /10x-frame, /10x-research, /10x-plan, /10x-plan-review, /10x-implement, /10x-impl-review.

## Oznaczaj daty z bazy w DTO jako UTC

- **Context**: Każdy endpoint serwera, który zwraca w DTO datę odczytaną z bazy (kolumny datetime2 przez EF Core), oraz testy tych endpointów.
- **Problem**: EF czyta datetime2 jako DateTimeKind.Unspecified, więc JSON nie ma sufiksu Z i przeglądarka traktuje czas UTC jako lokalny (przesunięcie o 1–2 h). Testy InMemory tego nie wykrywają, bo zachowują Kind z seeda.
- **Rule**: Każdą datę z bazy zwracaną w DTO API oznaczaj jako UTC (DateTime.SpecifyKind(..., DateTimeKind.Utc)) przed zbudowaniem odpowiedzi, a w teście endpointu seeduj ją z Kind = Unspecified i sprawdzaj w surowym JSON sufiks Z.
- **Applies to**: plan, implement, impl-review

## Sprawdzaj prettier lokalnie z --end-of-line auto

- **Context**: Lokalna weryfikacja formatowania klienta (src/client/app01) na Windows z core.autocrlf=true — bramka prettier w fazach dotykających frontendu.
- **Problem**: Kopia robocza ma CRLF, a prettier wymaga LF, więc `prettier --check` zgłasza pliki, które w indeksie i w CI są poprawne. To myli diagnozę, a `npm run format` przepisuje cudze pliki i rozszerza commit.
- **Rule**: Lokalnie weryfikuj formatowanie przez `npx prettier --check --end-of-line auto "src/**/*.{ts,tsx,css}"`, formatuj tylko pliki zmienione w fazie (`npx prettier --write <pliki>`) i nie uruchamiaj `npm run format` na całym `src/`.
- **Applies to**: plan, implement
