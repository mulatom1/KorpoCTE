import { describe, expect, it } from "vitest";

import { setAuth } from "./auth";
import { getIsAdminFromToken } from "./jwt";

function fakeJwt(payload: object): string {
  return `header.${btoa(JSON.stringify(payload))}.signature`;
}

function login(token: string, expiresInMs = 60_000): void {
  setAuth({
    token,
    tokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    email: "a@b.pl",
    id: 1,
  });
}

describe("getIsAdminFromToken", () => {
  it("zwraca true dla claimu isAdmin jako bool lub string", () => {
    login(fakeJwt({ isAdmin: true }));
    expect(getIsAdminFromToken()).toBe(true);

    login(fakeJwt({ isAdmin: "true" }));
    expect(getIsAdminFromToken()).toBe(true);
  });

  it("zwraca false dla zwykłego użytkownika", () => {
    login(fakeJwt({ isAdmin: "false" }));
    expect(getIsAdminFromToken()).toBe(false);
  });

  it("zwraca false, gdy sesja wygasła", () => {
    login(fakeJwt({ isAdmin: true }), -60_000);
    expect(getIsAdminFromToken()).toBe(false);
  });

  it("zwraca false dla uszkodzonego tokenu", () => {
    login("to-nie-jest-jwt");
    expect(getIsAdminFromToken()).toBe(false);
  });
});
