import { useEffect, useState } from 'react';
import { Link } from 'react-router';

function LottoPage() {
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    document.title = 'Lotto | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const menuItems = [
    {
      title: 'Wyniki losowań',
      description: 'Sprawdź najnowsze wyniki losowań Lotto, Lotto Plus i innych gier. Historia losowań.',
      path: '/lotto/draws',
      icon: (
        <svg className="w-6 h-6 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
        </svg>
      ),
    },
    {
      title: 'Moje kupony',
      description: 'Zarządzaj swoimi kuponami Lotto. Dodawaj nowe kupony, usuwaj stare, generuj wg systemu...',
      path: '/lotto/tickets',
      icon: (
        <svg className="w-6 h-6 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z" />
        </svg>
      ),
    },
    {
      title: 'Sprawdź wygrane',
      description: 'Automatyczne sprawdzanie Twoich kuponów z oficjalnymi wynikami losowań. Nie przegap żadnej wygranej! Zasymuluj wyniki Twoich kuponów w czasie...',
      path: '/lotto/winning-tickets',
      icon: (
        <svg className="w-6 h-6 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      ),
    },
    {
      title: 'Statystyki grup',
      description: 'Analiza najczesciej wystepujacych par, trojek, czworek i innych grup liczb. Sprawdz, ktore kombinacje pojawiaja sie najczesciej!',
      path: '/lotto/draws-numbers-stats',
      icon: (
        <svg className="w-6 h-6 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 3.055A9.001 9.001 0 1020.945 13H11V3.055z" />
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20.488 9H15V3.512A9.025 9.025 0 0120.488 9z" />
        </svg>
      ),
    },
  ];

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-5xl mx-auto w-full">
        {/* Header z obrazkiem */}
        <div className="flex flex-col lg:flex-row items-center gap-8 mb-12">
          {/* Obrazek Lotto */}
          <div
            className={`w-full lg:w-1/2 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 -translate-x-8'
            }`}
          >
            <div className="relative rounded-2xl overflow-hidden shadow-2xl shadow-cyan-500/20 border border-gray-700/50">
              <img
                src="/images/Lotto.png"
                alt="System wspomagający graczy Lotto"
                className="w-full h-auto object-cover"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-gray-900/60 to-transparent" />
            </div>
          </div>

          {/* Tekst powitalny */}
          <div
            className={`w-full lg:w-1/2 text-center lg:text-left transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-8'
            }`}
          >
            <h1 className="text-4xl sm:text-5xl font-bold mb-4 text-amber-400">
              Lotto Ticket Manager
            </h1>
            <p className="text-xl text-gray-300 mb-4">
              Twój inteligentny asystent do gry w systemie Lotto
            </p>
            <p className="text-gray-400 leading-relaxed">
              Witaj w systemie wspomagającym graczy Lotto! Nasza aplikacja pomoże Ci
              śledzić wyniki losowań, zarządzać kuponami i automatycznie sprawdzać wygrane.
              Koniec z ręcznym porównywaniem liczb — teraz wszystko masz w jednym miejscu.
              Analizuj statystyki, zapisuj swoje szczęśliwe liczby i nigdy nie przegap
              żadnego losowania!
            </p>
          </div>
        </div>

        {/* Opis funkcjonalności */}
        <div
          className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 sm:p-8 mb-10 border border-gray-700/50 transition-all duration-700 ease-out delay-300 ${
            isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
          }`}
        >
          <h2 className="text-2xl font-semibold text-white mb-4 text-center">
            Co oferuje nasz system?
          </h2>
          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4 text-gray-300">
            <div className="flex items-start gap-3">
              <div className="w-8 h-8 rounded-full bg-cyan-500/20 flex items-center justify-center flex-shrink-0 mt-1">
                <span className="text-cyan-400 text-sm font-bold">1</span>
              </div>
              <p className="text-sm">Aktualne wyniki wszystkich losowań Lotto i Lotto Plus</p>
            </div>
            <div className="flex items-start gap-3">
              <div className="w-8 h-8 rounded-full bg-cyan-500/20 flex items-center justify-center flex-shrink-0 mt-1">
                <span className="text-cyan-400 text-sm font-bold">2</span>
              </div>
              <p className="text-sm">Przechowywanie i zarządzanie Twoimi kuponami</p>
            </div>
            <div className="flex items-start gap-3">
              <div className="w-8 h-8 rounded-full bg-cyan-500/20 flex items-center justify-center flex-shrink-0 mt-1">
                <span className="text-cyan-400 text-sm font-bold">3</span>
              </div>
              <p className="text-sm">Automatyczne sprawdzanie wygranych po każdym losowaniu</p>
            </div>
          </div>
        </div>

        {/* Menu nawigacyjne */}
        <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {menuItems.map((item, index) => (
            <Link
              key={item.path}
              to={item.path}
              className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 hover:border-cyan-500/50 hover:bg-gray-800/70 transition-all duration-500 group block ${
                isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
              }`}
              style={{ transitionDelay: isVisible ? `${450 + index * 100}ms` : '0ms' }}
            >
              <div className="flex items-center gap-4 mb-4">
                <div className="w-12 h-12 rounded-xl bg-cyan-500/20 flex items-center justify-center group-hover:scale-110 group-hover:bg-cyan-500/30 transition-all duration-300">
                  {item.icon}
                </div>
                <h3 className="text-white font-semibold text-lg group-hover:text-cyan-400 transition-colors">
                  {item.title}
                </h3>
              </div>
              <p className="text-gray-400 text-sm leading-relaxed">
                {item.description}
              </p>
              <div className="mt-4 flex items-center text-cyan-400 text-sm font-medium group-hover:translate-x-2 transition-transform">
                Przejdź
                <svg className="w-4 h-4 ml-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </div>
            </Link>
          ))}
        </div>

        {/* Informacja na dole */}
        <div
          className={`text-center mt-10 transition-all duration-700 ease-out ${
            isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
          }`}
          style={{ transitionDelay: isVisible ? '800ms' : '0ms' }}
        >
          <p className="text-gray-500 text-sm">
            Graj odpowiedzialnie. Hazard może uzależniać.
          </p>
        </div>
      </div>
    </section>
  );
}

export default LottoPage;
