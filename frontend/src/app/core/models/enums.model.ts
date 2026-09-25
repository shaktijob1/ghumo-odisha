export enum TripStatus {
  Active = 0,
  Inactive = 1,
}

export enum TripDateSlotStatus {
  Active = 0,
  Inactive = 1,
}

export enum BookingStatus {
  Requested = 0,
  Pending = 1,
  Confirmed = 2,
  Rejected = 3,
  Cancelled = 4,
  Completed = 5,
}

export enum PaymentStatus {
  Unpaid = 0,
  AdvancePaid = 1,
  Paid = 2,
  Refunded = 3,
}

export enum BookingSource {
  Website = 0,
  Phone = 1,
  WhatsApp = 2,
  Offline = 3,
}

export enum Gender {
  Male = 0,
  Female = 1,
  Other = 2,
}

export const GenderLabels: Record<Gender, string> = {
  [Gender.Male]: 'Male',
  [Gender.Female]: 'Female',
  [Gender.Other]: 'Other',
};

export enum BookingEventType {
  Requested = 0,
  Confirmed = 1,
  Rejected = 2,
  Cancelled = 3,
  Completed = 4,
  PaymentReceived = 5,
  PaymentRemoved = 6,
  SeatsChanged = 7,
  TravellersUpdated = 8,
  GenderCountsUpdated = 9,
  Created = 10,
}

/** Colour of the timeline dot for each kind of step. */
export function bookingEventTone(type: BookingEventType): 'ok' | 'wait' | 'bad' | 'info' {
  switch (type) {
    case BookingEventType.Confirmed:
    case BookingEventType.PaymentReceived:
    case BookingEventType.Completed:
      return 'ok';
    case BookingEventType.Requested:
    case BookingEventType.Created:
      return 'wait';
    case BookingEventType.Cancelled:
    case BookingEventType.Rejected:
    case BookingEventType.PaymentRemoved:
      return 'bad';
    default:
      return 'info';
  }
}

export enum PaymentMethod {
  Razorpay = 0,
  Cash = 1,
  Upi = 2,
  BankTransfer = 3,
  Other = 4,
}

export const PaymentMethodLabels: Record<PaymentMethod, string> = {
  [PaymentMethod.Razorpay]: 'Online (Razorpay)',
  [PaymentMethod.Cash]: 'Cash',
  [PaymentMethod.Upi]: 'UPI',
  [PaymentMethod.BankTransfer]: 'Bank transfer',
  [PaymentMethod.Other]: 'Other',
};

/** Methods an admin can record by hand — Razorpay payments are only ever recorded by the server. */
export const OfflinePaymentMethods: PaymentMethod[] = [PaymentMethod.Cash, PaymentMethod.Upi, PaymentMethod.BankTransfer, PaymentMethod.Other];

/** Not persisted — only decides how much is charged now vs. left as balance. */
export enum PaymentPlan {
  Partial = 0,
  Full = 1,
}

export const BookingStatusLabels: Record<BookingStatus, string> = {
  [BookingStatus.Requested]: 'Requested',
  [BookingStatus.Pending]: 'Pending',
  [BookingStatus.Confirmed]: 'Confirmed',
  [BookingStatus.Rejected]: 'Rejected',
  [BookingStatus.Cancelled]: 'Cancelled',
  [BookingStatus.Completed]: 'Completed',
};

export const PaymentStatusLabels: Record<PaymentStatus, string> = {
  [PaymentStatus.Unpaid]: 'Unpaid',
  [PaymentStatus.AdvancePaid]: 'Advance paid',
  [PaymentStatus.Paid]: 'Paid',
  [PaymentStatus.Refunded]: 'Refunded',
};

export const BookingSourceLabels: Record<BookingSource, string> = {
  [BookingSource.Website]: 'Website',
  [BookingSource.Phone]: 'Phone',
  [BookingSource.WhatsApp]: 'WhatsApp',
  [BookingSource.Offline]: 'Offline',
};

export function bookingStatusBadgeClass(status: BookingStatus): string {
  switch (status) {
    case BookingStatus.Confirmed:
    case BookingStatus.Completed:
      return 'ok';
    case BookingStatus.Requested:
    case BookingStatus.Pending:
      return 'wait';
    case BookingStatus.Rejected:
    case BookingStatus.Cancelled:
      return 'bad';
    default:
      return '';
  }
}
