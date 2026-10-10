export type CoursesActivateFlagStatus =
  "Activated" | "AlreadyOwned" | "Invalid";

export interface CoursesActivateFlagResponse {
  status: CoursesActivateFlagStatus;
  // Stały tekst dla statusu – odpowiedź nigdy nie zawiera kodu flagi.
  message: string;
  // Tytuł flagi – wypełniony dla Activated i AlreadyOwned, null dla Invalid.
  flagTitle: string | null;
}
