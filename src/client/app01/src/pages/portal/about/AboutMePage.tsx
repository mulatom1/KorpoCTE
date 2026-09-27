import { useEffect, useState } from 'react';
import ImageModal from '../../../components/ImageModal';

interface Certificate {
  name?: string;
  image?: string | null;
}

interface Skill {
  name: string;
  level: number;
}

function OMniePage() {
  const [selectedCertificate, setSelectedCertificate] = useState<Certificate | null>(null);
  const [certificates, setCertificates] = useState<Certificate[]>([]);
  const [skills, setSkills] = useState<Skill[]>([]);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    document.title = 'O mnie | tomsoft1 workspace';

    fetch('/data/certificates.json?today=' + new Date().toISOString().split('T')[0])
      .then(res => res.json())
      .then((data: Certificate[]) => setCertificates(data))
      .catch(err => console.error('Błąd ładowania certyfikatów:', err));

    fetch('/data/skills.json')
      .then(res => res.json())
      .then(data => setSkills(data))
      .catch(err => console.error('Błąd ładowania umiejętności:', err));

    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-4xl mx-auto w-full">
        {/* Nagłówek strony */}
        <div className="text-center mb-12">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            O mnie
          </h1>
          <p
            className={`text-gray-400 text-lg italic transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            "Życie z pasji - to najlepsza droga do sukcesu!"
          </p>
        </div>

        {/* Główna sekcja */}
        <div className="grid md:grid-cols-3 gap-8 mb-12 items-stretch">
          {/* Zdjęcie / Avatar placeholder */}
          <div
            className={`md:col-span-1 flex justify-center transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 -translate-x-8'
            }`}
            style={{ transitionDelay: isVisible ? '300ms' : '0ms' }}
          >
            <img
              src="images/face.png"
              alt="Tomasz Mularczyk"
              className="w-full h-full min-h-48 rounded-2xl object-cover border border-cyan-500/30"
            />
          </div>

          {/* Bio */}
          <div
            className={`md:col-span-2 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-8'
            }`}
            style={{ transitionDelay: isVisible ? '400ms' : '0ms' }}
          >
            <div className="bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50">
              <h2 className="text-xl font-semibold text-white mb-4">Tomasz Mularczyk</h2>
              <p className="text-gray-300 leading-relaxed mb-4">
                Programuję od najmłodszych lat. Już w podstawówce pisałem pierwsze małe,
                ale fajne programiki i gry na stare, poczciwe 8-bitowe maszynki - ZX Spectrum, Atari, Commodore.
                Potem przyszedł czas zabawy z komputerami osobistymi i nagle moje hobby stało się moją pracą.
                Czy jestem dobry? Nie wiem, ale tym żyję.
              </p>
              <p className="text-gray-400 leading-relaxed">
                Swoją karierę zawodową rozpocząłem w 1999 roku jako informatyk w największym polskim banku.
                Z biegiem lat awansowałem, obejmując kolejno stanowiska: specjalisty, starszego specjalisty,
                programisty, full stack developera, aż do obecnej roli starszego projektanta. Jeszcze nigdy nie musiałem zmienić pracy! 😊
              </p>
            </div>
          </div>
        </div>

        {/* Doświadczenie */}
        <div className="grid sm:grid-cols-3 gap-4 mb-8">
          {[
            { label: 'Lat doświadczenia', value: '25+', icon: 'calendar' },
            { label: 'Zrealizowanych projektów', value: '100+', icon: 'folder' },
            { label: 'Technologii', value: '30+', icon: 'code' },
          ].map((stat, i) => (
            <div
              key={i}
              className={`bg-gray-800/50 backdrop-blur-sm rounded-xl p-5 border border-gray-700/50 text-center transition-all duration-500 ${
                isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
              }`}
              style={{ transitionDelay: isVisible ? `${500 + i * 100}ms` : '0ms' }}
            >
              <div className="text-3xl font-bold bg-gradient-to-r from-amber-400 to-orange-400 bg-clip-text text-transparent mb-1">
                {stat.value}
              </div>
              <div className="text-gray-400 text-sm">{stat.label}</div>
            </div>
          ))}
        </div>

        {/* Umiejętności */}
        <div
          className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 mb-8 transition-all duration-700 ease-out ${
            isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
          }`}
          style={{ transitionDelay: isVisible ? '800ms' : '0ms' }}
        >
          <h2 className="text-xl font-semibold text-white mb-6">Procentowy udział skillsów/technologii jakie stosuję w dniu pracy:</h2>
          <div className="space-y-4">
            {skills.map((skill) => (
              <div key={skill.name}>
                <div className="flex justify-between mb-1">
                  <span className="text-gray-300 text-sm font-medium">{skill.name}</span>
                  <span className="text-gray-500 text-sm">{skill.level}%</span>
                </div>
                <div className="w-full h-2 bg-gray-700 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-cyan-500 to-teal-500 rounded-full transition-all duration-500"
                    style={{ width: `${skill.level}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Certyfikaty */}
        <div
          className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 transition-all duration-700 ease-out ${
            isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
          }`}
          style={{ transitionDelay: isVisible ? '900ms' : '0ms' }}
        >
          <h2 className="text-xl font-semibold text-white mb-6">Moje certyfikaty</h2>
          <div className="grid sm:grid-cols-2 md:grid-cols-3 gap-4">
            {certificates.map((cert, index) => (
              <div
                key={index}
                className={`rounded-xl p-4 ${cert.name ? 'bg-gray-700/30 border border-gray-600/50 hover:border-amber-500/50 transition-colors' : ''} ${cert.image ? 'cursor-pointer' : ''}`}
                onClick={() => cert.image && setSelectedCertificate(cert)}
              >
                {cert.image ? (
                  <img
                    src={cert.image}
                    alt={cert.name || ''}
                    className="w-full h-auto rounded-lg mb-3"
                  />
                ) : (
                  <div className="w-full aspect-[4/3] rounded-lg mb-3" />
                )}
                {cert.name && <p className="text-gray-300 text-sm text-center">{cert.name}</p>}
              </div>
            ))}
          </div>
        </div>
      </div>

      <ImageModal
        isOpen={selectedCertificate !== null}
        imageUrl={selectedCertificate?.image ?? ''}
        title={selectedCertificate?.name ?? ''}
        onClose={() => setSelectedCertificate(null)}
      />
    </section>
  );
}

export default OMniePage;
