import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { ToastService } from '../services/toast.service';

/** Set on a request whose page shows its own inline error message, so it isn't also toasted. */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status !== 401 && !req.context.get(SKIP_ERROR_TOAST)) {
        const body = error.error as ApiResponse<unknown> | undefined;
        const message = body?.errors?.length
          ? body.errors.join(' ')
          : body?.message || 'Something went wrong. Please try again.';
        toast.error(message);
      }

      return throwError(() => error);
    }),
  );
};
