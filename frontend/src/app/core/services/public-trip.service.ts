import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { DateSlot, TripDetail, TripSummary } from '../models/trip.model';

@Injectable({ providedIn: 'root' })
export class PublicTripService {
  constructor(private readonly http: HttpClient) {}

  getTrips(page = 1, pageSize = 20): Observable<PagedResult<TripSummary>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<PagedResult<TripSummary>>>(`${environment.apiUrl}/trips`, { params }).pipe(map((r) => r.data!));
  }

  getTrip(id: number): Observable<TripDetail> {
    return this.http.get<ApiResponse<TripDetail>>(`${environment.apiUrl}/trips/${id}`).pipe(map((r) => r.data!));
  }

  getDateSlots(id: number): Observable<DateSlot[]> {
    return this.http.get<ApiResponse<DateSlot[]>>(`${environment.apiUrl}/trips/${id}/date-slots`).pipe(map((r) => r.data!));
  }
}
