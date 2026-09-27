import { useEffect, useState } from "react";
import { Link } from "react-router";

function MyCvPage() {
  const [isVisible, setIsVisible] = useState(false);
  const birthYear = 1975;
  const age = new Date().getFullYear() - birthYear;

  useEffect(() => {
    document.title = "Moje CV | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const experience = [
    {
      period: "1999 - obecnie",
      title: "Starszy Projektant / Full Stack Developer",
      company: "PKO Bank Polski",
      description:
        "Projektowanie i rozwój systemów bankowych, aplikacji webowych i narzędzi wewnętrznych. Awans od informatyka przez specjalistę, starszego specjalistę, programistę, full stack developera do obecnej roli.",
    },
  ];

  const skills = {
    backend: [
      "C#",
      ".NET Core",
      "ASP.NET",
      "Entity Framework",
      "SQL Server",
      "MySQL",
      "PostgreSQL",
      "ElasticSearch",
      "REST API",
      "LINQ",
      "Python",
    ],
    frontend: [
      "TypeScript",
      "React",
      "Vue",
      "Angular",
      "Svelte",
      "HTML5",
      "CSS3",
      "Tailwind CSS",
      "Vite",
      "PHP",
      "Delphi",
    ],
    tools: [
      "Git",
      "Visual Studio",
      "VS Code",
      "Azure DevOps",
      "Docker",
      "Postman",
    ],
    aiml: ["GenAI", "ML", "AI Agenci", "LLM", "Prompt Engineering", "AI Code"],
    other: ["Agile/Scrum", "CI/CD", "Unit Testing", "Code Review", "Mentoring"],
  };

  const education = [
    {
      period: "1994 - 1999",
      title: "Mgr inż. — Politechnika Śląska",
      description: "Specjalizacja: Informatyka i Ekonometria",
    },
    {
      period: "2002",
      title: "MBA — Katolicki Uniwersytet Lubelski",
      description: "Master of Business Administration",
    },
    {
      period: "Ciągły rozwój",
      title: "Kursy i bootcampy",
      description: "Niezliczona ilość kursów i bootcampów...",
      link: "/about",
    },
  ];

  const hobbies = [
    { icon: "💻", name: "Coding" },
    { icon: "🎮", name: "Retro gaming" },
    { icon: "📚", name: "Nowe technologie" },
    { icon: "🎯", name: "Nauka" },
    { icon: "⚽", name: "Sport" },
    { icon: "✈️", name: "Podróże" },
  ];

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-4xl mx-auto w-full">
        {/* Nagłówek */}
        <div className="text-center mb-8">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Curriculum Vitae
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Tomasz Mularczyk ({age} lat) — Starszy Projektant / Full Stack
            Developer — Katowice/Online
          </p>
          <div
            className={`flex flex-wrap justify-center gap-4 mt-3 text-sm transition-all duration-700 ease-out delay-200 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            <a
              href="mailto:contact@tomsoft1.pl"
              className="text-cyan-400 hover:text-cyan-300 flex items-center gap-1"
            >
              <svg
                className="w-4 h-4"
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
              contact@tomsoft1.pl
            </a>
            <a
              href="tel:+48600438205"
              className="text-cyan-400 hover:text-cyan-300 flex items-center gap-1"
            >
              <svg
                className="w-4 h-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"
                />
              </svg>
              600 438 205
            </a>
          </div>
        </div>

        {/* Podsumowanie ze zdjęciem */}
        <div
          className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 mb-8 transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
          style={{ transitionDelay: isVisible ? "200ms" : "0ms" }}
        >
          <div className="flex flex-col sm:flex-row gap-6">
            {/* Zdjęcie */}
            <div className="sm:w-1/4 flex-shrink-0">
              <img
                src="images/face.png"
                alt="Tomasz Mularczyk"
                className="w-full h-auto rounded-xl object-cover border border-cyan-500/30"
              />
            </div>
            {/* Podsumowanie */}
            <div className="sm:w-3/4">
              <h2 className="text-xl font-semibold text-white mb-3 flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                  <svg
                    className="w-4 h-4 text-cyan-400"
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
                Podsumowanie zawodowe
              </h2>
              <p className="text-gray-300 leading-relaxed text-sm">
                Doświadczony programista z ponad{" "}
                <span className="text-amber-400 font-semibold">
                  25-letnim stażem
                </span>{" "}
                w branży IT. Specjalizuję się w tworzeniu aplikacji webowych
                full stack z wykorzystaniem technologii .NET i React. Przez całą
                karierę pracuję w sektorze bankowym, gdzie zdobyłem
                wszechstronne doświadczenie w projektowaniu i implementacji
                złożonych systemów informatycznych. Pasjonat programowania od
                dzieciństwa, który swoją fascynację kodem przekuł w wieloletnią,
                stabilną karierę zawodową.
              </p>
              <div className="flex flex-wrap gap-4 mt-3 print:hidden">
                <a
                  href="https://tomsoft1.pl/apps"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 text-sm flex items-center gap-1"
                >
                  <svg
                    className="w-4 h-4"
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
                  Aplikacje
                </a>
                <a
                  href="https://tomsoft1.pl/games"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 text-sm flex items-center gap-1"
                >
                  <svg
                    className="w-4 h-4"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z"
                    />
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                    />
                  </svg>
                  Gry
                </a>
                <a
                  href="https://github.com/mulatom1"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 text-sm flex items-center gap-1"
                >
                  <svg
                    className="w-4 h-4"
                    fill="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
                  </svg>
                  GitHub
                </a>
                <a
                  href="https://www.linkedin.com/in/tomasz-mularczyk-b744b5265/"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 text-sm flex items-center gap-1"
                >
                  <svg
                    className="w-4 h-4"
                    fill="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z" />
                  </svg>
                  LinkedIn
                </a>
                <a
                  href="https://www.facebook.com/profile.php?id=100087925249315"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 text-sm flex items-center gap-1"
                >
                  <svg
                    className="w-4 h-4"
                    fill="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z" />
                  </svg>
                  Facebook
                </a>
              </div>
            </div>
          </div>
        </div>

        {/* Doświadczenie zawodowe */}
        <div
          className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 mb-8 transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
          style={{ transitionDelay: isVisible ? "300ms" : "0ms" }}
        >
          <h2 className="text-xl font-semibold text-white mb-6 flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
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
                  d="M21 13.255A23.931 23.931 0 0112 15c-3.183 0-6.22-.62-9-1.745M16 6V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v2m4 6h.01M5 20h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                />
              </svg>
            </div>
            Doświadczenie zawodowe
          </h2>
          <div className="space-y-6">
            {experience.map((exp, index) => (
              <div key={index} className="border-l-2 border-cyan-500/50 pl-4">
                <div className="text-amber-400 text-sm font-medium mb-1">
                  {exp.period}
                </div>
                <h3 className="text-white font-semibold text-lg">
                  {exp.title}
                </h3>
                <div className="text-cyan-400 text-sm mb-2">{exp.company}</div>
                <p className="text-gray-400 text-sm leading-relaxed">
                  {exp.description}
                </p>
              </div>
            ))}
          </div>
          <div className="mt-6 p-4 bg-gray-900/50 rounded-xl border border-gray-700/30">
            <h4 className="text-white font-medium mb-2">Ścieżka kariery:</h4>
            <div className="flex flex-wrap gap-2">
              {[
                "Informatyk",
                "Specjalista",
                "Starszy Specjalista",
                "Programista",
                "Full Stack Developer",
                "Starszy Projektant",
              ].map((role, i) => (
                <span
                  key={i}
                  className="px-3 py-1 bg-cyan-500/10 text-cyan-400 text-xs rounded-full border border-cyan-500/30"
                >
                  {role}
                </span>
              ))}
            </div>
          </div>
        </div>

        {/* Umiejętności techniczne */}
        <div
          className={`print:break-before-page bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 mb-8 transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
          style={{ transitionDelay: isVisible ? "400ms" : "0ms" }}
        >
          <h2 className="text-xl font-semibold text-white mb-6 flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
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
                  d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"
                />
              </svg>
            </div>
            Umiejętności techniczne
          </h2>
          {/* Pierwsza linia: Backend, Frontend, AI */}
          <div className="grid sm:grid-cols-3 gap-3 mb-3">
            <div className="bg-gray-900/50 rounded-xl p-3 border border-gray-700/30">
              <h3 className="text-amber-400 font-medium mb-2 text-xs uppercase tracking-wider">
                Backend
              </h3>
              <div className="flex flex-wrap gap-1">
                {skills.backend.map((skill, i) => (
                  <span
                    key={i}
                    className="px-2 py-0.5 bg-cyan-500/10 text-cyan-300 text-xs rounded border border-cyan-500/20"
                  >
                    {skill}
                  </span>
                ))}
              </div>
            </div>
            <div className="bg-gray-900/50 rounded-xl p-3 border border-gray-700/30">
              <h3 className="text-amber-400 font-medium mb-2 text-xs uppercase tracking-wider">
                Frontend
              </h3>
              <div className="flex flex-wrap gap-1">
                {skills.frontend.map((skill, i) => (
                  <span
                    key={i}
                    className="px-2 py-0.5 bg-teal-500/10 text-teal-300 text-xs rounded border border-teal-500/20"
                  >
                    {skill}
                  </span>
                ))}
              </div>
            </div>
            <div className="bg-gray-900/50 rounded-xl p-3 border border-gray-700/30">
              <h3 className="text-amber-400 font-medium mb-2 text-xs uppercase tracking-wider">
                AI & ML
              </h3>
              <div className="flex flex-wrap gap-1">
                {skills.aiml.map((skill, i) => (
                  <span
                    key={i}
                    className="px-2 py-0.5 bg-pink-500/10 text-pink-300 text-xs rounded border border-pink-500/20"
                  >
                    {skill}
                  </span>
                ))}
              </div>
            </div>
          </div>
          {/* Druga linia: Narzędzia, Inne */}
          <div className="grid sm:grid-cols-2 gap-3">
            <div className="bg-gray-900/50 rounded-xl p-3 border border-gray-700/30">
              <h3 className="text-amber-400 font-medium mb-2 text-xs uppercase tracking-wider">
                Narzędzia
              </h3>
              <div className="flex flex-wrap gap-1">
                {skills.tools.map((skill, i) => (
                  <span
                    key={i}
                    className="px-2 py-0.5 bg-purple-500/10 text-purple-300 text-xs rounded border border-purple-500/20"
                  >
                    {skill}
                  </span>
                ))}
              </div>
            </div>
            <div className="bg-gray-900/50 rounded-xl p-3 border border-gray-700/30">
              <h3 className="text-amber-400 font-medium mb-2 text-xs uppercase tracking-wider">
                Inne
              </h3>
              <div className="flex flex-wrap gap-1">
                {skills.other.map((skill, i) => (
                  <span
                    key={i}
                    className="px-2 py-0.5 bg-orange-500/10 text-orange-300 text-xs rounded border border-orange-500/20"
                  >
                    {skill}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Statystyki */}
        <div className="grid sm:grid-cols-4 gap-4 mb-8">
          {[
            {
              label: "Lat doświadczenia",
              value: "25+",
              color: "from-amber-400 to-orange-400",
            },
            {
              label: "Projektów",
              value: "100+",
              color: "from-cyan-400 to-teal-400",
            },
            {
              label: "Technologii",
              value: "30+",
              color: "from-purple-400 to-pink-400",
            },
            {
              label: "Lat w jednej firmie",
              value: "25+",
              color: "from-green-400 to-emerald-400",
            },
          ].map((stat, i) => (
            <div
              key={i}
              className={`bg-gray-800/50 backdrop-blur-sm rounded-xl p-4 border border-gray-700/50 text-center transition-all duration-500 ${
                isVisible
                  ? "opacity-100 translate-y-0"
                  : "opacity-0 translate-y-8"
              }`}
              style={{
                transitionDelay: isVisible ? `${500 + i * 100}ms` : "0ms",
              }}
            >
              <div
                className={`text-2xl font-bold bg-gradient-to-r ${stat.color} bg-clip-text text-transparent mb-1`}
              >
                {stat.value}
              </div>
              <div className="text-gray-400 text-xs">{stat.label}</div>
            </div>
          ))}
        </div>

        {/* Edukacja i Hobby */}
        <div className="grid sm:grid-cols-2 gap-8">
          {/* Edukacja */}
          <div
            className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-x-0"
                : "opacity-0 -translate-x-8"
            }`}
            style={{ transitionDelay: isVisible ? "900ms" : "0ms" }}
          >
            <h2 className="text-xl font-semibold text-white mb-4 flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
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
                    d="M12 14l9-5-9-5-9 5 9 5z"
                  />
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z"
                  />
                </svg>
              </div>
              Edukacja
            </h2>
            {education.map((edu, index) => (
              <div key={index} className="mb-3 last:mb-0">
                <div className="text-amber-400 text-sm font-medium">
                  {edu.period}
                </div>
                <h3 className="text-white font-medium">{edu.title}</h3>
                <p className="text-gray-400 text-sm">
                  {edu.description}
                  {edu.link && (
                    <Link
                      to={edu.link}
                      className="text-cyan-400 hover:text-cyan-300 ml-1 print:hidden"
                    >
                      certyfikaty →
                    </Link>
                  )}
                </p>
              </div>
            ))}
          </div>

          {/* Hobby */}
          <div
            className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-x-0"
                : "opacity-0 translate-x-8"
            }`}
            style={{ transitionDelay: isVisible ? "1000ms" : "0ms" }}
          >
            <h2 className="text-xl font-semibold text-white mb-4 flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
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
                    d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"
                  />
                </svg>
              </div>
              Zainteresowania
            </h2>
            <div className="grid grid-cols-2 gap-3">
              {hobbies.map((hobby, i) => (
                <div
                  key={i}
                  className="flex items-center gap-3 p-3 bg-gray-900/50 rounded-lg border border-gray-700/30"
                >
                  <span className="text-2xl">{hobby.icon}</span>
                  <span className="text-gray-300 text-sm">{hobby.name}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Kontakt */}
        <div
          className={`print:hidden mt-8 bg-gradient-to-r from-cyan-500/10 to-teal-500/10 rounded-2xl p-6 border border-cyan-500/30 text-center transition-all duration-700 ease-out ${
            isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"
          }`}
          style={{ transitionDelay: isVisible ? "1100ms" : "0ms" }}
        >
          <h2 className="text-xl font-semibold text-white mb-2">
            Zainteresowany współpracą?
          </h2>
          <p className="text-gray-400 mb-4">
            Skontaktuj się ze mną przez formularz kontaktowy lub media
            społecznościowe.
          </p>
          <a
            href="/contact"
            className="inline-flex items-center gap-2 px-6 py-3 bg-gradient-to-r from-cyan-500 to-teal-500 text-white font-semibold rounded-xl hover:from-cyan-600 hover:to-teal-600 transition-all duration-200 shadow-lg shadow-cyan-500/25"
          >
            <svg
              className="w-5 h-5"
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
            Skontaktuj się
          </a>
        </div>
      </div>
    </section>
  );
}

export default MyCvPage;
