import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResult, toHttpParams } from '../../shared/paging';
import { ProjectDetails, ProjectListItem, ProjectQuery, SaveProject } from './project.models';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);

  list(query: ProjectQuery): Observable<PagedResult<ProjectListItem>> {
    return this.http.get<PagedResult<ProjectListItem>>('/api/projects', { params: toHttpParams(query) });
  }

  get(id: number): Observable<ProjectDetails> {
    return this.http.get<ProjectDetails>(`/api/projects/${id}`);
  }

  create(project: SaveProject): Observable<ProjectDetails> {
    return this.http.post<ProjectDetails>('/api/projects', project);
  }

  update(id: number, project: SaveProject): Observable<ProjectDetails> {
    return this.http.put<ProjectDetails>(`/api/projects/${id}`, project);
  }

  addMember(id: number, employeeId: number, roleInProject: string | null): Observable<ProjectDetails> {
    return this.http.post<ProjectDetails>(`/api/projects/${id}/members`, { employeeId, roleInProject });
  }

  removeMember(id: number, employeeId: number): Observable<ProjectDetails> {
    return this.http.delete<ProjectDetails>(`/api/projects/${id}/members/${employeeId}`);
  }
}
