import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import HangarPage from "./HangarPage";

const getHangarFlags = vi.fn();
const activateFlag = vi.fn();

vi.mock("../../services/api-courses-service", () => ({
  ApiCoursesService: class {
    getHangarFlags = getHangarFlags;
    activateFlag = activateFlag;
    setUsrToken = vi.fn();
  },
}));

const earnedAt = "2026-03-15T10:20:30Z";

const flags = [
  {
    flagId: 1,
    title: "Flaga A",
    courseSlug: "kurs-a",
    isEarned: true,
    earnedAt,
    code: "FLAG-1234",
  },
  {
    flagId: 2,
    title: "Flaga B",
    courseSlug: "kurs-b",
    isEarned: false,
    earnedAt: null,
    code: null,
  },
];

function pageResponse(overrides: Record<string, unknown> = {}) {
  return {
    flags,
    totalCount: 2,
    page: 1,
    pageSize: 20,
    totalPages: 1,
    allCount: 5,
    earnedCount: 3,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <HangarPage />
    </MemoryRouter>,
  );
}

describe("HangarPage", () => {
  beforeEach(() => {
    getHangarFlags.mockReset();
    getHangarFlags.mockResolvedValue(pageResponse());
    activateFlag.mockReset();
  });

  it("pokazuje tabelę flag i pobiera pierwszą stronę bez filtra", async () => {
    renderPage();

    const table = await screen.findByRole("table");
    const rows = within(table).getAllByRole("row");
    // Nagłówek + dwa wiersze danych, w kolejności z serwera.
    expect(rows).toHaveLength(3);

    const earnedRow = within(rows[1]);
    expect(earnedRow.getByText("Flaga A")).toBeInTheDocument();
    // Kolumna Kurs linkuje do strony kursu po slugu
    expect(earnedRow.getByRole("link", { name: "kurs-a" })).toHaveAttribute(
      "href",
      "/courses/kurs-a",
    );
    expect(earnedRow.getByText("Zdobyta")).toBeInTheDocument();
    expect(
      // Format yyyy-MM-dd HH:mm:ss w strefie lokalnej - sekundy nie zależą od strefy
      earnedRow.getByText(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:30$/),
    ).toBeInTheDocument();

    const unearnedRow = within(rows[2]);
    expect(unearnedRow.getByText("Flaga B")).toBeInTheDocument();
    expect(unearnedRow.getByRole("link", { name: "kurs-b" })).toHaveAttribute(
      "href",
      "/courses/kurs-b",
    );
    expect(unearnedRow.getByText("Niezdobyta")).toBeInTheDocument();
    expect(unearnedRow.getAllByText("—")).toHaveLength(2);

    expect(getHangarFlags).toHaveBeenCalledWith({
      filter: "All",
      page: 1,
      pageSize: 20,
    });
  });

  it("licznik pokazuje earnedCount / allCount z odpowiedzi", async () => {
    renderPage();

    expect(await screen.findByText("Zdobyte flagi: 3 / 5")).toBeInTheDocument();
  });

  it("filtr Zdobyte pobiera pierwszą stronę z filtrem i jest aktywny", async () => {
    renderPage();
    await screen.findByRole("table");

    expect(screen.getByRole("button", { name: "Wszystkie" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    fireEvent.click(screen.getByRole("button", { name: "Zdobyte" }));

    await screen.findByRole("table");
    expect(getHangarFlags).toHaveBeenLastCalledWith({
      filter: "Earned",
      page: 1,
      pageSize: 20,
    });
    expect(screen.getByRole("button", { name: "Zdobyte" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(screen.getByRole("button", { name: "Wszystkie" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("paginacja: Następna pobiera stronę 2, przyciski blokowane na krańcach", async () => {
    getHangarFlags.mockImplementation(async ({ page }: { page: number }) =>
      pageResponse({ page, totalPages: 2, totalCount: 25 }),
    );

    renderPage();

    expect(await screen.findByText("Strona 1 z 2")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Poprzednia" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Następna" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Następna" }));

    expect(await screen.findByText("Strona 2 z 2")).toBeInTheDocument();
    expect(getHangarFlags).toHaveBeenLastCalledWith({
      filter: "All",
      page: 2,
      pageSize: 20,
    });
    expect(screen.getByRole("button", { name: "Następna" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Poprzednia" })).toBeEnabled();
  });

  it("ukrywa paginację, gdy jest jedna strona", async () => {
    renderPage();

    await screen.findByRole("table");
    expect(
      screen.queryByRole("button", { name: "Następna" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Poprzednia" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByText(/^Strona/)).not.toBeInTheDocument();
  });

  it("zdobyta flaga ma zamaskowany kod, odsłaniany po najechaniu i fokusie", async () => {
    renderPage();

    const code = await screen.findByLabelText("Kod flagi Flaga A");
    expect(code).toHaveTextContent("********");
    expect(screen.queryByText("FLAG-1234")).not.toBeInTheDocument();

    fireEvent.mouseEnter(code);
    expect(code).toHaveTextContent("FLAG-1234");
    fireEvent.mouseLeave(code);
    expect(code).toHaveTextContent("********");

    fireEvent.focus(code);
    expect(code).toHaveTextContent("FLAG-1234");
    fireEvent.blur(code);
    expect(code).toHaveTextContent("********");
    expect(screen.queryByText("FLAG-1234")).not.toBeInTheDocument();
  });

  it("niezdobyta flaga nie ma pola kodu", async () => {
    renderPage();

    await screen.findByRole("table");
    expect(
      screen.queryByLabelText("Kod flagi Flaga B"),
    ).not.toBeInTheDocument();
  });

  it("pusta lista bez filtra pokazuje komunikat o braku flag", async () => {
    getHangarFlags.mockResolvedValue(
      pageResponse({ flags: [], totalCount: 0, totalPages: 0, allCount: 0 }),
    );

    renderPage();

    expect(
      await screen.findByText("Brak flag do zdobycia"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("pusta lista z filtrem Zdobyte pokazuje komunikat i zostawia filtry", async () => {
    getHangarFlags.mockImplementation(async ({ filter }: { filter: string }) =>
      filter === "Earned"
        ? pageResponse({
            flags: [],
            totalCount: 0,
            totalPages: 0,
            earnedCount: 0,
          })
        : pageResponse(),
    );

    renderPage();
    await screen.findByRole("table");

    fireEvent.click(screen.getByRole("button", { name: "Zdobyte" }));

    expect(
      await screen.findByText("Nie masz jeszcze żadnej flagi"),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Wszystkie" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Niezdobyte" }),
    ).toBeInTheDocument();
  });

  it("pusta lista z filtrem Niezdobyte pokazuje komunikat", async () => {
    getHangarFlags.mockImplementation(async ({ filter }: { filter: string }) =>
      filter === "Unearned"
        ? pageResponse({ flags: [], totalCount: 0, totalPages: 0 })
        : pageResponse(),
    );

    renderPage();
    await screen.findByRole("table");

    fireEvent.click(screen.getByRole("button", { name: "Niezdobyte" }));

    expect(
      await screen.findByText("Wszystkie flagi zdobyte"),
    ).toBeInTheDocument();
  });

  it("błąd serwisu pokazuje komunikat błędu", async () => {
    getHangarFlags.mockRejectedValue(new Error("Serwer niedostępny"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Serwer niedostępny",
    );
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  describe("aktywacja flagi", () => {
    function typeCode(value: string) {
      fireEvent.change(screen.getByLabelText("Kod flagi"), {
        target: { value },
      });
    }

    function submit() {
      fireEvent.click(screen.getByRole("button", { name: "Aktywuj" }));
    }

    it("pokazuje nagłówek Hangar z trofeami", async () => {
      renderPage();

      expect(
        screen.getByRole("heading", { level: 1, name: "Hangar z trofeami" }),
      ).toBeInTheDocument();
      await screen.findByRole("table");
    });

    it("sekcja aktywacji jest w DOM przed tabelą", async () => {
      renderPage();

      const table = await screen.findByRole("table");
      const heading = screen.getByRole("heading", { name: "Aktywacja flagi" });
      expect(
        heading.compareDocumentPosition(table) &
          Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    });

    it("Aktywuj jest wyłączony przy pustym polu", async () => {
      renderPage();
      await screen.findByRole("table");

      expect(screen.getByRole("button", { name: "Aktywuj" })).toBeDisabled();
      typeCode("   ");
      expect(screen.getByRole("button", { name: "Aktywuj" })).toBeDisabled();
      typeCode("FLAG-1");
      expect(screen.getByRole("button", { name: "Aktywuj" })).toBeEnabled();
    });

    it("wysyłka woła activateFlag z kodem", async () => {
      activateFlag.mockResolvedValue({
        status: "Invalid",
        message: "Nieprawidłowy kod flagi.",
        flagTitle: null,
      });
      renderPage();
      await screen.findByRole("table");

      typeCode("FLAG-XYZ");
      submit();

      await screen.findByRole("status");
      expect(activateFlag).toHaveBeenCalledWith({ code: "FLAG-XYZ" });
    });

    it("Activated pokazuje komunikat, czyści pole i odświeża listę na bieżącej stronie", async () => {
      getHangarFlags.mockImplementation(async ({ page }: { page: number }) =>
        pageResponse({ page, totalPages: 2, totalCount: 25 }),
      );
      activateFlag.mockResolvedValue({
        status: "Activated",
        message: "Flaga aktywowana.",
        flagTitle: "Flaga B",
      });
      renderPage();
      await screen.findByText("Strona 1 z 2");
      fireEvent.click(screen.getByRole("button", { name: "Następna" }));
      await screen.findByText("Strona 2 z 2");
      const callsBefore = getHangarFlags.mock.calls.length;

      typeCode("FLAG-B");
      submit();

      const status = await screen.findByRole("status");
      expect(status).toHaveTextContent("Flaga aktywowana.");
      expect(status).toHaveTextContent("Flaga B");
      expect(screen.getByLabelText("Kod flagi")).toHaveValue("");
      await waitFor(() =>
        expect(getHangarFlags.mock.calls.length).toBe(callsBefore + 1),
      );
      expect(getHangarFlags).toHaveBeenLastCalledWith({
        filter: "All",
        page: 2,
        pageSize: 20,
      });
    });

    it("AlreadyOwned pokazuje komunikat bez ponownego pobierania listy", async () => {
      activateFlag.mockResolvedValue({
        status: "AlreadyOwned",
        message: "Masz już tę flagę.",
        flagTitle: "Flaga A",
      });
      renderPage();
      await screen.findByRole("table");
      const callsBefore = getHangarFlags.mock.calls.length;

      typeCode("FLAG-1234");
      submit();

      expect(await screen.findByRole("status")).toHaveTextContent(
        "Masz już tę flagę.",
      );
      expect(getHangarFlags).toHaveBeenCalledTimes(callsBefore);
    });

    it("Invalid pokazuje komunikat, zostawia kod w polu i nie odświeża listy", async () => {
      activateFlag.mockResolvedValue({
        status: "Invalid",
        message: "Nieprawidłowy kod flagi.",
        flagTitle: null,
      });
      renderPage();
      await screen.findByRole("table");
      const callsBefore = getHangarFlags.mock.calls.length;

      typeCode("ZLY-KOD");
      submit();

      expect(await screen.findByRole("status")).toHaveTextContent(
        "Nieprawidłowy kod flagi.",
      );
      expect(screen.getByLabelText("Kod flagi")).toHaveValue("ZLY-KOD");
      expect(getHangarFlags).toHaveBeenCalledTimes(callsBefore);
    });

    it("błąd serwisu pokazuje alert w sekcji aktywacji, tabela zostaje", async () => {
      activateFlag.mockRejectedValue(new Error("Kod flagi jest wymagany"));
      renderPage();
      await screen.findByRole("table");

      typeCode("FLAG-1");
      submit();

      expect(await screen.findByRole("alert")).toHaveTextContent(
        "Kod flagi jest wymagany",
      );
      expect(screen.getByRole("table")).toBeInTheDocument();
      expect(screen.queryByRole("status")).not.toBeInTheDocument();
    });
  });
});
