import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compressImage } from '../../shared/utils/compress-image';
import { ApiResponse } from '../models/api-response.model';
import {
  AdminHolidayDestination,
  AdminHolidayPackage,
  HolidayDestination,
  HolidayPackage,
  SaveHolidayDestinationRequest,
  SaveHolidayPackageRequest,
} from '../models/holiday.model';

/** Home page Holidays tab: destinations and packages. */
@Injectable({ providedIn: 'root' })
export class HolidayService {
  private readonly http = inject(HttpClient);

  getDestinations(): Observable<HolidayDestination[]> {
    return this.http.get<ApiResponse<HolidayDestination[]>>(`${environment.apiUrl}/holidays/destinations`).pipe(map((r) => r.data ?? []));
  }

  getPackages(destinationId?: number | null): Observable<HolidayPackage[]> {
    let params = new HttpParams();
    if (destinationId) params = params.set('destinationId', destinationId);
    return this.http.get<ApiResponse<HolidayPackage[]>>(`${environment.apiUrl}/holidays/packages`, { params }).pipe(map((r) => r.data ?? []));
  }
}

/** Admin → Holidays: destinations and packages. */
@Injectable({ providedIn: 'root' })
export class AdminHolidayService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/holidays`;

  getDestinations(): Observable<AdminHolidayDestination[]> {
    return this.http.get<ApiResponse<AdminHolidayDestination[]>>(`${this.base}/destinations`).pipe(map((r) => r.data ?? []));
  }

  createDestination(request: SaveHolidayDestinationRequest): Observable<number> {
    return this.http.post<ApiResponse<{ holidayDestinationId: number }>>(`${this.base}/destinations`, request).pipe(map((r) => r.data!.holidayDestinationId));
  }

  updateDestination(id: number, request: SaveHolidayDestinationRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${this.base}/destinations/${id}`, request).pipe(map(() => undefined));
  }

  deleteDestination(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/destinations/${id}`).pipe(map(() => undefined));
  }

  setDestinationPhoto(id: number, file: File): Observable<void> {
    return this.upload(`${this.base}/destinations/${id}/photo`, file);
  }

  getPackages(): Observable<AdminHolidayPackage[]> {
    return this.http.get<ApiResponse<AdminHolidayPackage[]>>(`${this.base}/packages`).pipe(map((r) => r.data ?? []));
  }

  createPackage(request: SaveHolidayPackageRequest): Observable<number> {
    return this.http.post<ApiResponse<{ holidayPackageId: number }>>(`${this.base}/packages`, request).pipe(map((r) => r.data!.holidayPackageId));
  }

  updatePackage(id: number, request: SaveHolidayPackageRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${this.base}/packages/${id}`, request).pipe(map(() => undefined));
  }

  deletePackage(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/packages/${id}`).pipe(map(() => undefined));
  }

  setPackagePhoto(id: number, file: File): Observable<void> {
    return this.upload(`${this.base}/packages/${id}/photo`, file);
  }

  /** Card photos: shrunk to 1200px before upload. */
  private upload(url: string, file: File): Observable<void> {
    return from(compressImage(file, 1200, 0.82, 0)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready);
        return this.http.post<ApiResponse<object>>(url, form);
      }),
      map(() => undefined),
    );
  }
}
