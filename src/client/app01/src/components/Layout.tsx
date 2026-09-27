import { useState, useMemo, useEffect } from "react";
import { Outlet, NavLink, useLocation, useNavigate } from "react-router";
import { getIsAdminFromToken } from "../utils/jwt";
import { AUTH_CHANGED_EVENT, clearAuth, isAuthenticated } from "../utils/auth";
import backgroundImage from "../assets/background.png";

// Przykładowe snippety kodu dla animacji tła
const codeSnippets = [
  // C#
  `public class App
{
    static void Main()
    {
        Console.WriteLine("Hello");
    }
}`,
  `async Task<T> GetAsync<T>()
{
    return await _http
        .GetFromJsonAsync<T>();
}`,
  `[HttpGet]
public IActionResult Get()
{
    return Ok(data);
}`,
  // HTML
  `<div class="container">
  <header>
    <nav>Menu</nav>
  </header>
</div>`,
  `<form method="post">
  <input type="text" />
  <button>Submit</button>
</form>`,
  // CSS
  `.card {
  display: flex;
  padding: 1rem;
  border-radius: 8px;
}`,
  `@keyframes fade {
  from { opacity: 0; }
  to { opacity: 1; }
}`,
  // JavaScript
  `const fetchData = async () => {
  const res = await fetch(url);
  return res.json();
}`,
  `array.filter(x => x.active)
     .map(x => x.name)
     .join(', ')`,
  // T-SQL
  `SELECT u.Name, COUNT(*)
FROM Users u
JOIN Orders o ON u.Id = o.UserId
GROUP BY u.Name`,
  `CREATE PROCEDURE GetUser
    @Id INT
AS
BEGIN
    SELECT * FROM Users
    WHERE Id = @Id
END`,
  // TypeScript
  `interface User {
  id: number;
  name: string;
  email?: string;
}`,
];

// Kolory dla różnych bloków (jaśniejsze, błyszczące)
const blockColors = [
  "text-emerald-400/30",
  "text-cyan-400/30",
  "text-blue-400/30",
  "text-purple-400/30",
  "text-pink-400/30",
  "text-amber-400/30",
];

// Kolory poświaty dla bloków (jaśniejsze, błyszczące)
const glowColors = [
  "rgba(52, 211, 153, 0.3)", // emerald
  "rgba(34, 211, 238, 0.3)", // cyan
  "rgba(96, 165, 250, 0.3)", // blue
  "rgba(192, 132, 252, 0.3)", // purple
  "rgba(244, 114, 182, 0.3)", // pink
  "rgba(251, 191, 36, 0.3)", // amber
];

// Typ dla spadających bloków kodu
interface FallingBlock {
  id: number;
  code: string;
  left: number;
  animationDuration: number;
  delay: number;
  fontSize: number;
  colorClass: string;
  glowColor: string;
}

// Typ dla spadających kulek lotto
interface FallingLottoBall {
  id: number;
  number: number;
  left: number;
  animationDuration: number;
  delay: number;
  size: number;
  colorClass: string;
  bgColor: string;
  glowColor: string;
}

// Kolory dla kulek lotto (bardziej przezroczyste)
const lottoBallColors = [
  {
    bg: "bg-red-500/30",
    text: "text-red-300/60",
    glow: "rgba(239, 68, 68, 0.2)",
  },
  {
    bg: "bg-blue-500/30",
    text: "text-blue-300/60",
    glow: "rgba(59, 130, 246, 0.2)",
  },
  {
    bg: "bg-green-500/30",
    text: "text-green-300/60",
    glow: "rgba(34, 197, 94, 0.2)",
  },
  {
    bg: "bg-yellow-400/30",
    text: "text-yellow-300/60",
    glow: "rgba(250, 204, 21, 0.2)",
  },
  {
    bg: "bg-purple-500/30",
    text: "text-purple-300/60",
    glow: "rgba(168, 85, 247, 0.2)",
  },
  {
    bg: "bg-pink-500/30",
    text: "text-pink-300/60",
    glow: "rgba(236, 72, 153, 0.2)",
  },
  {
    bg: "bg-orange-500/30",
    text: "text-orange-300/60",
    glow: "rgba(249, 115, 22, 0.2)",
  },
  {
    bg: "bg-cyan-400/30",
    text: "text-cyan-300/60",
    glow: "rgba(34, 211, 238, 0.2)",
  },
];

