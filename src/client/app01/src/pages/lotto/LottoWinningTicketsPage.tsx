import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { ApiLottoService } from "../../services/api-lotto-service";
import type {
  LottoWinningTicketsDraw,
  LottoWinningTicketsMatchingTicket,
  LottoWinningTicketsSummary,
  LottoWinningTicketsWinTierSummary,
} from "../../services/contracts/lotto-winning-tickets-response";
import type { LottoDrawsGetPrizesListResponse } from "../../services/contracts/lotto-draws-get-prizes-list-response";

import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import timezone from "dayjs/plugin/timezone";
import TextEdit from "../../components/TextEdit";
import ButtonPrimary from "../../components/ButtonPrimary";
import DateTimePicker from "../../components/DateTimePicker";
import ListSelect from "../../components/ListSelect";
import Card from "../../components/Card";
import CardListItem from "../../components/CardListItem";
import SubMenu from "../../components/SubMenu";
import FormCard from "../../components/FormCard";

dayjs.extend(utc);
dayjs.extend(timezone);

const DRAW_TYPES = [
  { id: 1, name: "Lotto", numbersCount: 6 },
  { id: 2, name: "Lotto Plus", numbersCount: 6 },
  { id: 3, name: "Mini Lotto", numbersCount: 5 },
  { id: 4, name: "Ekstra Pensja", numbersCount: 5 },
  { id: 5, name: "Ekstra Premia", numbersCount: 5 },
  { id: 6, name: "EuroJackpot", numbersCount: 5 },
  { id: 7, name: "Szybkie600", numbersCount: 6 },
  { id: 8, name: "Kaskada", numbersCount: 12 },
  { id: 9, name: "MultiMulti", numbersCount: 20 },
  { id: 10, name: "Keno", numbersCount: 20 },
];

const WIN_TIERS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

function LottoWinningTicketsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [isVisible, setIsVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draws, setDraws] = useState<LottoWinningTicketsDraw[]>([]);
  const [summary, setSummary] = useState<LottoWinningTicketsSummary | null>(
    null,
  );
  const [expandedDraws, setExpandedDraws] = useState<Set<number>>(new Set());

  // Prizes section state
  const [expandedPrizes, setExpandedPrizes] = useState<Set<number>>(new Set());
  const [prizesCache, setPrizesCache] = useState<
    Map<string, LottoDrawsGetPrizesListResponse>
  >(new Map());
  const [loadingPrizes, setLoadingPrizes] = useState<Set<number>>(new Set());

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 100;

  // Server-side filters
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
    date.setDate(date.getDate() - 7);
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
    searchParams.get("drawTypeId") || "",
  );
  const [groupName, setGroupName] = useState<string>(
    searchParams.get("groupName") || "",
  );

  // WinTier filters (server-side) - all default to true
  const [winTierFilters, setWinTierFilters] = useState<Record<number, boolean>>(
    () => {
      const initial: Record<number, boolean> = {};
      WIN_TIERS.forEach((tier) => {
        initial[tier] = true;
      });
      return initial;
    },
  );

  // Hide draws without matches filter - default to true
  const [hideDrawsWithoutMatches, setHideDrawsWithoutMatches] = useState(true);

  useEffect(() => {
    document.title = "Sprawdz wygrane | Lotto | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);

    // Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
    return () => clearTimeout(timer);
  }, []);

  // Pobieranie tylko przy montowaniu – kolejne strony i filtry obsługują handlery.
  // Ref trzyma najnowszą wersję fetchWinningTickets, więc efekt nie zależy od jej tożsamości.
  const fetchWinningTicketsRef = useRef<(page?: number) => Promise<void>>(
    async () => {},
  );

  // Musi być przed efektem montowania – efekty uruchamiają się w kolejności deklaracji.
  useEffect(() => {
    fetchWinningTicketsRef.current = fetchWinningTickets;
  });

  useEffect(() => {
    fetchWinningTicketsRef.current();
  }, []);

  // Reset results when filters change
  useEffect(() => {
    setDraws([]);
    setSummary(null);
    setTotalPages(1);
    setTotalCount(0);
    setError(null);
  }, [
    dateFrom,
    dateTo,
    drawTypeId,
    groupName,
    winTierFilters,
    hideDrawsWithoutMatches,
  ]);

  const fetchWinningTickets = async (page: number = 1) => {
    const token = localStorage.getItem("token");
    if (!token) return;

    setIsLoading(true);
    setError(null);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;

      const response = await apiLottoService.lottoWinningTicketsGetList({
        drawDateFrom: dateFrom
          ? dayjs.tz(dateFrom, tz).utc().format()
          : undefined,
        drawDateTo: dateTo ? dayjs.tz(dateTo, tz).utc().format() : undefined,
        drawTypeId: drawTypeId ? parseInt(drawTypeId, 10) : undefined,
        groupName: groupName || undefined,
        page,
        pageSize,
        winTier1: winTierFilters[1],
        winTier2: winTierFilters[2],
        winTier3: winTierFilters[3],
        winTier4: winTierFilters[4],
        winTier5: winTierFilters[5],
        winTier6: winTierFilters[6],
        winTier7: winTierFilters[7],
        winTier8: winTierFilters[8],
        winTier9: winTierFilters[9],
        winTier10: winTierFilters[10],
        winTier11: winTierFilters[11],
        winTier12: winTierFilters[12],
        hideDrawsWithoutMatches,
      });

      setDraws(response.draws);
      setSummary(response.summary);
      setCurrentPage(response.page);
      setTotalPages(response.totalPages);
      setTotalCount(response.totalCount);
      setExpandedPrizes(new Set());
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas pobierania danych",
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
    if (groupName) params.set("groupName", groupName);
    setSearchParams(params);
    setCurrentPage(1);
    fetchWinningTickets(1);
  };

  const handlePageChange = (page: number) => {
    if (page < 1 || page > totalPages) return;
    setCurrentPage(page);
    fetchWinningTickets(page);
  };

  const toggleWinTier = (tier: number) => {
    setWinTierFilters((prev) => ({
      ...prev,
      [tier]: !prev[tier],
    }));
  };

  const selectAllWinTiers = () => {
    const newFilters: Record<number, boolean> = {};
    WIN_TIERS.forEach((tier) => {
      newFilters[tier] = true;
    });
    setWinTierFilters(newFilters);
    setHideDrawsWithoutMatches(true);
  };

  const selectNoneWinTiers = () => {
    const newFilters: Record<number, boolean> = {};
    WIN_TIERS.forEach((tier) => {
      newFilters[tier] = false;
    });
    setWinTierFilters(newFilters);
    setHideDrawsWithoutMatches(false);
  };

  const toggleExpandDraw = (drawId: number) => {
    setExpandedDraws((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(drawId)) {
        newSet.delete(drawId);
      } else {
        newSet.add(drawId);
      }
      return newSet;
    });
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${year}-${month}-${day} ${hours}:${minutes}`;
  };

  const getDrawTypeName = (typeId: number) => {
    const type = DRAW_TYPES.find((t) => t.id === typeId);
    return type?.name || `Typ ${typeId}`;
  };

  const getNumberColor = (index: number) => {
    return index % 2 === 0 ? "bg-amber-500" : "bg-amber-400";
  };

  const getCacheKey = (drawTypeId: number, drawSystemId: number) =>
    `${drawTypeId}-${drawSystemId}`;

  const togglePrizes = async (draw: LottoWinningTicketsDraw) => {
    const drawId = draw.id;
    const cacheKey = getCacheKey(draw.drawTypeId, draw.drawSystemId);

    // If already expanded, just collapse
    if (expandedPrizes.has(drawId)) {
      setExpandedPrizes((prev) => {
        const next = new Set(prev);
        next.delete(drawId);
        return next;
      });
      return;
    }

    // Expand the section
    setExpandedPrizes((prev) => new Set(prev).add(drawId));

    // If we already have cached data, no need to fetch
    if (prizesCache.has(cacheKey)) {
      return;
    }

    // Fetch prizes data
    const token = localStorage.getItem("token");
    if (!token) return;

    setLoadingPrizes((prev) => new Set(prev).add(drawId));

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      const response = await apiLottoService.lottoDrawsGetPrizesList({
        drawTypeId: draw.drawTypeId,
        drawSystemId: draw.drawSystemId,
      });

      setPrizesCache((prev) => new Map(prev).set(cacheKey, response));
    } catch (err) {
      console.error("Error fetching prizes:", err);
    } finally {
      setLoadingPrizes((prev) => {
        const next = new Set(prev);
        next.delete(drawId);
        return next;
      });
    }
  };

  const formatNumber = (count: number) => {
    return new Intl.NumberFormat("pl-PL").format(count);
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat("pl-PL", {
      style: "currency",
      currency: "PLN",
    }).format(amount);
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
            Sprawdź swoje wygrane
            <br />
            lub zasymuluj je!
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Porównaj swoje kupony z wynikami losowań.
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

        {/* Server-side Filters */}
        <Card isVisible={isVisible} className="mb-6 delay-300">
          <h3 className="text-white font-medium mb-4">Filtry</h3>
          {/* First row: Date from, Date to, Draw type */}
          <div className="flex flex-col sm:flex-row gap-4 mb-4">
            <div className="flex flex-col sm:flex-row gap-4 sm:w-1/2">
              <DateTimePicker
                label="Data od"
                id="dateFrom"
                value={dateFrom}
                onChange={(e) => setDateFrom(e.target.value)}
              />
              <DateTimePicker
                label="Data do"
                id="dateTo"
                value={dateTo}
                onChange={(e) => setDateTo(e.target.value)}
              />
            </div>
            <div className="sm:w-1/2">
              <ListSelect
                label="Typ losowania"
                id="drawTypeIdWinning"
                value={drawTypeId}
                onChange={(e) => setDrawTypeId(e.target.value)}
                options={DRAW_TYPES.map((type) => ({
                  value: type.id.toString(),
                  label: type.name,
                }))}
                placeholder="Wszystkie"
              />
            </div>
          </div>
          {/* Second row: Group name, Search button */}
          <div className="flex flex-col sm:flex-row gap-4 mb-4">
            <div className="sm:w-1/2">
              <TextEdit
                label="Nazwa grupy"
                id="groupName"
                name="groupName"
                type="text"
                value={groupName}
                onChange={(e) => setGroupName(e.target.value)}
                placeholder="Filtruj po grupie..."
              />
            </div>
            <div className="sm:w-1/2">
              <label className="block text-sm font-medium text-gray-300 mb-2">
                &nbsp;
              </label>
              <ButtonPrimary
                className="whitespace-nowrap w-full"
                onClick={handleSearch}
                disabled={isLoading}
              >
                {isLoading ? "Szukam..." : "Szukaj"}
              </ButtonPrimary>
            </div>
          </div>

          {/* WinTier filters */}
          <div className="border-t border-gray-700/50 pt-4">
            <div className="flex items-center justify-between mb-3">
              <span className="text-gray-300 text-sm font-medium">
                Stopnie wygranych
              </span>
              <div className="flex gap-2">
                <button
                  onClick={selectAllWinTiers}
                  className="text-xs text-cyan-400 hover:text-cyan-300 transition-colors"
                >
                  Zaznacz wszystkie
                </button>
                <span className="text-gray-600">|</span>
                <button
                  onClick={selectNoneWinTiers}
                  className="text-xs text-cyan-400 hover:text-cyan-300 transition-colors"
                >
                  Odznacz wszystkie
                </button>
              </div>
            </div>
            <div className="flex flex-wrap gap-3">
              {WIN_TIERS.map((tier) => (
                <label
                  key={tier}
                  className="flex items-center gap-2 cursor-pointer"
                >
                  <input
                    type="checkbox"
                    checked={winTierFilters[tier]}
                    onChange={() => toggleWinTier(tier)}
                    className="w-4 h-4 border-gray-600 bg-gray-900 text-cyan-500 rounded focus:ring-cyan-500 focus:ring-offset-0"
                  />
                  <span className="text-gray-300 text-sm">{tier}</span>
                </label>
              ))}
            </div>
          </div>

          {/* Hide draws without matches filter */}
          <div className="border-t border-gray-700/50 pt-4">
            <label className="flex items-center gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={hideDrawsWithoutMatches}
                onChange={(e) => setHideDrawsWithoutMatches(e.target.checked)}
                className="w-4 h-4 border-gray-600 bg-gray-900 text-cyan-500 rounded focus:ring-cyan-500 focus:ring-offset-0"
              />
              <span className="text-gray-300 text-sm font-medium">
                Ukryj losowania bez dopasowań
              </span>
            </label>
          </div>
        </Card>

        {/* Error */}
        {error && (
          <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
            {error}
          </div>
        )}

        {/* Summary */}
        {summary && !isLoading && (
          <FormCard
            isVisible={isVisible}
            borderColor="cyan"
            className="delay-500"
          >
            <h3 className="text-white font-medium mb-4">
              Podsumowanie symulacji
            </h3>

            {/* Main stats */}
            <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
              <div className="bg-gray-900/50 rounded-xl p-4">
                <div className="text-gray-400 text-sm">Liczba losowań</div>
                <div className="text-2xl font-bold text-white">
                  {formatNumber(summary.totalDraws)}
                </div>
              </div>
              <div className="bg-gray-900/50 rounded-xl p-4">
                <div className="text-gray-400 text-sm">Liczba kuponów</div>
                <div className="text-2xl font-bold text-white">
                  {formatNumber(summary.totalTickets)}
                </div>
              </div>
              <div className="bg-gray-900/50 rounded-xl p-4">
                <div className="text-gray-400 text-sm">Suma zakładów</div>
                <div className="text-2xl font-bold text-white">
                  {formatNumber(summary.totalBets)}
                </div>
              </div>
              <div className="bg-gray-900/50 rounded-xl p-4">
                <div className="text-gray-400 text-sm">Wygrane zakłady</div>
                <div className="text-2xl font-bold text-cyan-400">
                  {formatNumber(summary.totalWinningBets)}
                </div>
              </div>
            </div>

            {/* Financial summary */}
            <div className="grid sm:grid-cols-3 gap-4 mb-6">
              <div className="bg-gradient-to-r from-orange-500/20 to-amber-500/20 rounded-xl p-4 border border-orange-500/30">
                <div className="text-orange-300 text-sm font-medium">
                  Koszty kuponów
                </div>
                <div className="text-2xl font-bold text-orange-400">
                  {formatCurrency(summary.totalCost)}
                </div>
              </div>
              <div className="bg-gradient-to-r from-green-500/20 to-emerald-500/20 rounded-xl p-4 border border-green-500/30">
                <div className="text-green-300 text-sm font-medium">
                  Suma wygranych
                </div>
                <div className="text-2xl font-bold text-green-400">
                  {formatCurrency(summary.totalWinPrize)}
                </div>
              </div>
              <div
                className={`bg-gradient-to-r ${summary.balance >= 0 ? "from-green-500/20 to-emerald-500/20 border-green-500/30" : "from-red-500/20 to-rose-500/20 border-red-500/30"} rounded-xl p-4 border`}
              >
                <div
                  className={`${summary.balance >= 0 ? "text-green-300" : "text-red-300"} text-sm font-medium`}
                >
                  Bilans
                </div>
                <div
                  className={`text-2xl font-bold ${summary.balance >= 0 ? "text-green-400" : "text-red-400"}`}
                >
                  {summary.balance >= 0 ? "+" : ""}
                  {formatCurrency(summary.balance)}
                </div>
              </div>
            </div>

            {/* Win tiers summary */}
            {summary.winsByTier.length > 0 && (
              <div>
                <h4 className="text-gray-300 font-medium mb-3">
                  Wygrane wg stopni
                </h4>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
                  {summary.winsByTier.map(
                    (tierSummary: LottoWinningTicketsWinTierSummary) => (
                      <div
                        key={tierSummary.winTier}
                        className="bg-gray-900/50 rounded-lg px-4 py-3"
                      >
                        <div className="text-gray-400 text-sm mb-1">
                          Stopien {tierSummary.winTier}
                        </div>
                        <div className="flex items-center justify-between gap-2">
                          <span className="text-cyan-400 font-semibold">
                            {tierSummary.winCount} szt.
                          </span>
                          <span className="text-green-400 font-semibold text-sm">
                            {formatCurrency(tierSummary.winPrize)}
                          </span>
                        </div>
                      </div>
                    ),
                  )}
                </div>
              </div>
            )}
          </FormCard>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin"></div>
          </div>
        )}

        {/* No Results */}
        {!isLoading && draws.length === 0 && !error && (
          <div className="text-center py-12 text-gray-400">
            Brak wynikow do wyswietlenia. Uzyj filtrow i kliknij "Szukaj".
          </div>
        )}

        {/* Results info and pagination */}
        {!isLoading && draws.length > 0 && (
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-4">
            <div className="text-gray-400 text-sm">
              Wyświetlono {draws.length} losowań (strona {currentPage} z{" "}
              {totalPages}, łącznie {formatNumber(totalCount)})
            </div>
            {totalPages > 1 && (
              <div className="flex items-center gap-2">
                <button
                  onClick={() => handlePageChange(1)}
                  disabled={currentPage === 1}
                  className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
                >
                  &laquo;
                </button>
                <button
                  onClick={() => handlePageChange(currentPage - 1)}
                  disabled={currentPage === 1}
                  className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
                >
                  &lsaquo;
                </button>
                <span className="px-3 py-1 text-sm text-gray-300">
                  {currentPage} / {totalPages}
                </span>
                <button
                  onClick={() => handlePageChange(currentPage + 1)}
                  disabled={currentPage === totalPages}
                  className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
                >
                  &rsaquo;
                </button>
                <button
                  onClick={() => handlePageChange(totalPages)}
                  disabled={currentPage === totalPages}
                  className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
                >
                  &raquo;
                </button>
              </div>
            )}
          </div>
        )}

        {/* Draws List */}
        {!isLoading && draws.length > 0 && (
          <div className="space-y-4">
            {draws.map((draw, index) => {
              const isExpanded = expandedDraws.has(draw.id);

              return (
                <CardListItem
                  key={draw.id}
                  isVisible={isVisible}
                  index={index}
                  delayBase={600}
                >
                  {/* Draw header */}
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    <div className="flex flex-col gap-2">
                      <div className="text-white font-medium">
                        {formatDate(draw.drawDate)}
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <span
                          className={`inline-block px-3 py-1 rounded-full text-xs font-semibold ${
                            draw.drawTypeId === 1
                              ? "bg-yellow-500/20 text-yellow-400"
                              : draw.drawTypeId === 2
                                ? "bg-orange-500/20 text-orange-400"
                                : draw.drawTypeId === 3
                                  ? "bg-green-500/20 text-green-400"
                                  : draw.drawTypeId === 4
                                    ? "bg-pink-500/20 text-pink-400"
                                    : draw.drawTypeId === 5
                                      ? "bg-purple-500/20 text-purple-400"
                                      : draw.drawTypeId === 6
                                        ? "bg-blue-500/20 text-blue-400"
                                        : draw.drawTypeId === 7
                                          ? "bg-red-500/20 text-red-400"
                                          : draw.drawTypeId === 8
                                            ? "bg-indigo-500/20 text-indigo-400"
                                            : draw.drawTypeId === 9
                                              ? "bg-fuchsia-500/20 text-fuchsia-400"
                                              : draw.drawTypeId === 10
                                                ? "bg-cyan-500/20 text-cyan-400"
                                                : "bg-slate-500/20 text-slate-400"
                          }`}
                        >
                          {getDrawTypeName(draw.drawTypeId)}
                        </span>
                        <span className="inline-block px-3 py-1 rounded-full text-xs font-semibold bg-gray-600/30 text-gray-400">
                          #{draw.drawSystemId}
                        </span>
                        {draw.matchingTickets.length > 0 && (
                          <span className="text-gray-400 text-sm">
                            (Kuponów: {draw.matchingTickets.length})
                          </span>
                        )}
                      </div>
                    </div>

                    {/* Draw numbers - sortowane: normalne, potem specjalne */}
                    <div className="flex flex-col gap-2">
                      {/* Normal numbers */}
                      {draw.numbers.length > 0 && (
                        <div className="grid grid-cols-10 gap-1.5">
                          {[...draw.numbers]
                            .sort((a, b) => a - b)
                            .map((num: number, numIndex: number) => (
                              <div
                                key={numIndex}
                                className={`w-8 h-8 rounded-full flex items-center justify-center text-gray-900 font-black text-xs shadow-lg ${getNumberColor(
                                  numIndex,
                                )}`}
                              >
                                {num}
                              </div>
                            ))}
                        </div>
                      )}
                      {/* Special numbers */}
                      {draw.specials.length > 0 && (
                        <div className="grid grid-cols-10 gap-1.5">
                          {[...draw.specials]
                            .sort((a, b) => a - b)
                            .map((num: number, numIndex: number) => (
                              <div
                                key={numIndex}
                                className={`w-8 h-8 rounded-full flex items-center justify-center text-gray-900 font-black text-xs shadow-lg ${getNumberColor(
                                  numIndex,
                                )} ring-2 ring-red-500 ring-offset-2 ring-offset-gray-800`}
                              >
                                {num}
                              </div>
                            ))}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Expand/Collapse tickets */}
                  {draw.matchingTickets.length > 0 && (
                    <div className="mt-4 pt-4 border-t border-gray-700/50">
                      <button
                        onClick={() => toggleExpandDraw(draw.id)}
                        className="flex items-center gap-1 text-sm text-cyan-400 hover:text-cyan-300 transition-colors py-1"
                      >
                        {isExpanded ? "Ukryj kupony" : "Pokaż kupony"}
                        <svg
                          className={`w-4 h-4 transition-transform duration-200 ${isExpanded ? "rotate-180" : ""}`}
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth={2}
                            d="M19 9l-7 7-7-7"
                          />
                        </svg>
                      </button>

                      {isExpanded && (
                        <div className="mt-4 space-y-3">
                          {draw.matchingTickets.map(
                            (ticket: LottoWinningTicketsMatchingTicket) => (
                              <div
                                key={ticket.id}
                                className="bg-gray-900/50 rounded-lg p-4"
                              >
                                <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4">
                                  {/* Left side - Tags and win info */}
                                  <div className="flex flex-col gap-2">
                                    {ticket.groupName && (
                                      <div className="text-cyan-400 font-medium">
                                        {ticket.groupName}
                                      </div>
                                    )}
                                    <div className="flex flex-wrap items-center gap-2">
                                      {ticket.winTier > 0 && (
                                        <>
                                          <span className="px-2 py-1 bg-green-500/20 text-green-400 rounded-full text-xs font-semibold">
                                            Wygrana {ticket.winTier} stopnia
                                          </span>
                                          {ticket.winPrize > 0 && (
                                            <span className="px-2 py-1 bg-yellow-500/20 text-yellow-400 rounded-full text-xs font-semibold">
                                              {formatCurrency(ticket.winPrize)}
                                            </span>
                                          )}
                                          <span className="px-2 py-1 bg-cyan-500/20 text-cyan-400 rounded-full text-xs">
                                            Trafień:{" "}
                                            {ticket.matchedNumbers.length}
                                          </span>
                                          {ticket.matchedSpecials.length >
                                            0 && (
                                            <span className="px-2 py-1 bg-fuchsia-500/20 text-fuchsia-400 rounded-full text-xs font-semibold">
                                              {ticket.drawTypeId === 9
                                                ? "Plus!"
                                                : `Specjalne: ${ticket.matchedSpecials.length}`}
                                            </span>
                                          )}
                                        </>
                                      )}
                                      {ticket.winTier === 0 &&
                                        (ticket.matchedNumbers.length > 0 ||
                                          ticket.matchedSpecials.length >
                                            0) && (
                                          <>
                                            {ticket.matchedNumbers.length >
                                              0 && (
                                              <span className="px-2 py-1 bg-gray-600/50 text-gray-400 rounded-full text-xs">
                                                {ticket.matchedNumbers.length}{" "}
                                                trafien
                                              </span>
                                            )}
                                            {ticket.matchedSpecials.length >
                                              0 && (
                                              <span className="px-2 py-1 bg-fuchsia-500/20 text-fuchsia-400 rounded-full text-xs">
                                                {ticket.drawTypeId === 9
                                                  ? "Plus!"
                                                  : `Specjalne: ${ticket.matchedSpecials.length}`}
                                              </span>
                                            )}
                                          </>
                                        )}
                                    </div>
                                  </div>

                                  {/* Right side - Numbers */}
                                  <div className="flex flex-col gap-2">
                                    {/* Normal numbers */}
                                    {ticket.numbers.length > 0 && (
                                      <div className="grid grid-cols-10 gap-1.5 justify-items-end">
                                        {[...ticket.numbers]
                                          .sort((a, b) => a - b)
                                          .map((num, numIndex) => {
                                            const isMatched =
                                              ticket.matchedNumbers.includes(
                                                num,
                                              );
                                            // MultiMulti: matchedSpecials zawiera numer z Numbers który trafił Plus
                                            const isPlus =
                                              ticket.drawTypeId === 9 &&
                                              ticket.matchedSpecials.includes(
                                                num,
                                              );
                                            return (
                                              <div
                                                key={numIndex}
                                                className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-sm shadow-md ${
                                                  isPlus
                                                    ? "bg-fuchsia-500 text-white ring-2 ring-fuchsia-300"
                                                    : isMatched
                                                      ? "bg-yellow-500 text-gray-900"
                                                      : "bg-gray-600 text-gray-200"
                                                }`}
                                                title={
                                                  isPlus ? "Plus!" : undefined
                                                }
                                              >
                                                {num}
                                              </div>
                                            );
                                          })}
                                      </div>
                                    )}
                                    {/* Special numbers - nie dotyczy MultiMulti (specials kuponu są puste) */}
                                    {ticket.specials.length > 0 && (
                                      <div className="grid grid-cols-10 gap-1.5 justify-items-end">
                                        {[...ticket.specials]
                                          .sort((a, b) => a - b)
                                          .map((num, numIndex) => {
                                            const isMatched =
                                              ticket.matchedSpecials.includes(
                                                num,
                                              );
                                            return (
                                              <div
                                                key={numIndex}
                                                className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-sm shadow-md ${
                                                  isMatched
                                                    ? "bg-yellow-500 text-gray-900"
                                                    : "bg-gray-600 text-gray-200"
                                                } ring-2 ring-red-500 ring-offset-2 ring-offset-gray-900`}
                                              >
                                                {num}
                                              </div>
                                            );
                                          })}
                                      </div>
                                    )}
                                  </div>
                                </div>
                              </div>
                            ),
                          )}
                        </div>
                      )}
                    </div>
                  )}

                  {/* No tickets message */}
                  {draw.matchingTickets.length === 0 && (
                    <div className="mt-4 pt-4 border-t border-gray-700/50 text-center text-gray-500 text-sm">
                      Brak kuponów pasujących do tego losowania
                    </div>
                  )}

                  {/* Prizes toggle button */}
                  <div className="mt-3 pt-3 border-t border-gray-700/50">
                    <button
                      onClick={() => togglePrizes(draw)}
                      className="flex items-center gap-1 text-sm text-cyan-400 hover:text-cyan-300 transition-colors py-1"
                    >
                      {expandedPrizes.has(draw.id)
                        ? "Ukryj nagrody"
                        : "Pokaż nagrody"}
                      <svg
                        className={`w-4 h-4 transition-transform ${expandedPrizes.has(draw.id) ? "rotate-180" : ""}`}
                        fill="none"
                        stroke="currentColor"
                        viewBox="0 0 24 24"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={2}
                          d="M19 9l-7 7-7-7"
                        />
                      </svg>
                    </button>

                    {/* Prizes section */}
                    {expandedPrizes.has(draw.id) && (
                      <div className="mt-3">
                        {loadingPrizes.has(draw.id) ? (
                          <div className="flex items-center gap-2 text-gray-400 text-sm">
                            <div className="w-4 h-4 border-2 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin"></div>
                            Ladowanie nagrod...
                          </div>
                        ) : prizesCache.has(
                            getCacheKey(draw.drawTypeId, draw.drawSystemId),
                          ) ? (
                          <div className="bg-gray-900/50 rounded-lg p-3">
                            <div className="grid grid-cols-3 gap-2 text-xs font-semibold text-gray-400 mb-2 pb-2 border-b border-gray-700/50">
                              <div>Wygrana n stopnia</div>
                              <div className="text-right">Ilosc</div>
                              <div className="text-right">Kwota</div>
                            </div>
                            {prizesCache
                              .get(
                                getCacheKey(draw.drawTypeId, draw.drawSystemId),
                              )!
                              .winTiers.map((tier, tierIndex) => (
                                <div
                                  key={tierIndex}
                                  className="grid grid-cols-3 gap-2 text-sm py-1"
                                >
                                  <div className="text-gray-300">
                                    {tier.tier}
                                  </div>
                                  <div className="text-right text-gray-400">
                                    {tier.winsCount.toLocaleString("pl-PL")}
                                  </div>
                                  <div className="text-right text-amber-400 font-medium">
                                    {tier.winsPrize.toLocaleString("pl-PL", {
                                      minimumFractionDigits: 2,
                                      maximumFractionDigits: 2,
                                    })}{" "}
                                    zl
                                  </div>
                                </div>
                              ))}
                          </div>
                        ) : (
                          <div className="text-gray-500 text-sm">
                            Brak danych o nagrodach
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </CardListItem>
              );
            })}
          </div>
        )}

        {/* Bottom pagination */}
        {!isLoading && draws.length > 0 && totalPages > 1 && (
          <div className="flex justify-center mt-6">
            <div className="flex items-center gap-2">
              <button
                onClick={() => handlePageChange(1)}
                disabled={currentPage === 1}
                className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
              >
                &laquo;
              </button>
              <button
                onClick={() => handlePageChange(currentPage - 1)}
                disabled={currentPage === 1}
                className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
              >
                &lsaquo;
              </button>
              <span className="px-3 py-1 text-sm text-gray-300">
                Strona {currentPage} z {totalPages}
              </span>
              <button
                onClick={() => handlePageChange(currentPage + 1)}
                disabled={currentPage === totalPages}
                className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
              >
                &rsaquo;
              </button>
              <button
                onClick={() => handlePageChange(totalPages)}
                disabled={currentPage === totalPages}
                className="px-3 py-1 text-sm bg-gray-700 hover:bg-gray-600 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded transition-colors"
              >
                &raquo;
              </button>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

export default LottoWinningTicketsPage;
