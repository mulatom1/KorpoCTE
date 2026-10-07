import { describe, expect, it } from "vitest";

import { resolveCourseMediaUrl } from "./courseMediaUrl";

describe("resolveCourseMediaUrl", () => {
  const apiUrl = "https://api.test";
  const mediaBaseUrl = "/media/courses/kurs";

  it("zamienia względną ścieżkę na adres mediów kursu", () => {
    expect(resolveCourseMediaUrl("images/01.png", apiUrl, mediaBaseUrl)).toBe(
      "https://api.test/media/courses/kurs/images/01.png",
    );
  });

  it("usuwa wiodące ./", () => {
    expect(resolveCourseMediaUrl("./thumbnail.png", apiUrl, mediaBaseUrl)).toBe(
      "https://api.test/media/courses/kurs/thumbnail.png",
    );
  });

  it("nie dubluje ukośników", () => {
    expect(
      resolveCourseMediaUrl(
        "images/01.png",
        "https://api.test/",
        "/media/courses/kurs/",
      ),
    ).toBe("https://api.test/media/courses/kurs/images/01.png");
  });

  it.each([
    "/images/przypadek.png",
    "https://x.pl/a.png",
    "#sekcja",
    "?a=1",
    "mailto:a@b.pl",
    "JavaScript:alert(1)",
    "",
  ])("zostawia %s bez zmian", (url) => {
    expect(resolveCourseMediaUrl(url, apiUrl, mediaBaseUrl)).toBe(url);
  });

  it("przy pustym apiUrl zwraca ścieżkę względem hosta", () => {
    expect(resolveCourseMediaUrl("images/01.png", "", mediaBaseUrl)).toBe(
      "/media/courses/kurs/images/01.png",
    );
  });
});
