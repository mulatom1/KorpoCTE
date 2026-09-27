import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { ApiLottoService } from "../../services/api-lotto-service";
import type { NumbersGroupStatsDto } from "../../services/contracts/lotto-draws-numbers-stats-list-response";
import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import timezone from "dayjs/plugin/timezone";

import ButtonPrimary from "../../components/ButtonPrimary";
import DateTimePicker from "../../components/DateTimePicker";
import ListSelect from "../../components/ListSelect";
import Card from "../../components/Card";
import SubMenu from "../../components/SubMenu";
import CardListItem from "../../components/CardListItem";

dayjs.extend(utc);
dayjs.extend(timezone);

const DRAW_TYPES = [
  { id: 1, name: "Lotto", numbersCount: 6, specialsCount: 0 },
  { id: 2, name: "Lotto Plus", numbersCount: 6, specialsCount: 0 },
  { id: 3, name: "Mini Lotto", numbersCount: 5, specialsCount: 0 },
  { id: 4, name: "Ekstra Pensja", numbersCount: 5, specialsCount: 1 },
  { id: 5, name: "Ekstra Premia", numbersCount: 5, specialsCount: 1 },
  { id: 6, name: "EuroJackpot", numbersCount: 5, specialsCount: 2 },
  { id: 7, name: "Szybkie600", numbersCount: 6, specialsCount: 0 },
  { id: 8, name: "Kaskada", numbersCount: 24, specialsCount: 0 },
  { id: 9, name: "MultiMulti", numbersCount: 20, specialsCount: 1 },
  { id: 10, name: "Keno", numbersCount: 20, specialsCount: 0 },
];

type ActivePanel = "numbers" | "specials";

function LottoDrawsNumbersStatsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [isVisible, setIsVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [numbersStats, setNumbersStats] = useState<NumbersGroupStatsDto[]>([]);
  const [specialsStats, setSpecialsStats] = useState<NumbersGroupStatsDto[]>(
    [],
  );
  const [totalDrawsCount, setTotalDrawsCount] = useState(0);
  const [totalNumbersGroupsCount, setTotalNumbersGroupsCount] = useState(0);
  const [totalSpecialsGroupsCount, setTotalSpecialsGroupsCount] = useState(0);
  const [activePanel, setActivePanel] = useState<ActivePanel>("numbers");

  // Filters state
  const formatLocalDateTime = (date: Date) => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  };

  const getDefaultDateFrom = () => {
    const date = new Date();
    date.setFullYear(date.getFullYear() - 1);
    date.setHours(0, 0, 0, 0);
    return formatLocalDateTime(date);
  };

  const getDefaultDateTo = () => {
    const date = new Date();
    date.setHours(23, 59, 59, 0);
    return formatLocalDateTime(date);
  };

  const [dateFrom, setDateFrom] = useState(
    searchParams.get("dateFrom") || getDefaultDateFrom(),
  );
  const [dateTo, setDateTo] = useState(
    searchParams.get("dateTo") || getDefaultDateTo(),
  );
  const [drawTypeId, setDrawTypeId] = useState<string>(
    searchParams.get("drawTypeId") || "1",
  );
  const [numbersGroup, setNumbersGroup] = useState<string>(
    searchParams.get("numbersGroup") || "2",
  );
  const [specialsGroup, setSpecialsGroup] = useState<string>(
    searchParams.get("specialsGroup") || "1",
  );
  const [sortOrder, setSortOrder] = useState<"asc" | "desc">(
    (searchParams.get("sortOrder") as "asc" | "desc") || "desc",
  );

  // Pagination for display
  const [numbersPage, setNumbersPage] = useState(1);
  const [specialsPage, setSpecialsPage] = useState(1);
  const pageSize = 100;

  const selectedDrawType = DRAW_TYPES.find(
    (t) => t.id === parseInt(drawTypeId, 10),
  );
  const selectedDrawTypeId = parseInt(drawTypeId, 10);
  // Limit max groups to 4 for Kaskada (8), MultiMulti (9), Keno (10)
  const isLimitedGame = [8, 9, 10].includes(selectedDrawTypeId);
  const maxNumbersGroup = isLimitedGame
    ? Math.min(selectedDrawType?.numbersCount || 6, 4)
    : selectedDrawType?.numbersCount || 6;
  const maxSpecialsGroup = selectedDrawType?.specialsCount || 0;
  const hasSpecials = maxSpecialsGroup > 0;

  useEffect(() => {
    document.title = "Statystyki grup liczb | Lotto | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);

    // Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
    return () => clearTimeout(timer);
  }, []);

  // Reset results when filters change
  useEffect(() => {
    setNumbersStats([]);
    setSpecialsStats([]);
    setTotalDrawsCount(0);
    setTotalNumbersGroupsCount(0);
    setTotalSpecialsGroupsCount(0);
    setError(null);
  }, [dateFrom, dateTo, drawTypeId, numbersGroup, specialsGroup, sortOrder]);

  const fetchStats = async () => {
    const token = localStorage.getItem("token");
    if (!token) return;

    if (!drawTypeId) {
      setError("Wybierz typ losowania");
      return;
    }

    if (!numbersGroup || parseInt(numbersGroup, 10) < 1) {
      setError("Wielkość grupy musi być >= 1");
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;

      const response = await apiLottoService.lottoDrawsNumbersStatsList({
        drawDateFrom: dateFrom
          ? dayjs.tz(dateFrom, tz).utc().format()
          : undefined,
        drawDateTo: dateTo ? dayjs.tz(dateTo, tz).utc().format() : undefined,
        drawTypeId: parseInt(drawTypeId, 10),
        numbersGroup: parseInt(numbersGroup, 10),
        specialsGroup:
          hasSpecials && specialsGroup
            ? parseInt(specialsGroup, 10)
            : undefined,
        sortOrder: sortOrder,
      });

      setNumbersStats(response.numbersStats);
      setSpecialsStats(response.specialsStats);
      setTotalDrawsCount(response.totalDrawsCount);
      setTotalNumbersGroupsCount(response.totalNumbersGroupsCount);
      setTotalSpecialsGroupsCount(response.totalSpecialsGroupsCount);
      setNumbersPage(1);
      setSpecialsPage(1);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystąpił błąd podczas pobierania statystyk",
      );
    } finally {
      setIsLoading(false);
    }
  };

  const handleSearch = () => {
    const params = new URLSearchParams();
    if (dateFrom) params.set("dateFrom", dateFrom);
    if (dateTo) params.set("dateTo", dateTo);
    if (drawTypeId) params.set("drawTypeId", drawTypeId);
    if (numbersGroup) params.set("numbersGroup", numbersGroup);
    if (hasSpecials && specialsGroup)
      params.set("specialsGroup", specialsGroup);
    params.set("sortOrder", sortOrder);
    setSearchParams(params);
    fetchStats();
  };

  const getDrawTypeName = (typeId: number) => {
    const type = DRAW_TYPES.find((t) => t.id === typeId);
    return type?.name || `Typ ${typeId}`;
  };

  const getGroupSizeName = (size: number) => {
    switch (size) {
      case 1:
        return "Pojedyncze";
      case 2:
        return "Pary";
      case 3:
        return "Trójki";
      case 4:
        return "Czwórki";
      case 5:
        return "Piątki";
      case 6:
        return "Szóstki";
      default:
        return `Grupy ${size}-elementowe`;
    }
  };

  // Current stats based on active panel
  const currentStats = activePanel === "numbers" ? numbersStats : specialsStats;
  const currentPage = activePanel === "numbers" ? numbersPage : specialsPage;
  const setCurrentPage =
    activePanel === "numbers" ? setNumbersPage : setSpecialsPage;

  // Paginated stats for display
  const totalPages = Math.ceil(currentStats.length / pageSize);

  const handlePageChange = (newPage: number) => {
    setCurrentPage(newPage);
  };

  const hasResults = numbersStats.length > 0 || specialsStats.length > 0;

  const renderStatsList = (
    stats: NumbersGroupStatsDto[],
    page: number,
    isSpecials: boolean,
  ) => {
    const paginated = stats.slice((page - 1) * pageSize, page * pageSize);

    return (
      <div className="space-y-2">
        {/* Header */}
        <div className="grid grid-cols-[auto_1fr_auto] gap-4 px-4 py-2 text-gray-400 text-sm font-semibold border-b border-gray-700/50">
          <div className="w-12 text-center">#</div>
          <div>Liczby</div>
          <div className="w-24 text-right">Wystąpienia</div>
        </div>

        {/* Stats list */}
        {paginated.map((stat, index) => {
          const rank = (page - 1) * pageSize + index + 1;
          return (
            <CardListItem
              key={stat.numbers.join("-")}
              isVisible={isVisible}
              index={index % 10}
              delayBase={400}
              delayStep={30}
              className="grid grid-cols-[auto_1fr_auto] gap-4 items-center"
            >
              {/* Rank */}
              <div className="w-12 text-center">
                <span
                  className={`text-sm font-bold ${
                    rank === 1
                      ? "text-yellow-400"
                      : rank === 2
                        ? "text-gray-300"
                        : rank === 3
                          ? "text-amber-600"
                          : "text-gray-500"
                  }`}
                >
                  {rank}
                </span>
              </div>

              {/* Numbers */}
              <div className="flex flex-wrap gap-1.5">
                {stat.numbers.map((num, numIndex) => (
                  <div
                    key={numIndex}
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-gray-900 font-black text-xs shadow-lg ${
                      isSpecials
                        ? "bg-red-500 ring-2 ring-red-300"
                        : numIndex % 2 === 0
                          ? "bg-amber-500"
                          : "bg-amber-400"
                    }`}
                  >
                    {num}
                  </div>
                ))}
              </div>

              {/* Count */}
              <div className="w-24 text-right">
                <span className="text-cyan-400 font-semibold">
                  {stat.count}
                </span>
                <span className="text-gray-500 text-sm ml-1">x</span>
              </div>
            </CardListItem>
          );
        })}
      </div>
    );
  };

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16 overflow-hidden">
      <div className="max-w-4xl mx-auto w-full">
        {/* Header */}
        <div className="text-center mb-8">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Statystyki grup liczb
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Analiza najczęściej występujących par, trójek, czwórek itp.
          </p>
        </div>

        {/* Navigation submenu */}
        <SubMenu
          backPath="/lotto"
          isVisible={isVisible}
          items={[
            { label: "Wyniki losowań", path: "/lotto/draws" },
            { label: "Moje kupony", path: "/lotto/tickets" },
            { label: "Sprawdź wygrane", path: "/lotto/winning-tickets" },
            { label: "Statystyki grup", path: "/lotto/draws-numbers-stats" },
          ]}
        />

        {/* Filters */}
        <Card isVisible={isVisible} className="mb-8 delay-300">
          {/* Row 1: Date from/to */}
          <div className="grid grid-cols-2 gap-4 mb-4">
            <DateTimePicker
              label="Data losowania od"
              id="statsDateFrom"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
            />
            <DateTimePicker
              label="Data losowania do"
              id="statsDateTo"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
            />
          </div>

          {/* Row 2: Draw type, Sort order */}
          <div className="grid grid-cols-2 gap-4 mb-4">
            <ListSelect
              label={
                <>
                  Typ losowania <span className="text-red-400">*</span>
                </>
              }
              id="statsDrawTypeId"
              value={drawTypeId}
              onChange={(e) => {
                setDrawTypeId(e.target.value);
                const newType = DRAW_TYPES.find(
                  (t) => t.id === parseInt(e.target.value, 10),
                );
                const newTypeId = parseInt(e.target.value, 10);
                const newIsLimited = [8, 9, 10].includes(newTypeId);
                const newMaxNumbers = newIsLimited
                  ? Math.min(newType?.numbersCount || 6, 4)
                  : newType?.numbersCount || 6;
                const newMaxSpecials = newType?.specialsCount || 0;
                if (parseInt(numbersGroup, 10) > newMaxNumbers) {
                  setNumbersGroup(newMaxNumbers.toString());
                }
                if (newMaxSpecials > 0) {
                  if (parseInt(specialsGroup, 10) > newMaxSpecials) {
                    setSpecialsGroup(newMaxSpecials.toString());
                  }
                } else {
                  setSpecialsGroup("1");
                }
              }}
              options={DRAW_TYPES.map((type) => ({
                value: type.id.toString(),
                label: type.name,
              }))}
            />
            <ListSelect
              label="Sortowanie"
              id="statsSortOrder"
              value={sortOrder}
              onChange={(e) => setSortOrder(e.target.value as "asc" | "desc")}
              options={[
                { value: "desc", label: "Malejąco (najczęstsze)" },
                { value: "asc", label: "Rosnąco (najrzadsze)" },
              ]}
            />
          </div>

          {/* Row 3: Numbers/Specials groups (2/3) + Search button (1/3) */}
          <div className="grid grid-cols-[2fr_1fr] gap-4 mb-4">
            <div
              className={`grid ${hasSpecials ? "grid-cols-2" : "grid-cols-1"} gap-4`}
            >
              <ListSelect
                label={
                  <span className="inline-flex items-center gap-2">
                    <span className="w-3 h-3 rounded-full bg-amber-500"></span>
                    Grupa liczb głównych <span className="text-red-400">*</span>
                  </span>
                }
                id="numbersGroup"
                value={numbersGroup}
                onChange={(e) => setNumbersGroup(e.target.value)}
                options={Array.from(
                  { length: maxNumbersGroup },
                  (_, i) => i + 1,
                ).map((size) => ({
                  value: size.toString(),
                  label: `${size} (${getGroupSizeName(size)})`,
                }))}
              />
              {hasSpecials && (
                <ListSelect
                  label={
                    <span className="inline-flex items-center gap-2">
                      <span className="w-3 h-3 rounded-full bg-red-500"></span>
                      Grupa liczb specjalnych
                    </span>
                  }
                  id="specialsGroup"
                  value={specialsGroup}
                  onChange={(e) => setSpecialsGroup(e.target.value)}
                  options={Array.from(
                    { length: maxSpecialsGroup },
                    (_, i) => i + 1,
                  ).map((size) => ({
                    value: size.toString(),
                    label: `${size} (${getGroupSizeName(size)})`,
                  }))}
                />
              )}
            </div>
            <div className="flex items-end">
              <ButtonPrimary
                onClick={handleSearch}
                disabled={isLoading}
                className="w-full"
              >
                {isLoading ? "Szukam..." : "Szukaj"}
              </ButtonPrimary>
            </div>
          </div>
        </Card>

        {/* Error */}
        {error && (
          <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
            {error}
          </div>
        )}

        {/* Results info */}
        {!isLoading && hasResults && (
          <div
            className={`text-gray-400 text-sm mb-4 transition-all duration-500 ${
              isVisible ? "opacity-100" : "opacity-0"
            }`}
          >
            <div className="flex flex-wrap gap-4">
              <span>
                Przeanalizowano{" "}
                <strong className="text-white">{totalDrawsCount}</strong>{" "}
                losowań
              </span>
              <span>|</span>
              <span>
                Typ:{" "}
                <strong className="text-amber-400">
                  {getDrawTypeName(parseInt(drawTypeId, 10))}
                </strong>
              </span>
              <span>|</span>
              <span>
                Grupy:{" "}
                <strong className="text-amber-400">
                  {getGroupSizeName(parseInt(numbersGroup, 10))}
                </strong>
              </span>
            </div>
          </div>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin"></div>
          </div>
        )}

        {/* Results */}
        {!isLoading && !hasResults && !error && (
          <div className="text-center py-12 text-gray-400">
            Brak wyników do wyświetlenia. Użyj filtrów i kliknij "Szukaj".
          </div>
        )}

        {/* Panel Toggles */}
        {!isLoading && hasResults && (
          <div className="flex gap-2 mb-6">
            <button
              onClick={() => setActivePanel("numbers")}
              className={`flex-1 px-4 py-2.5 rounded-xl font-semibold transition-all duration-300 ${
                activePanel === "numbers"
                  ? "bg-amber-500 text-gray-900 shadow-lg shadow-amber-500/25"
                  : "bg-gray-800/50 text-gray-400 border border-gray-700/50 hover:border-amber-500/30 hover:text-amber-400"
              }`}
            >
              <div className="flex items-center justify-center gap-2">
                <div className="w-4 h-4 rounded-full bg-amber-400 border-2 border-amber-600"></div>
                <span>Liczby główne</span>
                <span className="text-sm opacity-75">
                  ({totalNumbersGroupsCount})
                </span>
              </div>
            </button>
            {hasSpecials && (
              <button
                onClick={() => setActivePanel("specials")}
                disabled={specialsStats.length === 0}
                className={`flex-1 px-4 py-2.5 rounded-xl font-semibold transition-all duration-300 ${
                  activePanel === "specials"
                    ? "bg-red-500 text-white shadow-lg shadow-red-500/25"
                    : "bg-gray-800/50 text-gray-400 border border-gray-700/50 hover:border-red-500/30 hover:text-red-400 disabled:opacity-50 disabled:cursor-not-allowed"
                }`}
              >
                <div className="flex items-center justify-center gap-2">
                  <div className="w-4 h-4 rounded-full bg-red-500 border-2 border-red-300"></div>
                  <span>Liczby specjalne</span>
                  <span className="text-sm opacity-75">
                    ({totalSpecialsGroupsCount})
                  </span>
                </div>
              </button>
            )}
          </div>
        )}

        {/* Stats Panel */}
        {!isLoading && hasResults && (
          <>
            {activePanel === "numbers" &&
              numbersStats.length > 0 &&
              renderStatsList(numbersStats, numbersPage, false)}
            {activePanel === "specials" &&
              specialsStats.length > 0 &&
              renderStatsList(specialsStats, specialsPage, true)}
            {activePanel === "numbers" && numbersStats.length === 0 && (
              <div className="text-center py-12 text-gray-400">
                Brak statystyk dla liczb głównych.
              </div>
            )}
            {activePanel === "specials" && specialsStats.length === 0 && (
              <div className="text-center py-12 text-gray-400">
                Brak statystyk dla liczb specjalnych (wielkość grupy przekracza
                ilość liczb specjalnych).
              </div>
            )}
          </>
        )}

        {/* Pagination */}
        {!isLoading && totalPages > 1 && (
          <div className="flex justify-center items-center gap-2 mt-8">
            <button
              onClick={() => handlePageChange(1)}
              disabled={currentPage === 1}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &laquo;
            </button>
            <button
              onClick={() => handlePageChange(currentPage - 1)}
              disabled={currentPage === 1}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &lsaquo;
            </button>

            <div className="flex gap-1">
              {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                let pageNum: number;
                if (totalPages <= 5) {
                  pageNum = i + 1;
                } else if (currentPage <= 3) {
                  pageNum = i + 1;
                } else if (currentPage >= totalPages - 2) {
                  pageNum = totalPages - 4 + i;
                } else {
                  pageNum = currentPage - 2 + i;
                }

                return (
                  <button
                    key={pageNum}
                    onClick={() => handlePageChange(pageNum)}
                    className={`px-3 py-2 rounded-lg transition-all ${
                      currentPage === pageNum
                        ? "bg-cyan-500 text-white"
                        : "bg-gray-800/50 border border-gray-700/50 text-gray-300 hover:border-cyan-500/30"
                    }`}
                  >
                    {pageNum}
                  </button>
                );
              })}
            </div>

            <button
              onClick={() => handlePageChange(currentPage + 1)}
              disabled={currentPage === totalPages}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &rsaquo;
            </button>
            <button
              onClick={() => handlePageChange(totalPages)}
              disabled={currentPage === totalPages}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &raquo;
            </button>
          </div>
        )}
      </div>
    </section>
  );
}

export default LottoDrawsNumbersStatsPage;
