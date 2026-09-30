import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

export interface SiteFeatures {
  /** Customers see Trips only: no Cars / Holidays tabs, and the customer Cars pages redirect home. */
  hideCarsAndHolidays: boolean;
  /** Browser key for Google Maps / Places, from the API's GoogleMaps config. Null when not set up. */
  googleMapsApiKey: string | null;
  googleMapsMapId: string;
}

const FALLBACK: SiteFeatures = { hideCarsAndHolidays: true, googleMapsApiKey: null, googleMapsMapId: 'DEMO_MAP_ID' };

/**
 * Site switches from the API's appsettings "Features" section, read once per visit. Until they load
 * (or if the call fails) unfinished sections stay hidden, so they never flash on screen.
 */
@Injectable({ providedIn: 'root' })
export class FeatureService {
  private readonly http = inject(HttpClient);

  readonly hideCarsAndHolidays = signal(true);

  private readonly features$: Observable<SiteFeatures> = this.http
    .get<ApiResponse<SiteFeatures>>(`${environment.apiUrl}/features`)
    .pipe(
      map((r) => ({ ...FALLBACK, ...(r.data ?? {}) })),
      catchError(() => of(FALLBACK)),
      tap((f) => this.hideCarsAndHolidays.set(f.hideCarsAndHolidays)),
      shareReplay(1),
    );

  load(): Observable<SiteFeatures> {
    return this.features$;
  }
}
