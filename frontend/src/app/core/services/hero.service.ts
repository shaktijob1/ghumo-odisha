import { HttpClient } from '@angular/common/http';
import { Injectable, WritableSignal, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

/** Which customer page's admin-uploaded hero banner to load. */
export type HeroPage = 'home' | 'trips';

/** Admin-uploaded hero banner photos, one per page, if they have been set. */
@Injectable({ providedIn: 'root' })
export class HeroService {
  private readonly photos = new Map<HeroPage, WritableSignal<string | null>>();

  constructor(private readonly http: HttpClient) {}

  get(page: HeroPage = 'home'): WritableSignal<string | null> {
    let photo = this.photos.get(page);
    if (!photo) {
      const created = signal<string | null>(null);
      photo = created;
      this.photos.set(page, created);
      this.http
        .get<ApiResponse<{ imageUrl: string | null }>>(`${environment.apiUrl}/hero`, { params: { page } })
        .subscribe((r) => {
          if (r.data) created.set(r.data.imageUrl);
        });
    }
    return photo;
  }
}
