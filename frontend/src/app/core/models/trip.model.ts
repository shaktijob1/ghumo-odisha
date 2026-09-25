import { TripDateSlotStatus, TripStatus } from './enums.model';

export interface TripInclusions {
  breakfast: boolean;
  lunch: boolean;
  dinner: boolean;
  stay: boolean;
  coordinator: boolean;
}

export interface TripPhoto {
  tripPhotoId: number;
  imageUrl: string;
  displayOrder: number;
}

export interface RoomPhoto {
  roomPhotoId: number;
  imageUrl: string;
  displayOrder: number;
}

export interface VehiclePhoto {
  vehiclePhotoId: number;
  imageUrl: string;
  displayOrder: number;
}

export interface PickupPoint {
  pickupPointId: number;
  location: string;
  time: string;
  displayOrder: number;
}

export interface TripHighlight {
  tripHighlightId: number;
  placeName: string;
  description: string;
  photoUrl: string;
  displayOrder: number;
}

export interface ItineraryPoint {
  itineraryPointId: number;
  time: string;
  description: string;
  displayOrder: number;
}

export interface ItineraryDay {
  itineraryDayId: number;
  dayNumber: number;
  title: string;
  description: string;
  displayOrder: number;
  points: ItineraryPoint[];
}

export interface DateSlot {
  tripDateSlotId: number;
  startDate: string;
  endDate: string;
  totalSeats: number;
  availableSeats: number;
  isSoldOut: boolean;
}

export interface TripSummary {
  tripId: number;
  title: string;
  amountPerPerson: number;
  coverImageUrl: string | null;
  durationLabel: string | null;
  nextSlotStartDate: string | null;
  nextSlotEndDate: string | null;
  nextSlotAvailableSeats: number | null;
  nextSlotTotalSeats: number | null;
  inclusions: TripInclusions;
  highlightPlaceNames: string[];
  photos: TripPhoto[];
  destinationNames: string[];
  /** Display-only: upcoming active departures for the card's rolling dates strip. */
  upcomingSlots: UpcomingSlot[];
}

export interface TripDetail {
  tripId: number;
  title: string;
  description: string;
  amountPerPerson: number;
  inclusions: TripInclusions;
  photos: TripPhoto[];
  highlights: TripHighlight[];
  itineraryDays: ItineraryDay[];
  roomPhotos: RoomPhoto[];
  vehiclePhotos: VehiclePhoto[];
  pickupPoints: PickupPoint[];
  dateSlots: DateSlot[];
  itineraryPdfUrl: string | null;
}

export interface AdminTripListItem {
  tripId: number;
  title: string;
  amountPerPerson: number;
  status: TripStatus;
  dateSlotCount: number;
  nextSlotStartDate: string | null;
  nextSlotTotalSeats: number | null;
  nextSlotAvailableSeats: number | null;
  confirmedBookingCount: number;
}

export interface AdminTripDetail {
  tripId: number;
  title: string;
  description: string;
  amountPerPerson: number;
  inclusions: TripInclusions;
  status: TripStatus;
  createdAt: string;
  updatedAt: string;
  photos: TripPhoto[];
  highlights: TripHighlight[];
  itineraryDays: ItineraryDay[];
  roomPhotos: RoomPhoto[];
  vehiclePhotos: VehiclePhoto[];
  pickupPoints: PickupPoint[];
  dateSlots: DateSlot[];
  destinationIds: number[];
  destinationNames: string[];
  itineraryPdfUrl: string | null;
}

export interface CreateTripRequest {
  title: string;
  description: string;
  amountPerPerson: number;
  includesBreakfast: boolean;
  includesLunch: boolean;
  includesDinner: boolean;
  includesStay: boolean;
  includesCoordinator: boolean;
  destinationIds?: number[];
}

export interface UpdateTripRequest extends CreateTripRequest {
  status: TripStatus;
}

export interface AddDateSlotRequest {
  startDate: string;
  endDate: string;
  totalSeats: number;
}

export interface UpdateDateSlotRequest extends AddDateSlotRequest {
  status: TripDateSlotStatus;
}

export interface AddItineraryDayRequest {
  dayNumber: number;
  title: string;
  description: string;
}

export interface UpdateItineraryDayRequest extends AddItineraryDayRequest {
  displayOrder: number;
}

export interface AddItineraryPointRequest {
  time: string;
  description: string;
}

export interface UpdateItineraryPointRequest extends AddItineraryPointRequest {
  displayOrder: number;
}

export interface AddPickupPointRequest {
  location: string;
  time: string;
}

export interface UpdatePickupPointRequest extends AddPickupPointRequest {
  displayOrder: number;
}

export interface UpcomingSlot {
  startDate: string;
  availableSeats: number;
}
