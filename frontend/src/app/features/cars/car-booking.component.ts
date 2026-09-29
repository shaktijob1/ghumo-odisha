import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, map } from 'rxjs';
import { CarBooking, CarFareQuote, CarPaymentOrder, CarPublicDetail, CarWindow, FuelTypeLabels } from '../../core/models/car.model';
import { CarCheckoutService } from '../../core/services/car-checkout.service';
import { CarService } from '../../core/services/car.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { LoginModalService } from '../../core/services/login-modal.service';
import { ToastService } from '../../core/services/toast.service';
import { CarWindowFormComponent } from '../../shared/components/car-window-form.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { defaultWindow, durationLabel, istDateTime, timeLabel, windowDateLabel, windowFromParams, windowToParams } from '../../shared/utils/car-format';

/**
 * /cars/:id/book — the chosen date/time comes from the search (URL) and can be edited right here.
 * Every change asks the server for a fresh quote; nothing is priced in the browser. Paying is two
 * taps: "Continue" creates the booking + payment order, "Pay" opens Razorpay inside the tap.
 */
@Component({
  selector: 'app-car-booking',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ImageUrlPipe, StatePanelComponent, CarWindowFormComponent],
  template: `
    <div class="container cz-page">
      <a class="cz-back" [routerLink]="['/cars', carId()]" [queryParams]="params()">← Back to car</a>

      @switch (state()) {
        @case ('loading') { <app-state-panel kind="loading" message="Loading…"></app-state-panel> }
        @case ('error') {
          <div class="card pad cz-empty">
            <p>{{ loadError() }}</p>
            <a class="btn ghost sm" routerLink="/cars" [queryParams]="params()">See other cars</a>
          </div>
        }
        @default {
          @if (car(); as c) {
            @let s = c.summary;
            <h1 class="cz-pagetitle">Book your car</h1>
            <div class="cz-detail">
              <div class="cz-main">
                <div class="card pad cz-carline">
                  @if (s.coverPhotoUrl) { <img [src]="s.coverPhotoUrl | imageUrl" alt="" /> }
                  <div>
                    <b class="cz-carname">{{ s.displayName }}</b>
                    <span class="mut">{{ s.category }} · {{ fuel() }} · {{ s.hasAc ? 'AC' : 'Non-AC' }} · Driver included</span>
                  </div>
                </div>

                <div class="card pad">
                  <div class="cz-cardhead">
                    <h3 class="cz-h3">Trip details</h3>
                    @if (!editing() && step() === 'details') {
                      <button type="button" class="btn ghost sm" (click)="editing.set(true)">Edit</button>
                    }
                  </div>
                  @if (editing()) {
                    <app-car-window-form [initial]="window()" [compact]="true" submitLabel="Update" (search)="changeWindow($event.window)"></app-car-window-form>
                    <button type="button" class="linkbtn" style="padding:0;margin-top:6px" (click)="editing.set(false)">Cancel</button>
                  } @else {
                    <div class="cz-when">
                      <div><span class="k">Pickup location</span><b>{{ s.baseCity }}</b></div>
                      <div><span class="k">Pickup date</span><b>{{ dateText() }}</b></div>
                      <div><span class="k">Pickup time</span><b>{{ timeText() }}</b></div>
                      <div><span class="k">Duration</span><b>{{ hoursText() }}</b></div>
                    </div>
                  }
                </div>

                <div class="card pad">
                  <h3 class="cz-h3">Distance & pickup</h3>
                  <div class="fld">
                    <label class="lbl" for="cz-km">Approx. distance (km)</label>
                    <input id="cz-km" class="inp" type="number" inputmode="numeric" min="1" placeholder="e.g. 150" [max]="maxKm()" [disabled]="step() === 'pay'"
                      [ngModel]="km()" (ngModelChange)="setKm($event)" />
                    <span class="note">Your best guess for the whole trip, there and back. The final fare uses the actual km on the odometer.</span>
                  </div>
                  <div class="fld">
                    <label class="lbl" for="cz-addr">Pickup address in {{ s.baseCity }}</label>
                    <textarea id="cz-addr" class="inp" rows="2" maxlength="500" placeholder="House / hotel name, street, landmark" [disabled]="step() === 'pay'"
                      [ngModel]="address()" (ngModelChange)="address.set($event); resetPayment()"></textarea>
                  </div>
                  <div class="fld" style="margin-bottom:0">
                    <label class="lbl" for="cz-notes">Anything the driver should know? (optional)</label>
                    <textarea id="cz-notes" class="inp" rows="2" maxlength="1000" [disabled]="step() === 'pay'"
                      [ngModel]="notes()" (ngModelChange)="notes.set($event); resetPayment()"></textarea>
                  </div>
                </div>
              </div>

              <aside class="cz-side">
                <div class="card pad cz-sticky">
                  <h3 class="cz-h3">Pricing</h3>
                  @if (km() === null) {
                    <p class="note cz-askkm">Enter the approximate distance to see your fare.</p>
                    <div class="sumrow"><span>Price per km</span><b>₹{{ s.pricePerKm | number: '1.0-2' }}</b></div>
                    <div class="sumrow"><span>Base fare</span><b>depends on distance</b></div>
                    <div class="sumrow"><span>Night halt</span><b>₹{{ s.nightHaltPrice | number: '1.0-0' }} / night</b></div>
                  } @else if (quoteError(); as e) {
                    <div class="errorbox">{{ e }}</div>
                  } @else if (quote(); as q) {
                    <div class="cz-fare" [class.cz-dim]="quoting()">
                      <div class="sumrow"><span>Base fare</span><b>₹{{ q.baseFare | number: '1.0-0' }}</b></div>
                      <div class="sumrow"><span>{{ q.estimatedKm | number }} km × ₹{{ q.pricePerKm | number: '1.0-2' }}</span><b>₹{{ q.kmCharge | number: '1.0-0' }}</b></div>
                      <div class="sumrow"><span>Night halt @if (q.nights) { ({{ q.nights }} × ₹{{ q.nightHaltPrice | number: '1.0-0' }}) }</span><b>₹{{ q.nightHaltCharge | number: '1.0-0' }}</b></div>
                      <div class="sumrow total"><span>Estimated total</span><b>₹{{ q.estimatedTotal | number: '1.0-0' }}</b></div>
                      <div class="hr"></div>
                      <div class="sumrow cz-pay"><span>Pay now (booking amount)</span><b>₹{{ q.bookingAmount | number: '1.0-0' }}</b></div>
                      <div class="sumrow"><span>Pay the driver after the trip</span><b>₹{{ q.remainingAmount | number: '1.0-0' }}</b></div>
                      @if (q.nights) { <p class="note">Night halt applies only if the driver actually stays out overnight.</p> }
                    </div>
                    @if (!q.isAvailable) {
                      <div class="errorbox" style="margin-top:12px">{{ q.unavailableReason }}</div>
                    }
                  } @else {
                    <app-state-panel kind="loading" message="Calculating fare…"></app-state-panel>
                  }

                  @if (payError(); as e) {
                    <div class="errorbox" style="margin-top:12px">
                      {{ e }}
                      @if (needsPhone()) { <a routerLink="/profile" style="text-decoration:underline;margin-left:4px">Add it in your profile</a> }
                    </div>
                  }

                  <p class="note" style="margin-top:12px">By booking you agree to our <a routerLink="/terms" style="text-decoration:underline">terms</a>. You can cancel before pickup from My Bookings.</p>
                  <div class="cz-paybar">
                    @if (step() === 'details') {
                      <button type="button" class="btn block" [disabled]="!canContinue()" (click)="continueToPay()">
                        @if (working()) { <span class="spin"></span> } @else if (quote(); as q) { Continue · Pay ₹{{ q.bookingAmount | number: '1.0-0' }} } @else { Continue }
                      </button>
                      @if (!auth.isAuthenticated()) { <p class="note" style="text-align:center;margin-top:8px">You'll sign in with WhatsApp or Google first.</p> }
                    } @else if (booking(); as b) {
                      <p class="note">Booking <b>{{ b.reference }}</b> is held for you @if (b.holdExpiresAt) { until {{ holdUntil(b.holdExpiresAt) }} }.</p>
                      <button type="button" class="btn block" [disabled]="working()" (click)="pay()">
                        @if (working()) { <span class="spin"></span> } @else { Pay ₹{{ b.bookingAmount | number: '1.0-0' }} & Confirm }
                      </button>
                      <button type="button" class="linkbtn" style="padding:8px 0 0;display:block;margin:0 auto" [disabled]="working()" (click)="backToDetails()">Change details</button>
                    }
                  </div>
                </div>
              </aside>
            </div>
          }
        }
      }
    </div>
  `,
})
export class CarBookingComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly carService = inject(CarService);
  private readonly checkout = inject(CarCheckoutService);
  private readonly loginModal = inject(LoginModalService);
  private readonly toast = inject(ToastService);
  readonly auth = inject(CustomerAuthService);

  readonly carId = toSignal(this.route.paramMap.pipe(map((p) => Number(p.get('id')))), { requireSync: true });
  readonly window = toSignal(this.route.queryParamMap.pipe(map((p) => windowFromParams(p) ?? defaultWindow())), { requireSync: true });
  readonly params = computed(() => windowToParams(this.window()));

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly loadError = signal('');
  readonly car = signal<CarPublicDetail | null>(null);
  readonly editing = signal(false);
  readonly maxKm = signal(10000);

  readonly km = signal<number | null>(null);
  readonly address = signal('');
  readonly notes = signal('');

  readonly quote = signal<CarFareQuote | null>(null);
  readonly quoting = signal(false);
  readonly quoteError = signal<string | null>(null);

  readonly step = signal<'details' | 'pay'>('details');
  readonly booking = signal<CarBooking | null>(null);
  private order: CarPaymentOrder | null = null;
  private clientRequestId = crypto.randomUUID();
  readonly working = signal(false);
  readonly payError = signal<string | null>(null);
  readonly needsPhone = signal(false);

  readonly fuel = computed(() => (this.car() ? FuelTypeLabels[this.car()!.summary.fuelType] : ''));
  readonly dateText = computed(() => windowDateLabel(this.window().date));
  readonly timeText = computed(() => timeLabel(this.window().time));
  readonly hoursText = computed(() => durationLabel(this.window().hours));
  readonly canContinue = computed(() => !this.working() && !this.quoting() && !this.quoteError() && !!this.quote()?.isAvailable && !this.editing());

  private quoteTimer: ReturnType<typeof setTimeout> | undefined;
  private quoteSub?: Subscription;

  constructor() {
    // Load the car whenever the car or window changes.
    effect(() => {
      const id = this.carId();
      const w = this.window();
      untracked(() => this.loadCar(id, w));
    });
    // Re-quote (debounced) whenever the window or distance changes.
    effect(() => {
      const w = this.window();
      const km = this.km();
      untracked(() => this.scheduleQuote(w, km));
    });
  }

  ngOnInit(): void {
    this.checkout.warmUp();
    this.carService.settings().subscribe({ next: (s) => this.maxKm.set(s.maxEstimatedKm), error: () => undefined });
  }

  ngOnDestroy(): void {
    clearTimeout(this.quoteTimer);
    this.quoteSub?.unsubscribe();
  }

  setKm(value: number | string): void {
    const n = Math.floor(Number(value));
    this.km.set(Number.isFinite(n) && n > 0 ? n : null);
    this.resetPayment();
  }

  changeWindow(window: CarWindow): void {
    this.editing.set(false);
    this.resetPayment();
    this.router.navigate([], { relativeTo: this.route, queryParams: windowToParams(window), replaceUrl: true });
  }

  /** Any change to what's being booked means a fresh booking; the server lets the old unpaid one lapse. */
  resetPayment(): void {
    if (this.step() === 'pay') return;
    this.booking.set(null);
    this.order = null;
    this.payError.set(null);
    this.needsPhone.set(false);
    this.clientRequestId = crypto.randomUUID();
  }

  continueToPay(): void {
    if (!this.canContinue()) return;
    this.payError.set(null);
    this.needsPhone.set(false);

    if (!this.auth.isAuthenticated()) {
      this.loginModal.open();
      return;
    }
    if (!this.auth.currentCustomer()?.phoneNumber) {
      this.needsPhone.set(true);
      this.payError.set('Please add your WhatsApp number so your driver can reach you.');
      return;
    }

    const w = this.window();
    this.working.set(true);
    this.carService
      .createBooking({
        carId: this.carId(),
        pickupDate: w.date,
        pickupTime: w.time,
        durationHours: w.hours,
        estimatedKm: this.km()!,
        pickupAddress: this.address().trim() || null,
        customerNotes: this.notes().trim() || null,
        clientRequestId: this.clientRequestId,
      })
      .subscribe({
        next: (booking) => {
          this.booking.set(booking);
          this.checkout.createOrder(booking.carBookingId).subscribe({
            next: (order) => {
              this.order = order;
              this.working.set(false);
              this.step.set('pay');
            },
            error: (e: unknown) => this.fail(apiErrorMessage(e, 'Could not start payment. Please try again.')),
          });
        },
        error: (e: unknown) => this.fail(apiErrorMessage(e, 'Could not create the booking. Please try again.')),
      });
  }

  /** Runs synchronously inside the tap so Razorpay's overlay can open in-page. */
  pay(): void {
    const booking = this.booking();
    if (!booking || !this.order || this.working()) return;
    this.working.set(true);
    this.payError.set(null);
    this.checkout.pay(booking, this.order, {
      confirmed: (b) => {
        this.working.set(false);
        this.toast.success(`Booking ${b.reference} confirmed. Your driver will contact you before pickup.`);
        this.router.navigate(['/my-bookings/cars', b.carBookingId]);
      },
      failed: (message) => this.fail(message),
      dismissed: () => this.working.set(false),
    });
  }

  backToDetails(): void {
    this.step.set('details');
    this.resetPayment();
  }

  holdUntil(iso: string): string {
    return istDateTime(iso).split(' · ')[1];
  }

  private fail(message: string): void {
    this.working.set(false);
    this.payError.set(message);
  }

  private loadCar(id: number, window: CarWindow): void {
    if (!this.car()) this.state.set('loading');
    this.carService.getCar(id, window).subscribe({
      next: (c) => {
        this.car.set(c);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        if (this.car()) {
          this.quoteError.set(apiErrorMessage(e));
        } else {
          this.loadError.set(apiErrorMessage(e, 'Could not load this car.'));
          this.state.set('error');
        }
      },
    });
  }

  private scheduleQuote(window: CarWindow, km: number | null): void {
    clearTimeout(this.quoteTimer);
    this.quoteSub?.unsubscribe();
    if (km === null) {
      // No distance yet: the pricing card asks for one instead of showing a fare.
      this.quote.set(null);
      this.quoteError.set(null);
      this.quoting.set(false);
      return;
    }
    this.quoting.set(true);
    this.quoteTimer = setTimeout(() => {
      this.quoteSub = this.carService.quote(this.carId(), window, km).subscribe({
        next: (q) => {
          this.quote.set(q);
          this.quoteError.set(null);
          this.quoting.set(false);
        },
        error: (e: unknown) => {
          this.quoteError.set(apiErrorMessage(e, 'Could not calculate the fare. Please try again.'));
          this.quoting.set(false);
        },
      });
    }, 350);
  }
}