// Komponent pojedynczego spadającego bloku kodu
function FallingCodeBlock({ block }: { block: FallingBlock }) {
  return (
    <pre
      className={`absolute whitespace-pre font-mono pointer-events-none select-none animate-fall ${block.colorClass}`}
      style={{
        left: `${block.left}%`,
        animationDuration: `${block.animationDuration}s`,
        animationDelay: `${block.delay}s`,
        fontSize: `${block.fontSize}px`,
        textShadow: `0 0 10px ${block.glowColor}, 0 0 20px ${block.glowColor}`,
      }}
    >
      {block.code}
    </pre>
  );
}

// Komponent pojedynczej spadającej kulki lotto
function FallingLottoBallComponent({ ball }: { ball: FallingLottoBall }) {
  return (
    <div
      className={`absolute rounded-full pointer-events-none select-none animate-fall flex items-center justify-center font-bold ${ball.bgColor} ${ball.colorClass}`}
      style={{
        left: `${ball.left}%`,
        animationDuration: `${ball.animationDuration}s`,
        animationDelay: `${ball.delay}s`,
        width: `${ball.size}px`,
        height: `${ball.size}px`,
        fontSize: `${ball.size * 0.4}px`,
        boxShadow: `0 0 10px ${ball.glowColor}, 0 0 20px ${ball.glowColor}, inset 0 -2px 6px rgba(0,0,0,0.15), inset 0 2px 6px rgba(255,255,255,0.1)`,
      }}
    >
      {ball.number}
    </div>
  );
}

// Komponent tła z animacją kodów
function AnimatedCodeBackground() {
  const blocks = useMemo<FallingBlock[]>(() => {
    return Array.from({ length: 35 }, (_, i) => ({
      id: i,
      code: codeSnippets[i % codeSnippets.length],
      left: Math.random() * 90,
      animationDuration: 50 + Math.random() * 40,
      delay: Math.random() * 15,
      fontSize: 10 + Math.random() * 4,
      colorClass: blockColors[i % blockColors.length],
      glowColor: glowColors[i % glowColors.length],
    }));
  }, []);

  return (
    <div className="fixed inset-0 overflow-hidden pointer-events-none z-0">
      {blocks.map((block) => (
        <FallingCodeBlock key={block.id} block={block} />
      ))}
    </div>
  );
}

// Komponent tła z animacją kulek lotto
function AnimatedLottoBackground() {
  const balls = useMemo<FallingLottoBall[]>(() => {
    return Array.from({ length: 50 }, (_, i) => {
      const colorIndex = Math.floor(Math.random() * lottoBallColors.length);
      const color = lottoBallColors[colorIndex];
      return {
        id: i,
        number: (i % 49) + 1, // Numery 1-49
        left: Math.random() * 95,
        animationDuration: 25 + Math.random() * 35,
        delay: Math.random() * 20,
        size: 30 + Math.random() * 30,
        colorClass: color.text,
        bgColor: color.bg,
        glowColor: color.glow,
      };
    });
  }, []);

  return (
    <div className="fixed inset-0 overflow-hidden pointer-events-none z-0">
      {balls.map((ball) => (
        <FallingLottoBallComponent key={ball.id} ball={ball} />
      ))}
    </div>
  );
}

// Komponent tła - wybiera odpowiednią animację
function AnimatedBackground({ isLotto }: { isLotto: boolean }) {
  return isLotto ? <AnimatedLottoBackground /> : <AnimatedCodeBackground />;
}

// Elementy menu
const menuItems = [
  { label: "Home", to: "/" },
  { label: "Apki", to: "/apps" },
  { label: "Gry", to: "/games" },
  { label: "O mnie", to: "/about" },
  { label: "Kontakt", to: "/contact" },
];

