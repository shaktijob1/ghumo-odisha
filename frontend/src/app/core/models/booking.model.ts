import { AdminRefund, CustomerRefund } from './refund.model';
import { BookingEventType, BookingSource, BookingStatus, Gender, PaymentMethod, PaymentStatus } from './enums.model';

export interface AdminBookingListItem {
  bookingId: number;
  /** Customer-facing number, e.g. "GO-985676". */
  bookingReference: string;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  tripId: number;
  tripTitle: string;
  /** Null once the departure was deleted (only ever for bookings that were never confirmed). */
  tripDateSlotId: number | null;
  startDate: string;
  endDate: string;
  numberOfSeats: number;
  totalAmount: number;
  advanceAmount: number;
  bookingStatus: BookingStatus;
  paymentStatus: PaymentStatus;
  bookingSource: BookingSource;
  requestedAt: string;
}

export interface AdminBookingDetail {
  bookingId: number;
  bookingReference: string;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  customerEmail: string | null;
  customerBookingCount: number;
  tripId: number;
  tripTitle: string;
  tripDateSlotId: number | null;
  startDate: string;
  endDate: string;
  slotAvailableSeats: number;
  numberOfSeats: number;
  amountPerPerson: number;
  totalAmount: number;
  advanceAmount: number;
  remainingAmount: number;
  bookingStatus: BookingStatus;
  paymentStatus: PaymentStatus;
  bookingSource: BookingSource;
  customerNotes: string | null;
  adminNotes: string | null;
  requestedAt: string;
  confirmedAt: string | null;
  cancelledAt: string | null;
  discountAmount: number;
  couponCode: string | null;
  payments: BookingPayment[];
  roomsAllotted: number;
  maleCount: number | null;
  femaleCount: number | null;
  cancellationReason: string | null;
  refundWaived: boolean;
  travellers: AdminTraveller[];
  timeline: BookingEvent[];
  /** Present when the booking was cancelled with money paid (not waived). */
  refund: AdminRefund | null;
}

export interface AdminTraveller {
  bookingTravellerId: number;
  seatNumber: number;
  fullName: string;
  gender: Gender | null;
  age: number | null;
  aadhaarLast4: string | null;
  phoneNumber: string | null;
  linkedCustomerId: number | null;
  linkedCustomerName: string | null;
}

export interface BookingEvent {
  eventType: BookingEventType;
  title: string;
  description: string | null;
  actor: string;
  createdAt: string;
}

export interface TravellerInput {
  seatNumber: number;
  fullName: string;
  gender: Gender | null;
  age: number | null;
  aadhaarLast4: string | null;
  phoneNumber: string | null;
}

export interface ChangeSeatsRequest {
  numberOfSeats: number;
  reason: string;
}

export interface UpdateGenderCountsRequest {
  maleCount: number | null;
  femaleCount: number | null;
}

export interface BookingPayment {
  bookingPaymentId: number;
  amount: number;
  method: PaymentMethod;
  reference: string | null;
  notes: string | null;
  recordedBy: string;
  paidAt: string;
}

export interface AddBookingPaymentRequest {
  amount: number;
  method: PaymentMethod;
  reference?: string | null;
  notes?: string | null;
  paidAt?: string | null;
}

export interface AdminBookingFilter {
  bookingStatus?: BookingStatus | null;
  paymentStatus?: PaymentStatus | null;
  tripId?: number | null;
  search?: string | null;
}

export interface CancelBookingRequest {
  adminNotes?: string | null;
  waiveRefund?: boolean;
  reason?: string | null;
}

export interface CreateManualBookingRequest {
  customerId?: number | null;
  newCustomerName?: string | null;
  newCustomerPhoneNumber?: string | null;
  newCustomerEmail?: string | null;
  tripId: number;
  tripDateSlotId: number;
  numberOfSeats: number;
  /** What the customer has already paid — a manual booking is created confirmed, so this is required. */
  amountPaid: number;
  paymentMethod: PaymentMethod;
  paymentReference?: string | null;
  bookingSource: BookingSource;
  adminNotes?: string | null;
}

export interface CreateBookingRequest {
  tripId: number;
  tripDateSlotId: number;
  numberOfSeats: number;
  customerNotes?: string | null;
  clientRequestId?: string | null;
  pickupPointId?: number | null;
  agreedToTerms: boolean;
  /** Gents and ladies travelling — they must add up to numberOfSeats. */
  maleCount: number;
  femaleCount: number;
}

export interface CreateBookingResult {
  booking: BookingResponse;
  whatsAppMessage: string;
}

export interface BookingResponse {
  bookingId: number;
  /** Customer-facing number, e.g. "GO-985676". */
  bookingReference: string;
  tripId: number;
  tripTitle: string;
  tripCoverImageUrl: string | null;
  tripDateSlotId: number | null;
  startDate: string;
  endDate: string;
  pickupPointLocation: string | null;
  pickupPointTime: string | null;
  numberOfSeats: number;
  amountPerPerson: number;
  totalAmount: number;
  advanceAmount: number;
  remainingAmount: number;
  bookingStatus: BookingStatus;
  paymentStatus: PaymentStatus;
  bookingSource: BookingSource;
  customerNotes: string | null;
  adminNotes: string | null;
  requestedAt: string;
  confirmedAt: string | null;
  cancelledAt: string | null;
  slotAvailableSeats: number;
  /** False for a booking the organizer added this customer to as a traveller — view-only. */
  isOwner: boolean;
  roomsAllotted: number;
  timeline: BookingEvent[];
  /** Empty for a linked traveller — only the booker sees payment details. */
  payments: BookingPayment[];
  /** Cancelled with money paid: the refund's progress (booker only). */
  refund: CustomerRefund | null;
  /** False when the organizer has switched coupons off for this trip. */
  couponsAllowed: boolean;
}
