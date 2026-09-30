import { CommonModule } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, map } from 'rxjs';
import { CarBooking, CarFareQuote, CarPaymentOrder, CarPhotoKind, CarPublicDetail, CarSearchResult, FuelTypeLabels } from '../../core/models/car.model';
import { CarCheckoutService } from '../../core/services/car-checkout.service';
import { CarService } from '../../core/services/car.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { LoginModalService } from '../../core/services/login-modal.service';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { VehicleSearchFormComponent } from '../../shared/components/vehicle-search-form.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { durationLabel, istDateTime, timeLabel, windowDateLabel } from '../../shared/utils/car-format';
import { VehicleSearch, searchFromParams, searchToParams, shortPlace } from '../../shared/utils/vehicle-search';

/**
 * /cars/:id/book — confirm and book one vehicle for the search in the URL. Laid out like the trip page:
 * breadcrumb, photos with the name, key facts, then the chosen trip (date, time, duration, pickup and
 * drop) to confirm, and the customer's full pickup address. The bottom bar has Cancel,
 * the fare (tap the arrow for how it's worked out) and Book. The fare is always the server's quote.
 * Paying is two taps: "Book" creates the booking + payment order, the next tap opens Razorpay inside
 * the tap (mobile browsers only show its overlay in-page from a direct tap).
 */
