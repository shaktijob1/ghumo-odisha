import {
  CarBooking,
  CarBookingSummary,
  CarStatus,
  DriverStatus,
  FuelType,
} from './car.model';
import { CarPricing, DriverCar, DriverDocument, DriverProfile } from './driver.model';

/** Admin list filters. "Review" = sent for review and waiting for a decision. */
export enum AdminCarFilter {
  All = 0,
  Review = 1,
  Approved = 2,
  Rejected = 3,
  Suspended = 4,
  Inactive = 5,
  /** Signed up / registered but not sent for review yet. */
  Draft = 6,
}

export enum CarAuditEntity {
  Driver = 0,
  Car = 1,
  Pricing = 2,
  Booking = 3,
  Trip = 4,
}

export interface CarAuditEvent {
  carAuditEventId: number;
  entityType: CarAuditEntity;
  action: string;
  title: string;
  oldValue: string | null;
  newValue: string | null;
  note: string | null;
  actorRole: string;
  actorId: number | null;
  createdAt: string;
}

export interface AdminDriverListItem {
  driverId: number;
  name: string;
  phoneNumber: string | null;
  city: string | null;
  profilePhotoUrl: string | null;
  status: DriverStatus;
  statusReason: string | null;
  awaitingReview: boolean;
  submittedForReviewAt: string | null;
  approvedAt: string | null;
  carCount: number;
  approvedCarCount: number;
  createdAt: string;
}

export interface AdminCarListItem {
  carId: number;
  displayName: string;
  category: string;
  registrationNumber: string;
  seatCapacity: number;
  fuelType: FuelType;
  baseCity: string;
  coverPhotoUrl: string | null;
  status: CarStatus;
  statusReason: string | null;
  awaitingReview: boolean;
  submittedForReviewAt: string | null;
  driverId: number;
  driverName: string;
  driverStatus: DriverStatus;
  hasPendingPricing: boolean;
  isListed: boolean;
  createdAt: string;
}

export interface AdminDriverDetail {
  profile: DriverProfile;
  googleEmail: string | null;
  /** Added by an admin — can be approved without a submitted profile. */
  addedByAdmin: boolean;
  cars: AdminCarListItem[];
  history: CarAuditEvent[];
}

export interface AdminCarDetail {
  car: DriverCar;
  driver: { driverId: number; name: string; phoneNumber: string | null; profilePhotoUrl: string | null; status: DriverStatus };
  documents: DriverDocument[];
  pricingHistory: CarPricing[];
  /** Why customers can't see the car right now; empty when it's listed. */
  notListedReasons: string[];
  upcomingBookings: number;
  history: CarAuditEvent[];
}

export interface AdminPendingPricing {
  carId: number;
  carDisplayName: string;
  category: string;
  registrationNumber: string;
  carStatus: CarStatus;
  driverId: number;
  driverName: string;
  proposed: CarPricing;
  current: CarPricing | null;
}

export interface CarAdminDashboard {
  totalDrivers: number;
  driversAwaitingReview: number;
  approvedDrivers: number;
  totalCars: number;
  carsAwaitingReview: number;
  approvedCars: number;
  activeCars: number;
  pricingAwaitingReview: number;
  pendingApprovals: number;
  upcomingBookings: number;
  tripsInProgress: number;
  completedTrips: number;
  bookingAmountCollected: number;
  completedTripsFareTotal: number;
  refundsPending: number;
  recentBookings: CarBookingSummary[];
}

export interface AdminCarBookingDetail {
  booking: CarBooking;
  customer: { customerId: number; name: string; phoneNumber: string | null; email: string | null };
  adminNotes: string | null;
  razorpayOrderId: string | null;
  razorpayPaymentId: string | null;
  razorpayRefundId: string | null;
  history: CarAuditEvent[];
}

export interface AdminCorrectFareRequest {
  startOdometerKm: number;
  endOdometerKm: number;
  nightHalts: number;
  additionalCharges: number;
  additionalChargesNote: string | null;
  reason: string | null;
}

/** Which approval action a decision dialog is for. Reject / suspend / deactivate need a reason. */
export type AdminDecision = 'approve' | 'reject' | 'suspend' | 'deactivate';
