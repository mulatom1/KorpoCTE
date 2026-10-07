export interface CourseTileDto {
  slug: string;
  title: string;
  shortDescription: string;
  tags: string[];
  imageUrl: string | null;
}

export interface CoursesCourseTilesResponse {
  courses: CourseTileDto[];
}
