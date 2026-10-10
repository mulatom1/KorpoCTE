export interface CoursesGroupProgressRequest {
  // Moment (ISO UTC), na który liczone są wskaźniki; null = teraz.
  asOf: string | null;
}
