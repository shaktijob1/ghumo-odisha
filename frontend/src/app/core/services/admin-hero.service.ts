import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compressImage } from '../../shared/utils/compress-image';
import { ApiResponse } from '../models/api-response.model';
import { HeroPage } from './hero.service';

@Injectable({ providedIn: 'root' })
export class AdminHeroService {
  constructor(private readonly http: HttpClient) {}

  /** The banner is shrunk in the browser first (max 1920px, JPEG) so the home page stays light. */
  setPhoto(file: File, page: HeroPage = 'home'): Observable<void> {
    return from(compressImage(file, 1920)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready);
        return this.http.post<ApiResponse<object>>(`${environment.apiUrl}/admin/hero/photo`, form, { params: { page } });
      }),
      map(() => undefined),
    );
  }
}
