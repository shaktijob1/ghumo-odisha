import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { DateSlot, TripDetail, TripSummary } from '../models/trip.model';

@Injectable({ providedIn: 'root' })
export class PublicTripService {
  constructor(private readonly http: HttpClient) {}

  getTrips(
    page = 1,
    pageSize = 20,
    filters?: { search?: string; fromDate?: string; toDate?: string; destination?: string; upcomingOnly?: boolean },
  ): Observable<PagedResult<TripSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters?.search) params = params.set('search', filters.search);
    if (filters?.fromDate) params = params.set('fromDate', filters.fromDate);
    if (filters?.toDate) params = params.set('toDate', filters.toDate);
    if (filters?.destination) params = params.set('destination', filters.destination);
    if (filters?.upcomingOnly) params = params.set('upcomingOnly', true);
    return this.http.get<ApiResponse<PagedResult<TripSummary>>>(`${environment.apiUrl}/trips`, { params }).pipe(map((r) => r.data!));
  }

  /** Every place with an upcoming trip (in the date range, if given). */
  getTripLocations(range?: { fromDate?: string; toDate?: string }): Observable<string[]> {
    let params = new HttpParams();
    if (range?.fromDate) params = params.set('fromDate', range.fromDate);
    if (range?.toDate) params = params.set('toDate', range.toDate);
    return this.http.get<ApiResponse<string[]>>(`${environment.apiUrl}/trips/locations`, { params }).pipe(map((r) => r.data!));
  }

  /** Places covered by upcoming trips — extra home search suggestions (searched as text, not a destination). */
  getPlaces(): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${environment.apiUrl}/trips/places`).pipe(map((r) => r.data ?? []));
  }

  getTrip(id: number): Observable<TripDetail> {
    return this.http.get<ApiResponse<TripDetail>>(`${environment.apiUrl}/trips/${id}`).pipe(map((r) => r.data!));
  }

  getDateSlots(id: number): Observable<DateSlot[]> {
    return this.http.get<ApiResponse<DateSlot[]>>(`${environment.apiUrl}/trips/${id}/date-slots`).pipe(map((r) => r.data!));
  }
}
