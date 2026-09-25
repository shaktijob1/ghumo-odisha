import { HttpClient, HttpContext } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '../interceptors/error.interceptor';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { PaymentMethod } from '../models/enums.model';

/** A coupon holder's own totals — the API never includes who booked. */
export interface PartnerCouponSummary {
  code: string;
  holderName: string;
  discountAmount: number;
  commissionPerSeat: number;
  isActive: boolean;
  validUntil: string | null;
  activeBookings: number;
  cancelledBookings: number;
  seatsTravelling: number;
  commissionEarned: number;
  commissionReversed: number;
  commissionPaid: number;
  balanceDue: number;
  payouts: { paidAt: string; amount: number; method: PaymentMethod }[];
  activity: { bookedOn: string; tripTitle: string; travelDate: string; seats: number; commission: number; counts: boolean }[];
}

/** Public (no login) lookup behind the influencer / partner earnings page. */
@Injectable({ providedIn: 'root' })
export class PartnerService {
  constructor(private readonly http: HttpClient) {}

  getSummary(code: string): Observable<PartnerCouponSummary> {
    return this.http
      .get<ApiResponse<PartnerCouponSummary>>(`${environment.apiUrl}/partner/coupons/${encodeURIComponent(code)}`, {
        // The page shows "code not found" / "too many tries" inline.
        context: new HttpContext().set(SKIP_ERROR_TOAST, true),
      })
      .pipe(map((r) => r.data!));
  }
}
