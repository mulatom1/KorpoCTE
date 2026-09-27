import { useEffect, useState } from "react";

interface App {
  id: number;
  name: string;
  description: string;
  image: string;
  tags: string[];
  url: string | null;
}

function ApkiPage() {
  const [apps, setApps] = useState<App[]>([]);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    document.title = "Aplikacje | tomsoft1 workspace";

    fetch("/data/apps.json?today=" + new Date().toISOString().split("T")[0])
      .then((res) => res.json())
      .then((data) => setApps(data))
      .catch((err) => console.error("Błąd ładowania aplikacji:", err));

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
            Aplikacje
          </h1>
          <p
            className={`text-gray-400 text-lg max-w-2xl mx-auto transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Praktyczne narzędzia i aplikacje webowe stworzone do rozwiązywania
            niektórych problemów.
          </p>
        </div>

        {/* Lista aplikacji */}
        <div className="flex flex-wrap justify-center gap-6">
          {apps.map((app, index) => (
            <a
              key={app.id}
              href={app.url || "#"}
              onClick={(e) => !app.url && e.preventDefault()}
              className={`w-full md:w-80 bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 hover:border-cyan-500/30 transition-all duration-500 group block ${app.url ? "cursor-pointer hover:bg-gray-800/70" : "cursor-default"} ${
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
                  src={`/images/${app.image}`}
                  alt={app.name}
                  className="w-full h-full object-contain group-hover:scale-105 transition-transform duration-300"
                />
              </div>
              <h3 className="text-white font-semibold text-lg mb-2 text-center">
                {app.name}
              </h3>
              <p className="text-gray-400 text-sm mb-4 text-center">
                {app.description}
              </p>
              <div className="flex items-center justify-center gap-2 text-xs text-gray-500">
                {app.tags?.map((tag, index) => (
                  <span
                    key={index}
                    className="px-2 py-1 bg-gray-700/50 rounded"
                  >
                    {tag}
                  </span>
                ))}
              </div>
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
              ? `${300 + apps.length * 100 + 100}ms`
              : "0ms",
          }}
        >
          <p className="text-gray-500 text-sm">Więcej aplikacji wkrótce...</p>
        </div>
      </div>
    </section>
  );
}

export default ApkiPage;
