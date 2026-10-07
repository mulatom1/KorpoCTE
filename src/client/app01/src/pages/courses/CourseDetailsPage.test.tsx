import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import CourseDetailsPage from "./CourseDetailsPage";

const getCourseContent = vi.fn();

vi.mock("../../services/api-courses-service", () => ({
  ApiCoursesService: class {
    getCourseContent = getCourseContent;
    setUsrToken = vi.fn();
  },
}));

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/courses/kurs-a"]}>
      <Routes>
        <Route path="courses/:slug" element={<CourseDetailsPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("CourseDetailsPage", () => {
  beforeEach(() => {
    getCourseContent.mockReset();
  });

  it("pobiera kurs ze slugiem z adresu", async () => {
    getCourseContent.mockResolvedValue({
      slug: "kurs-a",
      title: "Kurs A",
      tags: [],
      content: "Treść",
      mediaBaseUrl: "/media/courses/kurs-a",
    });

    renderPage();

    expect(await screen.findByText("Treść")).toBeInTheDocument();
    expect(getCourseContent).toHaveBeenCalledWith({ slug: "kurs-a" });
  });

  it("pokazuje tytuł, tagi i treść kursu", async () => {
    getCourseContent.mockResolvedValue({
      slug: "kurs-a",
      title: "Kurs A",
      tags: ["sql", "cte"],
      content: "## Rozdział 1\n\nPierwszy akapit",
      mediaBaseUrl: "/media/courses/kurs-a",
    });

    renderPage();

    expect(
      await screen.findByRole("heading", { level: 1, name: "Kurs A" }),
    ).toBeInTheDocument();
    expect(screen.getByText("sql")).toBeInTheDocument();
    expect(screen.getByText("cte")).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { level: 2, name: "Rozdział 1" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Pierwszy akapit")).toBeInTheDocument();
    expect(document.title).toBe("Kurs A | tomsoft1 workspace");
  });

  it("pokazuje komunikat błędu, gdy pobranie się nie powiedzie", async () => {
    getCourseContent.mockRejectedValue(
      new Error("Kurs nie istnieje lub nie jest jeszcze opublikowany"),
    );

    renderPage();

    expect(
      await screen.findByText(
        "Kurs nie istnieje lub nie jest jeszcze opublikowany",
      ),
    ).toBeInTheDocument();
  });

  it("ma przycisk powrotu do listy kursów", async () => {
    getCourseContent.mockResolvedValue({
      slug: "kurs-a",
      title: "Kurs A",
      tags: [],
      content: "Treść",
      mediaBaseUrl: "/media/courses/kurs-a",
    });

    renderPage();

    expect(
      await screen.findByRole("button", { name: "Powrót do kursów" }),
    ).toBeInTheDocument();
  });
});
