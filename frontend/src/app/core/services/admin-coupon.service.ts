import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  AddCouponPayoutRequest,
  AdminCoupon,
  AdminCouponBooking,
  AdminCreateCouponRequest,
  AdminUpdateCouponRequest,
  CouponPayout,
} from '../models/coupon.model';

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

  getPayouts(id: number): Observable<CouponPayout[]> {
    return this.http.get<ApiResponse<CouponPayout[]>>(`${base()}/${id}/payouts`).pipe(map((r) => r.data!));
  }

  addPayout(id: number, request: AddCouponPayoutRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/${id}/payouts`, request).pipe(map(() => undefined));
  }

  removePayout(id: number, payoutId: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/${id}/payouts/${payoutId}`).pipe(map(() => undefined));
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
