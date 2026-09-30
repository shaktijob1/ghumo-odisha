import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminCarBookingDetail } from '../../../core/models/admin-car.model';
import {
  CarBookingStatus,
  CarBookingStatusLabels,
  CarPaymentStatus,
  CarPaymentStatusLabels,
  FuelTypeLabels,
  carBookingBadgeClass,
} from '../../../core/models/car.model';
import { OfflinePaymentMethods, PaymentMethod, PaymentMethodLabels } from '../../../core/models/enums.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../shared/utils/api-error';
import { directionsLink, kmBreakdown } from '../../../shared/utils/maps-link';
import { durationLabel, istDateTime } from '../../../shared/utils/car-format';
import { AuditHistoryComponent } from './audit-history.component';
import { carPaymentBadgeClass } from './car-booking-list.component';

type LoadState = 'loading' | 'ready' | 'error';
type Dialog = 'cancel' | 'razorpay' | 'manual' | 'settle' | 'fare';

@Component({
  selector: 'app-admin-car-booking-detail',
  standalone: true,
  imports: [DecimalPipe, FormsModule, RouterLink, StatePanelComponent, AuditHistoryComponent],
  template: `
    <a class="aback" routerLink="/admin/car-bookings">← Car bookings</a>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load this booking."></app-state-panel> }
      @case ('ready') {
        @let d = detail()!;
        @let b = d.booking;

        <div class="ahead">
          <div>
            <h2>Car booking {{ b.reference }}</h2>
            <div class="sub">
              <span [class]="'badge ' + statusBadge()">{{ statusLabels[b.status] }}</span>
              <span [class]="'badge ' + paymentBadge()">{{ paymentLabels[b.paymentStatus] }}</span>
              Booked {{ when(b.createdAt) }}
            </div>
          </div>
          <div class="row" style="gap:8px;flex-wrap:wrap">
            @if (canCancel()) { <button class="btn sm dang" (click)="open('cancel')">Cancel booking</button> }
            @if (b.status === CarBookingStatus.Completed && b.trip) { <button class="btn sm ghost" (click)="open('fare')">Correct final fare</button> }
          </div>
        </div>

        @if (b.status === CarBookingStatus.Cancelled) {
          <div class="panel acar-reason">
            <b>Cancelled by {{ (b.cancelledBy || 'unknown').toLowerCase() }}{{ b.cancelledAt ? ' on ' + when(b.cancelledAt) : '' }}.</b>
            {{ b.cancellationReason || '' }}
          </div>
        }

        @if (refundStage(); as stage) {
          <div class="panel acar-refund" style="margin-bottom:14px">
            <div class="row sp" style="align-items:flex-start;gap:12px;flex-wrap:wrap">
              <div>
                <h4>Refund</h4>
                <div class="sub">
                  @switch (stage) {
                    @case ('pending') { ₹{{ b.bookingAmount | number: '1.0-2' }} paid online is waiting to be refunded. }
                    @case ('issued') { Refund issued. Mark it received once the customer has the money. }
                    @case ('done') { Refund complete. }
                  }
                </div>
              </div>
              <div class="row" style="gap:8px;flex-wrap:wrap">
                @if (stage === 'pending') {
                  @if (d.razorpayPaymentId) { <button class="btn sm" (click)="open('razorpay')">Refund via Razorpay</button> }
                  <button class="btn sm ghost" (click)="open('manual')">Record manual refund</button>
                }
                @if (stage === 'issued') { <button class="btn sm" (click)="open('settle')">Mark received</button> }
              </div>
            </div>
            @if (b.refund; as rf) {
              @if (stage !== 'pending') {
                <div class="kv" style="margin-top:10px">
                  <span class="k">Amount</span><b>₹{{ rf.amount | number: '1.0-2' }}</b>
                  <span class="k">Method</span><b>{{ methodLabel(rf.method) }}</b>
                  <span class="k">Reference</span><b class="mono">{{ rf.reference || '—' }}</b>
                  <span class="k">Issued</span><b>{{ rf.issuedAt ? when(rf.issuedAt) : '—' }}</b>
                  <span class="k">Received</span><b>{{ rf.settledAt ? when(rf.settledAt) : '—' }}</b>
                </div>
              }
            }
          </div>
        }

        <div class="panels">
          <div class="panel">
            <h4>Trip</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">Pickup</span><b>{{ when(b.pickupAt) }}</b>
              <span class="k">Duration</span><b>{{ duration(b.durationHours) }} (till {{ when(b.endsAt) }})</b>
              <span class="k">City</span><b>{{ b.pickupCity }}</b>
              <span class="k">Pickup address</span><b>{{ b.pickupAddress || '—' }}</b>
              @if (b.pickupLocation) {
                <span class="k">Pickup on map</span>
                <span>{{ b.pickupLocation }}@if (b.pickupPoint) { · <a class="note" target="_blank" rel="noopener" [href]="directions(b.pickupPoint)">Directions ↗</a> }</span>
              }
              @if (b.dropLocation) { <span class="k">Where to</span><b>{{ b.dropLocation }} · {{ b.roundTrip ? 'round trip' : 'one way' }}</b> }
              @if (kmText(b); as k) { <span class="k">Distance</span><b>{{ k }}</b> }
              <span class="k">Car</span>
              <span><a [routerLink]="['/admin/cars', b.carId]"><b>{{ b.carDisplayName }}</b></a> <span class="note">· {{ b.category }} · {{ fuel[b.fuelType] }}{{ b.hasAc ? ' · AC' : '' }}</span></span>
              <span class="k">Number plate</span><b class="mono">{{ b.registrationNumber || '—' }}</b>
              <span class="k">Driver</span>
              @if (b.driver; as dr) {
                <span><a [routerLink]="['/admin/drivers', dr.driverId]"><b>{{ dr.name }}</b></a>@if (dr.phoneNumber) { <span class="note"> · +91 {{ dr.phoneNumber }}</span> }</span>
              } @else { <b>—</b> }
              <span class="k">Customer note</span><b>{{ b.customerNotes || '—' }}</b>
            </div>
          </div>
          <div class="panel">
            <h4>Customer</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">Name</span><a [routerLink]="['/admin/customers', d.customer.customerId]"><b>{{ d.customer.name }}</b></a>
              <span class="k">WhatsApp</span><b>{{ d.customer.phoneNumber ? '+91 ' + d.customer.phoneNumber : '—' }}</b>
              <span class="k">Email</span><b>{{ d.customer.email || '—' }}</b>
            </div>
            <div class="hr"></div>
            <h4>Online payment</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">Booking amount</span><b>₹{{ b.bookingAmount | number: '1.0-2' }}</b>
              <span class="k">Confirmed</span><b>{{ b.confirmedAt ? when(b.confirmedAt) : '—' }}</b>
              <span class="k">Razorpay order</span><b class="mono">{{ d.razorpayOrderId || '—' }}</b>
              <span class="k">Razorpay payment</span><b class="mono">{{ d.razorpayPaymentId || '—' }}</b>
              @if (d.razorpayRefundId) { <span class="k">Razorpay refund</span><b class="mono">{{ d.razorpayRefundId }}</b> }
            </div>
          </div>
        </div>

        <div class="panel" style="margin-bottom:14px">
          <h4>Fare</h4>
          <div class="sub">₹{{ b.pricePerKm | number: '1.0-2' }}/km · night halt ₹{{ b.nightHaltPrice | number: '1.0-0' }}, as agreed at booking</div>
          <div class="tblwrap" style="margin-top:10px">
            <table class="tbl acar-fare">
              <tr><th></th><th class="num">Estimate</th>@if (b.final) { <th class="num">Final</th> }</tr>
              <tr><td>Distance</td><td class="num">{{ b.estimate.km | number }} km</td>@if (b.final; as f) { <td class="num">{{ f.km | number }} km</td> }</tr>
              <tr><td>Base fare</td><td class="num">₹{{ b.estimate.baseFare | number: '1.0-0' }}</td>@if (b.final; as f) { <td class="num">₹{{ f.baseFare | number: '1.0-0' }}</td> }</tr>
              <tr><td>Km charge</td><td class="num">₹{{ b.estimate.kmCharge | number: '1.0-0' }}</td>@if (b.final; as f) { <td class="num">₹{{ f.kmCharge | number: '1.0-0' }}</td> }</tr>
              <tr>
                <td>Night halt</td>
                <td class="num">₹{{ b.estimate.nightHaltCharge | number: '1.0-0' }} <span class="note">({{ b.estimate.nights }})</span></td>
                @if (b.final; as f) { <td class="num">₹{{ f.nightHaltCharge | number: '1.0-0' }} <span class="note">({{ f.nights }})</span></td> }
              </tr>
              @if (b.final; as f) {
                <tr>
                  <td>Tolls / extras @if (f.additionalChargesNote) { <div class="note">{{ f.additionalChargesNote }}</div> }</td>
                  <td class="num">—</td><td class="num">₹{{ f.additionalCharges | number: '1.0-0' }}</td>
                </tr>
              }
              <tr class="acar-total"><td>Total</td><td class="num">₹{{ b.estimate.total | number: '1.0-0' }}</td>@if (b.final; as f) { <td class="num">₹{{ f.total | number: '1.0-0' }}</td> }</tr>
            </table>
          </div>
          <div class="kv" style="margin-top:12px">
            <span class="k">Paid online</span><b>₹{{ b.bookingAmount | number: '1.0-2' }}</b>
            @if (b.status !== CarBookingStatus.Cancelled && b.status !== CarBookingStatus.Expired) {
              <span class="k">Balance to driver</span>
              <b [style.color]="b.balanceCollectedAt ? 'var(--ok)' : 'var(--wait)'">
                ₹{{ b.remainingAmount | number: '1.0-0' }}{{ b.balanceCollectedAt ? ' · collected ' + when(b.balanceCollectedAt) : (b.status === CarBookingStatus.Completed ? ' · not marked collected yet' : '') }}
              </b>
            }
          </div>
        </div>

        @if (b.trip; as t) {
          <div class="panel" style="margin-bottom:14px">
            <h4>Odometer</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">Started</span><b>{{ when(t.startedAt) }} · {{ t.startOdometerKm | number }} km</b>
              <span class="k">Ended</span><b>{{ t.endedAt ? when(t.endedAt) + ' · ' + (t.endOdometerKm | number) + ' km' : 'Trip running' }}</b>
              <span class="k">Actual distance</span><b>{{ t.actualKm !== null ? (t.actualKm | number) + ' km' : '—' }}</b>
              <span class="k">Night halts</span><b>{{ t.nightHalts ?? '—' }}</b>
              <span class="k">Completed</span><b>{{ t.completedAt ? when(t.completedAt) : '—' }}</b>
            </div>
          </div>
        }

        <div class="panels">
          <div class="panel">
            <h4>Admin notes</h4>
            <div class="sub">Only admins see these.</div>
            <textarea class="inp" rows="4" maxlength="4000" style="margin-top:10px" [(ngModel)]="notes"></textarea>
            @if (notesError()) { <div class="errorbox">{{ notesError() }}</div> }
            <div class="row" style="justify-content:flex-end;margin-top:8px">
              <button class="btn sm" [disabled]="notesBusy() || notes === (d.adminNotes ?? '')" (click)="saveNotes()">
                @if (notesBusy()) { <span class="spin"></span> } @else { Save notes }
              </button>
            </div>
          </div>
          <div class="panel">
            <h4>What the customer sees</h4>
            @if (b.timeline.length === 0) {
              <p class="note" style="margin-top:8px">Nothing yet.</p>
            } @else {
              <ol class="ach" style="margin-top:10px">
                @for (e of b.timeline; track $index) {
                  <li><b>{{ e.title }}</b> <span class="note">· {{ when(e.createdAt) }}</span>@if (e.note) { <div class="note">{{ e.note }}</div> }</li>
                }
              </ol>
            }
          </div>
        </div>

        <div class="panel">
          <h4>Full history</h4>
          <div style="margin-top:10px"><app-audit-history [events]="d.history"></app-audit-history></div>
        </div>
      }
    }

    @if (dialog(); as dlg) {
      @let b = detail()!.booking;
      <div class="modal-backdrop" (click)="close()">
        <div class="modal" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
          @switch (dlg) {
            @case ('cancel') {
              <h4>Cancel booking {{ b.reference }}</h4>
              <p class="note" style="margin-bottom:14px">The car becomes free for this time again. The customer sees your reason.</p>
              @if (paidOnline()) {
                <label class="paycard" [class.on]="!waiveRefund" style="cursor:pointer;margin-bottom:8px">
                  <input type="radio" name="waive" [value]="false" [(ngModel)]="waiveRefund" style="margin-top:3px" />
                  <div style="flex:1">
                    <b style="font-size:14px">Cancel and refund</b>
                    <p class="note" style="margin-top:2px">The ₹{{ b.bookingAmount | number: '1.0-0' }} goes to "Refunds to issue". Refund it from this page.</p>
                  </div>
                </label>
                <label class="paycard" [class.on]="waiveRefund" style="cursor:pointer;margin-bottom:12px">
                  <input type="radio" name="waive" [value]="true" [(ngModel)]="waiveRefund" style="margin-top:3px" />
                  <div style="flex:1">
                    <b style="font-size:14px">Cancel and keep the booking amount</b>
                    <p class="note" style="margin-top:2px">For no-shows or late cancellations. No refund is created.</p>
                  </div>
                </label>
              }
              <div class="fld">
                <label class="lbl" for="cb-reason">Reason (the customer will see this)</label>
                <textarea id="cb-reason" class="inp" rows="3" maxlength="500" [(ngModel)]="reason"></textarea>
              </div>
            }
            @case ('razorpay') {
              <h4>Refund via Razorpay</h4>
              <p class="note" style="margin-bottom:14px">Razorpay returns the money to the customer's original payment method, usually in 5–7 working days. Up to ₹{{ b.bookingAmount | number: '1.0-2' }}.</p>
              <div class="fld">
                <label class="lbl" for="cb-amt">Refund amount (₹)</label>
                <input id="cb-amt" class="inp" type="number" min="1" [max]="b.bookingAmount" [(ngModel)]="amount" />
              </div>
            }
            @case ('manual') {
              <h4>Record manual refund</h4>
              <p class="note" style="margin-bottom:14px">Send the money yourself (UPI, bank transfer or cash), then record it here.</p>
              <div class="f2">
                <div class="fld">
                  <label class="lbl" for="cb-mamt">Amount (₹)</label>
                  <input id="cb-mamt" class="inp" type="number" min="1" [max]="b.bookingAmount" [(ngModel)]="amount" />
                </div>
                <div class="fld">
                  <label class="lbl" for="cb-method">Method</label>
                  <select id="cb-method" class="inp" [(ngModel)]="method">
                    @for (m of offlineMethods; track m) { <option [ngValue]="m">{{ methodLabels[m] }}</option> }
                  </select>
                </div>
              </div>
              <div class="fld">
                <label class="lbl" for="cb-ref">Reference (UTR / receipt, optional)</label>
                <input id="cb-ref" class="inp" maxlength="120" [(ngModel)]="reference" />
              </div>
            }
            @case ('settle') {
              <h4>Mark refund received</h4>
              <p class="note" style="margin-bottom:14px">Only do this once the ₹{{ b.refund?.amount | number: '1.0-2' }} has reached the customer. They'll then see it as refunded.</p>
            }
            @case ('fare') {
              <h4>Correct final fare</h4>
              <p class="note" style="margin-bottom:14px">For a mistyped reading or missed toll. The fare is recalculated at the booking's agreed prices, and the customer sees the correction.</p>
              <div class="f2">
                <div class="fld"><label class="lbl" for="cf-s">Start odometer (km)</label><input id="cf-s" class="inp" type="number" min="0" [(ngModel)]="fare.startOdometerKm" /></div>
                <div class="fld"><label class="lbl" for="cf-e">End odometer (km)</label><input id="cf-e" class="inp" type="number" min="0" [(ngModel)]="fare.endOdometerKm" /></div>
                <div class="fld"><label class="lbl" for="cf-n">Night halts</label><input id="cf-n" class="inp" type="number" min="0" [(ngModel)]="fare.nightHalts" /></div>
                <div class="fld"><label class="lbl" for="cf-x">Tolls / extras (₹)</label><input id="cf-x" class="inp" type="number" min="0" [(ngModel)]="fare.additionalCharges" /></div>
              </div>
              <div class="fld"><label class="lbl" for="cf-xn">What the extras were for</label><input id="cf-xn" class="inp" maxlength="200" [(ngModel)]="fare.additionalChargesNote" /></div>
              <div class="fld"><label class="lbl" for="cf-r">Reason for the correction</label><textarea id="cf-r" class="inp" rows="2" maxlength="500" [(ngModel)]="fare.reason"></textarea></div>
            }
          }
          @if (error()) { <div class="errorbox">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="close()">Close</button>
            <button type="button" class="btn" style="flex:1" [class.dang]="dlg === 'cancel'" [disabled]="busy()" (click)="submit(dlg)">
              @if (busy()) { <span class="spin"></span> } @else { {{ submitLabels[dlg] }} }
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminCarBookingDetailComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly CarBookingStatus = CarBookingStatus;
  readonly statusLabels = CarBookingStatusLabels;
  readonly paymentLabels = CarPaymentStatusLabels;
  readonly methodLabels = PaymentMethodLabels;
  readonly offlineMethods = OfflinePaymentMethods;
  readonly fuel = FuelTypeLabels;
  readonly when = istDateTime;
  readonly directions = directionsLink;
  readonly kmText = kmBreakdown;
  readonly duration = durationLabel;
  readonly submitLabels: Record<Dialog, string> = {
    cancel: 'Cancel booking',
    razorpay: 'Issue refund',
    manual: 'Record refund',
    settle: 'Mark received',
    fare: 'Save correction',
  };

  readonly state = signal<LoadState>('loading');
  readonly detail = signal<AdminCarBookingDetail | null>(null);
  readonly dialog = signal<Dialog | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notesBusy = signal(false);
  readonly notesError = signal<string | null>(null);

  // Dialog fields
  reason = '';
  waiveRefund = false;
  amount = 0;
  method: PaymentMethod = PaymentMethod.Upi;
  reference = '';
  notes = '';
  fare = { startOdometerKm: 0, endOdometerKm: 0, nightHalts: 0, additionalCharges: 0, additionalChargesNote: '', reason: '' };

  private readonly booking = computed(() => this.detail()?.booking ?? null);
  readonly canCancel = computed(() => {
    const s = this.booking()?.status;
    return s === CarBookingStatus.PendingPayment || s === CarBookingStatus.Confirmed;
  });
  readonly paidOnline = computed(() => this.booking()?.paymentStatus === CarPaymentStatus.BookingAmountPaid);
  readonly refundStage = computed<'pending' | 'issued' | 'done' | null>(() => {
    switch (this.booking()?.paymentStatus) {
      case CarPaymentStatus.RefundPending:
        return 'pending';
      case CarPaymentStatus.RefundProcessing:
        return 'issued';
      case CarPaymentStatus.Refunded:
        return 'done';
      default:
        return null;
    }
  });
  readonly statusBadge = computed(() => carBookingBadgeClass(this.booking()?.status ?? CarBookingStatus.PendingPayment));
  readonly paymentBadge = computed(() => carPaymentBadgeClass(this.booking()?.paymentStatus ?? CarPaymentStatus.Unpaid));

  private get id(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  methodLabel(method: number | null): string {
    return method === null ? "—" : (PaymentMethodLabels[method as PaymentMethod] ?? "—");
  }

  ngOnInit(): void {
    this.cars.booking(this.id).subscribe({
      next: (d) => {
        this.show(d);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  open(dialog: Dialog): void {
    const b = this.booking()!;
    this.error.set(null);
    this.reason = '';
    this.waiveRefund = false;
    this.amount = b.bookingAmount;
    this.method = PaymentMethod.Upi;
    this.reference = '';
    if (dialog === 'fare' && b.trip) {
      this.fare = {
        startOdometerKm: b.trip.startOdometerKm,
        endOdometerKm: b.trip.endOdometerKm ?? b.trip.startOdometerKm,
        nightHalts: b.trip.nightHalts ?? 0,
        additionalCharges: b.final?.additionalCharges ?? 0,
        additionalChargesNote: b.final?.additionalChargesNote ?? '',
        reason: '',
      };
    }
    this.dialog.set(dialog);
  }

  close(): void {
    if (!this.busy()) this.dialog.set(null);
  }

  submit(dialog: Dialog): void {
    const validation = this.validate(dialog);
    if (validation) {
      this.error.set(validation);
      return;
    }
    const id = this.id;
    const calls: Record<Dialog, () => Observable<AdminCarBookingDetail>> = {
      cancel: () => this.cars.cancelBooking(id, this.reason.trim(), this.paidOnline() && this.waiveRefund),
      razorpay: () => this.cars.refundRazorpay(id, Number(this.amount)),
      manual: () => this.cars.refundManual(id, Number(this.amount), this.method, this.reference.trim() || null),
      settle: () => this.cars.settleRefund(id),
      fare: () =>
        this.cars.correctFare(id, {
          startOdometerKm: Number(this.fare.startOdometerKm),
          endOdometerKm: Number(this.fare.endOdometerKm),
          nightHalts: Number(this.fare.nightHalts),
          additionalCharges: Number(this.fare.additionalCharges) || 0,
          additionalChargesNote: this.fare.additionalChargesNote.trim() || null,
          reason: this.fare.reason.trim(),
        }),
    };
    this.busy.set(true);
    this.error.set(null);
    calls[dialog]().subscribe({
      next: (d) => {
        this.show(d);
        this.busy.set(false);
        this.dialog.set(null);
        this.toast.success(SUCCESS[dialog]);
        this.cars.dashboard().subscribe({ error: () => undefined });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(apiErrorMessage(err));
      },
    });
  }

  saveNotes(): void {
    this.notesBusy.set(true);
    this.notesError.set(null);
    this.cars.saveNotes(this.id, this.notes.trim() || null).subscribe({
      next: (d) => {
        this.show(d);
        this.notesBusy.set(false);
        this.toast.success('Notes saved.');
      },
      error: (err) => {
        this.notesBusy.set(false);
        this.notesError.set(apiErrorMessage(err, 'Could not save notes.'));
      },
    });
  }

  /** Quick checks so obvious mistakes don't need a round trip; the server re-checks everything. */
  private validate(dialog: Dialog): string | null {
    const b = this.booking()!;
    switch (dialog) {
      case 'cancel':
        return this.reason.trim() ? null : 'Please give a reason. The customer will see it.';
      case 'razorpay':
      case 'manual':
        return this.amount > 0 && this.amount <= b.bookingAmount ? null : `Enter an amount between ₹1 and ₹${b.bookingAmount}.`;
      case 'fare':
        if (Number(this.fare.endOdometerKm) < Number(this.fare.startOdometerKm)) return 'End km can’t be less than start km.';
        if (Number(this.fare.additionalCharges) > 0 && !this.fare.additionalChargesNote.trim()) return 'Say what the extra charges were for.';
        return this.fare.reason.trim() ? null : 'Please give a reason for the correction.';
      default:
        return null;
    }
  }

  private show(d: AdminCarBookingDetail): void {
    this.detail.set(d);
    this.notes = d.adminNotes ?? '';
  }
}

const SUCCESS: Record<Dialog, string> = {
  cancel: 'Booking cancelled.',
  razorpay: 'Refund issued through Razorpay.',
  manual: 'Manual refund recorded.',
  settle: 'Refund marked as received.',
  fare: 'Final fare corrected.',
};
