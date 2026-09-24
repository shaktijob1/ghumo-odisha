import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AdminCoupon, AdminCouponBooking, AdminCreateCouponRequest, AdminUpdateCouponRequest } from '../models/coupon.model';

const base = () => `${environment.apiUrl}/admin/coupons`;

@Injectable({ providedIn: 'root' })
export class AdminCouponService {
  constructor(private readonly http: HttpClient) {}

  getCoupons(): Observable<AdminCoupon[]> {
    return this.http.get<ApiResponse<AdminCoupon[]>>(base()).pipe(map((r) => r.data!));
  }

  getCouponBookings(id: number): Observable<AdminCouponBooking[]> {
    return this.http.get<ApiResponse<AdminCouponBooking[]>>(`${base()}/${id}/bookings`).pipe(map((r) => r.data!));
  }

  createCoupon(request: AdminCreateCouponRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(base(), request).pipe(map(() => undefined));
  }

  updateCoupon(id: number, request: AdminUpdateCouponRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/${id}`, request).pipe(map(() => undefined));
  }

  deleteCoupon(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/${id}`).pipe(map(() => undefined));
  }
}
