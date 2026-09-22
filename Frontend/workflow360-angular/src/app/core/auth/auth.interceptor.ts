import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';

const SESSION_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      const isExpiredToken =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !SESSION_ENDPOINTS.includes(request.url);

      if (!isExpiredToken) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(() => {
          router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
          return throwError(() => error);
        }),
        switchMap(() => next(withToken(request, auth.accessToken()))),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}
