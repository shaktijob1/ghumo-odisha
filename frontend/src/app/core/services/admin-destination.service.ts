import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compressImage } from '../../shared/utils/compress-image';
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
    return this.uploadPhoto(`${base()}/${id}/hero-image`, file, 1920);
  }

  updateCoverImage(id: number, file: File): Observable<void> {
    // Cover images show on the small destination cards: 1000px is plenty, and any size gets shrunk.
    return this.uploadPhoto(`${base()}/${id}/cover-image`, file, 1000, 0);
  }

  /** Photos are shrunk in the browser first, so the site never serves multi-MB originals. */
  private uploadPhoto(url: string, file: File, maxSide: number, minBytes?: number): Observable<void> {
    return from(compressImage(file, maxSide, 0.82, minBytes)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready);
        return this.http.post<ApiResponse<object>>(url, form);
      }),
      map(() => undefined),
    );
  }
}
