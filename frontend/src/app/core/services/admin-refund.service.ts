import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { PaymentMethod, RefundStatus } from '../models/enums.model';
import { AdminRefund, RefundCounts } from '../models/refund.model';

const base = () => `${environment.apiUrl}/admin/refunds`;

@Injectable({ providedIn: 'root' })
export class AdminRefundService {
  /** Refunds waiting for the admin — shown as a badge on the sidebar's Refunds link. */
  readonly pendingCount = signal(0);

  constructor(private readonly http: HttpClient) {}

  getRefunds(status: RefundStatus, search: string, page: number, pageSize: number): Observable<PagedResult<AdminRefund>> {
    let params = new HttpParams().set('status', status).set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<ApiResponse<PagedResult<AdminRefund>>>(base(), { params }).pipe(map((r) => r.data!));
  }

  getCounts(): Observable<RefundCounts> {
    return this.http.get<ApiResponse<RefundCounts>>(`${base()}/counts`).pipe(
      map((r) => r.data!),
      tap((c) => this.pendingCount.set(c.pending)),
    );
  }

  issueRazorpay(refundId: number, amount: number, notes: string | null): Observable<AdminRefund> {
    return this.http.post<ApiResponse<AdminRefund>>(`${base()}/${refundId}/razorpay`, { amount, notes }).pipe(map((r) => r.data!));
  }

  recordManual(refundId: number, amount: number, method: PaymentMethod, reference: string | null, notes: string | null): Observable<AdminRefund> {
    return this.http
      .post<ApiResponse<AdminRefund>>(`${base()}/${refundId}/manual`, { amount, method, reference, notes })
      .pipe(map((r) => r.data!));
  }

  settle(refundId: number, notes: string | null): Observable<AdminRefund> {
    return this.http.post<ApiResponse<AdminRefund>>(`${base()}/${refundId}/settle`, { notes }).pipe(map((r) => r.data!));
  }

  gatewayStatus(refundId: number): Observable<string | null> {
    return this.http.get<ApiResponse<{ status: string | null }>>(`${base()}/${refundId}/gateway-status`).pipe(map((r) => r.data?.status ?? null));
  }
}
