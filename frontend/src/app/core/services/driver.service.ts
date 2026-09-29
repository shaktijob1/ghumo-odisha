import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/error.interceptor';
import { ApiResponse } from '../models/api-response.model';
import { RequestOtpResponse } from '../models/auth.model';
import { CarPhotoKind, DriverDocumentType } from '../models/car.model';
import {
  CarPricing,
  DriverBooking,
  DriverBookingScope,
  DriverCar,
  DriverDocument,
  DriverEarnings,
  DriverProfile,
  EndTripRequest,
  SaveCarRequest,
  StartTripRequest,
  SubmitPricingRequest,
  TripFarePreview,
  UpdateDriverProfileRequest,
} from '../models/driver.model';
import { compressImage } from '../../shared/utils/compress-image';

/** Errors on driver screens are shown inline next to what failed, not as a toast. */
const quiet = () => new HttpContext().set(SKIP_ERROR_TOAST, true);

/** The signed-in driver's own profile, documents, cars, pricing, bookings and trips. */
@Injectable({ providedIn: 'root' })
export class DriverService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/driver`;

  // ---------- Profile ----------

  profile(): Observable<DriverProfile> {
    return this.get<DriverProfile>('/profile');
  }

  updateProfile(request: UpdateDriverProfileRequest): Observable<DriverProfile> {
    return this.put<DriverProfile>('/profile', request);
  }

  setProfilePhoto(file: File): Observable<DriverProfile> {
    return this.upload<DriverProfile>('/profile/photo', file, {}, true);
  }

  submitProfile(): Observable<DriverProfile> {
    return this.post<DriverProfile>('/profile/submit', {});
  }

  requestPhoneOtp(whatsAppNumber: string): Observable<RequestOtpResponse> {
    return this.post<RequestOtpResponse>('/profile/phone/request-otp', { whatsAppNumber });
  }

  verifyPhone(whatsAppNumber: string, otp: string): Observable<DriverProfile> {
    return this.post<DriverProfile>('/profile/phone/verify', { whatsAppNumber, otp });
  }

  uploadDocument(file: File, documentType: DriverDocumentType, carId: number | null): Observable<DriverDocument> {
    const fields: Record<string, string> = { documentType: String(documentType) };
    if (carId !== null) fields['carId'] = String(carId);
    return this.upload<DriverDocument>('/documents', file, fields, true);
  }

  deleteDocument(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/documents/${id}`, { context: quiet() }).pipe(map(() => undefined));
  }

  /** The document file itself (private — fetched with the driver's token, never a public URL). */
  documentFile(id: number): Observable<Blob> {
    return this.http.get(`${this.base}/documents/${id}/file`, { responseType: 'blob', context: quiet() });
  }

  // ---------- Cars ----------

  cars(): Observable<DriverCar[]> {
    return this.get<DriverCar[]>('/cars');
  }

  car(id: number): Observable<DriverCar> {
    return this.get<DriverCar>(`/cars/${id}`);
  }

  createCar(request: SaveCarRequest): Observable<DriverCar> {
    return this.post<DriverCar>('/cars', request);
  }

  updateCar(id: number, request: SaveCarRequest): Observable<DriverCar> {
    return this.put<DriverCar>(`/cars/${id}`, request);
  }

  addCarPhoto(id: number, kind: CarPhotoKind, file: File): Observable<DriverCar> {
    return this.upload<DriverCar>(`/cars/${id}/photos`, file, { kind: String(kind) }, true);
  }

  deleteCarPhoto(id: number, photoId: number): Observable<DriverCar> {
    return this.http.delete<ApiResponse<DriverCar>>(`${this.base}/cars/${id}/photos/${photoId}`, { context: quiet() }).pipe(map((r) => r.data!));
  }

  submitCar(id: number): Observable<DriverCar> {
    return this.post<DriverCar>(`/cars/${id}/submit`, {});
  }

  deactivateCar(id: number): Observable<DriverCar> {
    return this.post<DriverCar>(`/cars/${id}/deactivate`, {});
  }

  pricingHistory(id: number): Observable<CarPricing[]> {
    return this.get<CarPricing[]>(`/cars/${id}/pricing`);
  }

  submitPricing(id: number, request: SubmitPricingRequest): Observable<DriverCar> {
    return this.post<DriverCar>(`/cars/${id}/pricing`, request);
  }

  // ---------- Bookings & trips ----------

  bookings(scope: DriverBookingScope): Observable<DriverBooking[]> {
    return this.http
      .get<ApiResponse<DriverBooking[]>>(`${this.base}/bookings`, { params: new HttpParams().set('scope', scope), context: quiet() })
      .pipe(map((r) => r.data!));
  }

  booking(id: number): Observable<DriverBooking> {
    return this.get<DriverBooking>(`/bookings/${id}`);
  }

  startTrip(id: number, request: StartTripRequest): Observable<DriverBooking> {
    return this.post<DriverBooking>(`/bookings/${id}/start`, request);
  }

  previewEnd(id: number, request: EndTripRequest): Observable<TripFarePreview> {
    return this.post<TripFarePreview>(`/bookings/${id}/end/preview`, request);
  }

  completeTrip(id: number, request: EndTripRequest): Observable<DriverBooking> {
    return this.post<DriverBooking>(`/bookings/${id}/complete`, request);
  }

  markBalanceCollected(id: number): Observable<DriverBooking> {
    return this.post<DriverBooking>(`/bookings/${id}/balance-collected`, {});
  }

  cancelBooking(id: number, reason: string): Observable<DriverBooking> {
    return this.post<DriverBooking>(`/bookings/${id}/cancel`, { reason });
  }

  earnings(): Observable<DriverEarnings> {
    return this.get<DriverEarnings>('/earnings');
  }

  // ---------- helpers ----------

  private get<T>(path: string): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { context: quiet() }).pipe(map((r) => r.data!));
  }

  private post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.base}${path}`, body, { context: quiet() }).pipe(map((r) => r.data!));
  }

  private put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<ApiResponse<T>>(`${this.base}${path}`, body, { context: quiet() }).pipe(map((r) => r.data!));
  }

  /** Multipart upload; photos are shrunk in the browser first. */
  private upload<T>(path: string, file: File, fields: Record<string, string>, compress: boolean): Observable<T> {
    return from(compress ? compressImage(file) : Promise.resolve(file)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready, ready.name);
        for (const [k, v] of Object.entries(fields)) form.append(k, v);
        return this.http.post<ApiResponse<T>>(`${this.base}${path}`, form, { context: quiet() });
      }),
      map((r) => r.data!),
    );
  }
}
