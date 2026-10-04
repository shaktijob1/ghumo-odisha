import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { FaqItem } from '../models/trip.model';

/** The home page's intro, booking steps and FAQ — the same text the server renders (TripPageContent). */
export interface HomeContent {
  headingSub: string;
  intro: string;
  bookingSteps: FaqItem[];
  faqs: FaqItem[];
}

@Injectable({ providedIn: 'root' })
export class SiteContentService {
  private readonly http = inject(HttpClient);
  private home$?: Observable<HomeContent>;

  getHomeContent(): Observable<HomeContent> {
    this.home$ ??= this.http
      .get<ApiResponse<HomeContent>>(`${environment.apiUrl}/site/home-content`)
      .pipe(map((r) => r.data!), shareReplay(1));
    return this.home$;
  }
}
