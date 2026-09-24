import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { DestinationDetail, DestinationSummary } from '../models/destination.model';
import { TripSummary } from '../models/trip.model';

@Injectable({ providedIn: 'root' })
export class PublicDestinationService {
  constructor(private readonly http: HttpClient) {}

  getTrendingDestinations(): Observable<DestinationSummary[]> {
    return this.http
      .get<ApiResponse<DestinationSummary[]>>(`${environment.apiUrl}/destinations`)
      .pipe(map((r) => r.data ?? []));
  }

  getDestination(slug: string): Observable<DestinationDetail> {
    return this.http.get<ApiResponse<DestinationDetail>>(`${environment.apiUrl}/destinations/${slug}`).pipe(map((r) => r.data!));
  }

  getDestinationTrips(
    slug: string,
    page = 1,
    pageSize = 20,
    filters?: { fromDate?: string; toDate?: string },
  ): Observable<PagedResult<TripSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters?.fromDate) params = params.set('fromDate', filters.fromDate);
    if (filters?.toDate) params = params.set('toDate', filters.toDate);
    return this.http
      .get<ApiResponse<PagedResult<TripSummary>>>(`${environment.apiUrl}/destinations/${slug}/trips`, { params })
      .pipe(map((r) => r.data!));
  }
}
