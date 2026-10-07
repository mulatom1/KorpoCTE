import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import CoursesPage from "./CoursesPage";

const getCourseTiles = vi.fn();

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
