import { describe, expect, it } from "vitest";

import { formatDateTime } from "./formatDateTime";

describe("formatDateTime", () => {
  it("formatuje datę jako yyyy-MM-dd HH:mm:ss z zerami wiodącymi, bez milisekund", () => {
    // Konstruktor lokalny - wynik nie zależy od strefy czasowej środowiska testów
    const date = new Date(2026, 0, 5, 7, 8, 9, 45);

    expect(formatDateTime(date)).toBe("2026-01-05 07:08:09");
  });

  it("przelicza ISO string UTC na czas lokalny", () => {
    const iso = "2026-05-31T12:34:56.789Z";
    const local = new Date(iso);
    const expected =
      `${local.getFullYear()}-${String(local.getMonth() + 1).padStart(2, "0")}-` +
      `${String(local.getDate()).padStart(2, "0")} ` +
      `${String(local.getHours()).padStart(2, "0")}:` +
      `${String(local.getMinutes()).padStart(2, "0")}:56`;

    expect(formatDateTime(iso)).toBe(expected);
  });
});
