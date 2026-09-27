import { describe, expect, it } from "vitest";

import { parseFrontmatter } from "./parseFrontmatter";

describe("parseFrontmatter", () => {
  it("wyciąga subtitle i zwraca treść bez frontmattera", () => {
    const raw = '---\nsubtitle: "Lekcja 1"\n---\n# Tytuł\n';

    expect(parseFrontmatter(raw)).toEqual({
      frontmatter: { subtitle: "Lekcja 1" },
      content: "# Tytuł\n",
    });
  });

  it("obsługuje końce linii CRLF", () => {
    const raw = "---\r\nsubtitle: Wstęp\r\n---\r\nTreść";

    expect(parseFrontmatter(raw)).toEqual({
      frontmatter: { subtitle: "Wstęp" },
      content: "Treść",
    });
  });

  it("zwraca surową treść, gdy brak frontmattera", () => {
    expect(parseFrontmatter("# Tytuł")).toEqual({
      frontmatter: {},
      content: "# Tytuł",
    });
  });
});
