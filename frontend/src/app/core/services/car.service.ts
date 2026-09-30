import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '../interceptors/error.interceptor';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { GeoPoint, PickupCheck, ServiceZone } from '../models/location.model';
import { VehicleSearch, routeBody } from '../../shared/utils/vehicle-search';
import {
  CarBooking,
  CarFareQuote,
  CarFareSearchResults,
  CarPaymentOrder,
  CarPublicDetail,
  CarRentalSettings,
  CarSearchResults,
  CarWindow,
  CreateCarBookingRequest,
} from '../models/car.model';

/**
 * Public car search / details / quotes and the signed-in customer's own car bookings. Every price
 * shown comes from these API responses — nothing is calculated here.
 */
@Injectable({ providedIn: 'root' })
export class CarService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/cars`;
  private readonly customerBase = `${environment.apiUrl}/customer/car-bookings`;

  private settings$?: Observable<CarRentalSettings>;

  /** Booking amount, seat categories and limits (cached for the session). */
  settings(): Observable<CarRentalSettings> {
    this.settings$ ??= this.http
      .get<ApiResponse<CarRentalSettings>>(`${this.base}/settings`)
      .pipe(map((r) => r.data!), shareReplay(1));
    return this.settings$;
  }

  search(filters: { city?: string | null; window?: CarWindow | null; seats?: number | null }): Observable<CarSearchResults> {
    let params = this.windowParams(filters.window);
    if (filters.city) params = params.set('city', filters.city);
    if (filters.seats) params = params.set('seats', filters.seats);
    return this.http.get<ApiResponse<CarSearchResults>>(`${this.base}/search`, { params, context: quiet() }).pipe(map((r) => r.data!));
  }

  getCar(carId: number, window?: CarWindow | null): Observable<CarPublicDetail> {
    return this.http
      .get<ApiResponse<CarPublicDetail>>(`${this.base}/${carId}`, { params: this.windowParams(window), context: quiet() })
      .pipe(map((r) => r.data!));
  }

  /** The server measures every km itself from the points — the browser never sends a distance. */
  quote(carId: number, search: VehicleSearch): Observable<CarFareQuote> {
    return this.http
      .post<ApiResponse<CarFareQuote>>(`${this.base}/${carId}/quote`, routeBody(search), { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  /** Every vehicle for the search, each with its own server-calculated fare. */
  fares(search: VehicleSearch, seats: number | null = null): Observable<CarFareSearchResults> {
    return this.http
      .post<ApiResponse<CarFareSearchResults>>(`${this.base}/fares`, { ...routeBody(search), seats }, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  /** Active pickup zones, shaded on the customer's map picker. Cached for the visit. */
  private zones$?: Observable<ServiceZone[]>;
  serviceZones(): Observable<ServiceZone[]> {
    this.zones$ ??= this.http.get<ApiResponse<ServiceZone[]>>(`${this.base}/service-zones`, { context: quiet() }).pipe(
      map((r) => r.data ?? []),
      shareReplay(1),
    );
    return this.zones$;
  }

  /** Is this pickup inside the areas cars serve? Shown as soon as the customer chooses it. */
  checkPickup(point: GeoPoint): Observable<PickupCheck> {
    return this.http
      .post<ApiResponse<PickupCheck>>(`${this.base}/pickup-check`, { latitude: point.latitude, longitude: point.longitude }, { context: quiet() })
      .pipe(map((r) => r.data!));
  }

  // ---------- Customer bookings ----------

  createBooking(request: CreateCarBookingRequest): Observable<CarBooking> {
    return this.http.post<ApiResponse<CarBooking>>(this.customerBase, request, { context: quiet() }).pipe(map((r) => r.data!));
  }

  myBookings(): Observable<CarBooking[]> {
    return this.http.get<ApiResponse<CarBooking[]>>(this.customerBase, { context: quiet() }).pipe(map((r) => r.data!));
  }

  myBooking(id: number): Observable<CarBooking> {
    return this.http.get<ApiResponse<CarBooking>>(`${this.customerBase}/${id}`, { context: quiet() }).pipe(map((r) => r.data!));
  }

  cancel(id: number, reason: string | null): Observable<CarBooking> {
    return this.http.post<ApiResponse<CarBooking>>(`${this.customerBase}/${id}/cancel`, { reason }, { context: quiet() }).pipe(map((r) => r.data!));
  }

  createPaymentOrder(id: number): Observable<CarPaymentOrder> {
    return this.http.post<ApiResponse<CarPaymentOrder>>(`${this.customerBase}/${id}/payments/order`, {}, { context: quiet() }).pipe(map((r) => r.data!));
  }

  verifyPayment(id: number, payment: { razorpayOrderId: string; razorpayPaymentId: string; razorpaySignature: string }): Observable<CarBooking> {
    return this.http.post<ApiResponse<CarBooking>>(`${this.customerBase}/${id}/payments/verify`, payment, { context: quiet() }).pipe(map((r) => r.data!));
  }

  private windowParams(window?: CarWindow | null): HttpParams {
    let params = new HttpParams();
    if (window?.date && window.time && window.hours) {
      params = params.set('date', window.date).set('time', window.time).set('durationHours', window.hours);
    }
    return params;
  }
}

/** For requests whose screen shows the error inline — skips the global error toast. */
function quiet(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_TOAST, true);
}
