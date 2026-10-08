import { useEffect, useState } from "react";
import { Link } from "react-router";

function HomePage() {
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    document.title = "tomsoft1 workspace";
    // Trigger animations after mount
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-4xl mx-auto text-center">
        {/* Główny tytuł */}
        <h1
          className={`text-4xl sm:text-5xl md:text-6xl font-bold mb-6 text-amber-400 transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
        >
          Kod, pasja, projekty
        </h1>

        {/* Powitanie */}
        <p
          className={`text-xl sm:text-2xl text-gray-300 mb-8 leading-relaxed transition-all duration-700 ease-out delay-150 ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
        >
          Cieszę się, że tutaj zaglądasz!
        </p>

        {/* Opis workspace */}
        <div
          className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 sm:p-8 mb-8 border border-gray-700/50 transition-all duration-700 ease-out delay-300 ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
        >
          <p className="text-gray-300 text-lg leading-relaxed mb-6">
            To moja osobista przestrzeń twórcza — miejsce, gdzie kod staje się
            rzeczywistością. Znajdziesz tu praktyczne efekty mojej pracy i pasji
            do programowania, od prostych narzędzi po złożone aplikacje webowe.
          </p>

          <div className="grid sm:grid-cols-2 gap-4 text-left">
            {/* Apki i Gry */}
            <Link
              to="/apps"
              className={`bg-gray-900/50 rounded-xl p-5 border border-gray-700/30 hover:border-cyan-500/30 transition-all duration-500 group block ${
                isVisible
                  ? "opacity-100 translate-x-0"
                  : "opacity-0 -translate-x-4"
              }`}
              style={{ transitionDelay: isVisible ? "400ms" : "0ms" }}
            >
              <div className="flex items-center gap-3 mb-3">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <svg
                    className="w-5 h-5 text-cyan-400"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                    />
                  </svg>
                </div>
                <h3 className="text-white font-semibold">Aplikacje & Gry</h3>
              </div>
              <p className="text-gray-400 text-sm leading-relaxed">
                Wypróbuj praktyczne aplikacje, które ułatwią Ci codzienne
                zadania, lub zrelaksuj się przy moich grach w wolnej chwili.
              </p>
            </Link>

            {/* Kursy */}
            <Link
              to="/courses"
              className={`bg-gray-900/50 rounded-xl p-5 border border-gray-700/30 hover:border-cyan-500/30 transition-all duration-500 group block ${
                isVisible
                  ? 'opacity-100 translate-x-0'
                  : 'opacity-0 -translate-x-4'
              }`}
              style={{ transitionDelay: isVisible ? '600ms' : '0ms' }}
            >
              <div className="flex items-center gap-3 mb-3">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <svg className="w-5 h-5 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                  </svg>
                </div>
                <h3 className="text-white font-semibold">Kursy & Szkolenia</h3>
              </div>
              <p className="text-gray-400 text-sm leading-relaxed">
                Zainteresowany nauką programowania? Skontaktuj się,
                aby dowiedzieć się o dostępnych kursach i materiałach edukacyjnych.
              </p>
            </Link>


            {/* O mnie */}
            <Link
              to="/about"
              className={`bg-gray-900/50 rounded-xl p-5 border border-gray-700/30 hover:border-cyan-500/30 transition-all duration-500 group block ${
                isVisible
                  ? "opacity-100 translate-x-0"
                  : "opacity-0 translate-x-4"
              }`}
              style={{ transitionDelay: isVisible ? "500ms" : "0ms" }}
            >
              <div className="flex items-center gap-3 mb-3">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <svg
                    className="w-5 h-5 text-cyan-400"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
                    />
                  </svg>
                </div>
                <h3 className="text-white font-semibold">Poznaj mnie</h3>
              </div>
              <p className="text-gray-400 text-sm leading-relaxed">
                Dowiedz się więcej o moim doświadczeniu, stosie technologicznym
                i drodze, którą przeszedłem jako programista.
              </p>
            </Link>

            {/* Kontakt */}
            <Link
              to="/contact"
              className={`bg-gray-900/50 rounded-xl p-5 border border-gray-700/30 hover:border-cyan-500/30 transition-all duration-500 group block ${
                isVisible
                  ? "opacity-100 translate-x-0"
                  : "opacity-0 translate-x-4"
              }`}
              style={{ transitionDelay: isVisible ? "700ms" : "0ms" }}
            >
              <div className="flex items-center gap-3 mb-3">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <svg
                    className="w-5 h-5 text-cyan-400"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                    />
                  </svg>
                </div>
                <h3 className="text-white font-semibold">Współpraca</h3>
              </div>
              <p className="text-gray-400 text-sm leading-relaxed">
                Potrzebujesz konsultacji lub chcesz zlecić projekt? Napisz do
                mnie — chętnie porozmawiam o Twoich pomysłach.
              </p>
            </Link>
          </div>
        </div>

        {/* CTA Buttons */}
        <div
          className={`flex flex-col sm:flex-row gap-4 justify-center transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
          style={{ transitionDelay: isVisible ? "850ms" : "0ms" }}
        ></div>
      </div>
    </section>
  );
}

export default HomePage;
