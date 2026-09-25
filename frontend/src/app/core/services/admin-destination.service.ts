import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  AdminDestinationDetail,
  AdminDestinationListItem,
  CreateDestinationRequest,
  UpdateDestinationRequest,
} from '../models/destination.model';

const base = () => `${environment.apiUrl}/admin/destinations`;

@Injectable({ providedIn: 'root' })
export class AdminDestinationService {
  constructor(private readonly http: HttpClient) {}

  getDestinations(page: number, pageSize: number, search?: string): Observable<PagedResult<AdminDestinationListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<ApiResponse<PagedResult<AdminDestinationListItem>>>(base(), { params }).pipe(map((r) => r.data!));
  }

  getAllForPicker(): Observable<AdminDestinationListItem[]> {
    return this.http.get<ApiResponse<AdminDestinationListItem[]>>(`${base()}/picker`).pipe(map((r) => r.data ?? []));
  }

  getDestination(id: number): Observable<AdminDestinationDetail> {
    return this.http.get<ApiResponse<AdminDestinationDetail>>(`${base()}/${id}`).pipe(map((r) => r.data!));
  }

  createDestination(request: CreateDestinationRequest): Observable<{ destinationId: number }> {
    return this.http.post<ApiResponse<{ destinationId: number }>>(base(), request).pipe(map((r) => r.data!));
  }

  updateDestination(id: number, request: UpdateDestinationRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/${id}`, request).pipe(map(() => undefined));
  }

  deleteDestination(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/${id}`).pipe(map(() => undefined));
  }

  updateHeroImage(id: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<object>>(`${base()}/${id}/hero-image`, form).pipe(map(() => undefined));
  }

  updateCoverImage(id: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<object>>(`${base()}/${id}/cover-image`, form).pipe(map(() => undefined));
  }
}
