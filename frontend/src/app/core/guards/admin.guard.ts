import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Gates the `/admin/**` routes. Composes with `authGuard` (which runs
 * first per the route definition) — this guard only checks the role
 * claim and never logs the user out: a regular user that lands on
 * `/admin` is sent back to `/`.
 */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAdmin()) {
    return true;
  }
  return router.parseUrl('/');
};
