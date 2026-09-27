import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import ConfirmModal from "./ConfirmModal";

describe("ConfirmModal", () => {
  const props = {
    title: "Usunąć kupon?",
    message: "Tej operacji nie można cofnąć.",
    onConfirm: () => {},
    onCancel: () => {},
  };

  it("nic nie renderuje, gdy jest zamknięty", () => {
    const { container } = render(<ConfirmModal {...props} isOpen={false} />);

    expect(container).toBeEmptyDOMElement();
  });

  it("wywołuje onConfirm i onCancel po kliknięciu przycisków", () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <ConfirmModal
        {...props}
        isOpen
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    );

    expect(screen.getByText("Usunąć kupon?")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Tak" }));
    fireEvent.click(screen.getByRole("button", { name: "Anuluj" }));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onCancel).toHaveBeenCalledOnce();
  });
});
