import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, shareReplay } from 'rxjs';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ContactService } from '../../core/services/contact.service';
import { PaymentOrder, PaymentService } from '../../core/services/payment.service';
import { BookingResponse } from '../../core/models/booking.model';
import { PaymentPlan } from '../../core/models/enums.model';
import { BookingPriceSummaryComponent, payNowFor } from './booking-price-summary.component';
import { CouponFieldComponent, CouponSelection } from './coupon-field.component';

// Loaded globally via the <script> tag in index.html — no official Angular types published.
declare const Razorpay: any;

/**
 * Reserves a Requested booking's seat(s) online for ₹99/seat (with an optional coupon knocking
 * money off the cash balance) via Razorpay. Used right after a booking is created (trip-detail)
 * and later from My Bookings for anyone who skipped payment the first time — same component,
 * same verified logic, in both places.
 */
@Component({
  selector: 'app-payment-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, BookingPriceSummaryComponent, CouponFieldComponent],
  templateUrl: './payment-panel.component.html',
})
export class PaymentPanelComponent implements OnInit {
  @Input({ required: true }) booking!: BookingResponse;
  @Input() showSkip = true;
  /** Coupon picked earlier (e.g. in the trip page sidebar, possibly before sign-in) — re-checked on open. */
  @Input() coupon: CouponSelection | null = null;
  @Output() couponChange = new EventEmitter<CouponSelection | null>();
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

  readonly selection = signal<CouponSelection | null>(null);
  readonly couponError = signal<string | null>(null);
  readonly revalidatingCoupon = signal(false);

  /** Only a coupon the API has validated for this customer counts toward the amounts. */
  private readonly appliedCode = computed(() => (this.selection()?.validated ? this.selection()!.code : null));
  readonly couponDiscount = computed(() => (this.selection()?.validated ? this.selection()!.discountAmount : 0));

  // Coupons only ever come off the cash balance due before the trip, never the ₹99 reservation
  // fee. Same formula as BookingPaymentService.ComputeAmounts server-side.
  readonly payNowAmount = computed(() =>
    payNowFor(this.booking.amountPerPerson, this.booking.numberOfSeats, this.couponDiscount()),
  );

  ngOnInit(): void {
    const carried = this.coupon;
    if (!carried) {
      this.prefetchOrder();
      return;
    }

    // A code picked before sign-in (or before this customer's checks could run) is validated now,
    // for this customer — "already used" and "first-time only" depend on who they are.
    this.revalidatingCoupon.set(true);
    this.paymentService.validateCoupon(carried.code).subscribe({
      next: (r) => {
        this.revalidatingCoupon.set(false);
        this.setSelection({ code: r.code, discountAmount: r.discountAmount, validated: true });
      },
      error: (err) => {
        this.revalidatingCoupon.set(false);
        this.couponError.set(`${carried.code} couldn't be applied: ${err?.error?.message || 'this coupon is not valid.'}`);
        this.setSelection(null);
      },
    });
  }

  payLater(): void {
    this.skip.emit();
  }

  onCouponChange(selection: CouponSelection | null): void {
    this.couponError.set(null);
    this.setSelection(selection);
  }

  private setSelection(selection: CouponSelection | null): void {
    this.selection.set(selection);
    this.couponChange.emit(selection);
    this.prefetchOrder();
  }

  /** Keyed by whatever changes the amount, so a stale prefetch is never used for the wrong price. */
  private orderKey(): string {
    return this.appliedCode() ?? '';
  }

  /** Returns the one in-flight/cached request for the current key, never starting a second. */
  private order$(): Observable<PaymentOrder> {
    const key = this.orderKey();
    if (!this.pendingOrder$ || this.pendingOrderKey !== key) {
      this.pendingOrderKey = key;
      this.pendingOrder$ = this.paymentService
        .createOrder(this.booking.bookingId, PaymentPlan.Partial, this.appliedCode() ?? undefined)
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
      next: (order) => {
        // The server computed this charge (₹99 × seats) on its own — if it ever differs from what
        // this screen is showing, stop rather than charge an amount the customer didn't see.
        if (order.amountPaise !== Math.round(this.payNowAmount() * 100)) {
          this.invalidateOrder(key);
          this.payingNow.set(false);
          this.paymentError.set(`The amount to pay is ₹${order.amountPaise / 100}, not ₹${this.payNowAmount()}. Please reload the page and try again.`);
          return;
        }
        if (order.devBypass) this.confirmDevBypassPayment(order.orderId);
        else this.openRazorpayCheckout(order);
      },
      error: () => {
        this.invalidateOrder(key);
        this.payingNow.set(false);
        this.paymentError.set('Could not start payment. Please try again.');
      },
    });
  }

  // Local development only (API Razorpay:DevBypassEnabled): the order was never sent to Razorpay, so
  // skip the checkout overlay and let the API confirm the booking directly. The server ignores the
  // placeholder payment id/signature for these orders.
  private confirmDevBypassPayment(orderId: string): void {
    this.paymentService
      .verifyPayment(this.booking.bookingId, { razorpayOrderId: orderId, razorpayPaymentId: 'dev_pay', razorpaySignature: 'dev_bypass' })
      .subscribe({
        next: () => {
          this.payingNow.set(false);
          this.confirmed.emit();
        },
        error: () => {
          this.payingNow.set(false);
          this.paymentError.set('Could not confirm the booking. Please try again.');
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
