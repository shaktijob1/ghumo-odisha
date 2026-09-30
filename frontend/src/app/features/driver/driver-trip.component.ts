import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CarBookingStatus, CarBookingStatusLabels, CarPaymentStatus, carBookingBadgeClass } from '../../core/models/car.model';
import { DriverBooking, EndTripRequest, TripFarePreview } from '../../core/models/driver.model';
import { DriverService } from '../../core/services/driver.service';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { directionsLink } from '../../shared/utils/maps-link';
import { durationLabel, istDate, istDateTime, istTime } from '../../shared/utils/car-format';

/** Best-effort GPS: resolves null if the phone says no, has no fix in time, or has no GPS. */
function currentPosition(timeoutMs = 8000): Promise<{ latitude: number; longitude: number } | null> {
  if (!('geolocation' in navigator)) return Promise.resolve(null);
  return new Promise((resolve) =>
    navigator.geolocation.getCurrentPosition(
      (p) => resolve({ latitude: +p.coords.latitude.toFixed(6), longitude: +p.coords.longitude.toFixed(6) }),
      () => resolve(null),
      { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 60_000 },
    ),
  );
}

/**
 * /driver/bookings/:id — the driver's trip screen, made for one hand on a phone:
 * Start Trip (odometer) → TRIP IN PROGRESS → End Trip (odometer, night halts, tolls) → the server's
 * final fare → Complete Trip → Mark balance collected. Nothing is priced on the phone.
 */
