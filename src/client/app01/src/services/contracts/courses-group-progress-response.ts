export interface CoursesGroupProgressResponse {
  asOf: string;
  userCount: number;
  availableFlagCount: number;
  earnedFlagCount: number;
  // null, gdy nie ma żadnej flagi do zdobycia.
  earnedPercent: number | null;
}
