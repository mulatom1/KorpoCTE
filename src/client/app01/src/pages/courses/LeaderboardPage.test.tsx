import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import LeaderboardPage from "./LeaderboardPage";

const getLeaderboard = vi.fn();

vi.mock("../../services/api-courses-service", () => ({
  ApiCoursesService: class {
    getLeaderboard = getLeaderboard;
    setUsrToken = vi.fn();
  },
}));

// Remis: dwóch uczestników na wspólnym 1. miejscu, następny na 3.
const entries = [
  { rank: 1, displayName: "anna", flagCount: 5 },
  { rank: 1, displayName: "bartek", flagCount: 5 },
  { rank: 3, displayName: "celina", flagCount: 2 },
];

function pageResponse(overrides: Record<string, unknown> = {}) {
  return {
    entries,
    totalCount: 3,
    page: 1,
    pageSize: 20,
    totalPages: 1,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <LeaderboardPage />
    </MemoryRouter>,
  );
}

describe("LeaderboardPage", () => {
  beforeEach(() => {
    getLeaderboard.mockReset();
    getLeaderboard.mockResolvedValue(pageResponse());
  });

  it("pokazuje nagłówek Lista zasłużonych", async () => {
    renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: "Lista zasłużonych" }),
    ).toBeInTheDocument();
    await screen.findByRole("table");
  });

  it("tabela pokazuje miejsce, uczestnika i flagi, także wspólne miejsce przy remisie", async () => {
    renderPage();

    const table = await screen.findByRole("table");
    const rows = within(table).getAllByRole("row");
    // Nagłówek + trzy wiersze danych, w kolejności z serwera.
    expect(rows).toHaveLength(4);

    const header = within(rows[0]);
    expect(header.getByText("Miejsce")).toBeInTheDocument();
    expect(header.getByText("Uczestnik")).toBeInTheDocument();
    expect(header.getByText("Flagi")).toBeInTheDocument();

    const cells = rows.slice(1).map((row) =>
      within(row)
        .getAllByRole("cell")
        .map((cell) => cell.textContent),
    );
    expect(cells).toEqual([
      ["1", "anna", "5"],
      ["1", "bartek", "5"],
      ["3", "celina", "2"],
    ]);

    expect(getLeaderboard).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it("paginacja: Następna pobiera stronę 2, przyciski blokowane na krańcach", async () => {
    getLeaderboard.mockImplementation(async ({ page }: { page: number }) =>
      pageResponse({ page, totalPages: 2, totalCount: 25 }),
    );

    renderPage();

    expect(await screen.findByText("Strona 1 z 2")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Poprzednia" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Następna" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Następna" }));

    expect(await screen.findByText("Strona 2 z 2")).toBeInTheDocument();
    expect(getLeaderboard).toHaveBeenLastCalledWith({ page: 2, pageSize: 20 });
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

  it("pusta lista pokazuje komunikat", async () => {
    getLeaderboard.mockResolvedValue(
      pageResponse({ entries: [], totalCount: 0, totalPages: 0 }),
    );

    renderPage();

    expect(
      await screen.findByText("Nikt jeszcze nie zdobył flagi"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("błąd serwisu pokazuje komunikat błędu", async () => {
    getLeaderboard.mockRejectedValue(new Error("Serwer niedostępny"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Serwer niedostępny",
    );
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
});
