import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { CollectionSheet, CollectionTrip, PaymentQr } from '../models/collection.model';

const base = () => `${environment.apiUrl}/admin/collections`;

/**
 * Collections desk: paid / remaining per trip, and the organizer's payment QR. Recording a collected
 * amount goes through AdminBookingService.addPayment — the same ledger as the booking page.
 */
@Injectable({ providedIn: 'root' })
export class AdminCollectionService {
  constructor(private readonly http: HttpClient) {}

  getTrips(): Observable<CollectionTrip[]> {
    return this.http.get<ApiResponse<CollectionTrip[]>>(`${base()}/trips`).pipe(map((r) => r.data ?? []));
  }

  getSheet(tripId: number, dateSlotId: number | null): Observable<CollectionSheet> {
    let params = new HttpParams();
    if (dateSlotId) params = params.set('dateSlotId', dateSlotId);
    return this.http.get<ApiResponse<CollectionSheet>>(`${base()}/trips/${tripId}`, { params }).pipe(map((r) => r.data!));
  }

  getQr(): Observable<PaymentQr | null> {
    return this.http.get<ApiResponse<PaymentQr | null>>(`${base()}/qr`).pipe(map((r) => r.data ?? null));
  }

  /** Uploaded as-is — re-encoding a QR as JPEG can blur the modules and make it unscannable. */
  setQr(file: File, caption: string | null): Observable<PaymentQr> {
    const form = new FormData();
    form.append('file', file);
    if (caption) form.append('caption', caption);
    return this.http.post<ApiResponse<PaymentQr>>(`${base()}/qr`, form).pipe(map((r) => r.data!));
  }

  removeQr(): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/qr`).pipe(map(() => undefined));
  }
}
