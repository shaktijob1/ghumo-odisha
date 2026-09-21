import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { PaymentPlan } from '../models/enums.model';

export interface PaymentOrder {
  orderId: string;
  amountPaise: number;
  currency: string;
  keyId: string;
  discountApplied: number;
}

export interface VerifyPaymentRequest {
  razorpayOrderId: string;
  razorpayPaymentId: string;
  razorpaySignature: string;
}

export interface CouponValidationResult {
  code: string;
  discountAmount: number;
}

@Injectable({ providedIn: 'root' })
export class PaymentService {
  constructor(private readonly http: HttpClient) {}

  createOrder(bookingId: number, plan: PaymentPlan, couponCode?: string | null): Observable<PaymentOrder> {
    return this.http
      .post<ApiResponse<PaymentOrder>>(`${environment.apiUrl}/customer/bookings/${bookingId}/payments/order`, {
        plan,
        couponCode: couponCode || null,
      })
      .pipe(map((r) => r.data!));
  }

  verifyPayment(bookingId: number, request: VerifyPaymentRequest): Observable<void> {
    return this.http
      .post<ApiResponse<object>>(`${environment.apiUrl}/customer/bookings/${bookingId}/payments/verify`, request)
      .pipe(map(() => undefined));
  }

  validateCoupon(code: string): Observable<CouponValidationResult> {
    return this.http
      .post<ApiResponse<CouponValidationResult>>(`${environment.apiUrl}/customer/coupons/validate`, { code })
      .pipe(map((r) => r.data!));
  }
}
