import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();

  const authed = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authed).pipe(
    catchError((err) => {
      if (err?.status === 401 && token) {
        // Access token expired or invalidated. v1: drop user back to login.
        // Phase 1 will add refresh-token rotation here.
        auth.logout();
      }
      return throwError(() => err);
    }),
  );
};
