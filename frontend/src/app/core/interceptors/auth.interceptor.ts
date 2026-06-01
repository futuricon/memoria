import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

const REFRESH_PATH = '/api/v1/auth/refresh';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const isRefreshCall = req.url.includes(REFRESH_PATH);

  // Never attach an expired token to the refresh request itself — backend
  // accepts the call anonymously and the refresh token in the body is what
  // matters. Sending the dying Authorization header just risks a spurious 401.
  const authed =
    token && !isRefreshCall
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      // Anonymous request, non-auth error, or the refresh endpoint itself
      // failed — propagate without retry. A failed refresh means the refresh
      // token is dead too; kick the user back to /login.
      if (err.status !== 401 || !token) {
        return throwError(() => err);
      }
      if (isRefreshCall) {
        auth.logout();
        return throwError(() => err);
      }

      // Single refresh attempt, then retry the original request with the
      // fresh access token. AuthService.refresh() dedupes parallel callers.
      return from(auth.refresh()).pipe(
        switchMap((newToken) => {
          if (!newToken) {
            auth.logout();
            return throwError(() => err);
          }
          const retried = req.clone({
            setHeaders: { Authorization: `Bearer ${newToken}` },
          });
          return next(retried);
        }),
      );
    }),
  );
};
