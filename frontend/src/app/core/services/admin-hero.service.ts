import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { HeroPage } from './hero.service';

@Injectable({ providedIn: 'root' })
export class AdminHeroService {
  constructor(private readonly http: HttpClient) {}

  setPhoto(file: File, page: HeroPage = 'home'): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http
      .post<ApiResponse<object>>(`${environment.apiUrl}/admin/hero/photo`, form, { params: { page } })
      .pipe(map(() => undefined));
  }
}
