export interface CoursesHangarTaskDto {
  flagId: number;
  courseSlug: string;
  title: string;
  isOwned: boolean;
}

export interface CoursesHangarTasksResponse {
  tasks: CoursesHangarTaskDto[];
}
