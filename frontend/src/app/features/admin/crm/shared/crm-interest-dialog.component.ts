import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import { CrmDeparture, CrmEnquiryStatuses, CrmStatus, CrmStatusLabels } from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/** "Add trip interest" for one customer: trip, optional departure, travellers, stage and a follow-up date. */
@Component({
  selector: 'app-crm-interest-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <form class="modal crm-modal" [formGroup]="form" (ngSubmit)="save()" (click)="$event.stopPropagation()" novalidate>
          <h4>Add trip interest{{ customerName ? ' · ' + customerName : '' }}</h4>
          <div class="f2">
            <div class="fld">
              <span class="lbl">Trip</span>
              <select class="inp" formControlName="tripId">
                <option [ngValue]="null">Choose a trip…</option>
                @for (t of trips(); track t.tripId) { <option [ngValue]="t.tripId">{{ t.title }}</option> }
              </select>
            </div>
            <div class="fld">
              <span class="lbl">Departure (optional)</span>
              <select class="inp" formControlName="tripDateSlotId" [attr.disabled]="form.controls.tripId.value ? null : true">
                <option [ngValue]="null">Date not decided</option>
                @for (d of departures(); track d.tripDateSlotId) {
                  <option [ngValue]="d.tripDateSlotId">{{ d.startDate | date: 'd MMM y' }} · {{ d.availableSeats }} left · ₹{{ d.currentPrice | number: '1.0-0' }}</option>
                }
              </select>
            </div>
          </div>
          <div class="f3">
            <div class="fld">
              <span class="lbl">Travellers</span>
              <input class="inp" type="number" min="1" max="100" formControlName="travellers" />
            </div>
            <div class="fld">
              <span class="lbl">Stage</span>
              <select class="inp" formControlName="status">
                @for (s of stages; track s) { <option [ngValue]="s">{{ labels[s] }}</option> }
              </select>
            </div>
            <div class="fld">
              <span class="lbl">Follow up on</span>
              <input class="inp" type="date" formControlName="nextFollowUpDate" />
            </div>
          </div>
          <div class="f2">
            <div class="fld">
              <span class="lbl">Expected amount (₹)</span>
              <input class="inp" type="number" min="0" formControlName="expectedAmount" />
            </div>
            <div class="fld">
              <span class="lbl">Notes</span>
              <input class="inp" formControlName="notes" maxlength="2000" placeholder="Wants twin sharing" />
            </div>
          </div>
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="submit" class="btn" style="flex:1" [disabled]="saving()">
              @if (saving()) { <span class="spin"></span> } @else { Add interest }
            </button>
          </div>
        </form>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmInterestDialogComponent implements OnChanges {
  private readonly fb = inject(FormBuilder);
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);

  @Input() open = false;
  @Input({ required: true }) customerId!: number;
  @Input() customerName: string | null = null;
  /** Pre-selected trip / departure (e.g. when adding from a departure's page). */
  @Input() tripId: number | null = null;
  @Input() tripDateSlotId: number | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<number>();

  readonly stages = CrmEnquiryStatuses;
  readonly labels = CrmStatusLabels;
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly departures = signal<CrmDeparture[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    tripId: [null as number | null, Validators.required],
    tripDateSlotId: [null as number | null],
    travellers: [2, [Validators.required, Validators.min(1), Validators.max(100)]],
    status: [CrmStatus.Interested as CrmStatus],
    nextFollowUpDate: [''],
    expectedAmount: [null as number | null, Validators.min(0)],
    notes: [''],
  });

  constructor() {
    this.form.controls.tripId.valueChanges.subscribe((tripId) => this.loadDepartures(tripId, null));
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.error.set(null);
    this.form.reset({ tripId: null, tripDateSlotId: null, travellers: 2, status: CrmStatus.Interested, nextFollowUpDate: '', expectedAmount: null, notes: '' });
    if (this.trips().length === 0) {
      this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    }
    if (this.tripId) {
      this.form.controls.tripId.setValue(this.tripId, { emitEvent: false });
      this.loadDepartures(this.tripId, this.tripDateSlotId);
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Choose a trip and the number of travellers.');
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.crm
      .createInterest({
        customerId: this.customerId,
        tripId: v.tripId!,
        tripDateSlotId: v.tripDateSlotId,
        travellers: v.travellers!,
        status: v.status!,
        notes: v.notes?.trim() || null,
        expectedAmount: v.expectedAmount,
        nextFollowUpDate: v.nextFollowUpDate || null,
      })
      .subscribe({
        next: (r) => {
          this.saving.set(false);
          this.saved.emit(r.customerTripInterestId);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err, 'Could not add the trip interest.'));
        },
      });
  }

  private loadDepartures(tripId: number | null, select: number | null): void {
    this.departures.set([]);
    this.form.controls.tripDateSlotId.setValue(null, { emitEvent: false });
    if (!tripId) return;
    this.crm.departures({ tripId, pageSize: 100 }).subscribe({
      next: (r) => {
        this.departures.set(r.items);
        if (select && r.items.some((d) => d.tripDateSlotId === select)) this.form.controls.tripDateSlotId.setValue(select);
      },
      error: () => undefined,
    });
  }
}
