import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResult, toHttpParams } from '../../shared/paging';
import {
  EmployeeLeaveBalance,
  Holiday,
  LeaveBalance,
  LeaveBalanceQuery,
  LeavePreview,
  LeaveRequest,
  LeaveRequestQuery,
  LeaveType,
} from './leave.models';

@Injectable({ providedIn: 'root' })
export class LeaveService {
  private readonly http = inject(HttpClient);

  // ----- Requests -----

  requests(query: LeaveRequestQuery): Observable<PagedResult<LeaveRequest>> {
    return this.http.get<PagedResult<LeaveRequest>>('/api/leaves', { params: toHttpParams(query) });
  }

  myBalances(year?: number): Observable<LeaveBalance[]> {
    return this.http.get<LeaveBalance[]>('/api/leaves/my-balances', { params: toHttpParams({ year }) });
  }

  preview(leaveTypeId: number, startDate: string, endDate: string): Observable<LeavePreview> {
    return this.http.get<LeavePreview>('/api/leaves/preview', { params: { leaveTypeId, startDate, endDate } });
  }

  submit(request: { leaveTypeId: number; startDate: string; endDate: string; reason: string }): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>('/api/leaves', request);
  }

  approve(id: number, comment: string | null): Observable<LeaveRequest> {
    return this.http.put<LeaveRequest>(`/api/leaves/${id}/approve`, { comment });
  }

  reject(id: number, comment: string): Observable<LeaveRequest> {
    return this.http.put<LeaveRequest>(`/api/leaves/${id}/reject`, { comment });
  }

  cancel(id: number): Observable<LeaveRequest> {
    return this.http.put<LeaveRequest>(`/api/leaves/${id}/cancel`, {});
  }

  // ----- Settings (HR / Admin) -----

  leaveTypes(includeInactive = false): Observable<LeaveType[]> {
    return this.http.get<LeaveType[]>('/api/leave-types', { params: { includeInactive } });
  }

  saveLeaveType(id: number | null, leaveType: Omit<LeaveType, 'id'>): Observable<LeaveType> {
    return id
      ? this.http.put<LeaveType>(`/api/leave-types/${id}`, leaveType)
      : this.http.post<LeaveType>('/api/leave-types', leaveType);
  }

  holidays(year: number): Observable<Holiday[]> {
    return this.http.get<Holiday[]>('/api/holidays', { params: { year } });
  }

  addHoliday(date: string, name: string): Observable<Holiday> {
    return this.http.post<Holiday>('/api/holidays', { date, name });
  }

  deleteHoliday(id: number): Observable<void> {
    return this.http.delete<void>(`/api/holidays/${id}`);
  }

  balances(query: LeaveBalanceQuery): Observable<PagedResult<EmployeeLeaveBalance>> {
    return this.http.get<PagedResult<EmployeeLeaveBalance>>('/api/leave-balances', { params: toHttpParams(query) });
  }

  updateAllocation(id: number, allocatedDays: number): Observable<void> {
    return this.http.put<void>(`/api/leave-balances/${id}`, { allocatedDays });
  }

  generateBalances(year: number): Observable<{ year: number; created: number }> {
    return this.http.post<{ year: number; created: number }>('/api/leave-balances/generate', { year });
  }
}
