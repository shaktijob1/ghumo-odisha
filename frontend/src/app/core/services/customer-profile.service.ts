import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { CustomerProfile, UpdateProfileRequest } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class CustomerProfileService {
  constructor(private readonly http: HttpClient) {}

  getProfile(): Observable<CustomerProfile> {
    return this.http.get<ApiResponse<CustomerProfile>>(`${environment.apiUrl}/customer/profile`).pipe(map((r) => r.data!));
  }

  updateProfile(request: UpdateProfileRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${environment.apiUrl}/customer/profile`, request).pipe(map(() => undefined));
  }
}
