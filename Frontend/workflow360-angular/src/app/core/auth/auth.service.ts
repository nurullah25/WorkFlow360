import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  catchError,
  finalize,
  firstValueFrom,
  map,
  Observable,
  of,
  shareReplay,
  tap,
  throwError,
} from 'rxjs';

import { AuthResponse, AuthUser, Role } from './auth.models';

/**
 * The access token lives only in memory, so it is never exposed to other scripts through
 * localStorage. The refresh token is an HttpOnly cookie the browser sends to /api/auth/*,
 * which is how a page reload gets a new access token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly currentUser = signal<AuthUser | null>(null);
  private token: string | null = null;
  private refreshInFlight$: Observable<AuthResponse> | null = null;

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  accessToken(): string | null {
    return this.token;
  }

  hasRole(...roles: Role[]): boolean {
    const user = this.currentUser();
    return user !== null && roles.includes(user.role);
  }

  login(email: string, password: string): Observable<AuthUser> {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password }).pipe(
      tap((response) => this.startSession(response)),
      map((response) => response.user),
    );
  }

  /** Several requests can fail with 401 at the same time; they all share one refresh call. */
  refresh(): Observable<AuthResponse> {
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http.post<AuthResponse>('/api/auth/refresh', {}).pipe(
        tap((response) => this.startSession(response)),
        catchError((error) => {
          this.clearSession();
          return throwError(() => error);
        }),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay(1),
      );
    }
    return this.refreshInFlight$;
  }

  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.refresh().pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
      ),
    );
  }

  logout(): void {
    this.http.post('/api/auth/logout', {}).subscribe({ error: () => undefined });
    this.clearSession();
    this.router.navigateByUrl('/login');
  }

  clearSession(): void {
    this.token = null;
    this.currentUser.set(null);
  }

  private startSession(response: AuthResponse): void {
    this.token = response.accessToken;
    this.currentUser.set(response.user);
  }
}
