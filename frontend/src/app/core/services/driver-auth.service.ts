import { HttpClient, HttpContext } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '../interceptors/error.interceptor';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { RequestOtpResponse } from '../models/auth.model';
import { DriverAuthResponse } from '../models/driver.model';

const STORAGE_KEY = 'go_driver_session';
/** Sign-in errors are shown on the login form, not as a toast. */
const quiet = () => new HttpContext().set(SKIP_ERROR_TOAST, true);

/**
 * Driver sign-in session (WhatsApp OTP or Google) — kept apart from the customer session, so one
 * phone can be signed in as a customer and as a driver without the two tokens mixing.
 */
@Injectable({ providedIn: 'root' })
export class DriverAuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = `${environment.apiUrl}/auth/driver`;

  private readonly session = signal<DriverAuthResponse | null>(this.readStoredSession());
  private refreshInFlight: Observable<DriverAuthResponse | null> | null = null;

  readonly currentDriver = computed(() => this.session());
  readonly isAuthenticated = computed(() => !!this.session());

  requestOtp(name: string | null, whatsAppNumber: string): Observable<RequestOtpResponse> {
    return this.http.post<ApiResponse<RequestOtpResponse>>(`${this.base}/request-otp`, { name, whatsAppNumber }, { context: quiet() }).pipe(map((r) => r.data!));
  }

  verifyOtp(whatsAppNumber: string, otp: string): Observable<DriverAuthResponse> {
    return this.http
      .post<ApiResponse<DriverAuthResponse>>(`${this.base}/verify-otp`, { whatsAppNumber, otp }, { context: quiet() })
      .pipe(map((r) => r.data!), tap((s) => this.setSession(s)));
  }

  googleSignIn(credential: string): Observable<DriverAuthResponse> {
    return this.http
      .post<ApiResponse<DriverAuthResponse>>(`${this.base}/google`, { credential }, { context: quiet() })
      .pipe(map((r) => r.data!), tap((s) => this.setSession(s)));
  }

  /** One silent refresh shared by every request that hit a 401 at the same time. */
  refresh(): Observable<DriverAuthResponse | null> {
    const current = this.session();
    if (!current?.refreshToken) return of(null);

    this.refreshInFlight ??= this.http
      .post<ApiResponse<DriverAuthResponse>>(`${this.base}/refresh`, { refreshToken: current.refreshToken })
      .pipe(
        map((r) => {
          if (!r.data) return null;
          this.setSession(r.data);
          return r.data;
        }),
        catchError(() => {
          this.clearSession();
          return of(null);
        }),
        tap(() => (this.refreshInFlight = null)),
        shareReplay(1),
      );
    return this.refreshInFlight;
  }

  /** Keep the cached name / phone / status in step after profile changes. */
  updateCached(patch: Partial<Pick<DriverAuthResponse, 'name' | 'phoneNumber' | 'email' | 'status'>>): void {
    const current = this.session();
    if (current) this.setSession({ ...current, ...patch });
  }

  logout(): void {
    const current = this.session();
    this.clearSession();
    this.router.navigate(['/driver/login']);
    if (current?.refreshToken) {
      this.http.post(`${this.base}/logout`, { refreshToken: current.refreshToken }).subscribe({ error: () => undefined });
    }
  }

  getToken(): string | null {
    return this.session()?.token ?? null;
  }

  private setSession(auth: DriverAuthResponse): void {
    this.session.set(auth);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
    } catch {
      // Private mode / storage full: the session still works for this tab.
    }
  }

  private clearSession(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  private readStoredSession(): DriverAuthResponse | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as DriverAuthResponse) : null;
    } catch {
      return null;
    }
  }
}
