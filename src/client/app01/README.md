# tomsoft1 workspace - Frontend

Osobista strona portfolio i workspace developera - aplikacja SPA zbudowana w React 19 z TypeScript.

## Stos technologiczny

- **React 19** - biblioteka UI
- **TypeScript 5.9** - typowanie statyczne
- **Vite 7** - bundler i dev server
- **Tailwind CSS 4** - utility-first CSS framework
- **React Router 7** - routing

## Struktura projektu

```
src/
├── assets/              # Zasoby statyczne (obrazy, ikony)
├── components/          # Komponenty wielokrotnego użytku
│   ├── layout/          # Layout aplikacji (nawigacja, tło)
│   └── ImageModal.tsx   # Modal do podglądu obrazów
├── pages/               # Strony aplikacji
│   ├── home/            # Strona główna
│   ├── apps/            # Strona aplikacji
│   ├── games/           # Strona gier
│   ├── about/           # Strona "O mnie"
│   └── contact/         # Strona kontaktowa
├── services/            # Warstwa komunikacji z API
│   ├── api-service.ts   # Klient HTTP
│   └── contracts/       # Interfejsy TypeScript
├── index.css            # Style globalne + Tailwind
└── main.tsx             # Punkt wejścia aplikacji
```

## Routing

| Ścieżka       | Strona           |
|---------------|------------------|
| `/`           | Strona główna    |
| `/apps`       | Aplikacje        |
| `/games`      | Gry              |
| `/about`      | O mnie           |
| `/contact`    | Kontakt          |

## Komendy

```bash
# Instalacja zależności
npm install

# Uruchomienie serwera deweloperskiego
npm run dev

# Uruchomienie z konfiguracją produkcyjną
npm run prod

# Build dla środowiska deweloperskiego
npm run build

# Build dla produkcji
npm run build:prod

# Linting
npm run lint

# Podgląd builda
npm run preview
```

## Konfiguracja środowiska

Pliki `.env`:
- `.env.dev` - konfiguracja deweloperska
- `.env.prod` - konfiguracja produkcyjna

Zmienne środowiskowe:
- `VITE_BASE_URL` - bazowy URL aplikacji

## Funkcjonalności

- Responsywny layout z nawigacją desktop/mobile
- Animowane tło ze spadającymi snippetami kodu
- Ciemny motyw z kolorystyką cyan/teal
- Formularz kontaktowy
- Galeria certyfikatów z podglądem modalnym
- Dane pobierane z plików JSON (`/public/data/`)