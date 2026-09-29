import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';
import { CustomerAuthService } from '../services/customer-auth.service';
import { DriverAuthService } from '../services/driver-auth.service';

const CUSTOMER_AUTH_ENDPOINTS = ['/auth/customer/request-otp', '/auth/customer/verify-otp', '/auth/customer/refresh'];

/** Which signed-in account a request belongs to: admin, driver or (default) customer. */
function audienceOf(url: string): 'admin' | 'driver' | 'customer' {
  if (url.includes('/admin/')) return 'admin';
  if (url.includes('/api/driver/') || url.includes('/auth/driver/')) return 'driver';
  return 'customer';
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const adminAuth = inject(AdminAuthService);
  const customerAuth = inject(CustomerAuthService);
  const driverAuth = inject(DriverAuthService);
  const router = inject(Router);

  const audience = audienceOf(req.url);
  const isCustomerAuthEndpoint = CUSTOMER_AUTH_ENDPOINTS.some((path) => req.url.includes(path));
  // Driver sign-in endpoints never carry (or refresh) a token themselves.
  const isDriverAuthEndpoint = req.url.includes('/auth/driver/');
  const token =
    audience === 'admin' ? adminAuth.getToken() : audience === 'driver' ? (isDriverAuthEndpoint ? null : driverAuth.getToken()) : customerAuth.getToken();

  const authedReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !token) {
        return throwError(() => error);
      }

      if (audience === 'admin') {
        adminAuth.logout();
        router.navigate(['/admin/login']);
        return throwError(() => error);
      }

      if (audience === 'driver') {
        return driverAuth.refresh().pipe(
          switchMap((refreshed) => {
            if (!refreshed) {
              driverAuth.logout();
              return throwError(() => error);
            }
            return next(req.clone({ setHeaders: { Authorization: `Bearer ${refreshed.token}` } }));
          }),
        );
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