@Component({
  selector: 'app-driver-trip',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  template: `
    <a class="cz-back" routerLink="/driver/bookings">← Trips</a>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <div class="card pad cz-empty"><p>{{ error() }}</p><button type="button" class="btn ghost sm" (click)="load()">Try again</button></div> }
      @default {
        @if (data(); as d) {
          @let b = d.booking;
          <div class="cz-cardhead" style="margin-bottom:12px">
            <div><h1 class="dv-h1" style="margin:0">{{ date(b.pickupAt) }}</h1><span class="mut">{{ b.reference }} · {{ b.carDisplayName }}</span></div>
            <span class="badge" [class]="badge()">{{ statusLabel() }}</span>
          </div>

          <!-- Running trip banner -->
          @if (b.status === Status.InProgress && b.trip; as t) {
            <section class="dv-running">
              <b>TRIP IN PROGRESS</b>
              <div class="dv-running-grid">
                <div><span>Start</span><strong>{{ dateTime(t.startedAt) }}</strong></div>
                <div><span>Start KM</span><strong>{{ t.startOdometerKm | number }} KM</strong></div>
              </div>
            </section>
          }

          <!-- Customer & pickup -->
          <section class="card pad">
            <div class="cz-cardhead"><h3 class="cz-h3" style="margin:0">{{ d.customerName }}</h3>
              @if (d.customerPhone) {
                <div class="row"><a class="btn sm" [href]="'tel:+91' + d.customerPhone">Call</a>
                  <a class="btn ghost sm" [href]="'https://wa.me/91' + d.customerPhone" target="_blank" rel="noopener">WhatsApp</a></div>
              }
            </div>
            <div class="kv" style="margin-top:10px">
              <span class="k">Pickup</span><b>{{ time(b.pickupAt) }}, {{ b.pickupCity }}@if (b.pickupAddress) { — {{ b.pickupAddress }} }</b>
              @if (b.pickupLocation) {
                <span class="k">Pickup on map</span>
                <span>{{ b.pickupLocation }}@if (b.pickupPoint) { · <a target="_blank" rel="noopener" [href]="directions(b.pickupPoint)" style="color:var(--accent);font-weight:600">Directions ↗</a> }</span>
              }
              @if (b.dropLocation) { <span class="k">Where to</span><b>{{ b.dropLocation }} · {{ b.roundTrip ? 'round trip' : 'one way' }}</b> }
              <span class="k">Duration</span><b>{{ duration(b.durationHours) }} (till {{ dateTime(b.endsAt) }})</b>
              <span class="k">Estimated</span><b>{{ b.estimate.km | number }} km · ₹{{ b.estimate.total | number: '1.0-0' }}</b>
              <span class="k">Paid online</span><b>₹{{ b.bookingAmount | number: '1.0-0' }}</b>
              @if (b.customerNotes) { <span class="k">Note</span><b>{{ b.customerNotes }}</b> }
            </div>
          </section>

          <!-- START -->
          @if (b.status === Status.Confirmed) {
            <section class="card pad dv-action">
              <h3 class="cz-h3">Start trip</h3>
              <label class="lbl" for="t-start">Odometer now (KM)</label>
              <input id="t-start" class="inp dv-big" type="number" inputmode="numeric" min="0" placeholder="e.g. 24520" [(ngModel)]="startKm" name="startKm" />
              @if (d.lastEndOdometerKm !== null) { <p class="note">This car's last trip ended at {{ d.lastEndOdometerKm | number }} KM.</p> }
              <label class="dv-check"><input type="checkbox" [(ngModel)]="useGps" name="gps" /> Save my location</label>
              @if (actionError()) { <div class="errorbox">{{ actionError() }}</div> }
              <button type="button" class="btn block dv-bigbtn" [disabled]="busy() || startKm === null || startKm < 0" (click)="start()">
                @if (busy()) { <span class="spin"></span> } @else { Start Trip }
              </button>
            </section>
            <section class="card pad">
              @if (cancelling()) {
                <label class="lbl" for="t-cancel">Why are you cancelling? The customer will see this.</label>
                <textarea id="t-cancel" class="inp" rows="2" maxlength="500" [(ngModel)]="cancelReason" name="cancelReason"></textarea>
                @if (cancelError()) { <div class="errorbox" style="margin-top:10px">{{ cancelError() }}</div> }
                <div class="row" style="margin-top:10px">
                  <button type="button" class="btn dang sm" [disabled]="busy() || !cancelReason.trim()" (click)="cancel()">Cancel booking</button>
                  <button type="button" class="btn ghost sm" (click)="cancelling.set(false)">Keep it</button>
                </div>
              } @else {
                <button type="button" class="linkbtn" style="padding:0;color:var(--danger)" (click)="cancelling.set(true)">Can't do this trip? Cancel booking</button>
              }
            </section>
          }

          <!-- END -->
          @if (b.status === Status.InProgress) {
            <section class="card pad dv-action">
              <h3 class="cz-h3">End trip</h3>
              <label class="lbl" for="t-end">Odometer now (KM)</label>
              <input id="t-end" class="inp dv-big" type="number" inputmode="numeric" [min]="b.trip?.startOdometerKm ?? 0" placeholder="End KM" [(ngModel)]="end.endOdometerKm" name="endKm" (ngModelChange)="preview.set(null)" />
              <div class="f2 dv-f2" style="margin-top:12px">
                <div class="fld">
                  <span class="lbl">Night halts</span>
                  <div class="dv-stepper">
                    <button type="button" (click)="stepNights(-1)" [disabled]="end.nightHalts <= 0" aria-label="Fewer nights">−</button>
                    <b>{{ end.nightHalts }}</b>
                    <button type="button" (click)="stepNights(1)" aria-label="More nights">+</button>
                  </div>
                  <span class="note">Only nights you stayed out with the car.</span>
                </div>
                <div class="fld">
                  <label class="lbl" for="t-extra">Tolls / parking (₹)</label>
                  <input id="t-extra" class="inp" type="number" inputmode="numeric" min="0" [(ngModel)]="end.additionalCharges" name="extra" (ngModelChange)="preview.set(null)" />
                </div>
              </div>
              @if (end.additionalCharges > 0) {
                <div class="fld"><label class="lbl" for="t-extra-note">What were they for?</label>
                  <input id="t-extra-note" class="inp" maxlength="300" placeholder="e.g. 2 tolls + parking" [(ngModel)]="end.additionalChargesNote" name="extraNote" (ngModelChange)="preview.set(null)" /></div>
              }
              @if (actionError()) { <div class="errorbox">{{ actionError() }}</div> }

              @if (preview(); as p) {
                <div class="dv-fare">
                  <div class="dv-fare-km"><span>Actual distance</span><strong>{{ p.actualKm | number }} KM</strong><small>{{ p.startOdometerKm | number }} → {{ p.endOdometerKm | number }}</small></div>
                  <table class="cz-table cz-fare-table">
                    <thead><tr><th></th><th>Estimate</th><th>Final</th></tr></thead>
                    <tbody>
                      <tr><td>Distance</td><td>{{ p.estimate.km | number }} km</td><td>{{ p.final.km | number }} km</td></tr>
                      <tr><td>Base fare</td><td>₹{{ p.estimate.baseFare | number: '1.0-0' }}</td><td>₹{{ p.final.baseFare | number: '1.0-0' }}</td></tr>
                      <tr><td>Km charge</td><td>₹{{ p.estimate.kmCharge | number: '1.0-0' }}</td><td>₹{{ p.final.kmCharge | number: '1.0-0' }}</td></tr>
                      <tr><td>Night halt</td><td>₹{{ p.estimate.nightHaltCharge | number: '1.0-0' }}</td><td>₹{{ p.final.nightHaltCharge | number: '1.0-0' }}</td></tr>
                      @if (p.final.additionalCharges > 0) { <tr><td>Tolls / parking</td><td>—</td><td>₹{{ p.final.additionalCharges | number: '1.0-0' }}</td></tr> }
                      <tr class="tot"><td>Total</td><td>₹{{ p.estimate.total | number: '1.0-0' }}</td><td>₹{{ p.final.total | number: '1.0-0' }}</td></tr>
                    </tbody>
                  </table>
                  <div class="sumrow"><span>Paid online</span><b>− ₹{{ p.bookingAmountPaid | number: '1.0-0' }}</b></div>
                  <div class="sumrow total dv-collect"><span>Collect from customer</span><b>₹{{ p.balanceDue | number: '1.0-0' }}</b></div>
                </div>
                <button type="button" class="btn block dv-bigbtn" [disabled]="busy()" (click)="complete()">
                  @if (busy()) { <span class="spin"></span> } @else { Complete Trip }
                </button>
              } @else {
                <button type="button" class="btn block dv-bigbtn" [disabled]="busy() || end.endOdometerKm === null" (click)="calculate()">
                  @if (busy()) { <span class="spin"></span> } @else { End Trip · See final fare }
                </button>
              }
            </section>
          }

          <!-- DONE -->
          @if (b.status === Status.Completed && b.trip; as t) {
            <section class="card pad dv-action">
              <h3 class="cz-h3">Trip completed</h3>
              <div class="kv">
                <span class="k">Start</span><b>{{ dateTime(t.startedAt) }} · {{ t.startOdometerKm | number }} KM</b>
                <span class="k">End</span><b>{{ dateTime(t.completedAt ?? t.endedAt ?? '') }} · {{ t.endOdometerKm | number }} KM</b>
                <span class="k">Actual distance</span><b>{{ t.actualKm | number }} KM</b>
                @if (t.nightHalts) { <span class="k">Night halts</span><b>{{ t.nightHalts }}</b> }
              </div>
              @if (b.final; as f) {
                <div class="hr"></div>
                <div class="sumrow"><span>Final fare</span><b>₹{{ f.total | number: '1.0-0' }}</b></div>
                <div class="sumrow"><span>Paid online</span><b>− ₹{{ b.bookingAmount | number: '1.0-0' }}</b></div>
                <div class="sumrow total dv-collect"><span>{{ b.paymentStatus === Payment.BalanceCollected ? 'Collected' : 'Collect from customer' }}</span><b>₹{{ b.remainingAmount | number: '1.0-0' }}</b></div>
              }
              @if (b.paymentStatus !== Payment.BalanceCollected) {
                @if (actionError()) { <div class="errorbox">{{ actionError() }}</div> }
                <button type="button" class="btn block dv-bigbtn" [disabled]="busy()" (click)="collected()">
                  @if (busy()) { <span class="spin"></span> } @else { I've collected ₹{{ b.remainingAmount | number: '1.0-0' }} }
                </button>
              } @else {
                <p class="note">Balance collected {{ b.balanceCollectedAt ? dateTime(b.balanceCollectedAt) : '' }}. Readings can't be changed now — contact Ghumo Odisha if something is wrong.</p>
              }
            </section>
          }

          @if (b.status === Status.Cancelled) {
            <section class="card pad cz-alert"><b>Cancelled</b>@if (b.cancellationReason) { <span>{{ b.cancellationReason }}</span> }</section>
          }
        }
      }
    }
  `,
})
export class DriverTripComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly driverService = inject(DriverService);
  private readonly toast = inject(ToastService);

  readonly Status = CarBookingStatus;
  readonly Payment = CarPaymentStatus;
  private readonly id = Number(this.route.snapshot.paramMap.get('id'));

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly data = signal<DriverBooking | null>(null);
  readonly busy = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly preview = signal<TripFarePreview | null>(null);
  readonly cancelling = signal(false);
  readonly cancelError = signal<string | null>(null);

  startKm: number | null = null;
  useGps = true;
  end: { endOdometerKm: number | null; nightHalts: number; additionalCharges: number; additionalChargesNote: string } = {
    endOdometerKm: null,
    nightHalts: 0,
    additionalCharges: 0,
    additionalChargesNote: '',
  };
  cancelReason = '';

  readonly statusLabel = computed(() => (this.data() ? CarBookingStatusLabels[this.data()!.booking.status] : ''));
  readonly badge = computed(() => (this.data() ? carBookingBadgeClass(this.data()!.booking.status) : ''));
  readonly date = istDate;
  readonly time = istTime;
  readonly directions = directionsLink;
  readonly dateTime = istDateTime;
  readonly duration = durationLabel;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.driverService.booking(this.id).subscribe({
      next: (d) => {
        this.set(d);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load this trip.'));
        this.state.set('error');
      },
    });
  }

  async start(): Promise<void> {
    if (this.startKm === null) return;
    this.busy.set(true);
    this.actionError.set(null);
    const gps = this.useGps ? await currentPosition() : null;
    this.driverService
      .startTrip(this.id, { startOdometerKm: Math.floor(this.startKm), latitude: gps?.latitude ?? null, longitude: gps?.longitude ?? null })
      .subscribe({
        next: (d) => {
          this.busy.set(false);
          this.set(d);
          this.toast.success('Trip started. Drive safe!');
          window.scrollTo({ top: 0 });
        },
        error: (e: unknown) => this.fail(e),
      });
  }

  stepNights(delta: number): void {
    this.end.nightHalts = Math.max(0, this.end.nightHalts + delta);
    this.preview.set(null);
  }

  calculate(): void {
    this.busy.set(true);
    this.actionError.set(null);
    this.driverService.previewEnd(this.id, this.endRequest(null)).subscribe({
      next: (p) => {
        this.busy.set(false);
        this.preview.set(p);
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  async complete(): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    const gps = this.useGps ? await currentPosition() : null;
    this.driverService.completeTrip(this.id, this.endRequest(gps)).subscribe({
      next: (d) => {
        this.busy.set(false);
        this.preview.set(null);
        this.set(d);
        this.toast.success('Trip completed. The customer can see the final fare.');
        window.scrollTo({ top: 0 });
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  collected(): void {
    this.busy.set(true);
    this.actionError.set(null);
    this.driverService.markBalanceCollected(this.id).subscribe({
      next: (d) => {
        this.busy.set(false);
        this.set(d);
        this.toast.success('Marked as collected.');
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  cancel(): void {
    this.busy.set(true);
    this.cancelError.set(null);
    this.driverService.cancelBooking(this.id, this.cancelReason.trim()).subscribe({
      next: (d) => {
        this.busy.set(false);
        this.cancelling.set(false);
        this.set(d);
        this.toast.success('Booking cancelled.');
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.cancelError.set(apiErrorMessage(e));
      },
    });
  }

  private endRequest(gps: { latitude: number; longitude: number } | null): EndTripRequest {
    return {
      endOdometerKm: Math.floor(this.end.endOdometerKm ?? 0),
      nightHalts: this.end.nightHalts,
      additionalCharges: Number(this.end.additionalCharges) || 0,
      additionalChargesNote: this.end.additionalChargesNote.trim() || null,
      latitude: gps?.latitude ?? null,
      longitude: gps?.longitude ?? null,
    };
  }

  private set(d: DriverBooking): void {
    this.data.set(d);
  }

  private fail(e: unknown): void {
    this.busy.set(false);
    this.actionError.set(apiErrorMessage(e));
  }
}
