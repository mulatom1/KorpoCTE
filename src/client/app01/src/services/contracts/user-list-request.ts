export interface UserListRequest {
  email?: string;
  isAdmin?: boolean;
  page: number;
  pageSize: number;
}
