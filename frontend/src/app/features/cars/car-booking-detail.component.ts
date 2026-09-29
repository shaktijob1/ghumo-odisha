import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CarBooking,
  CarBookingStatus,
  CarBookingStatusLabels,
  CarPaymentOrder,
  CarPaymentStatus,
  CarPaymentStatusLabels,
  FuelTypeLabels,
  carBookingBadgeClass,
} from '../../core/models/car.model';
import { CarCheckoutService } from '../../core/services/car-checkout.service';
import { CarService } from '../../core/services/car.service';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { durationLabel, istDate, istDateTime, istTime } from '../../shared/utils/car-format';

interface Step {
  label: string;
  detail: string | null;
  done: boolean;
  current: boolean;
}

/**
 * /my-bookings/cars/:id — the customer's car booking: live trip progress (updated by the driver's
 * Start / End Trip), estimate vs final fare, payment and refund status, and cancel / pay. Read-only
 * for trip readings — only the driver records them.
 */
@Component({
  selector: 'app-car-booking-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ImageUrlPipe, StatePanelComponent],
  template: `
    <div class="container cz-page">
      <a class="cz-back" routerLink="/my-bookings">← My bookings</a>

      @switch (state()) {
        @case ('loading') { <app-state-panel kind="loading" message="Loading booking…"></app-state-panel> }
        @case ('error') {
          <div class="card pad cz-empty">
            <p>{{ error() }}</p>
            <button type="button" class="btn ghost sm" (click)="load()">Try again</button>
          </div>
        }
        @default {
          @if (booking(); as b) {
            <div class="cz-bkhead">
              <div>
                <h1 class="cz-pagetitle">{{ b.carDisplayName }}</h1>
                <span class="mut">Booking {{ b.reference }}</span>
              </div>
              <span class="badge" [class]="badge()">{{ statusLabel() }}</span>
            </div>

            @if (b.status !== Status.Cancelled && b.status !== Status.Expired && b.status !== Status.PendingPayment) {
              <ol class="cz-steps card pad" aria-label="Trip progress">
                @for (st of steps(); track st.label) {
                  <li [class.done]="st.done" [class.current]="st.current">
                    <span class="dot" aria-hidden="true"></span>
                    <div><b>{{ st.label }}</b>@if (st.detail) { <span class="mut">{{ st.detail }}</span> }</div>
                  </li>
                }
              </ol>
            }

            <div class="cz-detail">
              <div class="cz-main">
                @if (b.status === Status.Cancelled) {
                  <div class="card pad cz-alert">
                    <b>Cancelled @if (b.cancelledBy === 'Driver') { by the driver } @else if (b.cancelledBy === 'Admin') { by Ghumo Odisha }</b>
                    @if (b.cancellationReason) { <p class="mut">{{ b.cancellationReason }}</p> }
                    @if (b.refund; as r) {
                      <p><b>{{ refundText() }}</b></p>
                    }
                  </div>
                }

                <div class="card pad">
                  <h3 class="cz-h3">Trip</h3>
                  <div class="kv">
                    <span class="k">Car</span><b>{{ b.carDisplayName }} · {{ b.category }} · {{ fuel() }} · {{ b.hasAc ? 'AC' : 'Non-AC' }}</b>
                    @if (b.registrationNumber) { <span class="k">Number plate</span><b>{{ b.registrationNumber }}</b> }
                    <span class="k">Pickup</span><b>{{ b.pickupCity }}@if (b.pickupAddress) { — {{ b.pickupAddress }} }</b>
                    <span class="k">Date</span><b>{{ date(b.pickupAt) }}</b>
                    <span class="k">Time</span><b>{{ time(b.pickupAt) }}</b>
                    <span class="k">Duration</span><b>{{ duration(b.durationHours) }}</b>
                    <span class="k">Estimated distance</span><b>{{ b.estimate.km | number }} km</b>
                  </div>
                </div>

                @if (b.trip; as t) {
                  <div class="card pad">
                    <h3 class="cz-h3">On the road</h3>
                    <div class="cz-odo">
                      <div><span class="k">Started</span><b>{{ dateTime(t.startedAt) }}</b><span class="mut">Start KM {{ t.startOdometerKm | number }}</span></div>
                      @if (t.completedAt) {
                        <div><span class="k">Completed</span><b>{{ dateTime(t.completedAt) }}</b><span class="mut">End KM {{ t.endOdometerKm | number }}</span></div>
                        <div><span class="k">Actual distance</span><b>{{ t.actualKm | number }} km</b>@if (t.nightHalts) { <span class="mut">{{ t.nightHalts }} night halt{{ t.nightHalts > 1 ? 's' : '' }}</span> }</div>
                      } @else {
                        <div><span class="k">Status</span><b>Trip in progress</b></div>
                      }
                    </div>
                  </div>
                }

                <div class="card pad">
                  <h3 class="cz-h3">Fare</h3>
                  <table class="cz-table cz-fare-table">
                    <thead><tr><th></th><th>Estimate</th>@if (b.final) { <th>Final</th> }</tr></thead>
                    <tbody>
                      <tr><td>Distance</td><td>{{ b.estimate.km | number }} km</td>@if (b.final; as f) { <td>{{ f.km | number }} km</td> }</tr>
                      <tr><td>Base fare</td><td>₹{{ b.estimate.baseFare | number: '1.0-0' }}</td>@if (b.final; as f) { <td>₹{{ f.baseFare | number: '1.0-0' }}</td> }</tr>
                      <tr><td>Km charge (₹{{ b.pricePerKm | number: '1.0-2' }}/km)</td><td>₹{{ b.estimate.kmCharge | number: '1.0-0' }}</td>@if (b.final; as f) { <td>₹{{ f.kmCharge | number: '1.0-0' }}</td> }</tr>
                      <tr><td>Night halt</td><td>₹{{ b.estimate.nightHaltCharge | number: '1.0-0' }}</td>@if (b.final; as f) { <td>₹{{ f.nightHaltCharge | number: '1.0-0' }}</td> }</tr>
                      @if (b.final; as f) {
                        @if (f.additionalCharges > 0) {
                          <tr><td>{{ f.additionalChargesNote || 'Other charges' }}</td><td>—</td><td>₹{{ f.additionalCharges | number: '1.0-0' }}</td></tr>
                        }
                      }
                      <tr class="tot"><td>Total</td><td>₹{{ b.estimate.total | number: '1.0-0' }}</td>@if (b.final; as f) { <td>₹{{ f.total | number: '1.0-0' }}</td> }</tr>
                    </tbody>
                  </table>
                  <div class="hr"></div>
                  <div class="sumrow"><span>Booking amount</span><b>₹{{ b.bookingAmount | number: '1.0-0' }} · {{ bookingAmountState() }}</b></div>
                  @if (b.status !== Status.Cancelled && b.status !== Status.Expired) {
                    <div class="sumrow total">
                      <span>{{ b.paymentStatus === Payment.BalanceCollected ? 'Paid to driver' : b.final ? 'Balance to pay the driver' : 'Estimated balance after the trip' }}</span>
                      <b>₹{{ b.remainingAmount | number: '1.0-0' }}</b>
                    </div>
                  }
                  <p class="note">Trip readings are recorded by the driver from the odometer. Questions about the fare? Contact us with your booking number.</p>
                </div>

                @if (b.timeline.length > 0) {
                  <div class="card pad">
                    <h3 class="cz-h3">Updates</h3>
                    <ul class="cz-timeline">
                      @for (e of reversedTimeline(); track $index) {
                        <li><b>{{ e.title }}</b>@if (e.note) { <span class="mut">{{ e.note }}</span> }<small class="mut">{{ dateTime(e.createdAt) }}</small></li>
                      }
                    </ul>
                  </div>
                }
              </div>

              <aside class="cz-side">
                @if (b.driver; as d) {
                  <div class="card pad cz-driver">
                    @if (d.profilePhotoUrl) { <img [src]="d.profilePhotoUrl | imageUrl" alt="" /> }
                    <div>
                      <span class="k">Your driver</span>
                      <b>{{ d.name }}</b>
                      @if (d.phoneNumber && (b.status === Status.Confirmed || b.status === Status.InProgress)) {
                        <div class="row" style="margin-top:8px">
                          <a class="btn sm" [href]="'tel:+91' + d.phoneNumber">Call</a>
                          <a class="btn ghost sm" [href]="'https://wa.me/91' + d.phoneNumber" target="_blank" rel="noopener">WhatsApp</a>
                        </div>
                      }
                    </div>
                  </div>
                }

                @if (b.canPay) {
                  <div class="card pad">
                    <h3 class="cz-h3">Complete your booking</h3>
                    <p class="note">Pay ₹{{ b.bookingAmount | number: '1.0-0' }} to confirm. If the car has been taken meanwhile, you'll be told before paying.</p>
                    @if (payError(); as e) { <div class="errorbox" style="margin-top:10px">{{ e }}</div> }
                    @if (order()) {
                      <button type="button" class="btn block" style="margin-top:10px" [disabled]="working()" (click)="pay()">
                        @if (working()) { <span class="spin"></span> } @else { Pay ₹{{ b.bookingAmount | number: '1.0-0' }} & Confirm }
                      </button>
                    } @else {
                      <button type="button" class="btn block" style="margin-top:10px" [disabled]="working()" (click)="prepare()">
                        @if (working()) { <span class="spin"></span> } @else { Continue to payment }
                      </button>
                    }
                  </div>
                }

                @if (b.canCancel) {
                  <div class="card pad">
                    @if (confirmingCancel()) {
                      <b>Cancel this booking?</b>
                      @if (b.paymentStatus === Payment.BookingAmountPaid) {
                        <p class="note" style="margin:6px 0 10px">Your ₹{{ b.bookingAmount | number: '1.0-0' }} booking amount goes to our team for refund.</p>
                      }
                      <textarea class="inp" rows="2" maxlength="500" placeholder="Reason (optional)" [(ngModel)]="cancelReason"></textarea>
                      @if (cancelError(); as e) { <div class="errorbox" style="margin-top:10px">{{ e }}</div> }
                      <div class="row" style="margin-top:10px">
                        <button type="button" class="btn dang sm" [disabled]="working()" (click)="cancel()">
                          @if (working()) { <span class="spin"></span> } @else { Yes, cancel }
                        </button>
                        <button type="button" class="btn ghost sm" (click)="confirmingCancel.set(false)">Keep booking</button>
                      </div>
                    } @else {
                      <button type="button" class="btn dang block" (click)="confirmingCancel.set(true)">Cancel booking</button>
                    }
                  </div>
                }
              </aside>
            </div>
          }
        }
      }
    </div>
  `,
})
export class CarBookingDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly carService = inject(CarService);
  private readonly checkout = inject(CarCheckoutService);
  private readonly toast = inject(ToastService);

  readonly Status = CarBookingStatus;
  readonly Payment = CarPaymentStatus;

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly booking = signal<CarBooking | null>(null);
  readonly order = signal<CarPaymentOrder | null>(null);
  readonly working = signal(false);
  readonly payError = signal<string | null>(null);
  readonly confirmingCancel = signal(false);
  readonly cancelError = signal<string | null>(null);
  cancelReason = '';

  private readonly id = Number(this.route.snapshot.paramMap.get('id'));
  private poll: ReturnType<typeof setInterval> | undefined;

  readonly fuel = computed(() => (this.booking() ? FuelTypeLabels[this.booking()!.fuelType] : ''));
  readonly badge = computed(() => (this.booking() ? carBookingBadgeClass(this.booking()!.status) : ''));
  readonly statusLabel = computed(() => (this.booking() ? CarBookingStatusLabels[this.booking()!.status] : ''));
  /** Short state for the "Booking amount" row: Paid / Not paid / refund wording. */
  readonly bookingAmountState = computed(() => {
    const s = this.booking()?.paymentStatus;
    if (s === undefined) return '';
    return s === CarPaymentStatus.BookingAmountPaid || s === CarPaymentStatus.BalanceCollected ? 'Paid' : CarPaymentStatusLabels[s];
  });
  readonly reversedTimeline = computed(() => [...(this.booking()?.timeline ?? [])].reverse());

  readonly refundText = computed(() => {
    const r = this.booking()?.refund;
    if (!r) return '';
    const amount = `₹${r.amount.toLocaleString('en-IN')}`;
    switch (r.status) {
      case CarPaymentStatus.RefundPending:
        return `Refund of ${amount} requested — our team will process it shortly.`;
      case CarPaymentStatus.RefundProcessing:
        return `Refund of ${amount} initiated${r.issuedAt ? ` on ${istDate(r.issuedAt)}` : ''}. It can take 5–7 working days to reach you.`;
      default:
        return `${amount} refunded.`;
    }
  });

  readonly steps = computed<Step[]>(() => {
    const b = this.booking();
    if (!b) return [];
    const started = !!b.trip;
    const completed = b.status === CarBookingStatus.Completed;
    return [
      { label: 'Booking confirmed', detail: b.confirmedAt ? istDateTime(b.confirmedAt) : null, done: true, current: false },
      { label: 'Driver assigned', detail: b.driver?.name ?? null, done: true, current: !started },
      {
        label: started ? 'Trip started' : 'Trip not started',
        detail: started ? `${istDateTime(b.trip!.startedAt)} · Start KM ${b.trip!.startOdometerKm.toLocaleString('en-IN')}` : `Pickup ${istDateTime(b.pickupAt)}`,
        done: started,
        current: started && !completed,
      },
      {
        label: 'Trip completed',
        detail: completed && b.trip?.completedAt ? `${istDateTime(b.trip.completedAt)} · ${b.trip.actualKm?.toLocaleString('en-IN')} km · Final fare ₹${b.final?.total.toLocaleString('en-IN')}` : null,
        done: completed,
        current: completed,
      },
    ];
  });

  readonly date = istDate;
  readonly time = istTime;
  readonly dateTime = istDateTime;
  readonly duration = durationLabel;

  ngOnInit(): void {
    this.load();
    this.checkout.warmUp();
    // The driver's Start / End Trip should show up without a manual refresh.
    this.poll = setInterval(() => {
      const s = this.booking()?.status;
      if (document.visibilityState === 'visible' && (s === CarBookingStatus.Confirmed || s === CarBookingStatus.InProgress)) this.load(true);
    }, 30_000);
  }

  ngOnDestroy(): void {
    clearInterval(this.poll);
  }

  load(silent = false): void {
    if (!silent) this.state.set('loading');
    this.carService.myBooking(this.id).subscribe({
      next: (b) => {
        this.booking.set(b);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        if (silent) return;
        this.error.set(apiErrorMessage(e, 'Could not load this booking.'));
        this.state.set('error');
      },
    });
  }

  prepare(): void {
    const b = this.booking();
    if (!b) return;
    this.working.set(true);
    this.payError.set(null);
    this.checkout.createOrder(b.carBookingId).subscribe({
      next: (o) => {
        this.order.set(o);
        this.working.set(false);
      },
      error: (e: unknown) => {
        this.working.set(false);
        this.payError.set(apiErrorMessage(e, 'Could not start payment. Please try again.'));
      },
    });
  }

  pay(): void {
    const b = this.booking();
    const o = this.order();
    if (!b || !o || this.working()) return;
    this.working.set(true);
    this.payError.set(null);
    this.checkout.pay(b, o, {
      confirmed: (updated) => {
        this.working.set(false);
        this.order.set(null);
        this.booking.set(updated);
        this.toast.success(`Booking ${updated.reference} confirmed.`);
      },
      failed: (message) => {
        this.working.set(false);
        this.payError.set(message);
        this.load(true);
      },
      dismissed: () => this.working.set(false),
    });
  }

  cancel(): void {
    const b = this.booking();
    if (!b) return;
    this.working.set(true);
    this.cancelError.set(null);
    this.carService.cancel(b.carBookingId, this.cancelReason.trim() || null).subscribe({
      next: (updated) => {
        this.working.set(false);
        this.confirmingCancel.set(false);
        this.booking.set(updated);
        this.toast.success('Booking cancelled.');
      },
      error: (e: unknown) => {
        this.working.set(false);
        this.cancelError.set(apiErrorMessage(e, 'Could not cancel. Please try again.'));
      },
    });
  }
}
