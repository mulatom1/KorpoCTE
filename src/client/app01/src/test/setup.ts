import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

// Każdy test startuje z czystym DOM i pustym localStorage (stan sesji).
afterEach(() => {
  cleanup();
  localStorage.clear();
});
