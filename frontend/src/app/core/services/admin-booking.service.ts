import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  AdminBookingDetail,
  AdminBookingFilter,
  AdminBookingListItem,
  BookingResponse,
  CancelBookingRequest,
  ConfirmBookingRequest,
  CreateManualBookingRequest,
  RejectBookingRequest,
} from '../models/booking.model';

const base = () => `${environment.apiUrl}/admin`;

@Injectable({ providedIn: 'root' })
export class AdminBookingService {
  constructor(private readonly http: HttpClient) {}

  getBookings(page: number, pageSize: number, filter: AdminBookingFilter): Observable<PagedResult<AdminBookingListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter.bookingStatus !== null && filter.bookingStatus !== undefined) params = params.set('bookingStatus', filter.bookingStatus);
    if (filter.paymentStatus !== null && filter.paymentStatus !== undefined) params = params.set('paymentStatus', filter.paymentStatus);
    if (filter.tripId) params = params.set('tripId', filter.tripId);
    if (filter.search) params = params.set('search', filter.search);

    return this.http
      .get<ApiResponse<PagedResult<AdminBookingListItem>>>(`${base()}/bookings`, { params })
      .pipe(map((r) => r.data!));
  }

  getBooking(id: number): Observable<AdminBookingDetail> {
    return this.http.get<ApiResponse<AdminBookingDetail>>(`${base()}/bookings/${id}`).pipe(map((r) => r.data!));
  }

  createManualBooking(request: CreateManualBookingRequest): Observable<{ bookingId: number }> {
    return this.http
      .post<ApiResponse<{ bookingId: number }>>(`${base()}/bookings`, request)
      .pipe(map((r) => r.data!));
  }

  confirmBooking(id: number, request: ConfirmBookingRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/bookings/${id}/confirm`, request).pipe(map(() => undefined));
  }

  rejectBooking(id: number, request: RejectBookingRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/bookings/${id}/reject`, request).pipe(map(() => undefined));
  }

  cancelBooking(id: number, request: CancelBookingRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/bookings/${id}/cancel`, request).pipe(map(() => undefined));
  }

  getTripBookings(tripId: number): Observable<AdminBookingListItem[]> {
    return this.http
      .get<ApiResponse<AdminBookingListItem[]>>(`${base()}/trips/${tripId}/bookings`)
      .pipe(map((r) => r.data!));
  }

  getDateSlotBookings(dateSlotId: number): Observable<AdminBookingListItem[]> {
    return this.http
      .get<ApiResponse<AdminBookingListItem[]>>(`${base()}/date-slots/${dateSlotId}/bookings`)
      .pipe(map((r) => r.data!));
  }

  getCustomerBookings(customerId: number, page: number, pageSize: number): Observable<PagedResult<BookingResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http
      .get<ApiResponse<PagedResult<BookingResponse>>>(`${base()}/customers/${customerId}/bookings`, { params })
      .pipe(map((r) => r.data!));
  }
}
