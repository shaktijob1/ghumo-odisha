import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  AddBookingPaymentRequest,
  AdminBookingDetail,
  AdminBookingFilter,
  AdminBookingListItem,
  BookingResponse,
  CancelBookingRequest,
  ChangeSeatsRequest,
  ConfirmBookingRequest,
  TravellerInput,
  UpdateGenderCountsRequest,
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

  addPayment(id: number, request: AddBookingPaymentRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/bookings/${id}/payments`, request).pipe(map(() => undefined));
  }

  removePayment(id: number, paymentId: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/bookings/${id}/payments/${paymentId}`).pipe(map(() => undefined));
  }

  changeSeats(id: number, request: ChangeSeatsRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/bookings/${id}/seats`, request).pipe(map(() => undefined));
  }

  updateTravellers(id: number, travellers: TravellerInput[]): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/bookings/${id}/travellers`, { travellers }).pipe(map(() => undefined));
  }

  updateGenderCounts(id: number, request: UpdateGenderCountsRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/bookings/${id}/gender-counts`, request).pipe(map(() => undefined));
  }

  downloadInvoice(id: number): Observable<Blob> {
    return this.http.get(`${base()}/bookings/${id}/invoice`, { responseType: 'blob' });
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
