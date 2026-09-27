export interface UserLoginResponse {
  id: number;
  email: string;
  isAdmin: boolean;
  createdAt: string;
  token: string;
  tokenExpiresAt: string;
}
