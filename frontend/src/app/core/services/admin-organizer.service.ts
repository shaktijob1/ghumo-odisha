import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compressImage } from '../../shared/utils/compress-image';
import { ApiResponse } from '../models/api-response.model';

/** Travel Organizer card texts: null = not set, the site shows the matching default. */
export interface OrganizerProfileFields {
  name: string | null;
  role: string | null;
  phone: string | null;
  email: string | null;
  instagramUrl: string | null;
  quote: string | null;
  photoCaption: string | null;
  helpTitle: string | null;
  helpText: string | null;
}

export interface OrganizerProfile extends OrganizerProfileFields {
  photoUrl: string | null;
  /** What each empty field shows instead (appsettings / built-in wording). */
  defaults: {
    name: string;
    role: string;
    phone: string;
    email: string;
    instagramUrl: string | null;
    quote: string;
    photoCaption: string;
    helpTitle: string;
    helpText: string;
    whatsAppNumber: string;
  };
}

@Injectable({ providedIn: 'root' })
export class AdminOrganizerService {
  constructor(private readonly http: HttpClient) {}

  getProfile(): Observable<OrganizerProfile> {
    return this.http.get<ApiResponse<OrganizerProfile>>(`${environment.apiUrl}/admin/organizer`).pipe(map((r) => r.data!));
  }

  updateProfile(fields: OrganizerProfileFields): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${environment.apiUrl}/admin/organizer`, fields).pipe(map(() => undefined));
  }

  /** Shown on the home page's Travel Organizer card, so it's shrunk to 800px. */
  setPhoto(file: File): Observable<void> {
    return from(compressImage(file, 800, 0.85, 0)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready);
        return this.http.post<ApiResponse<object>>(`${environment.apiUrl}/admin/organizer/photo`, form);
      }),
      map(() => undefined),
    );
  }
}
