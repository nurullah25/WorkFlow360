import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResult, toHttpParams } from '../../shared/paging';
import {
  SaveTask,
  TaskComment,
  TaskDetails,
  TaskHistoryEntry,
  TaskListItem,
  TaskQuery,
  TaskStatus,
} from './task.models';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);

  list(query: TaskQuery): Observable<PagedResult<TaskListItem>> {
    return this.http.get<PagedResult<TaskListItem>>('/api/tasks', { params: toHttpParams(query) });
  }

  get(id: number): Observable<TaskDetails> {
    return this.http.get<TaskDetails>(`/api/tasks/${id}`);
  }

  create(projectId: number, task: SaveTask): Observable<TaskDetails> {
    return this.http.post<TaskDetails>('/api/tasks', { ...task, projectId });
  }

  update(id: number, task: SaveTask): Observable<TaskDetails> {
    return this.http.put<TaskDetails>(`/api/tasks/${id}`, task);
  }

  changeStatus(id: number, status: TaskStatus): Observable<TaskDetails> {
    return this.http.put<TaskDetails>(`/api/tasks/${id}/status`, { status });
  }

  comments(id: number): Observable<TaskComment[]> {
    return this.http.get<TaskComment[]>(`/api/tasks/${id}/comments`);
  }

  addComment(id: number, body: string): Observable<TaskComment> {
    return this.http.post<TaskComment>(`/api/tasks/${id}/comments`, { body });
  }

  history(id: number): Observable<TaskHistoryEntry[]> {
    return this.http.get<TaskHistoryEntry[]>(`/api/tasks/${id}/history`);
  }
}
