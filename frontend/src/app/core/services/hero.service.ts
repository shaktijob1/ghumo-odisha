import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

/** The customer dashboard's admin-uploaded hero banner photo, if one has been set. */
@Injectable({ providedIn: 'root' })
export class HeroService {
  private readonly imageUrl = signal<string | null>(null);
  private loaded = false;

  constructor(private readonly http: HttpClient) {}

  get(): typeof this.imageUrl {
    if (!this.loaded) {
      this.loaded = true;
      this.http.get<ApiResponse<{ imageUrl: string | null }>>(`${environment.apiUrl}/hero`).subscribe((r) => {
        if (r.data) this.imageUrl.set(r.data.imageUrl);
      });
    }
    return this.imageUrl;
  }
}
