// Cars module — mirrors the API's Cars DTOs. Enums are sent as numbers (the API's default).

export enum FuelType {
  Petrol = 0,
  Diesel = 1,
  CNG = 2,
  EV = 3,
  Other = 4,
}

export const FuelTypeLabels: Record<FuelType, string> = {
  [FuelType.Petrol]: 'Petrol',
  [FuelType.Diesel]: 'Diesel',
  [FuelType.CNG]: 'CNG',
  [FuelType.EV]: 'Electric',
  [FuelType.Other]: 'Other',
};

export enum CarPhotoKind {
  Exterior = 0,
  Interior = 1,
}

export enum DriverStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Suspended = 3,
}

export enum CarStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Inactive = 3,
  Suspended = 4,
}

export enum CarPricingStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Superseded = 3,
  Withdrawn = 4,
}

export enum DriverDocumentType {
  DrivingLicence = 0,
  RegistrationCertificate = 1,
  Insurance = 2,
  Permit = 3,
  Other = 4,
}

export const DriverDocumentTypeLabels: Record<DriverDocumentType, string> = {
  [DriverDocumentType.DrivingLicence]: 'Driving licence',
  [DriverDocumentType.RegistrationCertificate]: 'Registration certificate (RC)',
  [DriverDocumentType.Insurance]: 'Insurance',
  [DriverDocumentType.Permit]: 'Permit',
  [DriverDocumentType.Other]: 'Other',
};

export enum CarBookingStatus {
  PendingPayment = 0,
  Confirmed = 1,
  InProgress = 2,
  Completed = 3,
  Cancelled = 4,
  Expired = 5,
}

export const CarBookingStatusLabels: Record<CarBookingStatus, string> = {
  [CarBookingStatus.PendingPayment]: 'Awaiting payment',
  [CarBookingStatus.Confirmed]: 'Confirmed',
  [CarBookingStatus.InProgress]: 'Trip in progress',
  [CarBookingStatus.Completed]: 'Completed',
  [CarBookingStatus.Cancelled]: 'Cancelled',
  [CarBookingStatus.Expired]: 'Not paid',
};

export function carBookingBadgeClass(status: CarBookingStatus): string {
  switch (status) {
    case CarBookingStatus.Confirmed:
    case CarBookingStatus.Completed:
      return 'ok';
    case CarBookingStatus.InProgress:
      return 'info';
    case CarBookingStatus.PendingPayment:
      return 'wait';
    case CarBookingStatus.Cancelled:
    case CarBookingStatus.Expired:
      return 'bad';
  }
}

export enum CarPaymentStatus {
  Unpaid = 0,
  BookingAmountPaid = 1,
  BalanceCollected = 2,
  RefundPending = 3,
  RefundProcessing = 4,
  Refunded = 5,
}

export const CarPaymentStatusLabels: Record<CarPaymentStatus, string> = {
  [CarPaymentStatus.Unpaid]: 'Not paid',
  [CarPaymentStatus.BookingAmountPaid]: 'Booking amount paid',
  [CarPaymentStatus.BalanceCollected]: 'Fully paid',
  [CarPaymentStatus.RefundPending]: 'Refund requested',
  [CarPaymentStatus.RefundProcessing]: 'Refund initiated',
  [CarPaymentStatus.Refunded]: 'Refunded',
};

/** Shared by driver + admin status badges. */
export const ApprovalLabels = {
  driver: {
    [DriverStatus.Pending]: 'Pending approval',
    [DriverStatus.Approved]: 'Approved',
    [DriverStatus.Rejected]: 'Rejected',
    [DriverStatus.Suspended]: 'Suspended',
  } as Record<DriverStatus, string>,
  car: {
    [CarStatus.Pending]: 'Pending approval',
    [CarStatus.Approved]: 'Approved',
    [CarStatus.Rejected]: 'Rejected',
    [CarStatus.Inactive]: 'Inactive',
    [CarStatus.Suspended]: 'Suspended',
  } as Record<CarStatus, string>,
  pricing: {
    [CarPricingStatus.Pending]: 'Pending approval',
    [CarPricingStatus.Approved]: 'Approved',
    [CarPricingStatus.Rejected]: 'Rejected',
    [CarPricingStatus.Superseded]: 'Replaced',
    [CarPricingStatus.Withdrawn]: 'Withdrawn',
  } as Record<CarPricingStatus, string>,
};

export function approvalBadgeClass(status: number, approved: number, rejectedLike: number[]): string {
  if (status === approved) return 'ok';
  if (rejectedLike.includes(status)) return 'bad';
  return 'wait';
}

// ---------- Public ----------

