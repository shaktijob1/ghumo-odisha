import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { CouponValidationResult, PaymentService } from '../../core/services/payment.service';
import { BookingResponse } from '../../core/models/booking.model';
import { PaymentPlan } from '../../core/models/enums.model';

// Loaded globally via the <script> tag in index.html — no official Angular types published.
declare const Razorpay: any;

// Mirrors GhumoOdisha.Application.Payments.BookingPaymentService — display-only; the backend
// recomputes and is the only source of truth for what actually gets charged.
const PER_SEAT_ADVANCE = 99;
const FULL_PAYMENT_DISCOUNT_PER_SEAT = 199;

/**
 * Pays for a Requested booking online (partial or full, with an optional coupon) via Razorpay.
 * Used right after a booking is created (trip-detail) and later from My Bookings for anyone who
 * skipped payment the first time — same component, same verified logic, in both places.
 */
@Component({
  selector: 'app-payment-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payment-panel.component.html',
})
export class PaymentPanelComponent {
  @Input({ required: true }) booking!: BookingResponse;
  @Input() showSkip = true;
  @Output() confirmed = new EventEmitter<void>();
  @Output() skip = new EventEmitter<void>();

  private readonly paymentService = inject(PaymentService);
  private readonly auth = inject(CustomerAuthService);

  readonly PaymentPlan = PaymentPlan;
  readonly paymentPlan = signal<PaymentPlan>(PaymentPlan.Partial);
  readonly payingNow = signal(false);
  readonly paymentError = signal<string | null>(null);

  readonly couponInput = signal('');
  readonly appliedCoupon = signal<CouponValidationResult | null>(null);
  readonly couponChecking = signal(false);
  readonly couponError = signal<string | null>(null);

  readonly totalAmount = computed(() => this.booking.totalAmount);
  readonly couponDiscount = computed(() => this.appliedCoupon()?.discountAmount ?? 0);
  readonly fullPaymentDiscount = computed(() => FULL_PAYMENT_DISCOUNT_PER_SEAT * this.booking.numberOfSeats);

  readonly effectiveTotal = computed(() => {
    const planDiscount = this.paymentPlan() === PaymentPlan.Full ? this.fullPaymentDiscount() : 0;
    return Math.max(0, this.totalAmount() - planDiscount - this.couponDiscount());
  });

  readonly payNowAmount = computed(() =>
    this.paymentPlan() === PaymentPlan.Partial
      ? Math.min(PER_SEAT_ADVANCE * this.booking.numberOfSeats, this.effectiveTotal())
      : this.effectiveTotal(),
  );

  readonly payLaterAmount = computed(() =>
    this.paymentPlan() === PaymentPlan.Partial ? this.effectiveTotal() - this.payNowAmount() : 0,
  );

  selectPaymentPlan(plan: PaymentPlan): void {
    this.paymentPlan.set(plan);
  }

  applyCoupon(): void {
    const code = this.couponInput().trim();
    if (!code || this.couponChecking()) return;

    this.couponChecking.set(true);
    this.couponError.set(null);
    this.paymentService.validateCoupon(code).subscribe({
      next: (result) => {
        this.couponChecking.set(false);
        this.appliedCoupon.set(result);
      },
      error: (err) => {
        this.couponChecking.set(false);
        this.couponError.set(err?.error?.message || 'Could not apply this coupon.');
      },
    });
  }

  removeCoupon(): void {
    this.appliedCoupon.set(null);
    this.couponInput.set('');
    this.couponError.set(null);
  }

  payNow(): void {
    if (this.payingNow()) return;

    this.paymentError.set(null);
    this.payingNow.set(true);

    this.paymentService.createOrder(this.booking.bookingId, this.paymentPlan(), this.appliedCoupon()?.code).subscribe({
      next: (order) => this.openRazorpayCheckout(order),
      error: () => {
        this.payingNow.set(false);
        this.paymentError.set('Could not start payment. Please try again.');
      },
    });
  }

  private openRazorpayCheckout(order: { orderId: string; amountPaise: number; currency: string; keyId: string }): void {
    const customer = this.auth.currentCustomer();

    const checkout = new Razorpay({
      key: order.keyId,
      order_id: order.orderId,
      amount: order.amountPaise,
      currency: order.currency,
      name: 'Ghumo Odisha',
      description: this.booking.tripTitle,
      prefill: { name: customer?.name ?? '', contact: customer?.phoneNumber ?? '' },
      theme: { color: '#0F6F5C' },
      modal: {
        ondismiss: () => this.payingNow.set(false),
      },
      handler: (response: { razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string }) => {
        this.paymentService
          .verifyPayment(this.booking.bookingId, {
            razorpayOrderId: response.razorpay_order_id,
            razorpayPaymentId: response.razorpay_payment_id,
            razorpaySignature: response.razorpay_signature,
          })
          .subscribe({
            next: () => {
              this.payingNow.set(false);
              this.confirmed.emit();
            },
            error: () => {
              this.payingNow.set(false);
              this.paymentError.set('Payment received but could not be verified. Please contact us with your payment ID before retrying.');
            },
          });
      },
    });

    checkout.on('payment.failed', () => {
      this.payingNow.set(false);
      this.paymentError.set('Payment failed. Please try again.');
    });

    checkout.open();
  }
}
