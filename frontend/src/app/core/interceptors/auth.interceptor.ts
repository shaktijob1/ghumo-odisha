import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';
import { CustomerAuthService } from '../services/customer-auth.service';

const CUSTOMER_AUTH_ENDPOINTS = ['/auth/customer/request-otp', '/auth/customer/verify-otp', '/auth/customer/refresh'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const adminAuth = inject(AdminAuthService);
  const customerAuth = inject(CustomerAuthService);
  const router = inject(Router);

  const isAdminRoute = req.url.includes('/admin/');
  const isCustomerAuthEndpoint = CUSTOMER_AUTH_ENDPOINTS.some((path) => req.url.includes(path));
  const token = isAdminRoute ? adminAuth.getToken() : customerAuth.getToken();

  const authedReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !token) {
        return throwError(() => error);
      }

      if (isAdminRoute) {
        adminAuth.logout();
        router.navigate(['/admin/login']);
        return throwError(() => error);
      }

      if (isCustomerAuthEndpoint) {
        return throwError(() => error);
      }

      // Try one silent refresh, then retry the original request before giving up.
      return customerAuth.refresh().pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            customerAuth.logout();
            return throwError(() => error);
          }

          const retriedReq = req.clone({ setHeaders: { Authorization: `Bearer ${refreshed.token}` } });
          return next(retriedReq);
        }),
      );
    }),
  );
};
