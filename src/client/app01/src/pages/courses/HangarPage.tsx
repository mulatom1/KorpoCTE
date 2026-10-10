import { useEffect, useState } from "react";
import { Link } from "react-router";
import { ApiCoursesService } from "../../services/api-courses-service";
import type { CoursesHangarFlagsFilter } from "../../services/contracts/courses-hangar-flags-request";
import type { CoursesHangarFlagsResponse } from "../../services/contracts/courses-hangar-flags-response";
import FormCard from "../../components/FormCard";
import SubMenu from "../../components/SubMenu";
import ButtonPrimary from "../../components/ButtonPrimary";
import ButtonSecondary from "../../components/ButtonSecondary";
import { formatDateTime } from "../../utils/formatDateTime";
import { coursesSubMenuItems } from "./coursesSubMenu";

// Stała maska – nie zdradza długości kodu.
const FLAG_CODE_MASK = "********";

// Liczba flag na stronie listy.
const PAGE_SIZE = 20;

const FILTERS: { value: CoursesHangarFlagsFilter; label: string }[] = [
  { value: "All", label: "Wszystkie" },
  { value: "Earned", label: "Zdobyte" },
  { value: "Unearned", label: "Niezdobyte" },
];

// Komunikat pustej listy zależny od wybranego filtra.
const EMPTY_MESSAGES: Record<CoursesHangarFlagsFilter, string> = {
  All: "Brak flag do zdobycia",
  Earned: "Nie masz jeszcze żadnej flagi",
  Unearned: "Wszystkie flagi zdobyte",
};

// Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
function createApiService(): ApiCoursesService {
  const apiService = new ApiCoursesService(
    import.meta.env.VITE_API_URL ?? "",
    import.meta.env.VITE_APP_TOKEN,
  );
  apiService.setUsrToken(localStorage.getItem("token") ?? "");
  return apiService;
}

function formatEarnedAt(earnedAt: string | null): string {
  if (!earnedAt) return "—";
  return formatDateTime(earnedAt);
}

interface FlagCodeProps {
  title: string;
  code: string;
}

// Kod flagi zamaskowany; odsłania się po najechaniu myszką albo po fokusie
// (klawiatura, dotknięcie na telefonie). Zamaskowany kod nie trafia do DOM.
function FlagCode({ title, code }: FlagCodeProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isFocused, setIsFocused] = useState(false);
  const isRevealed = isHovered || isFocused;

  return (
    <code
      tabIndex={0}
      aria-label={`Kod flagi ${title}`}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onFocus={() => setIsFocused(true)}
      onBlur={() => setIsFocused(false)}
      className={`inline-block break-all px-3 py-1 bg-gray-900/70 border border-green-500/50 rounded-lg font-mono text-green-300 cursor-pointer focus:outline-none focus:ring-1 focus:ring-green-500/50 ${
        isRevealed ? "select-all" : "select-none"
      }`}
    >
      {isRevealed ? code : FLAG_CODE_MASK}
    </code>
  );
}

function HangarPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [filter, setFilter] = useState<CoursesHangarFlagsFilter>("All");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<CoursesHangarFlagsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    document.title = "Hangar | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    // Odpowiedź dla nieaktualnego filtra/strony jest ignorowana.
    let isStale = false;

    const fetchFlags = async () => {
      setIsLoading(true);
      setError("");
      try {
        const response = await createApiService().getHangarFlags({
          filter,
          page,
          pageSize: PAGE_SIZE,
        });
        if (!isStale) setData(response);
      } catch (err) {
        if (!isStale) {
          setError(
            err instanceof Error ? err.message : "Błąd pobierania listy flag",
          );
        }
      } finally {
        if (!isStale) setIsLoading(false);
      }
    };
    fetchFlags();

    return () => {
      isStale = true;
    };
  }, [filter, page]);

  // Zmiana filtra zawsze wraca na pierwszą stronę.
  const changeFilter = (value: CoursesHangarFlagsFilter) => {
    setFilter(value);
    setPage(1);
  };

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
            Hangar
          </h1>
          <p className="text-gray-400 text-lg max-w-2xl mx-auto">
            Twoje flagi zdobyte w kursach i te, które wciąż czekają.
          </p>
        </div>

        <SubMenu
          backPath="/courses"
          isVisible={isVisible}
          items={coursesSubMenuItems}
        />

        <FormCard isVisible={isVisible} borderColor="green">
          <div className="flex flex-wrap gap-3 justify-center mb-6">
            {FILTERS.map(({ value, label }) => {
              const isActive = value === filter;
              const FilterButton = isActive ? ButtonPrimary : ButtonSecondary;
              return (
                <FilterButton
                  key={value}
                  type="button"
                  aria-pressed={isActive}
                  onClick={() => changeFilter(value)}
                >
                  {label}
                </FilterButton>
              );
            })}
          </div>

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
              <p className="text-center text-gray-300 text-lg mb-6">
                Zdobyte flagi: {data.earnedCount} / {data.allCount}
              </p>

              {data.flags.length === 0 ? (
                <div className="p-8 text-center text-gray-400">
                  {EMPTY_MESSAGES[filter]}
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm text-gray-300">
                    <thead className="text-gray-400 border-b border-gray-700">
                      <tr>
                        <th scope="col" className="px-3 py-2">
                          Flaga
                        </th>
                        <th scope="col" className="px-3 py-2">
                          Kurs
                        </th>
                        <th scope="col" className="px-3 py-2">
                          Status
                        </th>
                        <th scope="col" className="px-3 py-2 whitespace-nowrap">
                          Data zdobycia
                        </th>
                        <th scope="col" className="px-3 py-2">
                          Kod
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {/* Kolejność wierszy jak z serwera. */}
                      {data.flags.map((flag) => (
                        <tr
                          key={flag.flagId}
                          className="border-b border-gray-800 last:border-b-0"
                        >
                          <td className="px-3 py-2 font-semibold text-white">
                            {flag.title}
                          </td>
                          <td className="px-3 py-2">
                            {/* Link do strony kursu - jak w kafelku kursu */}
                            <Link
                              to={`/courses/${flag.courseSlug}`}
                              className="text-cyan-400 hover:text-cyan-300 underline-offset-2 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-cyan-500 rounded"
                            >
                              {flag.courseSlug}
                            </Link>
                          </td>
                          <td
                            className={`px-3 py-2 whitespace-nowrap ${
                              flag.isEarned ? "text-green-400" : "text-gray-400"
                            }`}
                          >
                            {flag.isEarned ? "Zdobyta" : "Niezdobyta"}
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap">
                            {formatEarnedAt(flag.earnedAt)}
                          </td>
                          <td className="px-3 py-2">
                            {flag.isEarned && flag.code ? (
                              <FlagCode title={flag.title} code={flag.code} />
                            ) : (
                              "—"
                            )}
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

export default HangarPage;
