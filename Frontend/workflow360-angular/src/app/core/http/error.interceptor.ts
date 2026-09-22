import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { ToastService } from '../toast.service';
import { getErrorMessage } from './api-error';

/**
 * Shows a toast for errors no single screen can handle well (offline, forbidden, server errors).
 * 400/404/409 are left to the calling component, which can show them next to the relevant form.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const toast = inject(ToastService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const message = globalErrorMessage(error);
        if (message) {
          toast.error(message);
        }
      }
      return throwError(() => error);
    }),
  );
};

function globalErrorMessage(error: HttpErrorResponse): string | null {
  if (error.status === 0) {
    return 'Cannot reach the server. Check your connection and try again.';
  }
  if (error.status === 403) {
    return getErrorMessage(error, "You don't have permission to do that.");
  }
  if (error.status === 429) {
    return 'Too many attempts. Please wait a minute and try again.';
  }
  if (error.status >= 500) {
    return 'The server ran into a problem. Please try again later.';
  }
  return null;
}
