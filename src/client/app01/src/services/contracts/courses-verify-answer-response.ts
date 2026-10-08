export type CoursesVerifyAnswerStatus =
  "Correct" | "Incorrect" | "Unavailable" | "AlreadyOwned";

export interface CoursesVerifyAnswerResponse {
  status: CoursesVerifyAnswerStatus;
  message: string;
  // Kod flagi do aktywacji – wypełniony wyłącznie dla statusu Correct.
  code?: string | null;
}
