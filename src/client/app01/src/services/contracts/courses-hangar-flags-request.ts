export type CoursesHangarFlagsFilter = "All" | "Earned" | "Unearned";

export interface CoursesHangarFlagsRequest {
  filter: CoursesHangarFlagsFilter;
  page: number;
  pageSize: number;
}
