import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

export interface TermsAndConditions {
  version: string;
  text: string;
}

@Injectable({ providedIn: 'root' })
export class TermsService {
  constructor(private readonly http: HttpClient) {}

  get(): Observable<TermsAndConditions> {
    return this.http.get<ApiResponse<TermsAndConditions>>(`${environment.apiUrl}/terms`).pipe(map((r) => r.data!));
  }
}
