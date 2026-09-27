export interface UserDto {
  id: number;
  email: string;
  isAdmin: boolean;
  createdAt: string;
}

export interface UserListResponse {
  users: UserDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
