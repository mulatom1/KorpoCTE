export interface CoursesLeaderboardEntryDto {
  rank: number;
  displayName: string;
  flagCount: number;
}

export interface CoursesLeaderboardResponse {
  entries: CoursesLeaderboardEntryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
