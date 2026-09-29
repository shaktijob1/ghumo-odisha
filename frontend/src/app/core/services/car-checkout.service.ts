import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CarBooking, CarPaymentOrder } from '../models/car.model';
import { CarService } from './car.service';
import { CustomerAuthService } from './customer-auth.service';
import { loadRazorpay, razorpayReady } from '../../shared/utils/razorpay-loader';
import { apiErrorMessage } from '../../shared/utils/api-error';

// Loaded on demand by loadRazorpay() — no official Angular types published.
declare const Razorpay: any;

export interface CarCheckoutCallbacks {
  confirmed: (booking: CarBooking) => void;
  failed: (message: string) => void;
  dismissed: () => void;
}

/**
 * Pays a car booking's booking amount through Razorpay (same gateway + flow as trip bookings).
 * The order is created first (so the amount comes from the server), then `open()` is called straight
 * from the Pay click — Razorpay's overlay only renders in-page inside a user gesture.
 */
@Injectable({ providedIn: 'root' })
export class CarCheckoutService {
  private readonly carService = inject(CarService);
  private readonly auth = inject(CustomerAuthService);

  /** Warm the gateway script so checkout opens instantly on tap. */
  warmUp(): void {
    loadRazorpay().catch(() => undefined);
  }

  createOrder(bookingId: number): Observable<CarPaymentOrder> {
    return this.carService.createPaymentOrder(bookingId);
  }

  /** Call synchronously from the Pay click, with an order created beforehand. */
  pay(booking: CarBooking, order: CarPaymentOrder, callbacks: CarCheckoutCallbacks): void {
    // The server decides the amount; never charge something other than what the screen shows.
    if (order.amountPaise !== Math.round(booking.bookingAmount * 100)) {
      callbacks.failed(`The amount to pay is ₹${order.amountPaise / 100}, not ₹${booking.bookingAmount}. Please reload the page and try again.`);
      return;
    }

    if (order.devBypass) {
      // Local development only (API Razorpay:DevBypassEnabled): no real checkout.
      this.verify(booking.carBookingId, { razorpayOrderId: order.orderId, razorpayPaymentId: 'dev_pay', razorpaySignature: 'dev_bypass' }, callbacks);
      return;
    }

    if (!razorpayReady()) {
      loadRazorpay().then(
        () => this.pay(booking, order, callbacks),
        () => callbacks.failed('Could not load the payment gateway. Please check your connection and try again.'),
      );
      return;
    }

    const customer = this.auth.currentCustomer();
    const checkout = new Razorpay({
      key: order.keyId,
      order_id: order.orderId,
      amount: order.amountPaise,
      currency: order.currency,
      name: 'Ghumo Odisha',
      description: `${booking.carDisplayName} · ${booking.reference}`,
      prefill: { name: customer?.name ?? '', contact: customer?.phoneNumber ?? '' },
      theme: { color: '#0F6F5C' },
      modal: { ondismiss: () => callbacks.dismissed() },
      handler: (response: { razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string }) =>
        this.verify(
          booking.carBookingId,
          {
            razorpayOrderId: response.razorpay_order_id,
            razorpayPaymentId: response.razorpay_payment_id,
            razorpaySignature: response.razorpay_signature,
          },
          callbacks,
        ),
    });
    checkout.on('payment.failed', () => callbacks.failed('Payment failed. No money was taken — please try again.'));
    checkout.open();
  }

  private verify(bookingId: number, payment: { razorpayOrderId: string; razorpayPaymentId: string; razorpaySignature: string }, callbacks: CarCheckoutCallbacks): void {
    this.carService.verifyPayment(bookingId, payment).subscribe({
      next: (booking) => callbacks.confirmed(booking),
      error: (e: unknown) =>
        callbacks.failed(apiErrorMessage(e, 'Payment received but could not be confirmed. Please contact us with your booking number before retrying.')),
    });
  }
}
