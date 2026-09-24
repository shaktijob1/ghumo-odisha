import { BookingSource, BookingStatus, PaymentStatus } from './enums.model';

export interface AdminBookingListItem {
  bookingId: number;
  customerId: number;
  customerName: string;
  customerPhone: string;
  tripId: number;
  tripTitle: string;
  tripDateSlotId: number;
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
  customerId: number;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  customerBookingCount: number;
  tripId: number;
  tripTitle: string;
  tripDateSlotId: number;
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
}

export interface AdminBookingFilter {
  bookingStatus?: BookingStatus | null;
  paymentStatus?: PaymentStatus | null;
  tripId?: number | null;
  search?: string | null;
}

export interface ConfirmBookingRequest {
  advanceAmount: number;
}

export interface RejectBookingRequest {
  adminNotes?: string | null;
}

export interface CancelBookingRequest {
  adminNotes?: string | null;
}

export interface CreateManualBookingRequest {
  customerId?: number | null;
  newCustomerName?: string | null;
  newCustomerPhoneNumber?: string | null;
  newCustomerEmail?: string | null;
  tripId: number;
  tripDateSlotId: number;
  numberOfSeats: number;
  advanceAmount: number;
  bookingSource: BookingSource;
  initialStatus: BookingStatus;
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
}

export interface CreateBookingResult {
  booking: BookingResponse;
  whatsAppMessage: string;
}

export interface BookingResponse {
  bookingId: number;
  tripId: number;
  tripTitle: string;
  tripCoverImageUrl: string | null;
  tripDateSlotId: number;
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
}