@Component({
  selector: 'app-car-booking',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ImageUrlPipe, StatePanelComponent, VehicleSearchFormComponent],
  template: `
    <div class="container cz-page cb-page">
      <nav class="cb-crumbs" aria-label="Breadcrumb">
        <a routerLink="/">Home</a>
        <span aria-hidden="true">/</span>
        <a routerLink="/cars" [queryParams]="backParams()">Vehicles</a>
        @if (car(); as c) {
          <span aria-hidden="true">/</span>
          <span aria-current="page">{{ c.summary.displayName }}</span>
        }
      </nav>

      @switch (state()) {
        @case ('loading') { <app-state-panel kind="loading" message="Loading…"></app-state-panel> }
        @case ('error') {
          <div class="card pad cz-empty">
            <p>{{ loadError() }}</p>
            <a class="btn ghost sm" routerLink="/cars" [queryParams]="backParams()">See other vehicles</a>
          </div>
        }
        @default {
          @if (car(); as c) {
            @let s = c.summary;

            <!-- 1. Photos with the vehicle's name on the main one — same gallery as the trip page. -->
            @if (photos().length > 0) {
              @let shown = photos().slice(0, 5);
              <section class="tdgallery" [attr.data-count]="shown.length">
                @for (p of shown; track $index; let i = $index; let last = $last) {
                  <button type="button" class="tdg-item" [class.tdg-main]="i === 0" (click)="openPhoto(i)" [attr.aria-label]="'View photo ' + (i + 1)">
                    <img [src]="p | imageUrl" [alt]="s.displayName + ' photo ' + (i + 1)" />
                    @if (i === 0) {
                      <span class="tdg-title">
                        <h1>{{ s.displayName }}</h1>
                        <span>₹{{ s.pricePerKm | number: '1.0-2' }} per km · driver included</span>
                      </span>
                    }
                    @if (last && photos().length > shown.length) {
                      <span class="tdg-more">+{{ photos().length - shown.length }} photos</span>
                    }
                  </button>
                }
              </section>
            }
            <section class="dhero cb-hero" [class.has-gallery]="photos().length > 0" (click)="photos().length && openPhoto(0)">
              @if (photos().length > 0) {
                <img [src]="photos()[0] | imageUrl" [alt]="s.displayName" />
                @if (photos().length > 1) { <span class="cb-hero-n">1 / {{ photos().length }} · View photos</span> }
              }
              <div class="dhero-inner">
                <h1>{{ s.displayName }}</h1>
                <p>₹{{ s.pricePerKm | number: '1.0-2' }} per km · driver included</p>
              </div>
            </section>

            <!-- 2. Key facts, each in its own colour like the trip page's fact tiles. -->
            <div class="cb-chips">
              <span data-tone="0">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 22V5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v17"></path><path d="M3 22h11M14 10h2a2 2 0 0 1 2 2v4a1.5 1.5 0 0 0 3 0V8l-3-3"></path><path d="M6 8h5"></path></svg>
                {{ fuel() }}
              </span>
              <span data-tone="1">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="9" cy="8" r="3.2"></circle><path d="M3.5 20a5.5 5.5 0 0 1 11 0"></path><circle cx="17" cy="9" r="2.4"></circle><path d="M16 14.2a4.5 4.5 0 0 1 5 4.8"></path></svg>
                {{ s.seatCapacity }} Seats
              </span>
              <span data-tone="2">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2v20M4.9 6l14.2 12M4.9 18 19.1 6"></path></svg>
                {{ s.hasAc ? 'AC' : 'Non-AC' }}
              </span>
              <span data-tone="3">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><circle cx="12" cy="12" r="2.5"></circle><path d="M12 3v6.5M4.5 16.5 9.8 13.3M19.5 16.5l-5.3-3.2"></path></svg>
                Driver included
              </span>
              <span data-tone="0">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle></svg>
                Based in {{ s.baseCity }}
              </span>
            </div>

            <!-- 3. The trip to confirm (or, opened without a search, the search form). -->
            <section class="tdsec">
              <div class="tdsec-head">
                <div>
                  <h2><span class="up-g">Your </span><span class="up-o">trip</span></h2>
                  @if (search() && !editing()) { <p>Check the details before you book.</p> }
                </div>
                @if (search() && !editing() && step() === 'details') {
                  <button type="button" class="btn ghost sm" (click)="editing.set(true)">Change</button>
                }
              </div>

              @if (!search() || editing()) {
                <div class="hx-card cb-editcard">
                  <div class="hx-panel">
                    <app-vehicle-search-form [initial]="search()" (search)="changeSearch($event)"></app-vehicle-search-form>
                  </div>
                </div>
                @if (search()) { <button type="button" class="linkbtn" style="padding:0;margin-top:10px" (click)="editing.set(false)">Keep my trip as it was</button> }
              } @else if (search(); as q) {
                <div class="cb-when">
                  <div class="tdfact" data-tone="0">
                    <span class="tdfact-ic" aria-hidden="true">
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2"></rect><path d="M16 2v4M8 2v4M3 10h18"></path></svg>
                    </span>
                    <div><span class="tdfact-l">Pickup day</span><b>{{ dateText() }}</b><small>at {{ timeText() }}</small></div>
                  </div>
                  <div class="tdfact" data-tone="1">
                    <span class="tdfact-ic" aria-hidden="true">
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"></circle><path d="M12 7v5l3 2"></path></svg>
                    </span>
                    <div><span class="tdfact-l">Duration</span><b>{{ hoursText() }}</b>@if (quote(); as fq) { <small>until {{ endText(fq.endsAt) }}</small> }</div>
                  </div>
                </div>

                <ol class="cb-route">
                  <li data-tone="0">
                    <span class="cb-route-l">Pickup</span>
                    <b>{{ shortName(q.pickup.label) }}</b>
                    <small>{{ q.pickup.label }}</small>
                  </li>
                  <li data-tone="1">
                    <span class="cb-route-l">Where to · {{ q.roundTrip ? 'round trip, back to pickup' : 'one way' }}</span>
                    <b>{{ shortName(q.drop.label) }}</b>
                    <small>{{ q.drop.label }}</small>
                  </li>
                </ol>

                @if (quoteError(); as e) {
                  <div class="errorbox" style="margin:14px 0 0">{{ e }}</div>
                } @else if (quote(); as fq) {
                  @if (!fq.isAvailable) { <div class="errorbox" style="margin:14px 0 0">{{ fq.unavailableReason }}</div> }
                }
              }
            </section>

            <!-- 4. Full pickup address -->
            @if (search() && !editing()) {
              <section class="tdsec">
                <div class="tdsec-head">
                  <div>
                    <h2><span class="up-g">Pickup </span><span class="up-o">address</span></h2>
                    <p>Exactly where the driver should come near {{ shortName(search()!.pickup.label) }}.</p>
                  </div>
                </div>
                <div class="fld">
                  <label class="lbl" for="cz-addr">Full address</label>
                  <textarea id="cz-addr" class="inp" rows="3" maxlength="500" placeholder="House / flat no., building, street, landmark" [disabled]="step() === 'pay'"
                    [class.cb-bad]="addressTouched() && !addressOk()"
                    [ngModel]="address()" (ngModelChange)="address.set($event); resetPayment()" (blur)="addressTouched.set(true)"></textarea>
                  @if (addressTouched() && !addressOk()) {
                    <div class="err-msg">Enter your full address — house / flat no., street and a landmark — so the driver can find you.</div>
                  }
                </div>
                <details class="cb-notes">
                  <summary>Anything the driver should know? (optional)</summary>
                  <textarea class="inp" rows="2" maxlength="1000" aria-label="Notes for the driver" [disabled]="step() === 'pay'"
                    [ngModel]="notes()" (ngModelChange)="notes.set($event); resetPayment()"></textarea>
                </details>
              </section>
            }

            <!-- 5. Other vehicles with the same seats -->
            @if (similar().length > 0 && !editing()) {
              <section class="tdsec">
                <div class="tdsec-head">
                  <div>
                    <h2><span class="up-g">Similar </span><span class="up-o">vehicles</span></h2>
                    <p>Other {{ s.seatCapacity }}-seaters for the same time.</p>
                  </div>
                </div>
                <div class="cb-similar">
                  @for (o of similar(); track o.carId) {
                    <a class="cb-sim" [routerLink]="['/cars', o.carId, 'book']" [queryParams]="backParams()">
                      <span class="cb-sim-shot">
                        @if (o.coverPhotoUrl) { <img [src]="o.coverPhotoUrl | imageUrl" [alt]="o.displayName" loading="lazy" /> }
                        @else { <span class="mut">No photo</span> }
                        @if (!o.isAvailable) { <span class="cb-sim-tag">Booked for this time</span> }
                      </span>
                      <span class="cb-sim-body">
                        <b>{{ o.displayName }}</b>
                        <span class="mut">{{ fuelOf(o) }} · {{ o.hasAc ? 'AC' : 'Non-AC' }} · {{ o.baseCity }}</span>
                        <span class="cb-sim-price"><b>₹{{ o.pricePerKm | number: '1.0-2' }}</b>/km <span class="mut">· base from ₹{{ o.baseFareFrom | number: '1.0-0' }}</span></span>
                      </span>
                    </a>
                  }
                </div>
              </section>
            }

            <p class="note cb-terms">
              @if (!auth.isAuthenticated()) { You'll sign in with WhatsApp or Google first. }
              By booking you agree to our <a routerLink="/terms" style="text-decoration:underline">terms</a>. You can cancel before pickup from My Bookings.
            </p>

            <!-- 6. Bottom bar: Cancel · fare (arrow opens the breakdown) · Book -->
            @if (search() && !editing()) {
              <div class="cb-actions">
                @if (payError(); as e) {
                  <div class="errorbox">
                    {{ e }}
                    @if (needsPhone()) { <a routerLink="/profile" style="text-decoration:underline;margin-left:4px">Add it in your profile</a> }
                  </div>
                }
                @if (step() === 'pay') {
                  @if (booking(); as b) {
                    <p class="note">Booking <b>{{ b.reference }}</b> is held for you @if (b.holdExpiresAt) { until {{ holdUntil(b.holdExpiresAt) }} }. Tap Pay to finish,
                      or <button type="button" class="linkbtn" style="padding:0" [disabled]="working()" (click)="backToDetails()">change the details</button>.</p>
                  }
                }

                @if (showBreakdown() && quote(); as fq) {
                  <div class="cb-sheet" id="cb-sheet" role="region" aria-label="How your fare is worked out">
                    <div class="cb-sheet-head">
                      <b>How your fare is worked out</b>
                      <button type="button" class="cb-sheet-x" aria-label="Close" (click)="showBreakdown.set(false)">×</button>
                    </div>
                    <div class="cb-km">
                      <div class="sumrow"><span>Driver to your pickup</span><b>{{ fq.driverApproachKm | number }} km</b></div>
                      <div class="sumrow"><span>Pickup → where to</span><b>{{ fq.pickupToDropKm | number }} km</b></div>
                      @if (fq.roundTrip) {
                        <div class="sumrow"><span>Where to → back to pickup</span><b>{{ fq.dropToPickupKm | number }} km</b></div>
                        <div class="sumrow"><span>Pickup → driver's base</span><b>{{ fq.returnToBaseKm | number }} km</b></div>
                      } @else {
                        <div class="sumrow"><span>Where to → driver's base</span><b>{{ fq.returnToBaseKm | number }} km</b></div>
                      }
                      <div class="sumrow"><span>Total distance</span><b>{{ fq.estimatedKm | number }} km</b></div>
                    </div>
                    <div class="sumrow"><span>Base fare</span><b>₹{{ fq.baseFare | number: '1.0-0' }}</b></div>
                    <div class="sumrow"><span>{{ fq.estimatedKm | number }} km × ₹{{ fq.pricePerKm | number: '1.0-2' }}</span><b>₹{{ fq.kmCharge | number: '1.0-0' }}</b></div>
                    <div class="sumrow"><span>Night halt @if (fq.nights) { ({{ fq.nights }} × ₹{{ fq.nightHaltPrice | number: '1.0-0' }}) }</span><b>₹{{ fq.nightHaltCharge | number: '1.0-0' }}</b></div>
                    <div class="sumrow total"><span>Estimated total</span><b>₹{{ fq.estimatedTotal | number: '1.0-0' }}</b></div>
                    <div class="cb-split">
                      <div><span>Pay now to confirm</span><b>₹{{ fq.bookingAmount | number: '1.0-0' }}</b></div>
                      <div><span>Pay the driver after the trip</span><b>₹{{ fq.remainingAmount | number: '1.0-0' }}</b></div>
                    </div>
                    <p class="note" style="margin-top:8px">Road distances from Google Maps. The final fare uses the actual km on the odometer@if (fq.nights) {; night halt only if the driver stays out overnight}.</p>
                  </div>
                }

                <div class="cb-bar">

                  <button type="button" class="cb-price" [disabled]="!hasFare()" [attr.aria-expanded]="showBreakdown()" aria-controls="cb-sheet" (click)="showBreakdown.set(!showBreakdown())">
                    @if (quoting()) {
                      <span class="spin"></span>
                    } @else if (hasFare()) {
                      <span class="cb-price-amt">₹{{ quote()!.estimatedTotal | number: '1.0-0' }}
                        <svg class="cb-price-chev" [class.up]="showBreakdown()" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m18 15-6-6-6 6"></path></svg>
                      </span>
                      <small>est. total · {{ quote()!.estimatedKm | number }} km</small>
                    } @else {
                      <span class="cb-price-amt">—</span><small>fare unavailable</small>
                    }
                  </button>

                  @if (step() === 'details') {
                    <button type="button" class="btn cb-book" [disabled]="!canContinue()" (click)="continueToPay()">
                      @if (working()) { <span class="spin"></span> } @else { Book }
                    </button>
                  } @else if (booking(); as b) {
                    <button type="button" class="btn cb-book" [disabled]="working()" (click)="pay()">
                      @if (working()) { <span class="spin"></span> } @else { Pay ₹{{ b.bookingAmount | number: '1.0-0' }} }
                    </button>
                  }
                </div>
              </div>
            }

            @if (viewerIndex() !== null) {
              @let vi = viewerIndex()!;
              <div class="modal-backdrop" (click)="viewerIndex.set(null)">
                <div class="modal pvmodal" role="dialog" aria-modal="true" [attr.aria-label]="s.displayName" (click)="$event.stopPropagation()">
                  <div class="pvmodal-shot">
                    <img [src]="photos()[vi] | imageUrl" [alt]="s.displayName + ' photo ' + (vi + 1)" />
                    <button class="modalclose" (click)="viewerIndex.set(null)" aria-label="Close">×</button>
                    @if (photos().length > 1) {
                      <button type="button" class="pvnav prev" aria-label="Previous photo" (click)="stepPhoto(-1)"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m15 18-6-6 6-6"></path></svg></button>
                      <button type="button" class="pvnav next" aria-label="Next photo" (click)="stepPhoto(1)"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 6 6 6-6 6"></path></svg></button>
                    }
                  </div>
                  <div class="pvmodal-bar"><b>{{ s.displayName }}</b><span>{{ vi + 1 }} / {{ photos().length }}</span></div>
                </div>
              </div>
            }
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
  /** The trip being booked, from the URL (null when the page was opened without a search). */
  readonly search = toSignal(this.route.queryParamMap.pipe(map((p) => searchFromParams(p))), { requireSync: true });
  readonly backParams = computed(() => (this.search() ? searchToParams(this.search()!) : {}));

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly loadError = signal('');
  readonly car = signal<CarPublicDetail | null>(null);
  readonly similar = signal<CarSearchResult[]>([]);
  readonly editing = signal(false);
  readonly viewerIndex = signal<number | null>(null);

  readonly address = signal('');
  readonly addressTouched = signal(false);
  readonly addressOk = computed(() => this.address().trim().length >= 10);
  readonly notes = signal('');

  readonly quote = signal<CarFareQuote | null>(null);
  readonly quoting = signal(false);
  readonly quoteError = signal<string | null>(null);
  readonly showBreakdown = signal(false);
  readonly hasFare = computed(() => (this.quote()?.estimatedKm ?? 0) > 0 && !this.quoteError());

  readonly step = signal<'details' | 'pay'>('details');
  readonly booking = signal<CarBooking | null>(null);
  private order: CarPaymentOrder | null = null;
  private clientRequestId = crypto.randomUUID();
  readonly working = signal(false);
  readonly payError = signal<string | null>(null);
  readonly needsPhone = signal(false);

  /** Outside photos first, then inside, each in the order the admin set. */
  readonly photos = computed(() => {
    const all = this.car()?.photos ?? [];
    const byKind = (k: CarPhotoKind) => all.filter((p) => p.kind === k).sort((a, b) => a.displayOrder - b.displayOrder);
    return [...byKind(CarPhotoKind.Exterior), ...byKind(CarPhotoKind.Interior)].map((p) => p.imageUrl);
  });

  readonly fuel = computed(() => (this.car() ? FuelTypeLabels[this.car()!.summary.fuelType] : ''));
  readonly dateText = computed(() => (this.search() ? windowDateLabel(this.search()!.window.date) : ''));
  readonly timeText = computed(() => (this.search() ? timeLabel(this.search()!.window.time) : ''));
  readonly hoursText = computed(() => (this.search() ? durationLabel(this.search()!.window.hours) : ''));
  readonly canContinue = computed(
    () => !this.working() && !this.quoting() && !this.quoteError() && !!this.quote()?.isAvailable && !this.editing() && this.addressOk(),
  );

  readonly shortName = shortPlace;

  private quoteSub?: Subscription;
  private similarSub?: Subscription;
  private loadedCarId: number | null = null;

  constructor() {
    // Load the vehicle whenever it changes (a similar vehicle opens this same page).
    effect(() => {
      const id = this.carId();
      untracked(() => this.loadCar(id));
    });
    // Re-quote whenever the vehicle or the trip changes.
    effect(() => {
      const id = this.carId();
      const s = this.search();
      untracked(() => this.requote(id, s));
    });
  }

  ngOnInit(): void {
    this.checkout.warmUp();
  }

  ngOnDestroy(): void {
    this.quoteSub?.unsubscribe();
    this.similarSub?.unsubscribe();
  }

  fuelOf(c: CarSearchResult): string {
    return FuelTypeLabels[c.fuelType];
  }

  endText(iso: string): string {
    return istDateTime(iso);
  }

  openPhoto(index: number): void {
    this.viewerIndex.set(index);
  }

  stepPhoto(delta: number): void {
    const total = this.photos().length;
    const i = this.viewerIndex();
    if (i === null || total === 0) return;
    this.viewerIndex.set((i + delta + total) % total);
  }

  changeSearch(s: VehicleSearch): void {
    this.editing.set(false);
    this.resetPayment();
    this.router.navigate([], { relativeTo: this.route, queryParams: searchToParams(s), replaceUrl: true });
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
    this.addressTouched.set(true);
    const s = this.search();
    if (!s || !this.canContinue()) {
      if (!this.addressOk()) document.getElementById('cz-addr')?.focus();
      return;
    }
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

    this.working.set(true);
    this.carService
      .createBooking({
        carId: this.carId(),
        pickupDate: s.window.date,
        pickupTime: s.window.time,
        durationHours: s.window.hours,
        pickup: s.pickup,
        drop: s.drop,
        roundTrip: s.roundTrip,
        pickupAddress: this.address().trim(),
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

  @HostListener('document:keydown.escape')
  closeSheet(): void {
    this.showBreakdown.set(false);
  }

  private fail(message: string): void {
    this.working.set(false);
    this.payError.set(message);
  }

  private loadCar(id: number): void {
    const switchingCar = this.loadedCarId !== id;
    if (switchingCar) {
      // Opened a similar vehicle: start its page fresh (the address the customer typed stays).
      this.car.set(null);
      this.similar.set([]);
      this.viewerIndex.set(null);
      this.editing.set(false);
      this.step.set('details');
      this.resetPayment();
      this.state.set('loading');
      if (this.loadedCarId !== null) scrollTo({ top: 0 });
    }
    this.loadedCarId = id;
    this.carService.getCar(id, this.search()?.window ?? null).subscribe({
      next: (c) => {
        if (this.carId() !== id) return;
        this.car.set(c);
        this.state.set('ready');
        this.loadSimilar(c);
      },
      error: (e: unknown) => {
        this.loadError.set(apiErrorMessage(e, 'Could not load this vehicle.'));
        this.state.set('error');
      },
    });
  }

  /** Same seat count, this vehicle left out; available ones first. */
  private loadSimilar(car: CarPublicDetail): void {
    this.similarSub?.unsubscribe();
    const me = car.summary;
    this.similarSub = this.carService.search({ window: this.search()?.window ?? null, seats: me.seatCapacity }).subscribe({
      next: (r) => {
        const others = r.cars
          .filter((o) => o.carId !== me.carId && o.seatCapacity === me.seatCapacity)
          .sort((a, b) => Number(b.isAvailable) - Number(a.isAvailable));
        this.similar.set(others);
      },
      error: () => this.similar.set([]),
    });
  }

  private requote(carId: number, s: VehicleSearch | null): void {
    this.quoteSub?.unsubscribe();
    this.showBreakdown.set(false);
    if (!s) {
      this.quote.set(null);
      this.quoteError.set(null);
      this.quoting.set(false);
      return;
    }
    this.quoting.set(true);
    this.quoteSub = this.carService.quote(carId, s).subscribe({
      next: (q) => {
        this.quote.set(q);
        this.quoteError.set(null);
        this.quoting.set(false);
      },
      error: (e: unknown) => {
        this.quote.set(null);
        this.quoteError.set(apiErrorMessage(e, 'Could not calculate the fare. Please try again.'));
        this.quoting.set(false);
      },
    });
  }
}
