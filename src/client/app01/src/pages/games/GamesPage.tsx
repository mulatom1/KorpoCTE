import { useEffect, useState } from "react";

interface Game {
  id: number;
  name: string;
  description: string;
  image: string;
  url: string | null;
}

function GryPage() {
  const [games, setGames] = useState<Game[]>([]);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    document.title = "Gry | tomsoft1 workspace";

    fetch("/data/games.json?today=" + new Date().toISOString().split("T")[0])
      .then((res) => res.json())
      .then((data) => setGames(data))
      .catch((err) => console.error("Błąd ładowania gier:", err));

    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-6xl mx-auto w-full">
        {/* Nagłówek strony */}
        <div className="text-center mb-12">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Gry
          </h1>
          <p
            className={`text-gray-400 text-lg max-w-2xl mx-auto transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Już od najmłodszych lat programowałem na poczciwych 8-bitowcach. Te
            rekonstrukcje gierek zawsze przypominają stare dobre czasy... każdy
            piksel, każda linijka kodu budzi mnie do życia i przenosi w świat, w
            którym ograniczenia sprzętowe były źródłem kreatywności, nie
            bariery. Wciąż pamiętam, jak miesiącami optymalizowałem fragmenty
            kodu, by zmieścić grę w zaledwie kilku kilobajtach pamięci, a
            dźwięki generowane przez układ SID czy AY-3-8910 sprawiały, że
            zwykła melodia stawała się prawdziwą symfonią. Dziś, sięgając po
            emulator czy kartę FPGA, znów czuję dreszcz emocji, gdy pierwsze
            logo wyświetla się na ekranie – to hołd dla tamtych czasów, gdy
            każdy bajt miał znaczenie, a radość płynęła prosto z pasji do
            programowania.
          </p>
        </div>

        {/* Lista gier */}
        <div className="flex flex-wrap justify-center gap-6">
          {games.map((game, index) => (
            <a
              key={game.id}
              href={game.url || "#"}
              onClick={(e) => !game.url && e.preventDefault()}
              className={`w-full md:w-80 bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 hover:border-cyan-500/30 transition-all duration-500 group block ${game.url ? "cursor-pointer hover:bg-gray-800/70" : "cursor-default"} ${
                isVisible
                  ? "opacity-100 translate-y-0"
                  : "opacity-0 translate-y-8"
              }`}
              style={{
                transitionDelay: isVisible ? `${300 + index * 100}ms` : "0ms",
              }}
            >
              <div className="w-full h-40 rounded-xl overflow-hidden mb-4 bg-gray-900/50">
                <img
                  src={`/images/${game.image}`}
                  alt={game.name}
                  className="w-full h-full object-contain group-hover:scale-105 transition-transform duration-300"
                />
              </div>
              <h3 className="text-white font-semibold text-lg mb-2 text-center">
                {game.name}
              </h3>
              <p className="text-gray-400 text-sm text-center">
                {game.description}
              </p>
            </a>
          ))}
        </div>

        {/* Info o wkrótce */}
        <div
          className={`text-center mt-12 transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
          style={{
            transitionDelay: isVisible
              ? `${300 + games.length * 100 + 100}ms`
              : "0ms",
          }}
        >
          <p className="text-gray-500 text-sm">
            Więcej gier w przygotowaniu...
          </p>
        </div>
      </div>
    </section>
  );
}

export default GryPage;
