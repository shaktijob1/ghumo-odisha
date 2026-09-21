import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { TripStatus } from '../models/enums.model';
import {
  AddDateSlotRequest,
  AddItineraryDayRequest,
  AddItineraryPointRequest,
  AddPickupPointRequest,
  AdminTripDetail,
  AdminTripListItem,
  CreateTripRequest,
  UpdateDateSlotRequest,
  UpdateItineraryDayRequest,
  UpdateItineraryPointRequest,
  UpdatePickupPointRequest,
  UpdateTripRequest,
} from '../models/trip.model';

const base = () => `${environment.apiUrl}/admin`;

@Injectable({ providedIn: 'root' })
export class AdminTripService {
  constructor(private readonly http: HttpClient) {}

  getTrips(page: number, pageSize: number, search?: string, status?: TripStatus | null): Observable<PagedResult<AdminTripListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (status !== null && status !== undefined) params = params.set('status', status);

    return this.http
      .get<ApiResponse<PagedResult<AdminTripListItem>>>(`${base()}/trips`, { params })
      .pipe(map((r) => r.data!));
  }

  getTrip(id: number): Observable<AdminTripDetail> {
    return this.http.get<ApiResponse<AdminTripDetail>>(`${base()}/trips/${id}`).pipe(map((r) => r.data!));
  }

  createTrip(request: CreateTripRequest): Observable<{ tripId: number }> {
    return this.http.post<ApiResponse<{ tripId: number }>>(`${base()}/trips`, request).pipe(map((r) => r.data!));
  }

  updateTrip(id: number, request: UpdateTripRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/trips/${id}`, request).pipe(map(() => undefined));
  }

  deleteTrip(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/trips/${id}`).pipe(map(() => undefined));
  }

  // Photos
  addTripPhoto(tripId: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/photos`, form).pipe(map(() => undefined));
  }

  deleteTripPhoto(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/trip-photos/${id}`).pipe(map(() => undefined));
  }

  updateTripPhotoOrder(id: number, displayOrder: number): Observable<void> {
    return this.http
      .put<ApiResponse<object>>(`${base()}/trip-photos/${id}/order`, { displayOrder })
      .pipe(map(() => undefined));
  }

  // Highlights
  addHighlight(tripId: number, placeName: string, description: string, photo: File): Observable<void> {
    const form = new FormData();
    form.append('placeName', placeName);
    form.append('description', description);
    form.append('photo', photo);
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/highlights`, form).pipe(map(() => undefined));
  }

  updateHighlight(id: number, placeName: string, description: string, displayOrder: number, photo?: File | null): Observable<void> {
    const form = new FormData();
    form.append('placeName', placeName);
    form.append('description', description);
    form.append('displayOrder', String(displayOrder));
    if (photo) form.append('photo', photo);
    return this.http.put<ApiResponse<object>>(`${base()}/highlights/${id}`, form).pipe(map(() => undefined));
  }

  deleteHighlight(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/highlights/${id}`).pipe(map(() => undefined));
  }

  // Date slots
  addDateSlot(tripId: number, request: AddDateSlotRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/date-slots`, request).pipe(map(() => undefined));
  }

  updateDateSlot(id: number, request: UpdateDateSlotRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/date-slots/${id}`, request).pipe(map(() => undefined));
  }

  deleteDateSlot(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/date-slots/${id}`).pipe(map(() => undefined));
  }

  // Itinerary
  addItineraryDay(tripId: number, request: AddItineraryDayRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/itinerary-days`, request).pipe(map(() => undefined));
  }

  updateItineraryDay(id: number, request: UpdateItineraryDayRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/itinerary-days/${id}`, request).pipe(map(() => undefined));
  }

  deleteItineraryDay(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/itinerary-days/${id}`).pipe(map(() => undefined));
  }

  addItineraryPoint(dayId: number, request: AddItineraryPointRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/itinerary-days/${dayId}/points`, request).pipe(map(() => undefined));
  }

  updateItineraryPoint(id: number, request: UpdateItineraryPointRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/itinerary-points/${id}`, request).pipe(map(() => undefined));
  }

  deleteItineraryPoint(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/itinerary-points/${id}`).pipe(map(() => undefined));
  }

  // Room photos
  addRoomPhoto(tripId: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/room-photos`, form).pipe(map(() => undefined));
  }

  deleteRoomPhoto(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/room-photos/${id}`).pipe(map(() => undefined));
  }

  updateRoomPhotoOrder(id: number, displayOrder: number): Observable<void> {
    return this.http
      .put<ApiResponse<object>>(`${base()}/room-photos/${id}/order`, { displayOrder })
      .pipe(map(() => undefined));
  }

  // Vehicle photos
  addVehiclePhoto(tripId: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/vehicle-photos`, form).pipe(map(() => undefined));
  }

  deleteVehiclePhoto(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/vehicle-photos/${id}`).pipe(map(() => undefined));
  }

  updateVehiclePhotoOrder(id: number, displayOrder: number): Observable<void> {
    return this.http
      .put<ApiResponse<object>>(`${base()}/vehicle-photos/${id}/order`, { displayOrder })
      .pipe(map(() => undefined));
  }

  // Pickup points
  addPickupPoint(tripId: number, request: AddPickupPointRequest): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${base()}/trips/${tripId}/pickup-points`, request).pipe(map(() => undefined));
  }

  updatePickupPoint(id: number, request: UpdatePickupPointRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${base()}/pickup-points/${id}`, request).pipe(map(() => undefined));
  }

  deletePickupPoint(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${base()}/pickup-points/${id}`).pipe(map(() => undefined));
  }
}
