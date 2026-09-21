import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { CustomerAuthResponse, CustomerProfile, RequestOtpRequest, RequestOtpResponse, VerifyOtpRequest } from '../models/auth.model';

const STORAGE_KEY = 'go_customer_session';

@Injectable({ providedIn: 'root' })
export class CustomerAuthService {
  private readonly session = signal<CustomerAuthResponse | null>(this.readStoredSession());
  private refreshInFlight: Observable<CustomerAuthResponse | null> | null = null;

  readonly currentCustomer = computed(() => this.session());
  readonly isAuthenticated = computed(() => !!this.session());

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {
    // Deferred one microtask: restoreSession() synchronously fires an HTTP request, which goes
    // through authInterceptor, which injects CustomerAuthService itself to read the token. Doing
    // that while still inside this constructor is a re-entrant injection into a service that
    // hasn't finished constructing — Angular can't resolve it, the request silently errors, and
    // that cascades into clearing the session we're trying to restore. Queueing this for right
    // after the constructor returns avoids the re-entrancy entirely.
    queueMicrotask(() => this.restoreSession().subscribe());
  }

  requestOtp(name: string | null, whatsAppNumber: string): Observable<ApiResponse<RequestOtpResponse>> {
    const body: RequestOtpRequest = { name, whatsAppNumber };
    return this.http.post<ApiResponse<RequestOtpResponse>>(`${environment.apiUrl}/auth/customer/request-otp`, body);
  }

  verifyOtp(whatsAppNumber: string, otp: string): Observable<ApiResponse<CustomerAuthResponse>> {
    const body: VerifyOtpRequest = { whatsAppNumber, otp };
    return this.http
      .post<ApiResponse<CustomerAuthResponse>>(`${environment.apiUrl}/auth/customer/verify-otp`, body)
      .pipe(tap((r) => r.data && this.setSession(r.data)));
  }

  /** Attempts to refresh the access token using the stored refresh token. Shares one in-flight call. */
  refresh(): Observable<CustomerAuthResponse | null> {
    const current = this.session();
    if (!current?.refreshToken) {
      return of(null);
    }

    if (!this.refreshInFlight) {
      this.refreshInFlight = this.http
        .post<ApiResponse<CustomerAuthResponse>>(`${environment.apiUrl}/auth/customer/refresh`, { refreshToken: current.refreshToken })
        .pipe(
          map((r) => {
            if (r.data) {
              this.setSession(r.data);
              return r.data;
            }
            return null;
          }),
          catchError(() => {
            this.clearSession();
            return of(null);
          }),
          tap(() => (this.refreshInFlight = null)),
          shareReplay(1),
        );
    }

    return this.refreshInFlight;
  }

  /**
   * Validates the stored session on app startup and refreshes the cached customer fields from
   * the backend (the source of truth) so the navbar never shows stale name/email. Silently
   * refreshes the token or clears the session as needed.
   */
  restoreSession(): Observable<void> {
    const current = this.session();
    if (!current) {
      return of(undefined);
    }

    return this.http.get<ApiResponse<CustomerProfile>>(`${environment.apiUrl}/customer/profile`).pipe(
      map((r) => {
        if (r.data) {
          this.setSession({ ...current, name: r.data.name, phoneNumber: r.data.phoneNumber, email: r.data.email });
        }
      }),
      catchError(() => this.refresh().pipe(map(() => undefined))),
    );
  }

  logout(): void {
    const current = this.session();
    this.clearSession();
    this.router.navigate(['/']);

    if (current?.refreshToken) {
      this.http.post(`${environment.apiUrl}/auth/customer/logout`, { refreshToken: current.refreshToken }).subscribe({
        error: () => undefined,
      });
    }
  }

  getToken(): string | null {
    return this.session()?.token ?? null;
  }

  private setSession(auth: CustomerAuthResponse): void {
    this.session.set(auth);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
  }

  private clearSession(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private readStoredSession(): CustomerAuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as CustomerAuthResponse;
    } catch {
      return null;
    }
  }
}
