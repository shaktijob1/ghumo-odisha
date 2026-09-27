import { PaymentMethod, RefundStatus } from './enums.model';

/** What the customer sees about the refund on a cancelled booking. */
export interface CustomerRefund {
  amount: number;
  status: RefundStatus;
  method: PaymentMethod | null;
  reference: string | null;
  requestedAt: string;
  initiatedAt: string | null;
  settledAt: string | null;
}

export interface AdminRefund {
  refundId: number;
  bookingId: number;
  amountPaid: number;
  amount: number;
  /** Paid through Razorpay — the most a Razorpay refund can return. */
  onlinePaidAmount: number;
  status: RefundStatus;
  method: PaymentMethod | null;
  reference: string | null;
  notes: string | null;
  requestedBy: string;
  requestedAt: string;
  initiatedAt: string | null;
  settledAt: string | null;
  customerName: string;
  customerPhone: string | null;
  customerEmail: string | null;
  tripTitle: string;
  startDate: string;
  numberOfSeats: number;
  cancellationReason: string | null;
  cancelledAt: string | null;
}

export interface RefundCounts {
  pending: number;
  processing: number;
  settled: number;
}
