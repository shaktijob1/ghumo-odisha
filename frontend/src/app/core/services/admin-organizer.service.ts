import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class AdminOrganizerService {
  constructor(private readonly http: HttpClient) {}

  setPhoto(file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http
      .post<ApiResponse<object>>(`${environment.apiUrl}/admin/organizer/photo`, form)
      .pipe(map(() => undefined));
  }
}