export interface CarRentalSettings {
  bookingAmount: number;
  defaultNightHaltPrice: number;
  seatCapacities: number[];
  minDurationHours: number;
  maxDurationHours: number;
  maxEstimatedKm: number;
  minimumLeadMinutes: number;
  maxAdvanceBookingDays: number;
}

/** The customer's chosen rental window. date "yyyy-MM-dd", time "HH:mm" — both India time. */
export interface CarWindow {
  date: string;
  time: string;
  hours: number;
}

export interface CarSearchResult {
  carId: number;
  displayName: string;
  category: string;
  brand: string;
  modelName: string;
  fuelType: FuelType;
  seatCapacity: number;
  hasAc: boolean;
  baseCity: string;
  coverPhotoUrl: string | null;
  pricePerKm: number;
  /** Lowest and highest base fare across the distance ranges. */
  baseFareFrom: number;
  baseFareTo: number;
  nightHaltPrice: number;
  isAvailable: boolean;
}

export interface CarSearchResults {
  cars: CarSearchResult[];
  locations: string[];
}

export interface CarPhoto {
  carPhotoId: number;
  kind: CarPhotoKind;
  imageUrl: string;
  displayOrder: number;
}

export interface CarPricingTier {
  upToKm: number | null;
  baseFare: number;
}

export interface CarPublicDetail {
  summary: CarSearchResult;
  description: string | null;
  photos: CarPhoto[];
  baseFareTiers: CarPricingTier[];
  driver: { firstName: string; profilePhotoUrl: string | null; experienceYears: number | null };
}

export interface CarFareQuote {
  carId: number;
  pickupAt: string;
  endsAt: string;
  durationHours: number;
  estimatedKm: number;
  pricePerKm: number;
  baseFare: number;
  kmCharge: number;
  nights: number;
  nightHaltPrice: number;
  nightHaltCharge: number;
  estimatedTotal: number;
  bookingAmount: number;
  remainingAmount: number;
  isAvailable: boolean;
  unavailableReason: string | null;
}

// ---------- Bookings ----------

export interface FareBreakdown {
  km: number;
  baseFare: number;
  kmCharge: number;
  nights: number;
  nightHaltCharge: number;
  additionalCharges: number;
  additionalChargesNote: string | null;
  total: number;
}

export interface CarTrip {
  startedAt: string;
  startOdometerKm: number;
  endedAt: string | null;
  endOdometerKm: number | null;
  actualKm: number | null;
  nightHalts: number | null;
  completedAt: string | null;
}

export interface CarRefund {
  amount: number;
  status: CarPaymentStatus;
  method: number | null;
  reference: string | null;
  issuedAt: string | null;
  settledAt: string | null;
}

export interface CarTimelineEvent {
  title: string;
  note: string | null;
  actorRole: string;
  createdAt: string;
}

export interface CarBooking {
  carBookingId: number;
  reference: string;
  status: CarBookingStatus;
  paymentStatus: CarPaymentStatus;
  carId: number;
  carDisplayName: string;
  category: string;
  fuelType: FuelType;
  seatCapacity: number;
  hasAc: boolean;
  carPhotoUrl: string | null;
  registrationNumber: string | null;
  driver: { driverId: number; name: string; phoneNumber: string | null; profilePhotoUrl: string | null } | null;
  pickupCity: string;
  pickupAddress: string | null;
  pickupAt: string;
  durationHours: number;
  endsAt: string;
  pricePerKm: number;
  nightHaltPrice: number;
  estimate: FareBreakdown;
  final: FareBreakdown | null;
  bookingAmount: number;
  remainingAmount: number;
  balanceCollectedAt: string | null;
  trip: CarTrip | null;
  refund: CarRefund | null;
  holdExpiresAt: string | null;
  customerNotes: string | null;
  cancelledBy: string | null;
  cancellationReason: string | null;
  confirmedAt: string | null;
  cancelledAt: string | null;
  createdAt: string;
  canPay: boolean;
  canCancel: boolean;
  timeline: CarTimelineEvent[];
}

export interface CreateCarBookingRequest {
  carId: number;
  pickupDate: string;
  pickupTime: string;
  durationHours: number;
  estimatedKm: number;
  pickupAddress: string | null;
  customerNotes: string | null;
  clientRequestId: string;
}

export interface CarPaymentOrder {
  orderId: string;
  amountPaise: number;
  currency: string;
  keyId: string;
  discountApplied: number;
  devBypass?: boolean;
}

export interface CarBookingSummary {
  carBookingId: number;
  reference: string;
  customerName: string;
  carDisplayName: string;
  driverName: string;
  pickupAt: string;
  status: CarBookingStatus;
  paymentStatus: CarPaymentStatus;
  estimatedTotal: number;
  finalTotal: number | null;
  createdAt: string;
}
