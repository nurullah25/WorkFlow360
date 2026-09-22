import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface Department {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  employeeCount: number;
}

export interface Designation {
  id: number;
  title: string;
  isActive: boolean;
  employeeCount: number;
}

export interface SaveDepartment {
  code: string;
  name: string;
  isActive: boolean;
}

export interface SaveDesignation {
  title: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class OrganisationService {
  private readonly http = inject(HttpClient);

  departments(includeInactive = false): Observable<Department[]> {
    return this.http.get<Department[]>('/api/departments', { params: { includeInactive } });
  }

  createDepartment(department: SaveDepartment): Observable<Department> {
    return this.http.post<Department>('/api/departments', department);
  }

  updateDepartment(id: number, department: SaveDepartment): Observable<Department> {
    return this.http.put<Department>(`/api/departments/${id}`, department);
  }

  designations(includeInactive = false): Observable<Designation[]> {
    return this.http.get<Designation[]>('/api/designations', { params: { includeInactive } });
  }

  createDesignation(designation: SaveDesignation): Observable<Designation> {
    return this.http.post<Designation>('/api/designations', designation);
  }

  updateDesignation(id: number, designation: SaveDesignation): Observable<Designation> {
    return this.http.put<Designation>(`/api/designations/${id}`, designation);
  }
}
