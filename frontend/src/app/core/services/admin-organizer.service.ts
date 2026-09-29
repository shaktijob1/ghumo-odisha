import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compressImage } from '../../shared/utils/compress-image';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class AdminOrganizerService {
  constructor(private readonly http: HttpClient) {}

  /** Shown as a small round avatar, so it's always shrunk to 400px (a few tens of KB). */
  setPhoto(file: File): Observable<void> {
    return from(compressImage(file, 400, 0.85, 0)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready);
        return this.http.post<ApiResponse<object>>(`${environment.apiUrl}/admin/organizer/photo`, form);
      }),
      map(() => undefined),
    );
  }
}
