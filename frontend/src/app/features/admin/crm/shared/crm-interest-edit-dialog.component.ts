import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { CrmDeparture, CrmTripInterest } from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/** Edit a trip interest: departure (same trip), travellers, expected amount, notes and lost reason. */
@Component({
  selector: 'app-crm-interest-edit-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (open && interest) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <form class="modal crm-modal" [formGroup]="form" (ngSubmit)="save()" (click)="$event.stopPropagation()" novalidate>
          <h4>Edit · {{ interest.customerName || 'Customer' }} · {{ interest.tripTitle }}</h4>
          <div class="f2">
            <div class="fld">
              <span class="lbl">Departure</span>
              <select class="inp" formControlName="tripDateSlotId" [attr.disabled]="interest.bookingId ? true : null">
                <option [ngValue]="null">Date not decided</option>
                @for (d of departures(); track d.tripDateSlotId) {
                  <option [ngValue]="d.tripDateSlotId">{{ d.startDate | date: 'd MMM y' }} · {{ d.availableSeats }} left</option>
                }
              </select>
              @if (interest.bookingId) { <div class="note" style="margin-top:4px">Booked — change the date on the booking itself.</div> }
            </div>
            <div class="fld">
              <span class="lbl">Travellers</span>
              <input class="inp" type="number" min="1" max="100" formControlName="travellers" />
            </div>
          </div>
          <div class="f2">
            <div class="fld">
              <span class="lbl">Expected amount (₹)</span>
              <input class="inp" type="number" min="0" formControlName="expectedAmount" />
            </div>
            <div class="fld">
              <span class="lbl">Lost reason</span>
              <input class="inp" formControlName="lostReason" maxlength="500" placeholder="Only if it didn't work out" />
            </div>
          </div>
          <div class="fld">
            <span class="lbl">Notes</span>
            <textarea class="inp" rows="3" formControlName="notes" maxlength="2000"></textarea>
          </div>
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="submit" class="btn" style="flex:1" [disabled]="saving()">
              @if (saving()) { <span class="spin"></span> } @else { Save }
            </button>
          </div>
        </form>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmInterestEditDialogComponent implements OnChanges {
  private readonly fb = inject(FormBuilder);
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() interest: CrmTripInterest | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly departures = signal<CrmDeparture[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    tripDateSlotId: [null as number | null],
    travellers: [1, [Validators.required, Validators.min(1), Validators.max(100)]],
    expectedAmount: [null as number | null, Validators.min(0)],
    lostReason: [''],
    notes: [''],
  });

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    const i = this.interest;
    if (!this.open || !i) return;
    this.error.set(null);
    this.form.reset({ tripDateSlotId: i.tripDateSlotId, travellers: i.travellers, expectedAmount: i.expectedAmount, lostReason: i.lostReason ?? '', notes: i.notes ?? '' });
    // Upcoming departures of the same trip, plus the current one even if it has already left.
    this.crm.departures({ tripId: i.tripId, includePast: true, pageSize: 100 }).subscribe({
      next: (r) => this.departures.set(r.items.filter((d) => d.tripDateSlotId === i.tripDateSlotId || d.endDate >= new Date().toISOString().slice(0, 10))),
      error: () => this.departures.set([]),
    });
  }

  save(): void {
    if (this.form.invalid || !this.interest) {
      this.error.set('Travellers must be between 1 and 100, amounts 0 or more.');
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.crm
      .updateInterest(this.interest.customerTripInterestId, {
        tripId: this.interest.tripId,
        tripDateSlotId: this.interest.bookingId ? this.interest.tripDateSlotId : v.tripDateSlotId,
        travellers: v.travellers!,
        notes: v.notes?.trim() || null,
        expectedAmount: v.expectedAmount,
        lostReason: v.lostReason?.trim() || null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.saved.emit();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err, 'Could not save.'));
        },
      });
  }
}
