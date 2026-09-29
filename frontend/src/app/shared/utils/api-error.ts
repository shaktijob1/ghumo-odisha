import { HttpErrorResponse } from '@angular/common/http';

/** The API's own message for a failed request ({ message, errors }), or `fallback` (e.g. offline). */
export function apiErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'No internet connection. Please check your network and try again.';
    const body = error.error as { message?: string; errors?: string[] | null } | null;
    if (body?.errors?.length) return body.errors.join(' ');
    if (body?.message) return body.message;
  }
  return fallback;
}
