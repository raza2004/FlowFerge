import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/**
 * Attaches the access token, and when the API answers 401 (token expired) quietly refreshes
 * the session once and retries the request. If the refresh itself fails, the session is over
 * and the user is sent to the login page instead of being left in a half-working app.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const isAuthEndpoint = req.url.includes('/auth/');

  return next(withToken(req, auth.getAccessToken())).pipe(
    catchError(err => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401 || isAuthEndpoint || !auth.getRefreshToken()) {
        return throwError(() => err);
      }

      // catchError sits before switchMap so it only sees refresh failures - an ordinary
      // error from the retried request (404, 400...) must not log the user out.
      return auth.refreshSession().pipe(
        catchError(refreshErr => {
          auth.logout();
          return throwError(() => refreshErr);
        }),
        switchMap(session => next(withToken(req, session.accessToken)))
      );
    })
  );
};
