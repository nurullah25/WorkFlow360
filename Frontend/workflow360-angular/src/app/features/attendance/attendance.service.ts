import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResult, toHttpParams } from '../../shared/paging';
import { EmployeeMonth, TeamAttendanceRow, TodayAttendance, YearMonth } from './attendance.models';

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);

  today(): Observable<TodayAttendance> {
    return this.http.get<TodayAttendance>('/api/attendance/today');
  }

  checkIn(): Observable<TodayAttendance> {
    return this.http.post<TodayAttendance>('/api/attendance/check-in', {});
  }

  checkOut(): Observable<TodayAttendance> {
    return this.http.post<TodayAttendance>('/api/attendance/check-out', {});
  }

  myMonth(period: YearMonth): Observable<EmployeeMonth> {
    return this.http.get<EmployeeMonth>('/api/attendance/month', { params: toHttpParams(period) });
  }

  employeeMonth(employeeId: number, period: YearMonth): Observable<EmployeeMonth> {
    return this.http.get<EmployeeMonth>(`/api/attendance/employees/${employeeId}/month`, {
      params: toHttpParams(period),
    });
  }

  team(period: YearMonth, page: number, pageSize: number, search: string): Observable<PagedResult<TeamAttendanceRow>> {
    return this.http.get<PagedResult<TeamAttendanceRow>>('/api/attendance/team', {
      params: toHttpParams({ ...period, page, pageSize, search }),
    });
  }
}
