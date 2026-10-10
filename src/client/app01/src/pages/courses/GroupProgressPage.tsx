import { useEffect, useState } from "react";
import { ApiCoursesService } from "../../services/api-courses-service";
import type { CoursesGroupProgressResponse } from "../../services/contracts/courses-group-progress-response";
import Card from "../../components/Card";
import SubMenu from "../../components/SubMenu";
import { endOfLocalDay } from "../../utils/endOfLocalDay";
import { formatDateTime } from "../../utils/formatDateTime";
import { coursesSubMenuItems } from "./coursesSubMenu";

// Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
function createApiService(): ApiCoursesService {
  const apiService = new ApiCoursesService(
    import.meta.env.VITE_API_URL ?? "",
    import.meta.env.VITE_APP_TOKEN,
  );
  apiService.setUsrToken(localStorage.getItem("token") ?? "");
  return apiService;
}

// Dzisiejsza data w lokalnej strefie jako yyyy-MM-dd (nie UTC).
function todayLocal(): string {
  return formatDateTime(new Date()).slice(0, 10);
}

// Procent z jednym miejscem po przecinku; brak flag do zdobycia => „—”.
function formatPercent(value: number | null): string {
  return value === null ? "—" : `${value.toFixed(1)}%`;
}

function GroupProgressPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [today] = useState(todayLocal);
  const [date, setDate] = useState(today);
  const [data, setData] = useState<CoursesGroupProgressResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    document.title = "Postęp grupy | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    // Odpowiedź dla nieaktualnej daty jest ignorowana.
    let isStale = false;

    const fetchGroupProgress = async () => {
      setIsLoading(true);
      setError("");
      try {
        const response = await createApiService().getGroupProgress({
          asOf: endOfLocalDay(date),
        });
        if (!isStale) setData(response);
      } catch (err) {
        if (!isStale) {
          setError(
            err instanceof Error
              ? err.message
              : "Błąd pobierania postępu grupy",
          );
        }
      } finally {
        if (!isStale) setIsLoading(false);
      }
    };
    // Wyczyszczone pole daty – nie pytamy serwera o niepoprawną datę.
    if (date) fetchGroupProgress();

    return () => {
      isStale = true;
    };
  }, [date]);

  const tiles = data
    ? [
        { label: "Użytkownicy z flagą", value: String(data.userCount) },
        { label: "Flagi do zdobycia", value: String(data.availableFlagCount) },
        {
          label: "Flagi zdobyte przez grupę",
          value: String(data.earnedFlagCount),
        },
        {
          label: "Procent zdobytych flag",
          value: formatPercent(data.earnedPercent),
        },
      ]
    : [];

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16">
      <div className="max-w-4xl mx-auto w-full min-w-0">
        <div className="text-center mb-12">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Postęp grupy
          </h1>
          <p className="text-gray-400 text-lg max-w-2xl mx-auto">
            Wskaźniki zdobywania flag przez całą grupę na wybrany dzień.
          </p>
        </div>

        <SubMenu
          backPath="/courses"
          isVisible={isVisible}
          items={coursesSubMenuItems}
        />

        <Card isVisible={isVisible} className="mb-6">
          <div className="max-w-xs">
            <label
              htmlFor="group-progress-date"
              className="block text-gray-300 text-sm font-medium mb-2"
            >
              Stan na dzień
            </label>
            <input
              type="date"
              id="group-progress-date"
              name="date"
              value={date}
              max={today}
              onChange={(e) => setDate(e.target.value)}
              className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
            />
          </div>
        </Card>

        {isLoading ? (
          <div className="p-8 text-center text-gray-400">Ładowanie...</div>
        ) : error ? (
          <div
            role="alert"
            className="p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm"
          >
            {error}
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {tiles.map((tile) => (
              <Card key={tile.label} isVisible={isVisible}>
                <h2 className="text-gray-400 text-sm font-medium mb-2">
                  {tile.label}
                </h2>
                <p className="text-3xl font-bold text-white">{tile.value}</p>
              </Card>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

export default GroupProgressPage;
