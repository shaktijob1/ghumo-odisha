import { BookingStatus, PaymentStatus } from './enums.model';

/** A trip in the Collections dropdown — only trips with confirmed bookings are listed. */
export interface CollectionTrip {
  tripId: number;
  title: string;
  bookings: number;
  remaining: number;
  departures: CollectionDeparture[];
}

export interface CollectionDeparture {
  tripDateSlotId: number;
  startDate: string;
  endDate: string;
  bookings: number;
  remaining: number;
}

export interface CollectionTotals {
  bookings: number;
  seats: number;
  totalAmount: number;
  paid: number;
  remaining: number;
}

export interface CollectionBooking {
  bookingId: number;
  bookingReference: string;
  customerName: string;
  customerPhone: string | null;
  startDate: string;
  endDate: string;
  numberOfSeats: number;
  totalAmount: number;
  paid: number;
  remaining: number;
  bookingStatus: BookingStatus;
  paymentStatus: PaymentStatus;
}

export interface CollectionSheet {
  tripId: number;
  tripTitle: string;
  tripDateSlotId: number | null;
  totals: CollectionTotals;
  items: CollectionBooking[];
}

export interface PaymentQr {
  imageUrl: string;
  caption: string | null;
  updatedAt: string;
}
