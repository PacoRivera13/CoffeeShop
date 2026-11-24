import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const API_PREFIX = '/api/';
/** Endpoints called without a session, where a 401 means bad credentials rather than an ended session. */
const SESSIONLESS_URLS = ['/api/auth/login', '/api/auth/register'];

/** Sends the bearer token to the API; a 401 from any endpoint that needs a session ends it. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.token();

  const authorized =
    token && request.url.startsWith(API_PREFIX)
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !SESSIONLESS_URLS.includes(request.url)) {
        auth.logout();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
