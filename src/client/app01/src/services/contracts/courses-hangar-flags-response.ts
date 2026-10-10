export interface CoursesHangarFlagDto {
  flagId: number;
  title: string;
  courseSlug: string;
  isEarned: boolean;
  earnedAt: string | null;
  code: string | null;
}

export interface CoursesHangarFlagsResponse {
  flags: CoursesHangarFlagDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  allCount: number;
  earnedCount: number;
}
