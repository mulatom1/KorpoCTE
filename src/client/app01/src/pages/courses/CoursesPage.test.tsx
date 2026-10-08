import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import CoursesPage from "./CoursesPage";

const getCourseTiles = vi.fn();
const isAuthenticated = vi.fn();

vi.mock("../../utils/auth", () => ({
  isAuthenticated: () => isAuthenticated(),
}));

vi.mock("../../services/api-courses-service", () => ({
  ApiCoursesService: class {
    getCourseTiles = getCourseTiles;
  },
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <CoursesPage />
    </MemoryRouter>,
  );
}

describe("CoursesPage", () => {
  beforeEach(() => {
    getCourseTiles.mockReset();
    isAuthenticated.mockReset();
    isAuthenticated.mockReturnValue(false);
  });

  it("renderuje kursy w kolejności z odpowiedzi", async () => {
    getCourseTiles.mockResolvedValue({
      courses: [
        {
          slug: "b-kurs",
          title: "Kurs B",
          shortDescription: "Opis B",
          tags: [],
          imageUrl: null,
        },
        {
          slug: "a-kurs",
          title: "Kurs A",
          shortDescription: "Opis A",
          tags: ["sql"],
          imageUrl: null,
        },
      ],
    });

    renderPage();

    const headings = await screen.findAllByRole("heading", { level: 3 });
    expect(headings.map((h) => h.textContent)).toEqual(["Kurs B", "Kurs A"]);
  });

  it("gość nie widzi podmenu z terminalem TOMO-AI-001", async () => {
    getCourseTiles.mockResolvedValue({ courses: [] });

    renderPage();

    await screen.findByText("Brak opublikowanych kursów");
    expect(
      screen.queryByRole("button", {
        name: "Terminal TOMO-AI-001",
      }),
    ).not.toBeInTheDocument();
  });

  it("zalogowany widzi podmenu z terminalem TOMO-AI-001", async () => {
    isAuthenticated.mockReturnValue(true);
    getCourseTiles.mockResolvedValue({ courses: [] });

    renderPage();

    await screen.findByText("Brak opublikowanych kursów");
    expect(
      screen.getByRole("button", {
        name: "Terminal TOMO-AI-001",
      }),
    ).toBeInTheDocument();
  });

  it("pokazuje komunikat, gdy brak kursów", async () => {
    getCourseTiles.mockResolvedValue({ courses: [] });

    renderPage();

    expect(
      await screen.findByText("Brak opublikowanych kursów"),
    ).toBeInTheDocument();
  });

  it("pokazuje komunikat błędu, gdy pobranie się nie powiedzie", async () => {
    getCourseTiles.mockRejectedValue(new Error("Serwer niedostępny"));

    renderPage();

    expect(await screen.findByText("Serwer niedostępny")).toBeInTheDocument();
    expect(
      screen.queryByText("Brak opublikowanych kursów"),
    ).not.toBeInTheDocument();
  });
});
