import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import GroupProgressPage from "./GroupProgressPage";
import { endOfLocalDay } from "../../utils/endOfLocalDay";
import { formatDateTime } from "../../utils/formatDateTime";

const getGroupProgress = vi.fn();

vi.mock("../../services/api-courses-service", () => ({
  ApiCoursesService: class {
    getGroupProgress = getGroupProgress;
    setUsrToken = vi.fn();
  },
}));

function progressResponse(overrides: Record<string, unknown> = {}) {
  return {
    asOf: "2026-10-10T22:00:00Z",
    userCount: 3,
    availableFlagCount: 10,
    earnedFlagCount: 2,
    earnedPercent: 20,
    ...overrides,
  };
}

// Wartość kafelka o podanym tytule.
function tileValue(label: string): string | null | undefined {
  return screen.getByRole("heading", { level: 2, name: label }).nextSibling
    ?.textContent;
}

function renderPage() {
  return render(
    <MemoryRouter>
      <GroupProgressPage />
    </MemoryRouter>,
  );
}

describe("GroupProgressPage", () => {
  beforeEach(() => {
    getGroupProgress.mockReset();
    getGroupProgress.mockResolvedValue(progressResponse());
  });

  it("start pobiera stan na koniec dzisiejszego dnia i pokazuje cztery wskaźniki", async () => {
    renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: "Postęp grupy" }),
    ).toBeInTheDocument();
    await screen.findByRole("heading", {
      level: 2,
      name: "Użytkownicy z flagą",
    });

    const today = formatDateTime(new Date()).slice(0, 10);
    expect(screen.getByLabelText("Stan na dzień")).toHaveValue(today);
    expect(screen.getByLabelText("Stan na dzień")).toHaveAttribute(
      "max",
      today,
    );
    expect(getGroupProgress).toHaveBeenCalledWith({
      asOf: endOfLocalDay(today),
    });

    expect(tileValue("Użytkownicy z flagą")).toBe("3");
    expect(tileValue("Flagi do zdobycia")).toBe("10");
    expect(tileValue("Flagi zdobyte przez grupę")).toBe("2");
    expect(tileValue("Procent zdobytych flag")).toBe("20.0%");
  });

  it("procent null pokazuje się jako —", async () => {
    getGroupProgress.mockResolvedValue(
      progressResponse({
        availableFlagCount: 0,
        earnedFlagCount: 0,
        earnedPercent: null,
      }),
    );

    renderPage();

    await screen.findByRole("heading", {
      level: 2,
      name: "Procent zdobytych flag",
    });
    expect(tileValue("Procent zdobytych flag")).toBe("—");
  });

  it("zmiana daty pobiera dane z nowym asOf", async () => {
    renderPage();
    await screen.findByRole("heading", {
      level: 2,
      name: "Użytkownicy z flagą",
    });

    getGroupProgress.mockResolvedValue(progressResponse({ userCount: 1 }));
    fireEvent.change(screen.getByLabelText("Stan na dzień"), {
      target: { value: "2026-01-15" },
    });

    expect(getGroupProgress).toHaveBeenLastCalledWith({
      asOf: endOfLocalDay("2026-01-15"),
    });
    await screen.findByRole("heading", {
      level: 2,
      name: "Użytkownicy z flagą",
    });
    expect(tileValue("Użytkownicy z flagą")).toBe("1");
  });

  it("błąd serwisu pokazuje komunikat błędu", async () => {
    getGroupProgress.mockRejectedValue(new Error("Serwer niedostępny"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Serwer niedostępny",
    );
    expect(
      screen.queryByRole("heading", { level: 2, name: "Użytkownicy z flagą" }),
    ).not.toBeInTheDocument();
  });
});
