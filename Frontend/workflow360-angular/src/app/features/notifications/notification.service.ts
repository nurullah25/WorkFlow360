import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, exhaustMap, filter, fromEvent, map, merge, Observable, tap, timer } from 'rxjs';

import { PagedResult, toHttpParams } from '../../shared/paging';

export interface AppNotification {
  id: number;
  type: string;
  title: string;
  message: string;
  linkUrl: string | null;
  isRead: boolean;
  createdAt: string;
}

const POLL_INTERVAL_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);

  readonly unreadCount = signal(0);

  /**
   * Keeps {@link unreadCount} fresh: once a minute while the tab is visible, and straight away when the
   * user comes back to the tab. Hidden tabs don't poll, and a slow request is never overlapped by the next.
   */
  pollUnreadCount(): Observable<number> {
    return merge(timer(0, POLL_INTERVAL_MS), fromEvent(document, 'visibilitychange')).pipe(
      filter(() => document.visibilityState === 'visible'),
      exhaustMap(() => this.fetchUnreadCount()),
    );
  }

  refreshUnreadCount(): void {
    this.fetchUnreadCount().subscribe();
  }

  list(page: number, pageSize: number, unreadOnly = false): Observable<PagedResult<AppNotification>> {
    return this.http.get<PagedResult<AppNotification>>('/api/notifications', {
      params: toHttpParams({ page, pageSize, unreadOnly }),
    });
  }

  markRead(id: number): Observable<void> {
    return this.http.put<void>(`/api/notifications/${id}/read`, {}).pipe(tap(() => this.refreshUnreadCount()));
  }

  markAllRead(): Observable<void> {
    return this.http.put<void>('/api/notifications/read-all', {}).pipe(tap(() => this.unreadCount.set(0)));
  }

  private fetchUnreadCount(): Observable<number> {
    return this.http.get<{ count: number }>('/api/notifications/unread-count').pipe(
      map((result) => result.count),
      tap((count) => this.unreadCount.set(count)),
      catchError(() => EMPTY),
    );
  }
}
