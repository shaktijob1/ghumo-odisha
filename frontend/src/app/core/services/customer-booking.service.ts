import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { BookingResponse, CreateBookingRequest, CreateBookingResult } from '../models/booking.model';

@Injectable({ providedIn: 'root' })
export class CustomerBookingService {
  constructor(private readonly http: HttpClient) {}

  requestBooking(request: CreateBookingRequest): Observable<CreateBookingResult> {
    return this.http
      .post<ApiResponse<CreateBookingResult>>(`${environment.apiUrl}/bookings/request`, request)
      .pipe(map((r) => r.data!));
  }

  getMyBookings(page = 1, pageSize = 20): Observable<PagedResult<BookingResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http
      .get<ApiResponse<PagedResult<BookingResponse>>>(`${environment.apiUrl}/customer/bookings`, { params })
      .pipe(map((r) => r.data!));
  }

  getMyBooking(id: number): Observable<BookingResponse> {
    return this.http.get<ApiResponse<BookingResponse>>(`${environment.apiUrl}/customer/bookings/${id}`).pipe(map((r) => r.data!));
  }

  downloadInvoice(id: number): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/customer/bookings/${id}/invoice`, { responseType: 'blob' });
  }

  cancelBooking(id: number): Observable<BookingResponse> {
    return this.http
      .post<ApiResponse<BookingResponse>>(`${environment.apiUrl}/customer/bookings/${id}/cancel`, {})
      .pipe(map((r) => r.data!));
  }
}