// Komponent nawigacji
function Navigation() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [isVisible, setIsVisible] = useState(false);
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [isAdmin, setIsAdmin] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    const timer = setTimeout(() => setIsVisible(true), 100);

    // Sprawdź czy użytkownik jest zalogowany
    const checkAuth = () => {
      if (isAuthenticated()) {
        setUserEmail(localStorage.getItem("userEmail"));
        setIsAdmin(getIsAdminFromToken());
      } else {
        // Brak sesji lub token wygasł – czyścimy resztki i chowamy menu użytkownika.
        // O ewentualne przekierowanie z chronionej podstrony dba RequireAuth.
        if (localStorage.getItem("token")) clearAuth();
        setUserEmail(null);
        setIsAdmin(false);
      }
    };

    checkAuth();

    // Nasłuchuj na zmiany stanu logowania (także z innej karty)
    window.addEventListener("storage", checkAuth);
    window.addEventListener(AUTH_CHANGED_EVENT, checkAuth);
    window.addEventListener("focus", checkAuth);

    return () => {
      clearTimeout(timer);
      window.removeEventListener("storage", checkAuth);
      window.removeEventListener(AUTH_CHANGED_EVENT, checkAuth);
      window.removeEventListener("focus", checkAuth);
    };
  }, []);

  const handleLogout = () => {
    clearAuth();
    setUserEmail(null);
    setIsAdmin(false);
    // Wylogowanie z chronionej podstrony nie może zostawić użytkownika na tej
    // podstronie – kolejna akcja poleciałaby do API bez tokenu.
    navigate("/");
  };

  // Dynamiczne menu - dodaj "Users" dla adminów
  const currentMenuItems = isAdmin
    ? [...menuItems, { label: "Users", to: "/users" }]
    : menuItems;

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 bg-gray-900/80 backdrop-blur-md border-b border-gray-700/50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-[auto_1fr_auto] items-center h-16 gap-4">
          {/* Logo - lewa strona */}
          <NavLink
            to="/"
            className={`flex-shrink-0 flex items-center space-x-3 transition-all duration-500 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 -translate-y-4"
            }`}
          >
            <img
              src="/images/logo.png"
              alt="Tomsoft1 Logo"
              className="h-10 w-auto"
            />
            <span className="text-2xl font-bold bg-gradient-to-r from-orange-400 via-amber-600 to-yellow-700 bg-clip-text text-transparent">
              Workspace
            </span>
          </NavLink>

          {/* Desktop menu - środek */}
          <div className="hidden md:flex justify-center">
            <div className="flex items-center space-x-1">
              {currentMenuItems.map((item, index) => (
                <NavLink
                  key={item.label}
                  to={item.to}
                  className={({ isActive }) =>
                    `px-4 py-2 rounded-lg text-sm font-medium transition-all duration-300
                    ${
                      isActive
                        ? "bg-cyan-500/20 text-cyan-400"
                        : "text-gray-300 hover:bg-gray-700/50 hover:text-white"
                    } ${isVisible ? "opacity-100 translate-y-0" : "opacity-0 -translate-y-4"}`
                  }
                  style={{
                    transitionDelay: isVisible
                      ? `${150 + index * 50}ms`
                      : "0ms",
                  }}
                >
                  {item.label}
                </NavLink>
              ))}
            </div>
          </div>

          {/* Prawa kolumna: user info (desktop) + hamburger (mobile) */}
          <div className="flex items-center justify-end">
            {/* User info / Auth links - tylko desktop */}
            <div
              className={`hidden md:flex items-center space-x-3 transition-all duration-500 ease-out ${
                isVisible
                  ? "opacity-100 translate-y-0"
                  : "opacity-0 -translate-y-4"
              }`}
              style={{ transitionDelay: isVisible ? "400ms" : "0ms" }}
            >
              {userEmail ? (
                <div className="flex items-center space-x-3">
                  <div className="flex items-center space-x-2">
                    <div className="w-8 h-8 rounded-full bg-cyan-500/20 flex items-center justify-center">
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
                    <span className="text-sm text-gray-300">{userEmail}</span>
                  </div>
                  {isAdmin && (
                    <NavLink
                      to="/register"
                      className="px-3 py-1.5 rounded-lg text-sm font-medium bg-cyan-500/20 text-cyan-400 hover:bg-cyan-500/30 transition-colors"
                    >
                      Rejestracja
                    </NavLink>
                  )}
                  <NavLink
                    to="/pass-change"
                    className="px-3 py-1.5 rounded-lg text-sm font-medium text-gray-400 hover:text-white hover:bg-gray-700/50 transition-colors"
                  >
                    Zmień hasło
                  </NavLink>
                  <button
                    onClick={handleLogout}
                    className="px-3 py-1.5 rounded-lg text-sm font-medium text-gray-400 hover:text-white hover:bg-gray-700/50 transition-colors"
                  >
                    Wyloguj
                  </button>
                </div>
              ) : (
                <div className="flex items-center space-x-2">
                  <NavLink
                    to="/login"
                    className="px-4 py-2 rounded-lg text-sm font-medium text-gray-300 hover:bg-gray-700/50 hover:text-white transition-colors"
                  >
                    Zaloguj
                  </NavLink>
                </div>
              )}
            </div>

            {/* Mobile menu button */}
            <div
              className={`md:hidden transition-all duration-500 ease-out ${
                isVisible
                  ? "opacity-100 translate-y-0"
                  : "opacity-0 -translate-y-4"
              }`}
              style={{ transitionDelay: isVisible ? "150ms" : "0ms" }}
            >
              <button
                onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                className="p-2 rounded-lg text-gray-400 hover:text-white hover:bg-gray-700/50 transition-colors"
              >
                <svg
                  className={`w-6 h-6 transition-transform duration-300 ${mobileMenuOpen ? "rotate-90" : ""}`}
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  {mobileMenuOpen ? (
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M6 18L18 6M6 6l12 12"
                    />
                  ) : (
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M4 6h16M4 12h16M4 18h16"
                    />
                  )}
                </svg>
              </button>
            </div>
          </div>
        </div>

        {/* Mobile menu */}
        <div
          className={`md:hidden overflow-hidden transition-all duration-300 ease-out ${
            mobileMenuOpen
              ? "max-h-[500px] opacity-100 pb-4"
              : "max-h-0 opacity-0"
          }`}
        >
          <div className="flex flex-col space-y-1">
            {currentMenuItems.map((item, index) => (
              <NavLink
                key={item.label}
                to={item.to}
                onClick={() => setMobileMenuOpen(false)}
                className={({ isActive }) =>
                  `px-4 py-3 rounded-lg text-sm font-medium transition-all duration-300
                  ${
                    isActive
                      ? "bg-cyan-500/20 text-cyan-400"
                      : "text-gray-300 hover:bg-gray-700/50 hover:text-white"
                  } ${mobileMenuOpen ? "opacity-100 translate-x-0" : "opacity-0 -translate-x-4"}`
                }
                style={{
                  transitionDelay: mobileMenuOpen ? `${index * 50}ms` : "0ms",
                }}
              >
                {item.label}
              </NavLink>
            ))}

            {/* Mobile auth section */}
            <div className="border-t border-gray-700/50 mt-2 pt-2">
              {userEmail ? (
                <>
                  <div className="px-4 py-3 flex items-center space-x-2 text-gray-300">
                    <div className="w-8 h-8 rounded-full bg-cyan-500/20 flex items-center justify-center">
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
                    <span className="text-sm">{userEmail}</span>
                  </div>
                  {isAdmin && (
                    <NavLink
                      to="/register"
                      onClick={() => setMobileMenuOpen(false)}
                      className="block px-4 py-3 rounded-lg text-sm font-medium text-cyan-400 hover:bg-cyan-500/20 transition-colors"
                    >
                      Rejestracja
                    </NavLink>
                  )}
                  <NavLink
                    to="/pass-change"
                    onClick={() => setMobileMenuOpen(false)}
                    className="block px-4 py-3 rounded-lg text-sm font-medium text-gray-400 hover:bg-gray-700/50 hover:text-white transition-colors"
                  >
                    Zmień hasło
                  </NavLink>
                  <button
                    onClick={() => {
                      handleLogout();
                      setMobileMenuOpen(false);
                    }}
                    className="w-full px-4 py-3 rounded-lg text-sm font-medium text-left text-gray-400 hover:bg-gray-700/50 hover:text-white transition-colors"
                  >
                    Wyloguj
                  </button>
                </>
              ) : (
                <>
                  <NavLink
                    to="/login"
                    onClick={() => setMobileMenuOpen(false)}
                    className="block px-4 py-3 rounded-lg text-sm font-medium text-gray-300 hover:bg-gray-700/50 hover:text-white transition-colors"
                  >
                    Zaloguj
                  </NavLink>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </nav>
  );
}

// Główny komponent layoutu
function Layout() {
  const location = useLocation();
  const isLottoPage = location.pathname.startsWith("/lotto");
  // Sekcja kursów bez animowanego tła – karty z backdrop-blur nad ruchomą
  // warstwą wymuszają przeliczanie rozmycia w każdej klatce.
  const hideBackground = location.pathname.startsWith("/courses");

  return (
    <div
      className="min-h-screen bg-gray-900"
      style={{
        backgroundImage: `linear-gradient(rgba(17, 24, 39, 0.9), rgba(17, 24, 39, 0.9)), url(${backgroundImage})`,
        backgroundSize: "cover",
        backgroundPosition: "center",
        backgroundAttachment: "fixed",
      }}
    >
      {!hideBackground && <AnimatedBackground isLotto={isLottoPage} />}
      <Navigation />
      <main className="relative z-10 pt-16">
        <Outlet />
      </main>
    </div>
  );
}

export default Layout;
