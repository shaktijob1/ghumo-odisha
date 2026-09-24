import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, shareReplay } from 'rxjs';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ContactService } from '../../core/services/contact.service';
import { CouponValidationResult, PaymentOrder, PaymentService } from '../../core/services/payment.service';
import { BookingResponse } from '../../core/models/booking.model';
import { PaymentPlan } from '../../core/models/enums.model';

// Loaded globally via the <script> tag in index.html — no official Angular types published.
declare const Razorpay: any;

// Mirrors GhumoOdisha.Application.Payments.BookingPaymentService — display-only; the backend
// recomputes and is the only source of truth for what actually gets charged.
const PER_SEAT_ADVANCE = 99;

/**
 * Reserves a Requested booking's seat(s) online for ₹99/seat (with an optional coupon knocking
 * money off the cash balance) via Razorpay. Used right after a booking is created (trip-detail)
 * and later from My Bookings for anyone who skipped payment the first time — same component,
 * same verified logic, in both places.
 */
@Component({
  selector: 'app-payment-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payment-panel.component.html',
})
export class PaymentPanelComponent implements OnInit {
  @Input({ required: true }) booking!: BookingResponse;
  @Input() showSkip = true;
  @Output() confirmed = new EventEmitter<void>();
  @Output() skip = new EventEmitter<void>();

  private readonly paymentService = inject(PaymentService);
  private readonly auth = inject(CustomerAuthService);
  private readonly contactService = inject(ContactService);

  // Shared (shareReplay) request kicked off in the background whenever the coupon selection
  // settles, so payNow() can call checkout.open() synchronously inside the click handler once it
  // has already resolved. Razorpay's overlay only renders in-page when open() runs as part of a
  // trusted user gesture with no async gap since the click — an order created inside the click's
  // own HTTP callback breaks that chain and mobile browsers fall back to a new tab instead.
  //
  // payNow() always subscribes to this exact observable rather than issuing its own createOrder()
  // call: the backend treats "create order" as overwriting the booking's one pending-order pointer,
  // so two independent concurrent calls (prefetch racing a click-triggered fetch) can orphan
  // whichever order the customer actually pays against, and verification would then reject a
  // genuinely successful payment.
  private pendingOrder$: Observable<PaymentOrder> | null = null;
  private pendingOrderKey = '';

  readonly contact = this.contactService.get();

  readonly payingNow = signal(false);
  readonly paymentError = signal<string | null>(null);

  readonly couponInput = signal('');
  readonly appliedCoupon = signal<CouponValidationResult | null>(null);
  readonly couponChecking = signal(false);
  readonly couponError = signal<string | null>(null);

  readonly totalAmount = computed(() => this.booking.totalAmount);
  readonly couponDiscount = computed(() => this.appliedCoupon()?.discountAmount ?? 0);

  // Coupons only ever come off the cash balance due before the trip, never the ₹99 reservation
  // fee — a coupon large enough to wipe out the whole trip cost would otherwise push "pay now"
  // toward zero. Mirrors BookingPaymentService.ComputeAmounts server-side.
  readonly effectiveTotal = computed(() => Math.max(0, this.totalAmount() - this.couponDiscount()));

  readonly payNowAmount = computed(() =>
    Math.min(PER_SEAT_ADVANCE * this.booking.numberOfSeats, this.effectiveTotal()),
  );

  readonly cashDueAmount = computed(() => this.effectiveTotal() - this.payNowAmount());

  ngOnInit(): void {
    this.prefetchOrder();
  }

  payLater(): void {
    this.skip.emit();
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
        this.prefetchOrder();
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
    this.prefetchOrder();
  }

  /** Keyed by whatever changes the amount, so a stale prefetch is never used for the wrong price. */
  private orderKey(): string {
    return this.appliedCoupon()?.code ?? '';
  }

  /** Returns the one in-flight/cached request for the current key, never starting a second. */
  private order$(): Observable<PaymentOrder> {
    const key = this.orderKey();
    if (!this.pendingOrder$ || this.pendingOrderKey !== key) {
      this.pendingOrderKey = key;
      this.pendingOrder$ = this.paymentService
        .createOrder(this.booking.bookingId, PaymentPlan.Partial, this.appliedCoupon()?.code)
        .pipe(shareReplay(1));
    }
    return this.pendingOrder$;
  }

  /** shareReplay also caches errors — drop the failed request so the next order$() call retries. */
  private invalidateOrder(key: string): void {
    if (this.pendingOrderKey === key) {
      this.pendingOrder$ = null;
    }
  }

  private prefetchOrder(): void {
    const key = this.orderKey();
    this.order$().subscribe({ error: () => this.invalidateOrder(key) });
  }

  payNow(): void {
    if (this.payingNow()) return;

    this.paymentError.set(null);
    this.payingNow.set(true);

    const key = this.orderKey();
    // If the prefetch already resolved, shareReplay emits synchronously here, keeping
    // checkout.open() inside this click handler's trusted-gesture call stack (see order$()).
    this.order$().subscribe({
      next: (order) => this.openRazorpayCheckout(order),
      error: () => {
        this.invalidateOrder(key);
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
