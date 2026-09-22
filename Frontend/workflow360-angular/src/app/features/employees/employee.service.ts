import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResult, toHttpParams } from '../../shared/paging';
import {
  EmployeeDetails,
  EmployeeListItem,
  EmployeeLookup,
  EmployeeQuery,
  SaveEmployee,
} from './employee.models';

@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly http = inject(HttpClient);

  list(query: EmployeeQuery): Observable<PagedResult<EmployeeListItem>> {
    return this.http.get<PagedResult<EmployeeListItem>>('/api/employees', {
      params: toHttpParams(query),
    });
  }

  get(id: number): Observable<EmployeeDetails> {
    return this.http.get<EmployeeDetails>(`/api/employees/${id}`);
  }

  lookup(search: string, withoutUserAccount = false): Observable<EmployeeLookup[]> {
    return this.http.get<EmployeeLookup[]>('/api/employees/lookup', {
      params: toHttpParams({ search, withoutUserAccount }),
    });
  }

  create(employee: SaveEmployee): Observable<EmployeeDetails> {
    return this.http.post<EmployeeDetails>('/api/employees', employee);
  }

  update(id: number, employee: SaveEmployee): Observable<EmployeeDetails> {
    return this.http.put<EmployeeDetails>(`/api/employees/${id}`, employee);
  }
}
