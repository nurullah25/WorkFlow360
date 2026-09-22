import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { ToastService } from '../toast.service';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  if (inject(AuthService).isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = () => {
  return inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;
};

/** Reads the allowed roles from route data: `data: { roles: ['Admin', 'HR'] }`. */
export const roleGuard: CanActivateFn = (route) => {
  const roles = (route.data['roles'] as Role[] | undefined) ?? [];
  if (roles.length === 0 || inject(AuthService).hasRole(...roles)) {
    return true;
  }

  inject(ToastService).error("You don't have access to that page.");
  return inject(Router).createUrlTree(['/dashboard']);
};
