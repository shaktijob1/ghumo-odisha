import { TripDateSlotStatus, TripStatus } from './enums.model';

/** One question and answer, shown on the page and in its FAQPage structured data. */
export interface FaqItem {
  question: string;
  answer: string;
}

export interface TripDestinationLink {
  name: string;
  slug: string;
}

export interface TripInclusions {
  breakfast: boolean;
  lunch: boolean;
  dinner: boolean;
  stay: boolean;
  coordinator: boolean;
  acVehicle: boolean;
  pushbackVehicle: boolean;
  camping: boolean;
  bonfire: boolean;
  musicalNight: boolean;
  swimmingPool: boolean;
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
  /** Gents / ladies already booked on this date — shown instead of seats left. */
  gentsBooked: number;
  ladiesBooked: number;
  /** Places still open for gents / ladies — no gender cap, so both equal the seats still free. */
  gentsLeft: number;
  ladiesLeft: number;
  /** Online booking has closed (trip day and the 2 days before) — customers see "Seats filled". */
  isBookingClosed: boolean;
  status: TripDateSlotStatus;
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
  /** Bookable departures not already in upcomingSlots — offered when every date on the card is booked. */
  nextOpenSlots?: UpcomingSlot[];
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
  /** Destination pages this trip covers. */
  destinations: TripDestinationLink[];
  /** City the trip leaves from, when its itinerary names it (e.g. Bhubaneswar). */
  departureCity: string | null;
  /** "4 Days / 3 Nights", from the next departure. */
  durationLabel: string | null;
  /** Every place the trip visits, as entered by the admin. */
  placesCovered: string[];
  /** "Good to know" questions built by the API from this trip and the booking rules. */
  faqs: FaqItem[];
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
  allowCoupons: boolean;
  placesCovered: string[];
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
  includesAcVehicle: boolean;
  includesPushbackVehicle: boolean;
  includesCamping: boolean;
  includesBonfire: boolean;
  includesMusicalNight: boolean;
  includesSwimmingPool: boolean;
  allowCoupons: boolean;
  destinationIds?: number[];
  /** Every place the trip visits — shown on the trip page and matched by the site search. */
  placesCovered?: string[];
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
