import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";

import CourseTile from "./CourseTile";
import type { CourseTileDto } from "../services/contracts/courses-course-tiles-response";

function renderWithRouter(ui: ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

describe("CourseTile", () => {
  const tile: CourseTileDto = {
    slug: "korpo-cte-3_1",
    title: "CTE w praktyce",
    shortDescription: "Rekurencyjne zapytania w T-SQL",
    tags: ["sql", "cte"],
    imageUrl: "/media/courses/korpo-cte-3_1/cover.png",
  };

  it("renderuje tytuł, opis, tagi i grafikę z adresem API", () => {
    renderWithRouter(
      <CourseTile tile={tile} apiUrl="https://api.test" index={0} isVisible />,
    );

    expect(screen.getByText("CTE w praktyce")).toBeInTheDocument();
    expect(
      screen.getByText("Rekurencyjne zapytania w T-SQL"),
    ).toBeInTheDocument();
    expect(screen.getByText("sql")).toBeInTheDocument();
    expect(screen.getByText("cte")).toBeInTheDocument();
    expect(screen.getByRole("img")).toHaveAttribute(
      "src",
      "https://api.test/media/courses/korpo-cte-3_1/cover.png",
    );
  });

  it("pokazuje placeholder zamiast grafiki, gdy imageUrl jest null", () => {
    renderWithRouter(
      <CourseTile
        tile={{ ...tile, imageUrl: null }}
        apiUrl="https://api.test"
        index={0}
        isVisible
      />,
    );

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
    expect(screen.getByTestId("course-tile-placeholder")).toBeInTheDocument();
  });

  it("jest linkiem do /courses/<slug>", () => {
    renderWithRouter(
      <CourseTile tile={tile} apiUrl="https://api.test" index={0} isVisible />,
    );

    expect(screen.getByRole("link")).toHaveAttribute(
      "href",
      "/courses/korpo-cte-3_1",
    );
  });
});
