import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, from, map, switchMap, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/error.interceptor';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  AdminCarBookingDetail,
  AdminCarDetail,
  AdminCarFilter,
  AdminCarListItem,
  AdminCorrectFareRequest,
  AdminDecision,
  AdminDriverDetail,
  AdminDriverListItem,
  AdminPendingPricing,
  CarAdminDashboard,
} from '../models/admin-car.model';
import { CarBookingStatus, CarBookingSummary, CarPaymentStatus, CarPhotoKind } from '../models/car.model';
import { DriverCar, SaveCarRequest, SubmitPricingRequest } from '../models/driver.model';
import { GeoPoint, PickupCheck, SaveServiceAreaRequest, ServiceArea, TripPlace } from '../models/location.model';
import { PaymentMethod } from '../models/enums.model';
import { compressImage } from '../../shared/utils/compress-image';

/** Actions show their errors inline in the dialog that made them, so they skip the global toast. */
const quiet = () => new HttpContext().set(SKIP_ERROR_TOAST, true);

/** Admin review of drivers, cars and pricing, and admin handling of car bookings and ₹99 refunds. */
@Injectable({ providedIn: 'root' })
export class AdminCarService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin`;

  /** Sidebar badges: approvals waiting (drivers + cars + pricing) and car refunds waiting. */
  readonly pendingApprovals = signal(0);
  readonly refundsPending = signal(0);

  // ---------- Dashboard ----------

  dashboard(): Observable<CarAdminDashboard> {
    return this.get<CarAdminDashboard>('/cars/dashboard').pipe(
      tap((d) => {
        this.pendingApprovals.set(d.pendingApprovals);
        this.refundsPending.set(d.refundsPending);
      }),
    );
  }

  // ---------- Drivers ----------

  drivers(filter: AdminCarFilter, search: string, page: number, pageSize: number): Observable<PagedResult<AdminDriverListItem>> {
    return this.get<PagedResult<AdminDriverListItem>>('/drivers', listParams(filter, search, page, pageSize));
  }

  driver(id: number): Observable<AdminDriverDetail> {
    return this.get<AdminDriverDetail>(`/drivers/${id}`);
  }

  decideDriver(id: number, decision: Exclude<AdminDecision, 'deactivate'>, reason: string | null): Observable<AdminDriverDetail> {
    return this.post<AdminDriverDetail>(`/drivers/${id}/${decision}`, { reason });
  }

  /** A driver's private document (licence, RC, insurance) — fetched with the admin token, never a public URL. */
  documentFile(documentId: number): Observable<Blob> {
    return this.http.get(`${this.base}/drivers/documents/${documentId}/file`, { responseType: 'blob' });
  }

  /** Adds an owner-driver on their behalf; they sign in later with this WhatsApp number. */
  createDriver(request: { name: string; phoneNumber: string; city: string | null }): Observable<AdminDriverDetail> {
    return this.post<AdminDriverDetail>('/drivers', request);
  }

  // ---------- Cars ----------

  /** Adds a car for a driver. It starts ready to publish: add photos and pricing, then approve. */
  createCar(driverId: number, request: SaveCarRequest): Observable<DriverCar> {
    return this.post<DriverCar>(`/drivers/${driverId}/cars`, request);
  }

  /** Photos are shrunk in the browser first, like the driver's uploads. */
  addPhoto(carId: number, file: File, kind: CarPhotoKind): Observable<AdminCarDetail> {
    return from(compressImage(file)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready, ready.name);
        form.append('kind', String(kind));
        return this.http.post<ApiResponse<AdminCarDetail>>(`${this.base}/cars/${carId}/photos`, form, { context: quiet() });
      }),
      map((r) => r.data!),
    );
  }

  deletePhoto(carId: number, photoId: number): Observable<AdminCarDetail> {
    return this.http
      .delete<ApiResponse<AdminCarDetail>>(`${this.base}/cars/${carId}/photos/${photoId}`, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  cars(filter: AdminCarFilter, search: string, page: number, pageSize: number): Observable<PagedResult<AdminCarListItem>> {
    return this.get<PagedResult<AdminCarListItem>>('/cars', listParams(filter, search, page, pageSize));
  }

  car(id: number): Observable<AdminCarDetail> {
    return this.get<AdminCarDetail>(`/cars/${id}`);
  }

  decideCar(id: number, decision: AdminDecision, reason: string | null): Observable<AdminCarDetail> {
    return this.post<AdminCarDetail>(`/cars/${id}/${decision}`, { reason });
  }

  /** Admin-set pricing takes effect immediately (no approval step). */
  setPricing(carId: number, request: SubmitPricingRequest): Observable<AdminCarDetail> {
    return this.http
      .put<ApiResponse<AdminCarDetail>>(`${this.base}/cars/${carId}/pricing`, request, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  pendingPricing(): Observable<AdminPendingPricing[]> {
    return this.get<AdminPendingPricing[]>('/cars/pricing/pending');
  }

  approvePricing(pricingId: number): Observable<AdminCarDetail> {
    return this.post<AdminCarDetail>(`/cars/pricing/${pricingId}/approve`, {});
  }

  rejectPricing(pricingId: number, reason: string): Observable<AdminCarDetail> {
    return this.post<AdminCarDetail>(`/cars/pricing/${pricingId}/reject`, { reason });
  }

  // ---------- Car bookings ----------

  bookings(
    status: CarBookingStatus | null,
    paymentStatus: CarPaymentStatus | null,
    search: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<CarBookingSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status !== null) params = params.set('status', status);
    if (paymentStatus !== null) params = params.set('paymentStatus', paymentStatus);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get<PagedResult<CarBookingSummary>>('/car-bookings', params);
  }

  booking(id: number): Observable<AdminCarBookingDetail> {
    return this.get<AdminCarBookingDetail>(`/car-bookings/${id}`);
  }

  cancelBooking(id: number, reason: string, waiveRefund: boolean): Observable<AdminCarBookingDetail> {
    return this.post<AdminCarBookingDetail>(`/car-bookings/${id}/cancel`, { reason, waiveRefund });
  }

  saveNotes(id: number, adminNotes: string | null): Observable<AdminCarBookingDetail> {
    return this.http
      .put<ApiResponse<AdminCarBookingDetail>>(`${this.base}/car-bookings/${id}/notes`, { adminNotes }, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  refundRazorpay(id: number, amount: number): Observable<AdminCarBookingDetail> {
    return this.post<AdminCarBookingDetail>(`/car-bookings/${id}/refund/razorpay`, { amount });
  }

  refundManual(id: number, amount: number, method: PaymentMethod, reference: string | null): Observable<AdminCarBookingDetail> {
    return this.post<AdminCarBookingDetail>(`/car-bookings/${id}/refund/manual`, { amount, method, reference });
  }

  settleRefund(id: number): Observable<AdminCarBookingDetail> {
    return this.post<AdminCarBookingDetail>(`/car-bookings/${id}/refund/settle`, {});
  }

  correctFare(id: number, request: AdminCorrectFareRequest): Observable<AdminCarBookingDetail> {
    return this.post<AdminCarBookingDetail>(`/car-bookings/${id}/correct-fare`, request);
  }

  // ---------- Pickup areas & driver starting points ----------

  serviceAreas(): Observable<ServiceArea[]> {
    return this.get<ServiceArea[]>('/service-areas');
  }

  saveServiceArea(id: number | null, request: SaveServiceAreaRequest): Observable<ServiceArea> {
    return id === null
      ? this.post<ServiceArea>('/service-areas', request)
      : this.http.put<ApiResponse<ServiceArea>>(`${this.base}/service-areas/${id}`, request, { context: quiet() }).pipe(map((r) => r.data!));
  }

  deleteServiceArea(id: number): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/service-areas/${id}`, { context: quiet() }).pipe(map(() => undefined));
  }

  setDriverBaseLocation(driverId: number, place: TripPlace): Observable<AdminDriverDetail> {
    return this.http
      .put<ApiResponse<AdminDriverDetail>>(`${this.base}/drivers/${driverId}/base-location`, place, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  /** Same check the booking page makes — lets the admin test a spot against the saved areas. */
  checkPickup(point: GeoPoint): Observable<PickupCheck> {
    return this.http
      .post<ApiResponse<PickupCheck>>(`${environment.apiUrl}/cars/pickup-check`, point, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { params }).pipe(map((r) => r.data!));
  }

  private post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.base}${path}`, body, { context: quiet() }).pipe(map((r) => r.data!));
  }
}

function listParams(filter: AdminCarFilter, search: string, page: number, pageSize: number): HttpParams {
  let params = new HttpParams().set('filter', filter).set('page', page).set('pageSize', pageSize);
  if (search.trim()) params = params.set('search', search.trim());
  return params;
}
