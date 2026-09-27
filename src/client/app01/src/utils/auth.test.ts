import { describe, expect, it } from "vitest";

import { buildLoginUrl, isAuthenticated, setAuth } from "./auth";

describe("isAuthenticated", () => {
  it("zwraca false, gdy brak tokenu", () => {
    expect(isAuthenticated()).toBe(false);
  });

  it("zwraca true dla ważnego tokenu", () => {
    setAuth({
      token: "abc",
      tokenExpiresAt: new Date(Date.now() + 60_000).toISOString(),
      email: "a@b.pl",
      id: 1,
    });

    expect(isAuthenticated()).toBe(true);
  });

  it("zwraca false dla wygasłego tokenu", () => {
    setAuth({
      token: "abc",
      tokenExpiresAt: new Date(Date.now() - 60_000).toISOString(),
      email: "a@b.pl",
      id: 1,
    });

    expect(isAuthenticated()).toBe(false);
  });

  it("zwraca false dla nieprawidłowej daty wygaśnięcia", () => {
    localStorage.setItem("token", "abc");
    localStorage.setItem("tokenExpiresAt", "nie-data");

    expect(isAuthenticated()).toBe(false);
  });
});

describe("buildLoginUrl", () => {
  it("dokleja zakodowany returnUrl", () => {
    expect(buildLoginUrl("/courses/flags?x=1")).toBe(
      "/login?returnUrl=%2Fcourses%2Fflags%3Fx%3D1",
    );
  });

  it("nie zapętla przekierowania na /login", () => {
    expect(buildLoginUrl("/login?returnUrl=%2F")).toBe("/login");
    expect(buildLoginUrl("")).toBe("/login");
  });
});
