import { BookingStatus, PaymentMethod } from './enums.model';

export interface AdminCoupon {
  couponCodeId: number;
  code: string;
  holderName: string;
  discountAmount: number;
  commissionPerSeat: number;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
  isFirstTimeCustomerOnly: boolean;
  redemptionCount: number;
  newCustomerCount: number;
  existingCustomerCount: number;
  totalSeats: number;
  commissionEarned: number;
  createdAt: string;
  activeBookingCount: number;
  cancelledBookingCount: number;
  /** Commission lost because the booking was cancelled or rejected. */
  commissionReversed: number;
  commissionPaid: number;
  /** Earned − paid. Negative after a cancellation that came after a payout — netted against future commission. */
  balanceDue: number;
}

export interface CouponPayout {
  couponPayoutId: number;
  amount: number;
  method: PaymentMethod;
  reference: string | null;
  notes: string | null;
  paidAt: string;
}

export interface AddCouponPayoutRequest {
  amount: number;
  method: PaymentMethod;
  reference: string | null;
  notes: string | null;
  paidAt: string | null;
}

export interface AdminCouponBooking {
  bookingId: number;
  customerId: number;
  customerName: string;
  customerPhone: string;
  isNewCustomer: boolean;
  tripTitle: string;
  startDate: string;
  numberOfSeats: number;
  totalAmount: number;
  discountAmount: number;
  /** Commission recorded when the coupon was redeemed. */
  commissionAmount: number;
  /** Zero when the booking was later cancelled or rejected. */
  commissionEarned: number;
  bookingStatus: BookingStatus;
  redeemedAt: string;
}

export interface AdminCreateCouponRequest {
  code: string;
  holderName: string;
  discountAmount: number;
  commissionPerSeat: number;
  validFrom: string | null;
  validUntil: string | null;
  isFirstTimeCustomerOnly: boolean;
}

export interface AdminUpdateCouponRequest {
  holderName: string;
  discountAmount: number;
  commissionPerSeat: number;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
  isFirstTimeCustomerOnly: boolean;
}
