import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import TomoAiTerminalPage from "./TomoAiTerminalPage";

const getHangarTasks = vi.fn();
const verifyAnswer = vi.fn();

vi.mock("../../services/api-courses-service", () => ({
  ApiCoursesService: class {
    getHangarTasks = getHangarTasks;
    verifyAnswer = verifyAnswer;
    setUsrToken = vi.fn();
  },
}));

const tasks = [
  { flagId: 1, courseSlug: "kurs-a", title: "Zadanie A", isOwned: false },
  { flagId: 2, courseSlug: "kurs-b", title: "Zadanie B", isOwned: true },
];

function renderPage() {
  return render(
    <MemoryRouter>
      <TomoAiTerminalPage />
    </MemoryRouter>,
  );
}

async function fillForm(answer = "Moja odpowiedź") {
  const select = await screen.findByLabelText("Zadanie");
  fireEvent.change(select, { target: { value: "1" } });
  fireEvent.change(screen.getByLabelText("Twoja odpowiedź"), {
    target: { value: answer },
  });
}

describe("TomoAiTerminalPage", () => {
  beforeEach(() => {
    getHangarTasks.mockReset();
    verifyAnswer.mockReset();
    getHangarTasks.mockResolvedValue({ tasks });
  });

  it("renderuje zadania z listy, a zdobyte są wyłączone", async () => {
    renderPage();

    const free = await screen.findByRole("option", { name: "Zadanie A" });
    const owned = screen.getByRole("option", { name: "Zadanie B (zdobyta)" });

    expect(free).not.toBeDisabled();
    expect(owned).toBeDisabled();
  });

  it("pokazuje komunikat przy pustej liście zadań", async () => {
    getHangarTasks.mockResolvedValue({ tasks: [] });

    renderPage();

    expect(
      await screen.findByText("Brak zadań do sprawdzenia"),
    ).toBeInTheDocument();
  });

  it("błąd pobrania listy pokazuje sam błąd, bez komunikatu o pustej liście", async () => {
    getHangarTasks.mockRejectedValue(new Error("Serwer niedostępny"));

    renderPage();

    expect(await screen.findByText("Serwer niedostępny")).toBeInTheDocument();
    expect(
      screen.queryByText("Brak zadań do sprawdzenia"),
    ).not.toBeInTheDocument();
  });

  it("przycisk jest wyłączony bez zadania i bez odpowiedzi", async () => {
    renderPage();

    const button = await screen.findByRole("button", { name: "Sprawdź" });
    expect(button).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Zadanie"), {
      target: { value: "1" },
    });
    expect(button).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Twoja odpowiedź"), {
      target: { value: "Odpowiedź" },
    });
    expect(button).toBeEnabled();
  });

  it("Correct pokazuje sukces i kod flagi z odpowiedzi", async () => {
    verifyAnswer.mockResolvedValue({
      status: "Correct",
      message:
        "Odpowiedź poprawna! Zapisz kod flagi i aktywuj go w formularzu aktywacji.",
      code: "FLAG-1234",
    });

    renderPage();
    await fillForm();
    fireEvent.click(screen.getByRole("button", { name: "Sprawdź" }));

    // Komunikat z serwera wyświetlony dokładnie raz (bez powtórzonego nagłówka)
    expect(await screen.findAllByText(/Odpowiedź poprawna/)).toHaveLength(1);
    expect(screen.getByTestId("flag-code")).toHaveTextContent("FLAG-1234");
    expect(screen.getByLabelText("Twoja odpowiedź")).toHaveValue("");
    expect(verifyAnswer).toHaveBeenCalledWith({
      flagId: 1,
      answer: "Moja odpowiedź",
    });
  });

  it("Incorrect pokazuje komunikat negatywny bez przycisku ponowienia", async () => {
    verifyAnswer.mockResolvedValue({
      status: "Incorrect",
      message: "Odpowiedź niepoprawna.",
      code: null,
    });

    renderPage();
    await fillForm();
    fireEvent.click(screen.getByRole("button", { name: "Sprawdź" }));

    // Komunikat z serwera wyświetlony dokładnie raz (bez powtórzonego nagłówka)
    expect(await screen.findAllByText(/Odpowiedź niepoprawna/)).toHaveLength(1);
    expect(
      screen.queryByRole("button", { name: "Spróbuj ponownie" }),
    ).not.toBeInTheDocument();
    expect(screen.getByLabelText("Twoja odpowiedź")).toHaveValue(
      "Moja odpowiedź",
    );
  });

  it("Unavailable pokazuje „Spróbuj ponownie” i ponawia te same argumenty", async () => {
    verifyAnswer.mockResolvedValue({
      status: "Unavailable",
      message: "Weryfikacja chwilowo niedostępna.",
      code: null,
    });

    renderPage();
    await fillForm();
    fireEvent.click(screen.getByRole("button", { name: "Sprawdź" }));

    const retry = await screen.findByRole("button", {
      name: "Spróbuj ponownie",
    });
    fireEvent.click(retry);

    await waitFor(() => expect(verifyAnswer).toHaveBeenCalledTimes(2));
    expect(verifyAnswer).toHaveBeenNthCalledWith(1, {
      flagId: 1,
      answer: "Moja odpowiedź",
    });
    expect(verifyAnswer).toHaveBeenNthCalledWith(2, {
      flagId: 1,
      answer: "Moja odpowiedź",
    });
  });

  it("AlreadyOwned pokazuje komunikat i odświeża listę", async () => {
    verifyAnswer.mockResolvedValue({
      status: "AlreadyOwned",
      message: "Ta flaga jest już zdobyta.",
      code: null,
    });

    renderPage();
    await fillForm();
    fireEvent.click(screen.getByRole("button", { name: "Sprawdź" }));

    expect(
      await screen.findByText("Ta flaga jest już zdobyta."),
    ).toBeInTheDocument();
    await waitFor(() => expect(getHangarTasks).toHaveBeenCalledTimes(2));
  });

  it("pokazuje błąd HTTP z treścią z serwera", async () => {
    verifyAnswer.mockRejectedValue(new Error("Zadanie nie jest dostępne"));

    renderPage();
    await fillForm();
    fireEvent.click(screen.getByRole("button", { name: "Sprawdź" }));

    expect(
      await screen.findByText("Zadanie nie jest dostępne"),
    ).toBeInTheDocument();
  });

  it("w trakcie oceny przycisk jest wyłączony", async () => {
    let resolveVerify: (value: unknown) => void = () => {};
    verifyAnswer.mockReturnValue(
      new Promise((resolve) => {
        resolveVerify = resolve;
      }),
    );

    renderPage();
    await fillForm();
    fireEvent.click(screen.getByRole("button", { name: "Sprawdź" }));

    const checking = await screen.findByRole("button", { name: "Sprawdzam…" });
    expect(checking).toBeDisabled();

    resolveVerify({ status: "Incorrect", message: "Nie.", code: null });
    expect(
      await screen.findByRole("button", { name: "Sprawdź" }),
    ).toBeEnabled();
  });
});
