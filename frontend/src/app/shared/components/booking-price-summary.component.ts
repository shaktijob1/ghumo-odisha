import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { roomsForSeats } from '../utils/rooms';

/** Mirrors BookingPaymentService.PerSeatAdvanceAmount — display only; the API computes the real charge. */
export const PER_SEAT_ADVANCE = 99;

/** What "Pay now" will be for these seats — the same formula the API uses (coupon never lowers the ₹99/seat fee). */
export function payNowFor(pricePerPerson: number, seats: number, couponDiscount: number): number {
  const effectiveTotal = Math.max(0, pricePerPerson * seats - couponDiscount);
  return Math.min(PER_SEAT_ADVANCE * seats, effectiveTotal);
}

/**
 * The trip summary / Pay Now / Remaining block — one component so the trip page sidebar and the
 * payment step after WhatsApp verification always look and calculate the same. The coupon field
 * is projected between Pay Now and Remaining (the discount comes off the remaining amount).
 * Display-only: the API recomputes everything that is actually charged.
 */
@Component({
  selector: 'app-booking-price-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="bp-summary">
      <div class="bp-sumhead">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="2"></rect><path d="M8 8h8M8 12h8M8 16h5"></path></svg>
        <h4>Trip Summary</h4>
      </div>
      <div class="bp-sumrow">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"></rect><path d="M16 2v4M8 2v4M3 10h18"></path></svg>
        <span class="k">Date</span>
        <span class="v">{{ startDate | date: 'd MMM y' }} → {{ endDate | date: 'd MMM y' }}</span>
      </div>
      <div class="bp-sumrow">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.59 13.41 11 3.83A2 2 0 0 0 9.57 3H4a1 1 0 0 0-1 1v5.57a2 2 0 0 0 .83 1.42l9.59 9.58a2 2 0 0 0 2.83 0l4.34-4.34a2 2 0 0 0 0-2.82z"></path><circle cx="7.5" cy="7.5" r="1.2"></circle></svg>
        <span class="k">Trip Price</span>
        <span class="v">₹{{ pricePerPerson | number: '1.0-0' }} × {{ seats }}</span>
      </div>
      <div class="bp-sumrow">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 20v-8a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v8"></path><path d="M4 10V6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v4"></path><path d="M12 4v6"></path><path d="M2 18h20"></path></svg>
        <span class="k">Rooms</span>
        <span class="v">{{ rooms }} {{ rooms === 1 ? 'room' : 'rooms' }}</span>
      </div>
      <div class="bp-sumrow">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 7H5a2 2 0 0 1 0-4h13v4"></path><path d="M3 5v14a2 2 0 0 0 2 2h15V7"></path><path d="M16 14h.01"></path></svg>
        <span class="k">Booking Advance</span>
        <span class="v">₹{{ perSeatAdvance }} × {{ seats }}</span>
      </div>
      <div class="bp-paynow">
        <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 7H5a2 2 0 0 1 0-4h13v4"></path><path d="M3 5v14a2 2 0 0 0 2 2h15V7"></path><path d="M21 12h-4a2 2 0 0 0 0 4h4z"></path></svg>
        <div class="bp-rowtext"><b>Pay Now</b><small>To confirm your seat</small></div>
        <strong>₹{{ payNow | number: '1.0-0' }}</strong>
      </div>
    </div>

    <ng-content select="[couponSlot]"></ng-content>

    <div class="bp-remaining">
      <svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><ellipse cx="14" cy="6" rx="7" ry="3"></ellipse><path d="M7 6v4c0 1.7 3.1 3 7 3s7-1.3 7-3V6"></path><path d="M21 10v4c0 1.7-3.1 3-7 3"></path><ellipse cx="9" cy="14" rx="6" ry="2.6"></ellipse><path d="M3 14v4c0 1.4 2.7 2.6 6 2.6s6-1.2 6-2.6v-4"></path></svg>
      <div class="bp-rowtext">
        <b>Remaining Amount</b>
        <small>
          @if (couponDiscount > 0) { After ₹{{ couponDiscount | number: '1.0-0' }} coupon discount · }
          Pay manually before the trip
        </small>
      </div>
      <strong>₹{{ remaining | number: '1.0-0' }}</strong>
    </div>
  `,
})
export class BookingPriceSummaryComponent {
  @Input({ required: true }) startDate!: string;
  @Input({ required: true }) endDate!: string;
  @Input({ required: true }) pricePerPerson = 0;
  @Input({ required: true }) seats = 1;
  @Input() couponDiscount = 0;

  readonly perSeatAdvance = PER_SEAT_ADVANCE;

  get rooms(): number {
    return roomsForSeats(this.seats);
  }

  get payNow(): number {
    return payNowFor(this.pricePerPerson, this.seats, this.couponDiscount);
  }

  get remaining(): number {
    return Math.max(0, this.pricePerPerson * this.seats - this.couponDiscount - this.payNow);
  }
}
