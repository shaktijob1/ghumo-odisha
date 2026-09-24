import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { DateSlot, TripDetail, TripSummary } from '../models/trip.model';

@Injectable({ providedIn: 'root' })
export class PublicTripService {
  constructor(private readonly http: HttpClient) {}

  getTrips(page = 1, pageSize = 20, filters?: { search?: string; fromDate?: string; toDate?: string }): Observable<PagedResult<TripSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters?.search) params = params.set('search', filters.search);
    if (filters?.fromDate) params = params.set('fromDate', filters.fromDate);
    if (filters?.toDate) params = params.set('toDate', filters.toDate);
    return this.http.get<ApiResponse<PagedResult<TripSummary>>>(`${environment.apiUrl}/trips`, { params }).pipe(map((r) => r.data!));
  }

  getTrip(id: number): Observable<TripDetail> {
    return this.http.get<ApiResponse<TripDetail>>(`${environment.apiUrl}/trips/${id}`).pipe(map((r) => r.data!));
  }

  getDateSlots(id: number): Observable<DateSlot[]> {
    return this.http.get<ApiResponse<DateSlot[]>>(`${environment.apiUrl}/trips/${id}/date-slots`).pipe(map((r) => r.data!));
  }
}
