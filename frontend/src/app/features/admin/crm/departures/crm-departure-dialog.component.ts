import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import { CrmDeparture } from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/**
 * Create a departure (a real, bookable trip date), or edit an existing one's CRM details — vehicle,
 * minimum bookings and prices. Dates, seats and on/off for an existing departure stay in Admin → Trips.
 */
@Component({
  selector: 'app-crm-departure-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <form class="modal crm-modal" [formGroup]="form" (ngSubmit)="save()" (click)="$event.stopPropagation()" novalidate>
          <h4>{{ departure ? 'Edit departure · ' + departure.tripTitle : 'New departure' }}</h4>

          @if (!departure) {
            <div class="fld">
              <span class="lbl">Trip</span>
              <select class="inp" formControlName="tripId">
                <option [ngValue]="null">Choose a trip…</option>
                @for (t of trips(); track t.tripId) { <option [ngValue]="t.tripId">{{ t.title }} (₹{{ t.amountPerPerson | number: '1.0-0' }})</option> }
              </select>
            </div>
            <div class="f3">
              <div class="fld">
                <span class="lbl">Departure date</span>
                <input class="inp" type="date" formControlName="startDate" [min]="today" />
              </div>
              <div class="fld">
                <span class="lbl">Return date</span>
                <input class="inp" type="date" formControlName="endDate" [min]="form.controls.startDate.value || today" />
              </div>
              <div class="fld">
                <span class="lbl">Total seats</span>
                <input class="inp" type="number" min="1" max="500" formControlName="totalSeats" />
              </div>
            </div>
          } @else {
            <p class="note" style="margin:-6px 0 14px">
              {{ departure.startDate | date: 'd MMM y' }} → {{ departure.endDate | date: 'd MMM y' }} · {{ departure.totalSeats }} seats.
              Dates, seats and switching it on/off are changed in Admin → Trips.
            </p>
          }

          <div class="f2">
            <div class="fld">
              <span class="lbl">Vehicle</span>
              <input class="inp" formControlName="vehicle" maxlength="100" placeholder="AC Traveller, Innova…" />
            </div>
            <div class="fld">
              <span class="lbl">Minimum bookings to confirm the trip</span>
              <input class="inp" type="number" min="1" formControlName="minimumBookedSeats" placeholder="e.g. 10 seats" />
            </div>
          </div>

          <div class="f3">
            <div class="fld">
              <span class="lbl">Price per person (₹)</span>
              <input class="inp" type="number" min="1" formControlName="pricePerPerson" [placeholder]="tripPrice() ? 'Trip price ₹' + tripPrice() : 'Trip price'" />
            </div>
            <div class="fld">
              <span class="lbl">Early-booking price (₹)</span>
              <input class="inp" type="number" min="1" formControlName="earlyBirdPrice" placeholder="Optional" />
            </div>
            <div class="fld">
              <span class="lbl">Early booking ends on</span>
              <input class="inp" type="date" formControlName="earlyBirdEndsOn" [attr.disabled]="form.controls.earlyBirdPrice.value ? null : true" />
            </div>
          </div>
          <p class="note" style="margin:-4px 0 14px">
            The website and admin bookings charge these prices (the early price up to and including its last day).
            Bookings already made keep the price they were made at. The online reservation amount per seat doesn't change.
          </p>

          @if (!departure) {
            <label class="crm-check" style="margin-bottom:14px">
              <input type="checkbox" formControlName="openForBooking" /> Open for booking on the website now
            </label>
          }

          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="submit" class="btn" style="flex:1" [disabled]="saving()">
              @if (saving()) { <span class="spin"></span> } @else { {{ departure ? 'Save' : 'Create departure' }} }
            </button>
          </div>
        </form>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmDepartureDialogComponent implements OnChanges {
  private readonly fb = inject(FormBuilder);
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);

  @Input() open = false;
  /** Edit this departure's CRM details; null creates a new departure. */
  @Input() departure: CrmDeparture | null = null;
  /** Pre-selected trip for a new departure. */
  @Input() tripId: number | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<number>();

  readonly today = new Date().toISOString().slice(0, 10);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    tripId: [null as number | null],
    startDate: [''],
    endDate: [''],
    totalSeats: [16 as number | null, [Validators.min(1), Validators.max(500)]],
    vehicle: [''],
    minimumBookedSeats: [null as number | null, Validators.min(1)],
    pricePerPerson: [null as number | null, Validators.min(1)],
    earlyBirdPrice: [null as number | null, Validators.min(1)],
    earlyBirdEndsOn: [''],
    openForBooking: [true],
  });

  tripPrice(): number | null {
    if (this.departure) return this.departure.pricePerPerson ? null : this.departure.regularPrice;
    return this.trips().find((t) => t.tripId === this.form.controls.tripId.value)?.amountPerPerson ?? null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.error.set(null);
    const d = this.departure;
    this.form.reset({
      tripId: d?.tripId ?? this.tripId,
      startDate: '',
      endDate: '',
      totalSeats: 16,
      vehicle: d?.vehicle ?? '',
      minimumBookedSeats: d?.minimumBookedSeats ?? null,
      pricePerPerson: d?.pricePerPerson ?? null,
      earlyBirdPrice: d?.earlyBirdPrice ?? null,
      earlyBirdEndsOn: d?.earlyBirdEndsOn ?? '',
      openForBooking: true,
    });
    if (!d && this.trips().length === 0) {
      this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    }
  }

  save(): void {
    const v = this.form.getRawValue();
    const errors: string[] = [];
    if (!this.departure) {
      if (!v.tripId) errors.push('Choose a trip.');
      if (!v.startDate || !v.endDate) errors.push('Choose the departure and return dates.');
      else if (v.endDate < v.startDate) errors.push('The return date is before the departure date.');
      if (!v.totalSeats || v.totalSeats < 1) errors.push('Enter the number of seats.');
    }
    if (v.earlyBirdPrice && !v.earlyBirdEndsOn) errors.push('Choose when the early-booking price ends.');
    if (this.form.invalid) errors.push('Numbers must be 1 or more.');
    if (errors.length) {
      this.error.set(errors.join(' '));
      return;
    }

    const details = {
      vehicle: v.vehicle?.trim() || null,
      minimumBookedSeats: v.minimumBookedSeats || null,
      pricePerPerson: v.pricePerPerson || null,
      earlyBirdPrice: v.earlyBirdPrice || null,
      earlyBirdEndsOn: v.earlyBirdPrice ? v.earlyBirdEndsOn || null : null,
    };

    this.saving.set(true);
    const done = {
      next: (id: number) => {
        this.saving.set(false);
        this.saved.emit(id);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save the departure.'));
      },
    };

    if (this.departure) {
      const id = this.departure.tripDateSlotId;
      this.crm.updateDepartureDetails(id, details).subscribe({ next: () => done.next(id), error: done.error });
    } else {
      this.crm
        .createDeparture({ tripId: v.tripId!, startDate: v.startDate!, endDate: v.endDate!, totalSeats: v.totalSeats!, openForBooking: !!v.openForBooking, ...details })
        .subscribe({ next: (r) => done.next(r.tripDateSlotId), error: done.error });
    }
  }
}
