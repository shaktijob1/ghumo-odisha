import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AdminAuthResponse } from '../models/auth.model';

const STORAGE_KEY = 'go_admin_session';

@Injectable({ providedIn: 'root' })
export class AdminAuthService {
  private readonly session = signal<AdminAuthResponse | null>(this.readStoredSession());

  readonly currentAdmin = computed(() => this.session());
  readonly isAuthenticated = computed(() => !!this.session());

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {}

  login(username: string, password: string): Observable<ApiResponse<AdminAuthResponse>> {
    return this.http
      .post<ApiResponse<AdminAuthResponse>>(`${environment.apiUrl}/admin/auth/login`, { username, password })
      .pipe(
        tap((response) => {
          if (response.data) {
            this.setSession(response.data);
          }
        }),
      );
  }

  logout(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
    this.router.navigate(['/admin/login']);
  }

  getToken(): string | null {
    return this.session()?.token ?? null;
  }

  private setSession(auth: AdminAuthResponse): void {
    this.session.set(auth);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
  }

  private readStoredSession(): AdminAuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as AdminAuthResponse;
    } catch {
      return null;
    }
  }
}
