import { useEffect, useState, useRef } from 'react';
import { useSearchParams } from 'react-router';
import { getIsAdminFromToken } from '../../utils/jwt';
import { ApiLottoService } from '../../services/api-lotto-service';
import type { LottoDrawsGetListDraw } from '../../services/contracts/lotto-draws-get-list-response';
import type { LottoDrawsImportRequest } from '../../services/contracts/lotto-draws-import-request';
import type { LottoDrawsGetPrizesListResponse } from '../../services/contracts/lotto-draws-get-prizes-list-response';
import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import timezone from "dayjs/plugin/timezone";
import ButtonPrimary from '../../components/ButtonPrimary';
import ButtonSecondary from '../../components/ButtonSecondary';
import ButtonEdit from '../../components/ButtonEdit';
import ButtonDelete from '../../components/ButtonDelete';
import DateTimePicker from '../../components/DateTimePicker';
import ListSelect from '../../components/ListSelect';
import Card from '../../components/Card';
import CardListItem from '../../components/CardListItem';
import SubMenu from '../../components/SubMenu';
import ConfirmModal from '../../components/ConfirmModal';
import FormCard from '../../components/FormCard';

dayjs.extend(utc);
dayjs.extend(timezone);

const DRAW_TYPES = [
  { id: 1, name: 'Lotto', numbersCount: 6 },
  { id: 2, name: 'Lotto Plus', numbersCount: 6 },
  { id: 3, name: 'Mini Lotto', numbersCount: 5 },
  { id: 4, name: 'Ekstra Pensja', numbersCount: 5 },
  { id: 5, name: 'Ekstra Premia', numbersCount: 5 },
  { id: 6, name: 'EuroJackpot', numbersCount: 5 },
  { id: 7, name: 'Szybkie600', numbersCount: 6 },
  { id: 8, name: 'Kaskada', numbersCount: 24 },
  { id: 9, name: 'MultiMulti', numbersCount: 20 },
  { id: 10, name: 'Keno', numbersCount: 20 },
];


function LottoDrawsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [isVisible, setIsVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [draws, setDraws] = useState<LottoDrawsGetListDraw[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [isAdmin, setIsAdmin] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [importSuccess, setImportSuccess] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Prizes section state
  const [expandedPrizes, setExpandedPrizes] = useState<Set<number>>(new Set());
  const [prizesCache, setPrizesCache] = useState<Map<string, LottoDrawsGetPrizesListResponse>>(new Map());
  const [loadingPrizes, setLoadingPrizes] = useState<Set<number>>(new Set());

  // Import modal state
  const [showImportModal, setShowImportModal] = useState(false);
  const [importDrawTypeId, setImportDrawTypeId] = useState<string>('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  // Add/Edit draw form state
  const [showAddDrawForm, setShowAddDrawForm] = useState(false);
  const [editingDrawId, setEditingDrawId] = useState<number | null>(null);
  const [newDrawDrawTypeId, setNewDrawDrawTypeId] = useState<number>(1);
  const [newDrawSystemId, setNewDrawSystemId] = useState<string>('');
  const [newDrawDate, setNewDrawDate] = useState<string>('');
  const [newDrawNumbers, setNewDrawNumbers] = useState<number[]>([]);
  const [newDrawSpecials, setNewDrawSpecials] = useState<number[]>([]);
  const [isAddingDraw, setIsAddingDraw] = useState(false);

  // Delete draw state
  const [deletingDrawId, setDeletingDrawId] = useState<number | null>(null);
  const [drawToDelete, setDrawToDelete] = useState<number | null>(null);


  // Filters state
  const formatLocalDateTime = (date: Date) => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
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

  const [dateFrom, setDateFrom] = useState(searchParams.get('dateFrom') || getDefaultDateFrom());
  const [dateTo, setDateTo] = useState(searchParams.get('dateTo') || getDefaultDateTo());
  const [drawTypeId, setDrawTypeId] = useState<string>(searchParams.get('drawTypeId') || '');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>((searchParams.get('sortOrder') as 'asc' | 'desc') || 'desc');
  const [page, setPage] = useState(parseInt(searchParams.get('page') || '1', 10));
  const pageSize = 10;

  useEffect(() => {
    document.title = 'Wyniki losowan | Lotto | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);

    // Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
    setIsAdmin(getIsAdminFromToken());

    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    fetchDraws();
  }, [page]);

  // Reset results when filters change
  useEffect(() => {
    setDraws([]);
    setTotalCount(0);
    setTotalPages(0);
    setError(null);
  }, [dateFrom, dateTo, drawTypeId, sortOrder]);

  const fetchDraws = async () => {
    const token = localStorage.getItem('token');
    if (!token) return;

    setIsLoading(true);
    setError(null);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiLottoService.setUsrToken(token);

      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;

      const response = await apiLottoService.lottoDrawsGetList({
        drawDateFrom: (dateFrom ? dayjs.tz(dateFrom, tz).utc().format() : undefined),
        drawDateTo: (dateTo ? dayjs.tz(dateTo, tz).utc().format() : undefined),
        drawTypeId: drawTypeId ? parseInt(drawTypeId, 10) : undefined,
        page,
        pageSize,
        sortOrder,
      });

      setDraws(response.draws);
      setTotalCount(response.totalCount);
      setTotalPages(response.totalPages);
      setExpandedPrizes(new Set());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Wystapil blad podczas pobierania wynikow');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    const params = new URLSearchParams();
    if (dateFrom) params.set('dateFrom', dateFrom);
    if (dateTo) params.set('dateTo', dateTo);
    if (drawTypeId) params.set('drawTypeId', drawTypeId);
    params.set('sortOrder', sortOrder);
    params.set('page', '1');
    setSearchParams(params);
    fetchDraws();
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    const params = new URLSearchParams(searchParams);
    params.set('page', newPage.toString());
    setSearchParams(params);
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');
    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
  };

  const getDrawTypeName = (typeId: number) => {
    const type = DRAW_TYPES.find(t => t.id === typeId);
    return type?.name || `Typ ${typeId}`;
  };

  const getNumberColor = (index: number) => {
    // Wszystkie kulki maja jednolity zlotawy kolor
    return index % 2 === 0 ? 'bg-amber-500' : 'bg-amber-400';
  };

  const getCacheKey = (drawTypeId: number, drawSystemId: number) => `${drawTypeId}-${drawSystemId}`;

  const togglePrizes = async (draw: LottoDrawsGetListDraw) => {
    const drawId = draw.id;
    const cacheKey = getCacheKey(draw.drawTypeId, draw.drawSystemId);

    // If already expanded, just collapse
    if (expandedPrizes.has(drawId)) {
      setExpandedPrizes(prev => {
        const next = new Set(prev);
        next.delete(drawId);
        return next;
      });
      return;
    }

    // Expand the section
    setExpandedPrizes(prev => new Set(prev).add(drawId));

    // If we already have cached data, no need to fetch
    if (prizesCache.has(cacheKey)) {
      return;
    }

    // Fetch prizes data
    const token = localStorage.getItem('token');
    if (!token) return;

    setLoadingPrizes(prev => new Set(prev).add(drawId));

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiLottoService.setUsrToken(token);

      const response = await apiLottoService.lottoDrawsGetPrizesList({
        drawTypeId: draw.drawTypeId,
        drawSystemId: draw.drawSystemId,
      });

      setPrizesCache(prev => new Map(prev).set(cacheKey, response));
    } catch (err) {
      console.error('Error fetching prizes:', err);
    } finally {
      setLoadingPrizes(prev => {
        const next = new Set(prev);
        next.delete(drawId);
        return next;
      });
    }
  };

  const handleDrawNumberToggle = (num: number) => {
    const drawType = DRAW_TYPES.find(dt => dt.id === newDrawDrawTypeId);
    const maxNumbers = drawType?.numbersCount || 6;

    if (newDrawNumbers.includes(num)) {
      setNewDrawNumbers(newDrawNumbers.filter(n => n !== num));
    } else if (newDrawNumbers.length < maxNumbers) {
      setNewDrawNumbers([...newDrawNumbers, num]);
    }
  };

  const handleAddDraw = async () => {
    setError(null);
    setSuccess(null);

    const isEditing = editingDrawId !== null;

    const drawType = DRAW_TYPES.find(dt => dt.id === newDrawDrawTypeId);
    const requiredNumbers = drawType?.numbersCount || 6;

    if (newDrawNumbers.length !== requiredNumbers) {
      setError(`Wybierz dokładnie ${requiredNumbers} numerów`);
      return;
    }

    if (!newDrawSystemId || parseInt(newDrawSystemId) <= 0) {
      setError('Podaj prawidłowy numer systemowy losowania');
      return;
    }

    if (!newDrawDate) {
      setError('Wybierz datę i godzinę losowania');
      return;
    }

    const token = localStorage.getItem('token');
    if (!token) return;

    setIsAddingDraw(true);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiLottoService.setUsrToken(token);

      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
      const drawDateUtc = dayjs.tz(newDrawDate, tz).utc().format();

      if (isEditing) {
        await apiLottoService.lottoDrawsUpdate({
          id: editingDrawId,
          drawSystemId: parseInt(newDrawSystemId),
          drawDate: drawDateUtc,
          drawTypeId: newDrawDrawTypeId,
          numbers: newDrawNumbers,
          specials: newDrawSpecials,
        });
        setSuccess('Wynik losowania został zaktualizowany');
      } else {
        await apiLottoService.lottoDrawsAdd({
          drawSystemId: parseInt(newDrawSystemId),
          drawDate: drawDateUtc,
          drawTypeId: newDrawDrawTypeId,
          numbers: newDrawNumbers,
          specials: newDrawSpecials,
        });
        setSuccess('Wynik losowania został dodany');
      }

      setShowAddDrawForm(false);
      setNewDrawNumbers([]);
      setNewDrawSpecials([]);
      setNewDrawSystemId('');
      setNewDrawDate('');
      setEditingDrawId(null);
      fetchDraws();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Wystąpił błąd podczas zapisywania wyniku');
    } finally {
      setIsAddingDraw(false);
    }
  };

  const handleDeleteDraw = (drawId: number) => {
    setDrawToDelete(drawId);
  };

  const confirmDeleteDraw = async () => {
    if (drawToDelete === null) return;

    setError(null);
    setSuccess(null);
    setDrawToDelete(null);

    const token = localStorage.getItem('token');
    if (!token) return;

    setDeletingDrawId(drawToDelete);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiLottoService.setUsrToken(token);

      await apiLottoService.lottoDrawsDelete({ drawId: drawToDelete });

      setSuccess('Wynik losowania został usunięty');
      fetchDraws();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Wystąpił błąd podczas usuwania wyniku');
    } finally {
      setDeletingDrawId(null);
    }
  };

  const handleEditDraw = (draw: LottoDrawsGetListDraw) => {
    setEditingDrawId(draw.id);
    setNewDrawDrawTypeId(draw.drawTypeId);
    setNewDrawSystemId(draw.drawSystemId.toString());

    // Convert UTC date to local datetime-local format
    const localDate = new Date(draw.drawDate);
    const year = localDate.getFullYear();
    const month = String(localDate.getMonth() + 1).padStart(2, '0');
    const day = String(localDate.getDate()).padStart(2, '0');
    const hours = String(localDate.getHours()).padStart(2, '0');
    const minutes = String(localDate.getMinutes()).padStart(2, '0');
    setNewDrawDate(`${year}-${month}-${day}T${hours}:${minutes}`);

    setNewDrawNumbers([...draw.numbers]);
    setNewDrawSpecials([...draw.specials]);
    setShowAddDrawForm(true);
  };

  const handleExport = async () => {
    if (!drawTypeId) {
      setError('Wybierz typ losowania w filtrach aby eksportowac');
      return;
    }

    const token = localStorage.getItem('token');
    if (!token) return;

    setIsExporting(true);
    setError(null);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiLottoService.setUsrToken(token);

      const response = await apiLottoService.lottoDrawsExport({
        drawTypeId: parseInt(drawTypeId, 10),
        drawDateFrom: dateFrom || undefined,
        drawDateTo: dateTo || undefined,
      });

      const blob = new Blob([response.csv], { type: 'text/csv;charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = response.fileName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      setImportSuccess(`Wyeksportowano ${response.totalCount} losowan do pliku ${response.fileName}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Wystapil blad podczas eksportu');
    } finally {
      setIsExporting(false);
    }
  };

  const handleImportClick = () => {
    setShowImportModal(true);
    setSelectedFile(null);
    setImportDrawTypeId('');
  };

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      setSelectedFile(file);
    }
  };

  const getFileExtension = (filename: string): string => {
    return filename.split('.').pop()?.toLowerCase() || '';
  };

  const handleImportSubmit = async () => {
    if (!selectedFile) return;

    const token = localStorage.getItem('token');
    if (!token) return;

    setIsImporting(true);
    setError(null);
    setImportSuccess(null);

    try {
      const fileContent = await selectedFile.text();
      const fileExt = getFileExtension(selectedFile.name);

      let importRequest: LottoDrawsImportRequest;

      if (fileExt === 'json') {
        // JSON file - parse and use draws array
        const data = JSON.parse(fileContent);
        importRequest = {
          draws: data.draws || data,
        };
      } else {
        // CSV or TXT file - send as raw CSV
        if (!importDrawTypeId) {
          setError('Dla plikow CSV/TXT wymagany jest wybor typu losowania');
          setIsImporting(false);
          return;
        }
        importRequest = {
          csv: fileContent,
          drawTypeId: parseInt(importDrawTypeId, 10),
        };
      }

      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiLottoService.setUsrToken(token);

      const response = await apiLottoService.lottoDrawsImport(importRequest);

      const successMessage = `Zapisano ${response.savedCount} losowan do pliku CSV (do przetworzenia przez Worker).`;
      setImportSuccess(successMessage);

      if (response.errors.length > 0) {
        const errorList = response.errors.slice(0, 10).join('\n');
        const moreCount = response.errors.length > 10 ? `\n...i ${response.errors.length - 10} wiecej` : '';
        setError(errorList + moreCount);
      }

      setShowImportModal(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Wystapil blad podczas importu');
    } finally {
      setIsImporting(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const isCsvOrTxtFile = selectedFile ? ['csv', 'txt'].includes(getFileExtension(selectedFile.name)) : false;

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16 overflow-hidden">
      <div className="max-w-4xl mx-auto w-full">
        {/* Header */}
        <div className="text-center mb-8">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Wyniki losowań
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Przeglądaj historię losowań Lotto, Lotto Plus, Mini Lotto... itp.
          </p>
        </div>

        {/* Navigation submenu */}
        <SubMenu
          backPath="/lotto"
          isVisible={isVisible}
          items={[
            { label: 'Wyniki losowań', path: '/lotto/draws' },
            { label: 'Moje kupony', path: '/lotto/tickets' },
            { label: 'Sprawdź wygrane', path: '/lotto/winning-tickets' },
            { label: 'Statystyki grup', path: '/lotto/draws-numbers-stats' },
          ]}
        />

        {/* Filters */}
        <Card isVisible={isVisible} className="mb-8 delay-300">
          {/* Row 1: Date from/to */}
          <div className="grid grid-cols-2 gap-4 mb-4">
            <DateTimePicker
              label="Data losowania od"
              id="dateFrom"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
            />
            <DateTimePicker
              label="Data losowania do"
              id="dateTo"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
            />
          </div>

          {/* Row 2: Draw type, Sort order */}
          <div className="grid grid-cols-2 gap-4 mb-4">
            <ListSelect
              label="Typ losowania"
              id="drawTypeId"
              value={drawTypeId}
              onChange={(e) => setDrawTypeId(e.target.value)}
              options={DRAW_TYPES.map((type) => ({ value: type.id.toString(), label: type.name }))}
              placeholder="Wszystkie"
            />
            <ListSelect
              label="Sortowanie"
              id="sortOrder"
              value={sortOrder}
              onChange={(e) => setSortOrder(e.target.value as 'asc' | 'desc')}
              options={[
                { value: 'desc', label: 'Malejaco' },
                { value: 'asc', label: 'Rosnaco' },
              ]}
            />
          </div>

          {/* Row 3: Add, Import, Export (left) | gap | Search (right) */}
          <div className="flex items-end justify-between gap-4">
            <div className="flex gap-4">
              {isAdmin && (
                <>
                  <ButtonSecondary
                    onClick={() => {
                      if (showAddDrawForm) {
                        setShowAddDrawForm(false);
                        setEditingDrawId(null);
                        setNewDrawNumbers([]);
                        setNewDrawSpecials([]);
                        setNewDrawSystemId('');
                        setNewDrawDate('');
                      } else {
                        setShowAddDrawForm(true);
                        setEditingDrawId(null);
                        setNewDrawNumbers([]);
                        setNewDrawSpecials([]);
                        setNewDrawSystemId('');
                        setNewDrawDate('');
                      }
                    }}
                  >
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    {showAddDrawForm ? 'Anuluj' : 'Dodaj wynik'}
                  </ButtonSecondary>
                  <ButtonSecondary onClick={handleImportClick} disabled={isImporting}>
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                    </svg>
                    {isImporting ? 'Importowanie...' : 'Importuj'}
                  </ButtonSecondary>
                  <ButtonSecondary onClick={handleExport} disabled={isExporting}>
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                    </svg>
                    {isExporting ? 'Eksportowanie...' : 'Eksportuj'}
                  </ButtonSecondary>
                </>
              )}
            </div>
            <ButtonPrimary onClick={handleSearch} disabled={isLoading}>
              {isLoading ? 'Szukam...' : 'Szukaj'}
            </ButtonPrimary>
          </div>
        </Card>

        {/* Add/Edit draw form */}
        {showAddDrawForm && isAdmin && (
          <FormCard isVisible={isVisible} borderColor="green">
            <h2 className="text-xl font-bold text-white mb-4">
              {editingDrawId !== null ? 'Edytuj wynik losowania' : 'Nowy wynik losowania'}
            </h2>

            <div className="grid sm:grid-cols-3 gap-4 mb-6">
              <ListSelect
                label="Typ losowania"
                id="newDrawDrawTypeId"
                value={newDrawDrawTypeId.toString()}
                onChange={(e) => {
                  setNewDrawDrawTypeId(parseInt(e.target.value, 10));
                  setNewDrawNumbers([]);
                  setNewDrawSpecials([]);
                }}
                options={DRAW_TYPES.map((type) => ({ value: type.id.toString(), label: type.name }))}
              />
              <div>
                <label htmlFor="newDrawSystemId" className="block text-gray-300 text-sm font-medium mb-2">
                  Numer systemowy
                </label>
                <input
                  id="newDrawSystemId"
                  name="newDrawSystemId"
                  type="number"
                  value={newDrawSystemId}
                  onChange={(e) => setNewDrawSystemId(e.target.value)}
                  placeholder="np. 1234"
                  min={1}
                  className="w-full px-4 py-2.5 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
                />
              </div>
              <DateTimePicker
                label="Data i godzina losowania"
                id="newDrawDate"
                value={newDrawDate}
                onChange={(e) => setNewDrawDate(e.target.value)}
              />
            </div>

            {(() => {
              const currentDrawType = DRAW_TYPES.find(dt => dt.id === newDrawDrawTypeId);
              const maxNumber = currentDrawType?.numbersCount === 24 ? 24 :
                               currentDrawType?.numbersCount === 20 ? 80 :
                               currentDrawType?.numbersCount === 10 ? 70 :
                               currentDrawType?.numbersCount === 12 ? 24 :
                               currentDrawType?.numbersCount === 5 && currentDrawType.id === 6 ? 50 :
                               currentDrawType?.numbersCount === 5 && currentDrawType.id === 3 ? 42 :
                               currentDrawType?.numbersCount === 5 ? 35 :
                               currentDrawType?.numbersCount === 6 && currentDrawType.id === 7 ? 32 :
                               49;
              const requiredNumbers = currentDrawType?.numbersCount || 6;

              return (
                <>
                  <div className="mb-4">
                    <label className="block text-gray-300 text-sm font-medium mb-2 text-center">
                      Wybierz {requiredNumbers} numerów ({newDrawNumbers.length}/{requiredNumbers})
                    </label>
                    <div className="flex justify-center">
                      <div className="grid grid-cols-10 gap-2">
                        {Array.from({ length: maxNumber }, (_, i) => i + 1).map((num) => (
                          <button
                            key={num}
                            onClick={() => handleDrawNumberToggle(num)}
                            className={`w-8 h-8 rounded-full flex items-center justify-center font-black text-xs transition-all duration-200 ${
                              newDrawNumbers.includes(num)
                                ? 'bg-amber-500 text-gray-900 shadow-lg shadow-amber-500/50'
                                : 'bg-gray-700/50 text-gray-300 hover:bg-gray-600/50'
                            }`}
                          >
                            {num}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>

                  {newDrawNumbers.length > 0 && (
                    <div className="mb-4 p-3 bg-gray-900/50 rounded-xl">
                      <span className="text-gray-400 text-sm">Wybrane numery: </span>
                      <span className="text-amber-400 font-bold">
                        {[...newDrawNumbers].sort((a, b) => a - b).join(', ')}
                      </span>
                    </div>
                  )}
                </>
              );
            })()}

            <div className="flex justify-between gap-3">
              <ButtonSecondary
                onClick={() => {
                  setShowAddDrawForm(false);
                  setEditingDrawId(null);
                  setNewDrawNumbers([]);
                  setNewDrawSpecials([]);
                  setNewDrawSystemId('');
                  setNewDrawDate('');
                }}
              >
                Anuluj
              </ButtonSecondary>
              <ButtonPrimary
                onClick={handleAddDraw}
                disabled={isAddingDraw || newDrawNumbers.length !== (DRAW_TYPES.find(dt => dt.id === newDrawDrawTypeId)?.numbersCount || 6) || !newDrawSystemId || !newDrawDate}
              >
                {isAddingDraw ? 'Zapisywanie...' : (editingDrawId !== null ? 'Zapisz zmiany' : 'Dodaj wynik')}
              </ButtonPrimary>
            </div>
          </FormCard>
        )}

        {/* Error */}
        {error && (
          <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm max-h-64 overflow-y-auto">
            <div className="font-semibold mb-2">Bledy:</div>
            <pre className="whitespace-pre-wrap font-mono text-xs">{error}</pre>
          </div>
        )}

        {/* Success */}
        {success && (
          <div className="mb-6 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
            {success}
          </div>
        )}

        {/* Import Success */}
        {importSuccess && (
          <div className="mb-6 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
            {importSuccess}
          </div>
        )}

        {/* Results info */}
        {!isLoading && draws.length > 0 && (
          <div
            className={`text-gray-400 text-sm mb-4 transition-all duration-500 ${
              isVisible ? 'opacity-100' : 'opacity-0'
            }`}
          >
            Znaleziono {totalCount} wynikow. Strona {page} z {totalPages}.
          </div>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin"></div>
          </div>
        )}

        {/* Results */}
        {!isLoading && draws.length === 0 && !error && (
          <div className="text-center py-12 text-gray-400">
            Brak wynikow do wyswietlenia. Uzyj filtrow i kliknij "Szukaj".
          </div>
        )}

        {!isLoading && draws.length > 0 && (
          <div className="space-y-4">
            {draws.map((draw, index) => (
              <CardListItem key={draw.id} isVisible={isVisible} index={index}>
                <div className="flex flex-col gap-4">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    {/* Draw info */}
                    <div className="flex flex-col gap-2">
                      <div className="text-white font-medium">
                        {formatDate(draw.drawDate)}
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <span
                          className={`inline-block px-3 py-1 rounded-full text-xs font-semibold ${
                            draw.drawTypeId === 1 ? 'bg-yellow-500/20 text-yellow-400' :
                            draw.drawTypeId === 2 ? 'bg-orange-500/20 text-orange-400' :
                            draw.drawTypeId === 3 ? 'bg-green-500/20 text-green-400' :
                            draw.drawTypeId === 4 ? 'bg-pink-500/20 text-pink-400' :
                            draw.drawTypeId === 5 ? 'bg-purple-500/20 text-purple-400' :
                            draw.drawTypeId === 6 ? 'bg-blue-500/20 text-blue-400' :
                            draw.drawTypeId === 7 ? 'bg-red-500/20 text-red-400' :
                            draw.drawTypeId === 8 ? 'bg-indigo-500/20 text-indigo-400' :
                            draw.drawTypeId === 9 ? 'bg-fuchsia-500/20 text-fuchsia-400' :
                            draw.drawTypeId === 10 ? 'bg-cyan-500/20 text-cyan-400' :
                            'bg-slate-500/20 text-slate-400'
                          }`}
                        >
                          {getDrawTypeName(draw.drawTypeId)}
                        </span>
                        <span className="inline-block px-3 py-1 rounded-full text-xs font-semibold bg-gray-600/30 text-gray-400">
                          #{draw.drawSystemId}
                        </span>
                      </div>
                    </div>

                    {/* Numbers and Edit button */}
                    <div className="flex items-center gap-3">
                      <div className="flex flex-col gap-2">
                        {/* Normal numbers */}
                        {draw.numbers.length > 0 && (
                          <div className="grid grid-cols-10 gap-1.5 justify-items-end">
                            {[...draw.numbers]
                              .sort((a, b) => a - b)
                              .map((num: number, numIndex: number) => (
                                <div
                                  key={numIndex}
                                  className={`w-8 h-8 rounded-full flex items-center justify-center text-gray-900 font-black text-xs shadow-lg ${getNumberColor(
                                    numIndex
                                  )}`}
                                >
                                  {num}
                                </div>
                              ))}
                          </div>
                        )}
                        {/* Special numbers */}
                        {draw.specials.length > 0 && (
                          <div className="grid grid-cols-10 gap-1.5 justify-items-end">
                            {[...draw.specials]
                              .sort((a, b) => a - b)
                              .map((num: number, numIndex: number) => (
                                <div
                                  key={numIndex}
                                  className={`w-8 h-8 rounded-full flex items-center justify-center text-gray-900 font-black text-xs shadow-lg ${getNumberColor(
                                    numIndex
                                  )} ring-2 ring-red-500 ring-offset-2 ring-offset-gray-800`}
                                >
                                  {num}
                                </div>
                              ))}
                          </div>
                        )}
                      </div>

                      {/* Edit and Delete buttons (admin only) */}
                      {isAdmin && (
                        <div className="hidden md:flex gap-2">
                          <ButtonEdit onClick={() => handleEditDraw(draw)} />
                          <ButtonDelete
                            onClick={() => handleDeleteDraw(draw.id)}
                            disabled={deletingDrawId === draw.id}
                            isLoading={deletingDrawId === draw.id}
                          />
                        </div>
                      )}
                    </div>
                  </div>

                {/* Prizes toggle button */}
                <div className="mt-3 pt-3 border-t border-gray-700/50">
                  <button
                    onClick={() => togglePrizes(draw)}
                    className="flex items-center gap-1 text-sm text-cyan-400 hover:text-cyan-300 transition-colors py-1"
                  >
                    {expandedPrizes.has(draw.id) ? 'Ukryj nagrody' : 'Pokaż nagrody'}
                    <svg
                      className={`w-4 h-4 transition-transform ${expandedPrizes.has(draw.id) ? 'rotate-180' : ''}`}
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
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
                      ) : prizesCache.has(getCacheKey(draw.drawTypeId, draw.drawSystemId)) ? (
                        <div className="bg-gray-900/50 rounded-lg p-3">
                          <div className="grid grid-cols-3 gap-2 text-xs font-semibold text-gray-400 mb-2 pb-2 border-b border-gray-700/50">
                            <div>Wygrana n stopnia</div>
                            <div className="text-right">Ilosc</div>
                            <div className="text-right">Kwota</div>
                          </div>
                          {prizesCache.get(getCacheKey(draw.drawTypeId, draw.drawSystemId))!.winTiers.map((tier, tierIndex) => (
                            <div key={tierIndex} className="grid grid-cols-3 gap-2 text-sm py-1">
                              <div className="text-gray-300">{tier.tier}</div>
                              <div className="text-right text-gray-400">{tier.winsCount.toLocaleString('pl-PL')}</div>
                              <div className="text-right text-amber-400 font-medium">
                                {tier.winsPrize.toLocaleString('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} zl
                              </div>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <div className="text-gray-500 text-sm">Brak danych o nagrodach</div>
                      )}
                    </div>
                  )}
                </div>
                </div>

              </CardListItem>
            ))}
          </div>
        )}

        {/* Pagination */}
        {!isLoading && totalPages > 1 && (
          <div className="flex justify-center items-center gap-2 mt-8">
            <button
              onClick={() => handlePageChange(1)}
              disabled={page === 1}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &laquo;
            </button>
            <button
              onClick={() => handlePageChange(page - 1)}
              disabled={page === 1}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &lsaquo;
            </button>

            <div className="flex gap-1">
              {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                let pageNum: number;
                if (totalPages <= 5) {
                  pageNum = i + 1;
                } else if (page <= 3) {
                  pageNum = i + 1;
                } else if (page >= totalPages - 2) {
                  pageNum = totalPages - 4 + i;
                } else {
                  pageNum = page - 2 + i;
                }

                return (
                  <button
                    key={pageNum}
                    onClick={() => handlePageChange(pageNum)}
                    className={`px-3 py-2 rounded-lg transition-all ${
                      page === pageNum
                        ? 'bg-cyan-500 text-white'
                        : 'bg-gray-800/50 border border-gray-700/50 text-gray-300 hover:border-cyan-500/30'
                    }`}
                  >
                    {pageNum}
                  </button>
                );
              })}
            </div>

            <button
              onClick={() => handlePageChange(page + 1)}
              disabled={page === totalPages}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &rsaquo;
            </button>
            <button
              onClick={() => handlePageChange(totalPages)}
              disabled={page === totalPages}
              className="px-3 py-2 bg-gray-800/50 border border-gray-700/50 rounded-lg text-gray-300 hover:border-cyan-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              &raquo;
            </button>
          </div>
        )}

      </div>

      {/* Import Modal */}
      {showImportModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-black/70 backdrop-blur-sm"
            onClick={() => setShowImportModal(false)}
          />

          {/* Modal */}
          <div className="relative bg-gray-800 rounded-2xl p-6 w-full max-w-md mx-4 border border-gray-700/50 shadow-2xl">
            <h2 className="text-xl font-bold text-white mb-6">Import losowan</h2>

            <p className="text-gray-400 text-sm mb-4">
              Dane zostaną zapisane do pliku CSV i przetworzone przez Worker.
            </p>

            {/* File Selection */}
            <div className="mb-4">
              <label className="block text-gray-300 text-sm font-medium mb-2">
                Plik (JSON, CSV, TXT)
              </label>
              <input
                type="file"
                ref={fileInputRef}
                onChange={handleFileSelect}
                accept=".json,.csv,.txt"
                className="w-full px-4 py-2.5 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors file:mr-4 file:py-1 file:px-3 file:rounded-lg file:border-0 file:text-sm file:font-semibold file:bg-cyan-500/20 file:text-cyan-400 hover:file:bg-cyan-500/30"
              />
              {selectedFile && (
                <p className="mt-2 text-sm text-gray-400">
                  Wybrany: {selectedFile.name}
                </p>
              )}
            </div>

            {/* Draw Type (required for CSV/TXT) */}
            <div className="mb-4">
              <ListSelect
                label={<>Typ losowania {isCsvOrTxtFile && <span className="text-red-400">*</span>}</>}
                id="importDrawTypeId"
                value={importDrawTypeId}
                onChange={(e) => setImportDrawTypeId(e.target.value)}
                options={DRAW_TYPES.map((type) => ({ value: type.id.toString(), label: type.name }))}
                placeholder="Wybierz typ..."
              />
              <p className="mt-1 text-xs text-gray-500">
                {isCsvOrTxtFile
                  ? 'Wymagane dla plikow CSV/TXT'
                  : 'Opcjonalne dla plikow JSON (typ jest w danych)'}
              </p>
            </div>

            {/* CSV Format Info */}
            {isCsvOrTxtFile && (
              <div className="mb-4 p-3 bg-gray-900/50 rounded-xl border border-gray-700/50">
                <p className="text-xs text-gray-400">
                  <strong className="text-gray-300">Format CSV:</strong><br />
                  DrawSystemId;Data;Liczba1;Liczba2;...<br />
                  <span className="text-gray-500">Np: 1234;15.01.2024;5;12;23;34;45;49</span>
                </p>
              </div>
            )}

            {/* Buttons */}
            <div className="flex gap-3 mt-6">
              <ButtonSecondary className="flex-1 justify-center" onClick={() => setShowImportModal(false)}>
                Anuluj
              </ButtonSecondary>
              <ButtonPrimary
                className="flex-1"
                onClick={handleImportSubmit}
                disabled={!selectedFile || isImporting || (isCsvOrTxtFile && !importDrawTypeId)}
              >
                {isImporting ? 'Importowanie...' : 'Importuj'}
              </ButtonPrimary>
            </div>
          </div>
        </div>
      )}

      {/* Confirm delete draw modal */}
      <ConfirmModal
        isOpen={drawToDelete !== null}
        title="Usunąć wynik losowania?"
        message="Czy na pewno chcesz usunąć ten wynik losowania? Ta operacja jest nieodwracalna."
        confirmText="Usuń"
        cancelText="Anuluj"
        variant="danger"
        onConfirm={confirmDeleteDraw}
        onCancel={() => setDrawToDelete(null)}
      />

    </section>
  );
}

export default LottoDrawsPage;
