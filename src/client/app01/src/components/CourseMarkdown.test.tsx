import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import CourseMarkdown from "./CourseMarkdown";

function renderMarkdown(content: string) {
  return render(
    <CourseMarkdown
      content={content}
      apiUrl="https://api.test"
      mediaBaseUrl="/media/courses/kurs"
    />,
  );
}

describe("CourseMarkdown", () => {
  it("renderuje nagłówek i tabelę GFM", () => {
    renderMarkdown("# Wstęp\n\n| A | B |\n| - | - |\n| 1 | 2 |\n");

    expect(
      screen.getByRole("heading", { level: 1, name: "Wstęp" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "1" })).toBeInTheDocument();
  });

  it("zamienia względny adres obrazka na adres mediów kursu", () => {
    renderMarkdown("![Diagram](images/01.png)");

    expect(screen.getByRole("img", { name: "Diagram" })).toHaveAttribute(
      "src",
      "https://api.test/media/courses/kurs/images/01.png",
    );
  });

  it("nie renderuje znacznika <script> jako elementu", () => {
    const { container } = renderMarkdown(
      "Tekst\n\n<script>alert(1)</script>\n",
    );

    expect(container.querySelector("script")).toBeNull();
    expect(container.textContent).toContain("<script>alert(1)</script>");
  });

  it("nie zostawia href z javascript:", () => {
    const { container } = renderMarkdown("[kliknij](javascript:alert(1))");

    const link = container.querySelector("a");
    expect(link).not.toBeNull();
    expect(link?.getAttribute("href") ?? "").not.toMatch(/javascript:/i);
  });

  it("otwiera linki zewnętrzne w nowej karcie", () => {
    renderMarkdown("[strona](https://x.pl)");

    const link = screen.getByRole("link", { name: "strona" });
    expect(link).toHaveAttribute("href", "https://x.pl");
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });
});
