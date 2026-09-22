export type Role = 'Admin' | 'HR' | 'Manager' | 'Employee';

export interface AuthUser {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  employeeId: number | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}
