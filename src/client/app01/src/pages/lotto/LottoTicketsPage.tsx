import { useEffect, useState, useRef } from "react";
import { useSearchParams } from "react-router";
import { ApiLottoService } from "../../services/api-lotto-service";
import type { LottoTicketsGetListTicket } from "../../services/contracts/lotto-tickets-get-list-response";
import ConfirmModal from "../../components/ConfirmModal";
import TextEdit from "../../components/TextEdit";
import ButtonPrimary from "../../components/ButtonPrimary";
import ButtonSecondary from "../../components/ButtonSecondary";
import ListSelect from "../../components/ListSelect";
import Card from "../../components/Card";
import CardListItem from "../../components/CardListItem";
import SubMenu from "../../components/SubMenu";
import ButtonEdit from "../../components/ButtonEdit";
import ButtonDelete from "../../components/ButtonDelete";
import ButtonDanger from "../../components/ButtonDanger";
import FormCard from "../../components/FormCard";

const DRAW_TYPES = [
  {
    id: 1,
    name: "Lotto",
    numbersCount: 6,
    maxNumber: 49,
    specialCount: 0,
    specialMax: 0,
  },
  {
    id: 2,
    name: "Lotto Plus",
    numbersCount: 6,
    maxNumber: 49,
    specialCount: 0,
    specialMax: 0,
  },
  {
    id: 3,
    name: "Mini Lotto",
    numbersCount: 5,
    maxNumber: 42,
    specialCount: 0,
    specialMax: 0,
  },
  {
    id: 4,
    name: "Ekstra Pensja",
    numbersCount: 5,
    maxNumber: 35,
    specialCount: 1,
    specialMax: 4,
  },
  {
    id: 5,
    name: "Ekstra Premia",
    numbersCount: 5,
    maxNumber: 35,
    specialCount: 1,
    specialMax: 4,
  },
  {
    id: 6,
    name: "EuroJackpot",
    numbersCount: 5,
    maxNumber: 50,
    specialCount: 2,
    specialMax: 12,
  },
  {
    id: 7,
    name: "Szybkie600",
    numbersCount: 6,
    maxNumber: 32,
    specialCount: 0,
    specialMax: 0,
  },
  {
    id: 8,
    name: "Kaskada",
    numbersCount: 12,
    maxNumber: 24,
    specialCount: 12,
    specialMax: 24,
  },
  {
    id: 9,
    name: "MultiMulti",
    numbersCount: 10,
    maxNumber: 80,
    specialCount: 0,
    specialMax: 0,
  },
  {
    id: 10,
    name: "Keno",
    numbersCount: 10,
    maxNumber: 70,
    specialCount: 0,
    specialMax: 0,
  },
];

const MAX_TICKETS = 500;

function LottoTicketsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [isVisible, setIsVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [tickets, setTickets] = useState<LottoTicketsGetListTicket[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  // Filters
  const [groupNameFilter, setGroupNameFilter] = useState(
    searchParams.get("groupName") || "",
  );
  const [drawTypeIdFilter, setDrawTypeIdFilter] = useState<number | undefined>(
    searchParams.get("drawTypeId")
      ? parseInt(searchParams.get("drawTypeId")!, 10)
      : undefined,
  );
  const [page, setPage] = useState(
    parseInt(searchParams.get("page") || "1", 10),
  );
  const pageSize = 20;

  // Add ticket form
  const [showAddForm, setShowAddForm] = useState(false);
  const [newTicketDrawTypeId, setNewTicketDrawTypeId] = useState<number>(1);
  const [newTicketGroupName, setNewTicketGroupName] = useState("");
  const [newTicketNumbers, setNewTicketNumbers] = useState<number[]>([]);
  const [newTicketSpecialNumbers, setNewTicketSpecialNumbers] = useState<
    number[]
  >([]);
  const [isAdding, setIsAdding] = useState(false);
  const [editingTicketId, setEditingTicketId] = useState<number | null>(null);

  // Add multiple tickets form
  const [showAddManyForm, setShowAddManyForm] = useState(false);
  const [multiDrawTypeId, setMultiDrawTypeId] = useState<number>(1);
  const [multiGroupName, setMultiGroupName] = useState("");
  const [generatedTickets, setGeneratedTickets] = useState<
    { numbers: number[]; specials: number[] }[]
  >([]);
  const [isAddingMany, setIsAddingMany] = useState(false);
  const [addingProgress, setAddingProgress] = useState<{
    current: number;
    total: number;
  } | null>(null);

  // Delete
  const [deletingTicketId, setDeletingTicketId] = useState<number | null>(null);
  const [ticketToDelete, setTicketToDelete] = useState<number | null>(null);
  const [isDeletingAll, setIsDeletingAll] = useState(false);
  const [showDeleteAllConfirm, setShowDeleteAllConfirm] = useState(false);
  const [deletingAllProgress, setDeletingAllProgress] = useState<{
    current: number;
    total: number;
  } | null>(null);

  // Import/Export
  const [isExporting, setIsExporting] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Import modal state
  const [showImportModal, setShowImportModal] = useState(false);
  const [importDrawTypeId, setImportDrawTypeId] = useState<string>("");
  const [importGroupName, setImportGroupName] = useState<string>("");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  useEffect(() => {
    document.title = "Moje kupony | Lotto | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);

    // Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    fetchTickets();
  }, [page]);

  // Reset results when filters change
  useEffect(() => {
    setTickets([]);
    setTotalCount(0);
    setTotalPages(0);
    setError(null);
    setSuccess(null);
  }, [groupNameFilter, drawTypeIdFilter]);

  const fetchTickets = async () => {
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

      const response = await apiLottoService.lottoTicketsGetList({
        groupName: groupNameFilter || undefined,
        drawTypeId: drawTypeIdFilter,
        page,
        pageSize,
      });

      setTickets(response.tickets);
      setTotalCount(response.totalCount);
      setTotalPages(response.totalPages);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas pobierania kuponow",
      );
    } finally {
      setIsLoading(false);
    }
  };

  const handleSearch = () => {
    setError(null);
    setSuccess(null);
    setPage(1);
    const params = new URLSearchParams();
    if (groupNameFilter) params.set("groupName", groupNameFilter);
    if (drawTypeIdFilter !== undefined)
      params.set("drawTypeId", drawTypeIdFilter.toString());
    params.set("page", "1");
    setSearchParams(params);
    fetchTickets();
  };

  const handlePageChange = (newPage: number) => {
    setError(null);
    setSuccess(null);
    setPage(newPage);
    const params = new URLSearchParams(searchParams);
    params.set("page", newPage.toString());
    setSearchParams(params);
  };

  const handleNumberToggle = (num: number) => {
    const drawType = DRAW_TYPES.find((dt) => dt.id === newTicketDrawTypeId);
    const maxNumbers = drawType?.numbersCount || 6;

    if (newTicketNumbers.includes(num)) {
      setNewTicketNumbers(newTicketNumbers.filter((n) => n !== num));
    } else if (newTicketNumbers.length < maxNumbers) {
      setNewTicketNumbers([...newTicketNumbers, num]);
      // Kaskada (id: 8) - usuń numer z specjalnych jeśli tam jest
      if (newTicketDrawTypeId === 8 && newTicketSpecialNumbers.includes(num)) {
        setNewTicketSpecialNumbers(
          newTicketSpecialNumbers.filter((n) => n !== num),
        );
      }
    }
  };

  const handleSpecialNumberToggle = (num: number) => {
    const drawType = DRAW_TYPES.find((dt) => dt.id === newTicketDrawTypeId);
    const maxSpecial = drawType?.specialCount || 0;

    // Kaskada (id: 8) - nie pozwól wybrać numeru, który jest już w normalnych
    if (newTicketDrawTypeId === 8 && newTicketNumbers.includes(num)) {
      return;
    }

    if (newTicketSpecialNumbers.includes(num)) {
      setNewTicketSpecialNumbers(
        newTicketSpecialNumbers.filter((n) => n !== num),
      );
    } else if (newTicketSpecialNumbers.length < maxSpecial) {
      setNewTicketSpecialNumbers([...newTicketSpecialNumbers, num]);
    }
  };

  const handleRandomNumbers = () => {
    setError(null);
    setSuccess(null);
    const drawType = DRAW_TYPES.find((dt) => dt.id === newTicketDrawTypeId);
    const numbersCount = drawType?.numbersCount || 6;
    const maxNum = drawType?.maxNumber || 49;
    const specialCount = drawType?.specialCount || 0;
    const specialMax = drawType?.specialMax || 0;

    const randomNumbers: number[] = [];
    while (randomNumbers.length < numbersCount) {
      const num = Math.floor(Math.random() * maxNum) + 1;
      if (!randomNumbers.includes(num)) {
        randomNumbers.push(num);
      }
    }
    setNewTicketNumbers(randomNumbers);

    if (specialCount > 0 && specialMax > 0) {
      const randomSpecialNumbers: number[] = [];

      // Kaskada (id: 8) - specjalne to pozostałe nie wylosowane
      if (newTicketDrawTypeId === 8) {
        const availableNumbers = Array.from(
          { length: specialMax },
          (_, i) => i + 1,
        ).filter((num) => !randomNumbers.includes(num));

        while (
          randomSpecialNumbers.length < specialCount &&
          availableNumbers.length > 0
        ) {
          const randomIndex = Math.floor(
            Math.random() * availableNumbers.length,
          );
          const num = availableNumbers[randomIndex];
          randomSpecialNumbers.push(num);
          availableNumbers.splice(randomIndex, 1);
        }
      } else {
        // Inne gry - losuj normalnie
        while (randomSpecialNumbers.length < specialCount) {
          const num = Math.floor(Math.random() * specialMax) + 1;
          if (!randomSpecialNumbers.includes(num)) {
            randomSpecialNumbers.push(num);
          }
        }
      }

      setNewTicketSpecialNumbers(randomSpecialNumbers);
    } else {
      setNewTicketSpecialNumbers([]);
    }
  };

  // Generate default group name for multi-ticket form
  const generateDefaultGroupName = (drawTypeId: number): string => {
    const drawType = DRAW_TYPES.find((dt) => dt.id === drawTypeId);
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    const hours = String(now.getHours()).padStart(2, "0");
    const minutes = String(now.getMinutes()).padStart(2, "0");
    const seconds = String(now.getSeconds()).padStart(2, "0");
    return `${drawType?.name || "Lotto"} ${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
  };

  // Initialize multi form when opened
  const handleOpenAddManyForm = () => {
    setShowAddForm(false);
    setShowAddManyForm(true);
    setMultiGroupName(generateDefaultGroupName(multiDrawTypeId));
    setGeneratedTickets([]);
  };

  // Helper: count adjacent number pairs in a ticket
  const countAdjacentPairs = (numbers: number[]): number => {
    const sorted = [...numbers].sort((a, b) => a - b);
    let count = 0;
    for (let i = 0; i < sorted.length - 1; i++) {
      if (sorted[i + 1] - sorted[i] === 1) {
        count++;
      }
    }
    return count;
  };

  // Helper: optimize tickets to minimize adjacent numbers
  const optimizeTickets = (
    tickets: { numbers: number[]; specials: number[] }[],
    maxIterations: number = 100,
  ): void => {
    for (let iter = 0; iter < maxIterations; iter++) {
      let improved = false;

      // Try swapping numbers between different tickets
      for (let t1 = 0; t1 < tickets.length && !improved; t1++) {
        for (let t2 = t1 + 1; t2 < tickets.length && !improved; t2++) {
          const ticket1 = tickets[t1];
          const ticket2 = tickets[t2];

          const currentScore =
            countAdjacentPairs(ticket1.numbers) +
            countAdjacentPairs(ticket2.numbers);

          // Try swapping each pair of numbers
          for (let i = 0; i < ticket1.numbers.length && !improved; i++) {
            for (let j = 0; j < ticket2.numbers.length && !improved; j++) {
              const num1 = ticket1.numbers[i];
              const num2 = ticket2.numbers[j];

              // Skip if same number (shouldn't happen) or if it would create duplicate in ticket
              if (num1 === num2) continue;
              if (
                ticket1.numbers.includes(num2) ||
                ticket2.numbers.includes(num1)
              )
                continue;

              // Try the swap
              ticket1.numbers[i] = num2;
              ticket2.numbers[j] = num1;

              const newScore =
                countAdjacentPairs(ticket1.numbers) +
                countAdjacentPairs(ticket2.numbers);

              if (newScore < currentScore) {
                // Keep the swap
                improved = true;
              } else {
                // Revert the swap
                ticket1.numbers[i] = num1;
                ticket2.numbers[j] = num2;
              }
            }
          }
        }
      }

      // If no improvement was made in this iteration, we're done
      if (!improved) break;
    }
  };

  // Generate optimal tickets covering all numbers
  const handleGenerateTickets = () => {
    setError(null);
    setSuccess(null);

    const drawType = DRAW_TYPES.find((dt) => dt.id === multiDrawTypeId);
    if (!drawType) return;

    const { numbersCount, maxNumber, specialCount, specialMax } = drawType;

    // Create array of all numbers
    const allNumbers = Array.from({ length: maxNumber }, (_, i) => i + 1);

    // Shuffle for randomness
    const shuffled = [...allNumbers].sort(() => Math.random() - 0.5);

    // Calculate how many tickets we need to cover all numbers
    const ticketsNeeded = Math.ceil(maxNumber / numbersCount);

    const newTickets: { numbers: number[]; specials: number[] }[] = [];
    let numberIndex = 0;

    for (let i = 0; i < ticketsNeeded; i++) {
      const ticketNumbers: number[] = [];

      // Take numbers sequentially from shuffled array
      for (let j = 0; j < numbersCount; j++) {
        if (numberIndex < shuffled.length) {
          ticketNumbers.push(shuffled[numberIndex]);
          numberIndex++;
        } else {
          // If we run out of numbers (last ticket), fill with random already-used numbers
          const remainingNeeded = numbersCount - ticketNumbers.length;
          const availableForDuplication = shuffled.filter(
            (n) => !ticketNumbers.includes(n),
          );
          const shuffledAvailable = [...availableForDuplication].sort(
            () => Math.random() - 0.5,
          );
          for (
            let k = 0;
            k < remainingNeeded && k < shuffledAvailable.length;
            k++
          ) {
            ticketNumbers.push(shuffledAvailable[k]);
          }
          break;
        }
      }

      newTickets.push({ numbers: ticketNumbers, specials: [] });
    }

    // Optimize to minimize adjacent numbers in each ticket
    optimizeTickets(newTickets);

    // Now handle special numbers if the game has them
    if (specialCount > 0 && specialMax > 0) {
      // For Kaskada (id: 8), specials must not overlap with regular numbers
      if (multiDrawTypeId === 8) {
        // Each ticket gets specials from remaining numbers not in its regular set
        newTickets.forEach((ticket) => {
          const availableSpecials = Array.from(
            { length: specialMax },
            (_, i) => i + 1,
          ).filter((n) => !ticket.numbers.includes(n));
          const shuffledSpecials = [...availableSpecials].sort(
            () => Math.random() - 0.5,
          );
          ticket.specials = shuffledSpecials.slice(0, specialCount);
        });
      } else {
        // For other games, distribute specials trying to cover all numbers from range first
        // Similar approach to regular numbers - use a pool that gets refilled when empty
        let specialPool: number[] = [];

        const refillPool = () => {
          const refill = Array.from({ length: specialMax }, (_, i) => i + 1);
          const shuffledRefill = [...refill].sort(() => Math.random() - 0.5);
          specialPool = [...specialPool, ...shuffledRefill];
        };

        // Initial fill
        refillPool();

        newTickets.forEach((ticket) => {
          const specials: number[] = [];

          while (specials.length < specialCount) {
            if (specialPool.length === 0) {
              refillPool();
            }

            // Find next number not already in this ticket's specials
            const idx = specialPool.findIndex((n) => !specials.includes(n));
            if (idx !== -1) {
              specials.push(specialPool[idx]);
              specialPool.splice(idx, 1);
            } else {
              // All remaining pool numbers are duplicates for this ticket, refill
              refillPool();
            }
          }

          ticket.specials = specials;
        });
      }
    }

    // Replace existing tickets (clear first, then set new)
    setGeneratedTickets(newTickets);
  };

  // Clear generated tickets
  const handleClearGeneratedTickets = () => {
    setGeneratedTickets([]);
  };

  // Delete all tickets from backend
  const handleDeleteAllTickets = () => {
    setShowDeleteAllConfirm(true);
  };

  const confirmDeleteAllTickets = async () => {
    setShowDeleteAllConfirm(false);
    setError(null);
    setSuccess(null);

    const token = localStorage.getItem("token");
    if (!token) return;

    setIsDeletingAll(true);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      // First get all tickets (need to fetch all pages)
      const allTicketIds: number[] = [];
      let currentPage = 1;
      let hasMore = true;

      while (hasMore) {
        const response = await apiLottoService.lottoTicketsGetList({
          groupName: groupNameFilter || undefined,
          drawTypeId: drawTypeIdFilter,
          page: currentPage,
          pageSize: 100,
        });

        allTicketIds.push(...response.tickets.map((t) => t.id));
        hasMore = currentPage < response.totalPages;
        currentPage++;
      }

      if (allTicketIds.length === 0) {
        setError("Brak kuponow do usuniecia");
        setIsDeletingAll(false);
        return;
      }

      setDeletingAllProgress({ current: 0, total: allTicketIds.length });

      let successCount = 0;
      const errors: string[] = [];

      for (let i = 0; i < allTicketIds.length; i++) {
        setDeletingAllProgress({ current: i + 1, total: allTicketIds.length });

        try {
          await apiLottoService.lottoTicketsDelete({
            ticketId: allTicketIds[i],
          });
          successCount++;
        } catch (err) {
          errors.push(
            `Kupon ${allTicketIds[i]}: ${err instanceof Error ? err.message : "Blad"}`,
          );
        }
      }

      if (successCount > 0) {
        let message = `Usunieto ${successCount} kuponow`;
        if (errors.length > 0) {
          message += `, bledy w ${errors.length} kuponach`;
        }
        setSuccess(message);
        fetchTickets();
      } else {
        setError(
          `Nie udalo sie usunac zadnego kuponu. Bledy: ${errors.slice(0, 3).join("; ")}`,
        );
      }
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas usuwania kuponow",
      );
    } finally {
      setIsDeletingAll(false);
      setDeletingAllProgress(null);
    }
  };

  // Add all generated tickets to backend
  const handleAddManyTickets = async () => {
    setError(null);
    setSuccess(null);

    if (generatedTickets.length === 0) {
      setError('Najpierw wygeneruj kupony klikajac "Losuj i uzupelniaj"');
      return;
    }

    if (totalCount + generatedTickets.length > MAX_TICKETS) {
      setError(
        `Nie mozna dodac ${generatedTickets.length} kuponow. Przekroczono limit ${MAX_TICKETS} kuponow.`,
      );
      return;
    }

    const token = localStorage.getItem("token");
    if (!token) return;

    setIsAddingMany(true);
    setAddingProgress({ current: 0, total: generatedTickets.length });

    const apiLottoService = new ApiLottoService(
      import.meta.env.VITE_API_URL,
      import.meta.env.VITE_APP_TOKEN,
    );
    apiLottoService.setUsrToken(token);

    let successCount = 0;
    const errors: string[] = [];

    for (let i = 0; i < generatedTickets.length; i++) {
      const ticket = generatedTickets[i];
      setAddingProgress({ current: i + 1, total: generatedTickets.length });

      try {
        await apiLottoService.lottoTicketsAdd({
          drawTypeId: multiDrawTypeId,
          groupName: multiGroupName || null,
          numbers: ticket.numbers,
          specials: ticket.specials,
        });
        successCount++;
      } catch (err) {
        errors.push(
          `Kupon ${i + 1}: ${err instanceof Error ? err.message : "Blad"}`,
        );
      }
    }

    setIsAddingMany(false);
    setAddingProgress(null);

    if (successCount > 0) {
      let message = `Dodano ${successCount} kuponow`;
      if (errors.length > 0) {
        message += `, bledy w ${errors.length} kuponach`;
      }
      setSuccess(message);
      setGeneratedTickets([]);
      setShowAddManyForm(false);
      fetchTickets();
    } else {
      setError(
        `Nie udalo sie dodac zadnego kuponu. Bledy: ${errors.slice(0, 3).join("; ")}`,
      );
    }
  };

  const handleAddTicket = async () => {
    setError(null);
    setSuccess(null);

    const isEditing = editingTicketId !== null;

    if (!isEditing && totalCount >= MAX_TICKETS) {
      setError(`Osiagnieto maksymalna liczbe kuponow (${MAX_TICKETS})`);
      return;
    }

    const drawType = DRAW_TYPES.find((dt) => dt.id === newTicketDrawTypeId);
    const requiredNumbers = drawType?.numbersCount || 6;
    const requiredSpecial = drawType?.specialCount || 0;

    if (newTicketNumbers.length !== requiredNumbers) {
      setError(`Wybierz dokladnie ${requiredNumbers} numerow`);
      return;
    }

    if (
      requiredSpecial > 0 &&
      newTicketSpecialNumbers.length !== requiredSpecial
    ) {
      setError(
        `Wybierz dokladnie ${requiredSpecial} ${requiredSpecial === 1 ? "numer specjalny" : "numery specjalne"}`,
      );
      return;
    }

    const token = localStorage.getItem("token");
    if (!token) return;

    setIsAdding(true);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      // If editing, delete old ticket first
      if (isEditing) {
        await apiLottoService.lottoTicketsDelete({ ticketId: editingTicketId });
      }

      await apiLottoService.lottoTicketsAdd({
        drawTypeId: newTicketDrawTypeId,
        groupName: newTicketGroupName || null,
        numbers: newTicketNumbers,
        specials: requiredSpecial > 0 ? newTicketSpecialNumbers : [],
      });

      setSuccess(
        isEditing ? "Kupon zostal zaktualizowany" : "Kupon zostal dodany",
      );
      setShowAddForm(false);
      setNewTicketNumbers([]);
      setNewTicketSpecialNumbers([]);
      setNewTicketGroupName("");
      setEditingTicketId(null);
      fetchTickets();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas dodawania kuponu",
      );
    } finally {
      setIsAdding(false);
    }
  };

  const handleDeleteTicket = (ticketId: number) => {
    setTicketToDelete(ticketId);
  };

  const handleEditTicket = (ticket: LottoTicketsGetListTicket) => {
    setEditingTicketId(ticket.id);
    setNewTicketDrawTypeId(ticket.drawTypeId);
    setNewTicketGroupName(ticket.groupName || "");
    setNewTicketNumbers([...ticket.numbers]);
    setNewTicketSpecialNumbers([...ticket.specials]);
    setShowAddForm(true);
    setShowAddManyForm(false);
  };

  const confirmDeleteTicket = async () => {
    if (ticketToDelete === null) return;

    setError(null);
    setSuccess(null);
    setTicketToDelete(null);

    const token = localStorage.getItem("token");
    if (!token) return;

    setDeletingTicketId(ticketToDelete);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      await apiLottoService.lottoTicketsDelete({ ticketId: ticketToDelete });

      setSuccess("Kupon zostal usuniety");
      fetchTickets();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas usuwania kuponu",
      );
    } finally {
      setDeletingTicketId(null);
    }
  };

  const handleExport = async () => {
    setError(null);
    setSuccess(null);

    const token = localStorage.getItem("token");
    if (!token) return;

    setIsExporting(true);

    try {
      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      const response = await apiLottoService.lottoTicketsExport({
        groupName: groupNameFilter || undefined,
        drawTypeId: drawTypeIdFilter,
      });

      // Download CSV file
      const blob = new Blob([response.csv], { type: "text/csv;charset=utf-8" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = response.fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);

      setSuccess(`Wyeksportowano ${response.totalCount} kuponow do pliku CSV`);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas eksportu kuponow",
      );
    } finally {
      setIsExporting(false);
    }
  };

  const handleImportClick = () => {
    setError(null);
    setSuccess(null);
    setShowImportModal(true);
    setSelectedFile(null);
    setImportDrawTypeId("");
    setImportGroupName("");
  };

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      setSelectedFile(file);
    }
  };

  const getFileExtension = (filename: string): string => {
    return filename.split(".").pop()?.toLowerCase() || "";
  };

  const handleImportSubmit = async () => {
    if (!selectedFile) return;

    const token = localStorage.getItem("token");
    if (!token) return;

    setIsImporting(true);
    setError(null);
    setSuccess(null);

    try {
      const text = await selectedFile.text();
      const fileExt = getFileExtension(selectedFile.name);
      const isCsvFile = fileExt === "csv" || fileExt === "txt";

      const apiLottoService = new ApiLottoService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN,
      );
      apiLottoService.setUsrToken(token);

      let response;

      if (isCsvFile) {
        // CSV import - requires drawTypeId from modal
        if (!importDrawTypeId) {
          setError("Dla plikow CSV/TXT wymagany jest wybor typu losowania");
          setIsImporting(false);
          return;
        }

        response = await apiLottoService.lottoTicketsImport({
          csv: text,
          drawTypeId: parseInt(importDrawTypeId, 10),
          groupName: importGroupName || undefined,
        });
      } else {
        // JSON import
        const data = JSON.parse(text);

        let ticketsToImport: {
          drawTypeId: number;
          groupName: string | null;
          numbers: number[];
          specials: number[];
        }[] = [];

        if (data.tickets && Array.isArray(data.tickets)) {
          ticketsToImport = data.tickets.map(
            (t: {
              drawTypeId: number;
              groupName: string | null;
              numbers: number[];
              specials: number[];
            }) => ({
              drawTypeId: t.drawTypeId,
              groupName: t.groupName,
              numbers: t.numbers,
              specials: t.specials || [],
            }),
          );
        } else if (Array.isArray(data)) {
          ticketsToImport = data;
        } else {
          throw new Error("Nieprawidlowy format pliku");
        }

        response = await apiLottoService.lottoTicketsImport({
          tickets: ticketsToImport,
        });
      }

      let message = `Zaimportowano ${response.importedCount} kuponow`;
      if (response.skippedCount > 0) {
        message += `, pominieto ${response.skippedCount}`;
      }
      if (response.errors.length > 0) {
        message += `. Bledy: ${response.errors.slice(0, 5).join(", ")}`;
        if (response.errors.length > 5) {
          message += ` ...i ${response.errors.length - 5} wiecej`;
        }
      }

      setSuccess(message);
      setShowImportModal(false);
      fetchTickets();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Wystapil blad podczas importu kuponow",
      );
    } finally {
      setIsImporting(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  const isCsvOrTxtFile = selectedFile
    ? ["csv", "txt"].includes(getFileExtension(selectedFile.name))
    : false;

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${year}-${month}-${day} ${hours}:${minutes}`;
  };

  const getNumberColor = (index: number) => {
    return index % 2 === 0 ? "bg-amber-500" : "bg-amber-400";
  };

  const currentDrawType = DRAW_TYPES.find(
    (dt) => dt.id === newTicketDrawTypeId,
  );
  const maxNumber = currentDrawType?.maxNumber || 49;
  const requiredNumbers = currentDrawType?.numbersCount || 6;
  const specialCount = currentDrawType?.specialCount || 0;
  const specialMax = currentDrawType?.specialMax || 0;
  const hasSpecialNumbers = specialCount > 0;

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
            Moje kupony
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Zarzadzaj swoimi kuponami Lotto ({totalCount}/{MAX_TICKETS})
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

        {/* Filters and Add button */}
        <Card isVisible={isVisible} className="mb-8 delay-300">
          <div className="grid sm:grid-cols-3 gap-4 mb-4">
            <TextEdit
              label="Filtruj po grupie"
              id="groupNameFilter"
              name="groupNameFilter"
              type="text"
              value={groupNameFilter}
              onChange={(e) => setGroupNameFilter(e.target.value)}
              placeholder="Nazwa grupy..."
            />
            <ListSelect
              label="Filtruj po typie"
              id="drawTypeIdFilter"
              value={drawTypeIdFilter?.toString() ?? ""}
              onChange={(e) =>
                setDrawTypeIdFilter(
                  e.target.value ? parseInt(e.target.value, 10) : undefined,
                )
              }
              options={DRAW_TYPES.map((type) => ({
                value: type.id.toString(),
                label: type.name,
              }))}
              placeholder="Wszystkie typy"
            />
            <div>
              <label className="block text-sm font-medium mb-2 invisible">
                Szukaj
              </label>
              <ButtonPrimary
                className="w-full"
                onClick={handleSearch}
                disabled={isLoading}
              >
                {isLoading ? "Szukam..." : "Szukaj"}
              </ButtonPrimary>
            </div>
          </div>
          {/* Row 1: Import, Export, Delete all */}
          <div className="grid sm:grid-cols-3 gap-4 mb-4">
            <ButtonSecondary
              className="w-full justify-center"
              onClick={handleImportClick}
              disabled={isImporting}
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
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"
                />
              </svg>
              {isImporting ? "Importowanie..." : "Importuj"}
            </ButtonSecondary>
            <ButtonSecondary
              className="w-full justify-center"
              onClick={handleExport}
              disabled={isExporting || totalCount === 0}
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
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
                />
              </svg>
              {isExporting ? "Eksportowanie..." : "Eksportuj"}
            </ButtonSecondary>
            <ButtonDanger
              className="w-full"
              onClick={handleDeleteAllTickets}
              disabled={isDeletingAll || totalCount === 0}
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
                  d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                />
              </svg>
              {isDeletingAll ? "Usuwanie..." : "Usuń wszystkie"}
            </ButtonDanger>
          </div>
          {/* Row 2: Add single, Add many */}
          <div className="grid sm:grid-cols-2 gap-4">
            <ButtonSecondary
              className="w-full justify-center"
              onClick={() => {
                if (showAddForm) {
                  setShowAddForm(false);
                  setEditingTicketId(null);
                  setNewTicketNumbers([]);
                  setNewTicketSpecialNumbers([]);
                  setNewTicketGroupName("");
                } else {
                  setShowAddForm(true);
                  setEditingTicketId(null);
                  setNewTicketNumbers([]);
                  setNewTicketSpecialNumbers([]);
                  setNewTicketGroupName("");
                }
                setShowAddManyForm(false);
              }}
              disabled={totalCount >= MAX_TICKETS}
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
                  d="M12 4v16m8-8H4"
                />
              </svg>
              {showAddForm ? "Anuluj" : "Losuj/Dodaj kupon"}
            </ButtonSecondary>
            <ButtonSecondary
              className="w-full justify-center"
              onClick={() => {
                if (showAddManyForm) {
                  setShowAddManyForm(false);
                  setGeneratedTickets([]);
                } else {
                  handleOpenAddManyForm();
                }
              }}
              disabled={totalCount >= MAX_TICKETS}
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
                  d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
                />
              </svg>
              {showAddManyForm ? "Anuluj" : "Losuj wiele kuponow"}
            </ButtonSecondary>
          </div>
          {/* Progress bar for deleting all tickets */}
          {deletingAllProgress && (
            <div className="mt-4">
              <div className="flex items-center justify-between mb-1">
                <span className="text-red-400 text-sm">
                  Usuwanie kuponow...
                </span>
                <span className="text-red-400 text-sm">
                  {deletingAllProgress.current}/{deletingAllProgress.total}
                </span>
              </div>
              <div className="w-full bg-gray-700 rounded-full h-2">
                <div
                  className="bg-red-500 h-2 rounded-full transition-all duration-200"
                  style={{
                    width: `${(deletingAllProgress.current / deletingAllProgress.total) * 100}%`,
                  }}
                />
              </div>
            </div>
          )}
        </Card>

        {/* Add ticket form */}
        {showAddForm && (
          <FormCard isVisible={isVisible} borderColor="green">
            <h2 className="text-xl font-bold text-white mb-4">
              {editingTicketId !== null ? "Edytuj kupon" : "Nowy kupon"}
            </h2>

            <div className="grid sm:grid-cols-2 gap-4 mb-6">
              <ListSelect
                label="Typ losowania"
                id="newTicketDrawTypeId"
                value={newTicketDrawTypeId.toString()}
                onChange={(e) => {
                  setNewTicketDrawTypeId(parseInt(e.target.value, 10));
                  setNewTicketNumbers([]);
                  setNewTicketSpecialNumbers([]);
                }}
                options={DRAW_TYPES.map((type) => ({
                  value: type.id.toString(),
                  label: type.name,
                }))}
              />
              <TextEdit
                label="Nazwa grupy (opcjonalnie)"
                id="newTicketGroupName"
                name="newTicketGroupName"
                type="text"
                value={newTicketGroupName}
                onChange={(e) => setNewTicketGroupName(e.target.value)}
                placeholder="np. Rodzina, Praca..."
                maxLength={100}
              />
            </div>

            <div className="mb-4">
              <label className="block text-gray-300 text-sm font-medium mb-2 text-center">
                Wybierz {requiredNumbers} numerow ({newTicketNumbers.length}/
                {requiredNumbers})
              </label>
              <div className="flex justify-center">
                <div className="grid grid-cols-10 gap-2">
                  {Array.from({ length: maxNumber }, (_, i) => i + 1).map(
                    (num) => (
                      <button
                        key={num}
                        onClick={() => handleNumberToggle(num)}
                        className={`w-8 h-8 rounded-full flex items-center justify-center font-black text-xs transition-all duration-200 ${
                          newTicketNumbers.includes(num)
                            ? "bg-cyan-500 text-white shadow-lg shadow-cyan-500/50"
                            : "bg-gray-700/50 text-gray-300 hover:bg-gray-600/50"
                        }`}
                      >
                        {num}
                      </button>
                    ),
                  )}
                </div>
              </div>
            </div>

            {/* Special numbers grid */}
            {hasSpecialNumbers && (
              <div className="mb-4">
                <label className="block text-gray-300 text-sm font-medium mb-2 text-center">
                  Wybierz {specialCount}{" "}
                  {specialCount === 1
                    ? "numer specjalny"
                    : specialCount === 2
                      ? "numery specjalne (Eurokule)"
                      : "numery specjalne"}{" "}
                  ({newTicketSpecialNumbers.length}/{specialCount})
                </label>
                <div className="flex justify-center">
                  <div className="grid grid-cols-10 gap-2">
                    {Array.from({ length: specialMax }, (_, i) => i + 1).map(
                      (num) => {
                        const isDisabledForKaskada =
                          newTicketDrawTypeId === 8 &&
                          newTicketNumbers.includes(num);
                        return (
                          <button
                            key={num}
                            onClick={() => handleSpecialNumberToggle(num)}
                            disabled={isDisabledForKaskada}
                            className={`w-8 h-8 rounded-full flex items-center justify-center font-black text-xs transition-all duration-200 ${
                              isDisabledForKaskada
                                ? "bg-gray-900/50 text-gray-600 cursor-not-allowed opacity-40"
                                : newTicketSpecialNumbers.includes(num)
                                  ? "bg-red-500 text-white shadow-lg shadow-red-500/50 ring-2 ring-red-300"
                                  : "bg-gray-700/50 text-gray-300 hover:bg-gray-600/50"
                            }`}
                          >
                            {num}
                          </button>
                        );
                      },
                    )}
                  </div>
                </div>
              </div>
            )}

            {(newTicketNumbers.length > 0 ||
              newTicketSpecialNumbers.length > 0) && (
              <div className="mb-4 p-3 bg-gray-900/50 rounded-xl">
                {newTicketNumbers.length > 0 && (
                  <div className="mb-1">
                    <span className="text-gray-400 text-sm">Numery: </span>
                    <span className="text-cyan-400 font-bold">
                      {[...newTicketNumbers].sort((a, b) => a - b).join(", ")}
                    </span>
                  </div>
                )}
                {newTicketSpecialNumbers.length > 0 && (
                  <div>
                    <span className="text-gray-400 text-sm">Specjalne: </span>
                    <span className="text-red-400 font-bold">
                      {[...newTicketSpecialNumbers]
                        .sort((a, b) => a - b)
                        .join(", ")}
                    </span>
                  </div>
                )}
              </div>
            )}

            <div className="flex justify-between">
              <ButtonSecondary onClick={handleRandomNumbers}>
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
                    d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
                  />
                </svg>
                Losuj
              </ButtonSecondary>
              <ButtonPrimary
                onClick={handleAddTicket}
                disabled={
                  isAdding ||
                  newTicketNumbers.length !== requiredNumbers ||
                  (hasSpecialNumbers &&
                    newTicketSpecialNumbers.length !== specialCount)
                }
              >
                {isAdding
                  ? "Zapisywanie..."
                  : editingTicketId !== null
                    ? "Zapisz zmiany"
                    : "Dodaj kupon"}
              </ButtonPrimary>
            </div>
          </FormCard>
        )}

        {/* Add multiple tickets form */}
        {showAddManyForm && (
          <FormCard isVisible={isVisible} borderColor="purple">
            <h2 className="text-xl font-bold text-white mb-4">
              Dodaj wiele kuponow
            </h2>

            <div className="grid sm:grid-cols-2 gap-4 mb-6">
              <ListSelect
                label="Typ losowania"
                id="multiDrawTypeId"
                value={multiDrawTypeId.toString()}
                onChange={(e) => {
                  const newTypeId = parseInt(e.target.value, 10);
                  setMultiDrawTypeId(newTypeId);
                  setMultiGroupName(generateDefaultGroupName(newTypeId));
                  setGeneratedTickets([]);
                }}
                options={DRAW_TYPES.map((type) => ({
                  value: type.id.toString(),
                  label: `${type.name} (${type.numbersCount} z ${type.maxNumber}${type.specialCount > 0 ? ` + ${type.specialCount} spec.` : ""})`,
                }))}
              />
              <TextEdit
                label="Nazwa grupy"
                id="multiGroupName"
                name="multiGroupName"
                type="text"
                value={multiGroupName}
                onChange={(e) => setMultiGroupName(e.target.value)}
                placeholder="Nazwa grupy kuponow..."
                maxLength={100}
              />
            </div>

            {/* Info about selected game */}
            {(() => {
              const selectedType = DRAW_TYPES.find(
                (dt) => dt.id === multiDrawTypeId,
              );
              if (!selectedType) return null;
              const ticketsNeeded = Math.ceil(
                selectedType.maxNumber / selectedType.numbersCount,
              );
              return (
                <div className="mb-4 p-3 bg-purple-900/30 rounded-xl border border-purple-500/20">
                  <p className="text-purple-300 text-sm">
                    <strong>{selectedType.name}:</strong> Minimalna liczba
                    kuponow pokrywajaca wszystkie numery (1-
                    {selectedType.maxNumber}): <strong>{ticketsNeeded}</strong>
                    {selectedType.specialCount > 0 && (
                      <span>
                        {" "}
                        (numery specjalne 1-{selectedType.specialMax} zostana
                        rozlozone losowo)
                      </span>
                    )}
                  </p>
                </div>
              );
            })()}

            {/* Generated tickets preview */}
            {generatedTickets.length > 0 && (
              <div className="mb-4">
                <div className="flex items-center justify-between mb-2">
                  <label className="text-gray-300 text-sm font-medium">
                    Wygenerowane kupony ({generatedTickets.length})
                  </label>
                  <span className="text-gray-500 text-xs">
                    Kliknij "Dodaj kupony" aby zapisac
                  </span>
                </div>
                <div className="max-h-64 overflow-y-auto space-y-2 p-3 bg-gray-900/50 rounded-xl">
                  {generatedTickets.map((ticket, index) => (
                    <div
                      key={index}
                      className="flex items-center gap-3 p-2 bg-gray-800/50 rounded-lg"
                    >
                      <span className="text-gray-500 text-xs w-6">
                        #{index + 1}
                      </span>
                      <div className="flex flex-wrap gap-1">
                        {[...ticket.numbers]
                          .sort((a, b) => a - b)
                          .map((num, numIndex) => (
                            <span
                              key={numIndex}
                              className="w-7 h-7 rounded-full bg-purple-500/80 text-white text-xs font-bold flex items-center justify-center"
                            >
                              {num}
                            </span>
                          ))}
                        {ticket.specials.length > 0 && (
                          <>
                            <span className="text-gray-500 mx-1">|</span>
                            {[...ticket.specials]
                              .sort((a, b) => a - b)
                              .map((num, numIndex) => (
                                <span
                                  key={numIndex}
                                  className="w-7 h-7 rounded-full bg-red-500/80 text-white text-xs font-bold flex items-center justify-center ring-1 ring-red-300"
                                >
                                  {num}
                                </span>
                              ))}
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Progress bar during adding */}
            {addingProgress && (
              <div className="mb-4">
                <div className="flex items-center justify-between mb-1">
                  <span className="text-gray-400 text-sm">
                    Dodawanie kuponow...
                  </span>
                  <span className="text-gray-400 text-sm">
                    {addingProgress.current}/{addingProgress.total}
                  </span>
                </div>
                <div className="w-full bg-gray-700 rounded-full h-2">
                  <div
                    className="bg-purple-500 h-2 rounded-full transition-all duration-200"
                    style={{
                      width: `${(addingProgress.current / addingProgress.total) * 100}%`,
                    }}
                  />
                </div>
              </div>
            )}

            <div className="flex flex-wrap justify-between items-center gap-3">
              {/* Left group */}
              <div className="flex gap-3">
                <button
                  onClick={handleClearGeneratedTickets}
                  disabled={generatedTickets.length === 0 || isAddingMany}
                  className="px-6 py-2.5 bg-gradient-to-r from-red-500/50 to-red-600/50 text-white font-semibold rounded-xl hover:from-red-600/50 hover:to-red-700/50 transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
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
                      d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                    />
                  </svg>
                  Czysc
                </button>
                <ButtonSecondary
                  onClick={handleGenerateTickets}
                  disabled={isAddingMany}
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
                      d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
                    />
                  </svg>
                  Losuj i uzupelniaj
                </ButtonSecondary>
              </div>

              {/* Right group */}
              <div className="flex gap-3">
                <ButtonSecondary
                  onClick={() => {
                    setShowAddManyForm(false);
                    setGeneratedTickets([]);
                  }}
                  disabled={isAddingMany}
                >
                  Anuluj
                </ButtonSecondary>
                <ButtonPrimary
                  className="flex items-center gap-2"
                  onClick={handleAddManyTickets}
                  disabled={isAddingMany || generatedTickets.length === 0}
                >
                  {isAddingMany ? (
                    <>
                      <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                      Dodawanie...
                    </>
                  ) : (
                    <>
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
                          d="M12 4v16m8-8H4"
                        />
                      </svg>
                      Dodaj kupony ({generatedTickets.length})
                    </>
                  )}
                </ButtonPrimary>
              </div>
            </div>
          </FormCard>
        )}

        {/* Error */}
        {error && (
          <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
            {error}
          </div>
        )}

        {/* Success */}
        {success && (
          <div className="mb-6 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
            {success}
          </div>
        )}

        {/* Results info */}
        {!isLoading && tickets.length > 0 && (
          <div
            className={`text-gray-400 text-sm mb-4 transition-all duration-500 ${
              isVisible ? "opacity-100" : "opacity-0"
            }`}
          >
            Znaleziono {totalCount} kuponow. Strona {page} z {totalPages || 1}.
          </div>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin"></div>
          </div>
        )}

        {/* Empty state */}
        {!isLoading && tickets.length === 0 && !error && (
          <div className="text-center py-12 text-gray-400">
            Nie masz jeszcze zadnych kuponow. Kliknij "Dodaj kupon", aby
            utworzyc pierwszy.
          </div>
        )}

        {/* Tickets list */}
        {!isLoading && tickets.length > 0 && (
          <div className="space-y-4">
            {tickets.map((ticket, index) => (
              <CardListItem key={ticket.id} isVisible={isVisible} index={index}>
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                  {/* Ticket info */}
                  <div className="flex-1">
                    <div className="flex flex-col sm:flex-row sm:items-center gap-2 mb-2">
                      <h3 className="text-xl font-bold text-white">
                        {ticket.drawTypeName}
                      </h3>
                      {ticket.groupName && (
                        <span className="text-gray-400 text-sm">
                          ({ticket.groupName})
                        </span>
                      )}
                    </div>
                    <div className="text-gray-500 text-xs">
                      Utworzono: {formatDate(ticket.createdAt)}
                    </div>
                  </div>

                  {/* Numbers */}
                  <div className="flex flex-col gap-2">
                    {/* Normal numbers */}
                    {ticket.numbers.length > 0 && (
                      <div className="grid grid-cols-10 gap-1.5">
                        {[...ticket.numbers]
                          .sort((a, b) => a - b)
                          .map((num, index) => (
                            <div
                              key={index}
                              className={`w-8 h-8 rounded-full flex items-center justify-center text-gray-900 font-black text-xs shadow-lg ${getNumberColor(index)}`}
                            >
                              {num}
                            </div>
                          ))}
                      </div>
                    )}
                    {/* Special numbers */}
                    {ticket.specials.length > 0 && (
                      <div className="grid grid-cols-10 gap-1.5">
                        {[...ticket.specials]
                          .sort((a, b) => a - b)
                          .map((num, index) => (
                            <div
                              key={index}
                              className="w-8 h-8 rounded-full flex items-center justify-center bg-red-500 text-white font-black text-xs shadow-lg ring-2 ring-red-300"
                            >
                              {num}
                            </div>
                          ))}
                      </div>
                    )}
                  </div>

                  {/* Action buttons */}
                  <div className="flex gap-2">
                    <ButtonEdit onClick={() => handleEditTicket(ticket)} />
                    <ButtonDelete
                      onClick={() => handleDeleteTicket(ticket.id)}
                      disabled={deletingTicketId === ticket.id}
                      isLoading={deletingTicketId === ticket.id}
                    />
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

      {/* Confirm delete single ticket modal */}
      <ConfirmModal
        isOpen={ticketToDelete !== null}
        title="Usunąć kupon?"
        message="Czy na pewno chcesz usunac ten kupon? Ta operacja jest nieodwracalna."
        confirmText="Usuń"
        cancelText="Anuluj"
        variant="danger"
        onConfirm={confirmDeleteTicket}
        onCancel={() => setTicketToDelete(null)}
      />

      {/* Confirm delete all tickets modal */}
      <ConfirmModal
        isOpen={showDeleteAllConfirm}
        title="Usunąć WSZYSTKIE kupony?"
        message="Czy na pewno chcesz usunac wszystkie zapisane kupony? Ta operacja jest nieodwracalna i usunie wszystkie Twoje kupony."
        confirmText="Usuń wszystkie"
        cancelText="Anuluj"
        variant="danger"
        onConfirm={confirmDeleteAllTickets}
        onCancel={() => setShowDeleteAllConfirm(false)}
      />

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
            <h2 className="text-xl font-bold text-white mb-6">
              Import kuponów
            </h2>

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
                className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors file:mr-4 file:py-1 file:px-3 file:rounded-lg file:border-0 file:text-sm file:font-semibold file:bg-cyan-500/20 file:text-cyan-400 hover:file:bg-cyan-500/30"
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
                label={
                  <>
                    Typ losowania{" "}
                    {isCsvOrTxtFile && <span className="text-red-400">*</span>}
                  </>
                }
                id="importDrawTypeIdTickets"
                value={importDrawTypeId}
                onChange={(e) => setImportDrawTypeId(e.target.value)}
                options={DRAW_TYPES.map((type) => ({
                  value: type.id.toString(),
                  label: type.name,
                }))}
                placeholder="Wybierz typ..."
              />
              <p className="mt-1 text-xs text-gray-500">
                {isCsvOrTxtFile
                  ? "Wymagane dla plikow CSV/TXT"
                  : "Opcjonalne dla plikow JSON (typ jest w danych)"}
              </p>
            </div>

            {/* Group Name (optional) */}
            <div className="mb-4">
              <label className="block text-gray-300 text-sm font-medium mb-2">
                Nazwa grupy (opcjonalnie)
              </label>
              <input
                type="text"
                value={importGroupName}
                onChange={(e) => setImportGroupName(e.target.value)}
                placeholder="np. Import 2024-01-15"
                maxLength={100}
                className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
              />
              <p className="mt-1 text-xs text-gray-500">
                Zostanie przypisana do wszystkich importowanych kuponow
              </p>
            </div>

            {/* CSV Format Info */}
            {isCsvOrTxtFile && (
              <div className="mb-4 p-3 bg-gray-900/50 rounded-xl border border-gray-700/50">
                <p className="text-xs text-gray-400">
                  <strong className="text-gray-300">Format CSV:</strong>
                  <br />
                  Liczba1,Liczba2,...[,S:Spec1,S:Spec2]
                  <br />
                  <span className="text-gray-500">Np: 5,12,23,34,45,49</span>
                  <br />
                  <span className="text-gray-500">
                    Lub: 10,20,30,40,50,S:5,S:10
                  </span>
                </p>
              </div>
            )}

            {/* Buttons */}
            <div className="flex gap-3 mt-6">
              <ButtonSecondary
                className="flex-1 justify-center"
                onClick={() => setShowImportModal(false)}
              >
                Anuluj
              </ButtonSecondary>
              <ButtonPrimary
                className="flex-1"
                onClick={handleImportSubmit}
                disabled={
                  !selectedFile ||
                  isImporting ||
                  (isCsvOrTxtFile && !importDrawTypeId)
                }
              >
                {isImporting ? "Importowanie..." : "Importuj"}
              </ButtonPrimary>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

export default LottoTicketsPage;
