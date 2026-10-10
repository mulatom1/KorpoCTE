import { useEffect, useState } from "react";
import { ApiCoursesService } from "../../services/api-courses-service";
import type { CoursesLeaderboardResponse } from "../../services/contracts/courses-leaderboard-response";
import FormCard from "../../components/FormCard";
import SubMenu from "../../components/SubMenu";
import ButtonSecondary from "../../components/ButtonSecondary";
import { coursesSubMenuItems } from "./coursesSubMenu";

// Liczba uczestników na stronie rankingu.
const PAGE_SIZE = 20;

// Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
function createApiService(): ApiCoursesService {
  const apiService = new ApiCoursesService(
    import.meta.env.VITE_API_URL ?? "",
    import.meta.env.VITE_APP_TOKEN,
  );
  apiService.setUsrToken(localStorage.getItem("token") ?? "");
  return apiService;
}

function LeaderboardPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<CoursesLeaderboardResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    document.title = "Lista zasłużonych | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    // Odpowiedź dla nieaktualnej strony jest ignorowana.
    let isStale = false;

    const fetchLeaderboard = async () => {
      setIsLoading(true);
      setError("");
      try {
        const response = await createApiService().getLeaderboard({
          page,
          pageSize: PAGE_SIZE,
        });
        if (!isStale) setData(response);
      } catch (err) {
        if (!isStale) {
          setError(
            err instanceof Error
              ? err.message
              : "Błąd pobierania listy zasłużonych",
          );
        }
      } finally {
        if (!isStale) setIsLoading(false);
      }
    };
    fetchLeaderboard();

    return () => {
      isStale = true;
    };
  }, [page]);

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
            Lista zasłużonych
          </h1>
          <p className="text-gray-400 text-lg max-w-2xl mx-auto">
            Ranking uczestników według liczby zdobytych flag.
          </p>
        </div>

        <SubMenu
          backPath="/courses"
          isVisible={isVisible}
          items={coursesSubMenuItems}
        />

        <FormCard isVisible={isVisible} borderColor="green">
          {isLoading ? (
            <div className="p-8 text-center text-gray-400">Ładowanie...</div>
          ) : error ? (
            <div
              role="alert"
              className="p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm"
            >
              {error}
            </div>
          ) : data ? (
            <>
              {data.entries.length === 0 ? (
                <div className="p-8 text-center text-gray-400">
                  Nikt jeszcze nie zdobył flagi
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm text-gray-300">
                    <thead className="text-gray-400 border-b border-gray-700">
                      <tr>
                        <th scope="col" className="px-3 py-2">
                          Miejsce
                        </th>
                        <th scope="col" className="px-3 py-2">
                          Uczestnik
                        </th>
                        <th scope="col" className="px-3 py-2">
                          Flagi
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {/* Kolejność i miejsca (także wspólne przy remisie) jak z serwera. */}
                      {data.entries.map((entry, index) => (
                        <tr
                          key={`${entry.rank}-${entry.displayName}-${index}`}
                          className="border-b border-gray-800 last:border-b-0"
                        >
                          <td className="px-3 py-2 whitespace-nowrap">
                            {entry.rank}
                          </td>
                          <td className="px-3 py-2 font-semibold text-white break-all">
                            {entry.displayName}
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap">
                            {entry.flagCount}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {data.totalPages > 1 && (
                <div className="flex flex-wrap items-center justify-center gap-4 mt-6">
                  <ButtonSecondary
                    type="button"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => p - 1)}
                  >
                    Poprzednia
                  </ButtonSecondary>
                  <span className="text-gray-400">
                    Strona {data.page} z {data.totalPages}
                  </span>
                  <ButtonSecondary
                    type="button"
                    disabled={page >= data.totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Następna
                  </ButtonSecondary>
                </div>
              )}
            </>
          ) : null}
        </FormCard>
      </div>
    </section>
  );
}

export default LeaderboardPage;
