import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface SearchLogEntry {
  /** "yyyy-MM", or null for any month. */
  month: string | null;
  place: string | null;
  resultCount: number;
}

/** Records home-page trip searches so admins can see what visitors look for (Logs → Visitor searches). */
@Injectable({ providedIn: 'root' })
export class SearchLogService {
  private readonly http = inject(HttpClient);

  /** Fire-and-forget: a failed log write must never affect the visitor's search. */
  record(entry: SearchLogEntry): void {
    this.http.post(`${environment.apiUrl}/search-logs`, entry).subscribe({ error: () => undefined });
  }
}
