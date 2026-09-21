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
