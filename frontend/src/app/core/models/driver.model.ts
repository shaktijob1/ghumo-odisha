import { GeoPoint } from './location.model';
import {
  CarBooking,
  CarBookingSummary,
  CarPhoto,
  CarPhotoKind,
  CarPricingStatus,
  CarPricingTier,
  CarStatus,
  DriverDocumentType,
  DriverStatus,
  FareBreakdown,
  FuelType,
} from './car.model';

export interface DriverAuthResponse {
  token: string;
  refreshToken: string;
  driverId: number;
  name: string;
  phoneNumber: string | null;
  email: string | null;
  status: DriverStatus;
}

export interface DriverDocument {
  driverDocumentId: number;
  documentType: DriverDocumentType;
  carId: number | null;
  contentType: string;
  createdAt: string;
}

export interface DriverProfile {
  driverId: number;
  name: string;
  phoneNumber: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  /** Where the driver starts from; km from here to each pickup are added to the fare. */
  baseLocation: GeoPoint | null;
  baseLocationLabel: string | null;
  drivingLicenceNumber: string | null;
  licenceExpiryDate: string | null;
  experienceYears: number | null;
  profilePhotoUrl: string | null;
  status: DriverStatus;
  statusReason: string | null;
  submittedForReviewAt: string | null;
  approvedAt: string | null;
  createdAt: string;
  missingForReview: string[];
  documents: DriverDocument[];
}

export interface UpdateDriverProfileRequest {
  name: string;
  email: string | null;
  address: string | null;
  city: string | null;
  drivingLicenceNumber: string | null;
  licenceExpiryDate: string | null;
  experienceYears: number | null;
}

export interface CarPricing {
  carPricingId: number;
  pricePerKm: number;
  nightHaltPrice: number;
  tiers: CarPricingTier[];
  status: CarPricingStatus;
  submittedByRole: string;
  submittedAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

export interface DriverCar {
  carId: number;
  brand: string;
  modelName: string;
  displayName: string;
  category: string;
  registrationNumber: string;
  fuelType: FuelType;
  seatCapacity: number;
  hasAc: boolean;
  description: string | null;
  baseCity: string;
  status: CarStatus;
  statusReason: string | null;
  submittedForReviewAt: string | null;
  approvedAt: string | null;
  isListed: boolean;
  photos: CarPhoto[];
  activePricing: CarPricing | null;
  pendingPricing: CarPricing | null;
  rejectedPricing: CarPricing | null;
  missingForReview: string[];
  createdAt: string;
}

export interface SaveCarRequest {
  brand: string;
  modelName: string;
  registrationNumber: string;
  fuelType: FuelType;
  seatCapacity: number;
  hasAc: boolean;
  description: string | null;
  baseCity: string;
}

export interface SubmitPricingRequest {
  pricePerKm: number;
  nightHaltPrice: number;
  tiers: CarPricingTier[];
}

export enum DriverBookingScope {
  Upcoming = 0,
  Active = 1,
  History = 2,
}

export interface DriverBooking {
  booking: CarBooking;
  customerName: string;
  customerPhone: string | null;
  lastEndOdometerKm: number | null;
}

export interface StartTripRequest {
  startOdometerKm: number;
  latitude: number | null;
  longitude: number | null;
}

export interface EndTripRequest {
  endOdometerKm: number;
  nightHalts: number;
  additionalCharges: number;
  additionalChargesNote: string | null;
  latitude: number | null;
  longitude: number | null;
}

export interface TripFarePreview {
  startOdometerKm: number;
  endOdometerKm: number;
  actualKm: number;
  maxNightHalts: number;
  estimate: FareBreakdown;
  final: FareBreakdown;
  bookingAmountPaid: number;
  balanceDue: number;
}

export interface DriverEarnings {
  upcomingTrips: number;
  completedTrips: number;
  totalKm: number;
  totalFare: number;
  bookingAmountsPaidOnline: number;
  balanceCollected: number;
  balancePending: number;
  recentTrips: CarBookingSummary[];
}

export { CarPhotoKind, DriverDocumentType, DriverStatus, CarStatus, CarPricingStatus, FuelType };
