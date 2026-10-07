import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import CourseTile from "./CourseTile";
import type { CourseTileDto } from "../services/contracts/courses-course-tiles-response";

describe("CourseTile", () => {
  const tile: CourseTileDto = {
    slug: "korpo-cte-3_1",
    title: "CTE w praktyce",
    shortDescription: "Rekurencyjne zapytania w T-SQL",
    tags: ["sql", "cte"],
    imageUrl: "/media/courses/korpo-cte-3_1/cover.png",
  };

  it("renderuje tytuł, opis, tagi i grafikę z adresem API", () => {
    render(
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
    render(
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

  it("nie jest linkiem", () => {
    render(
      <CourseTile tile={tile} apiUrl="https://api.test" index={0} isVisible />,
    );

    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
