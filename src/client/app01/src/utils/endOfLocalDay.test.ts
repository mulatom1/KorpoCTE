import { describe, expect, it } from "vitest";

import { endOfLocalDay } from "./endOfLocalDay";

describe("endOfLocalDay", () => {
  it("zwraca początek następnego dnia w lokalnej strefie jako ISO UTC", () => {
    expect(endOfLocalDay("2026-10-10")).toBe(
      new Date(2026, 9, 11).toISOString(),
    );
  });

  it("przechodzi na następny miesiąc", () => {
    expect(endOfLocalDay("2026-02-28")).toBe(
      new Date(2026, 2, 1).toISOString(),
    );
  });

  it("przechodzi na następny rok", () => {
    expect(endOfLocalDay("2026-12-31")).toBe(
      new Date(2027, 0, 1).toISOString(),
    );
  });
});
