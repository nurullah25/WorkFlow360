import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { Role } from '../../core/auth/auth.models';
import { PagedResult, toHttpParams } from '../../shared/paging';

export const ROLES: Role[] = ['Admin', 'HR', 'Manager', 'Employee'];

export interface UserListItem {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  lastLoginAt: string | null;
  employeeId: number | null;
  employeeCode: string | null;
}

export interface CreateUser {
  email: string;
  fullName: string;
  role: Role;
  password: string;
  employeeId: number | null;
}

export interface UpdateUser {
  fullName: string;
  role: Role;
  isActive: boolean;
}

export interface UserQuery {
  page: number;
  pageSize: number;
  search?: string;
  role?: Role | '';
  isActive?: boolean | null;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc' | '';
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);

  list(query: UserQuery): Observable<PagedResult<UserListItem>> {
    return this.http.get<PagedResult<UserListItem>>('/api/users', { params: toHttpParams(query) });
  }

  create(user: CreateUser): Observable<UserListItem> {
    return this.http.post<UserListItem>('/api/users', user);
  }

  update(id: number, user: UpdateUser): Observable<UserListItem> {
    return this.http.put<UserListItem>(`/api/users/${id}`, user);
  }

  resetPassword(id: number, newPassword: string): Observable<void> {
    return this.http.put<void>(`/api/users/${id}/password`, { newPassword });
  }
}
