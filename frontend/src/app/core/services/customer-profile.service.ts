import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { CustomerProfile, RequestOtpResponse, UpdateProfileRequest } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class CustomerProfileService {
  private readonly base = `${environment.apiUrl}/customer/profile`;

  constructor(private readonly http: HttpClient) {}

  getProfile(): Observable<CustomerProfile> {
    return this.http.get<ApiResponse<CustomerProfile>>(this.base).pipe(map((r) => r.data!));
  }

  updateProfile(request: UpdateProfileRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(this.base, request).pipe(map(() => undefined));
  }

  /** Step 1 of adding a WhatsApp number: sends an OTP to it. */
  requestAddPhoneOtp(whatsAppNumber: string): Observable<RequestOtpResponse> {
    return this.http
      .post<ApiResponse<RequestOtpResponse>>(`${this.base}/phone/request-otp`, { whatsAppNumber })
      .pipe(map((r) => r.data!));
  }

  /** Step 2: the OTP proves the number is theirs, and it's saved to the account. */
  verifyAddPhone(whatsAppNumber: string, otp: string): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${this.base}/phone/verify`, { whatsAppNumber, otp }).pipe(map(() => undefined));
  }

  linkGoogle(credential: string): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${this.base}/google`, { credential }).pipe(map(() => undefined));
  }
}
