import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { CrmAdminUser, CrmEnquiryStatuses, CrmFollowUp, CrmStatus, CrmStatusLabels, CrmTripInterest } from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { daysFromToday, todayKey } from '../crm-utils';

/** Quick date picks used by every follow-up dialog. */
const QUICK_DAYS: { label: string; days: number }[] = [
  { label: 'Tomorrow', days: 1 },
  { label: 'In 3 days', days: 3 },
  { label: 'Next week', days: 7 },
];

/** Schedule a follow-up for a customer, optionally about one of their trip interests. */
@Component({
  selector: 'app-crm-followup-add-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>Add follow-up{{ customerName ? ' · ' + customerName : '' }}</h4>
          <div class="fld">
            <span class="lbl">Date</span>
            <input class="inp" type="date" [(ngModel)]="dueDate" [min]="today" />
            <div class="row wrap crm-quick">
              <button type="button" class="btn sm ghost" (click)="dueDate = today">Today</button>
              @for (q of quick; track q.days) { <button type="button" class="btn sm ghost" (click)="dueDate = pick(q.days)">{{ q.label }}</button> }
            </div>
          </div>
          @if (interests.length > 0) {
            <div class="fld">
              <span class="lbl">About</span>
              <select class="inp" [(ngModel)]="interestId">
                <option [ngValue]="null">General</option>
                @for (i of interests; track i.customerTripInterestId) { <option [ngValue]="i.customerTripInterestId">{{ i.tripTitle }}</option> }
              </select>
            </div>
          }
          <div class="fld">
            <span class="lbl">What to do</span>
            <input class="inp" [(ngModel)]="note" maxlength="1000" placeholder="Share itinerary, confirm pickup point" />
          </div>
          @if (admins.length > 1) {
            <div class="fld">
              <span class="lbl">Assigned to</span>
              <select class="inp" [(ngModel)]="assignedAdminUserId">
                <option [ngValue]="null">Customer's owner (or me)</option>
                @for (a of admins; track a.adminUserId) { <option [ngValue]="a.adminUserId">{{ a.name }}</option> }
              </select>
            </div>
          }
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="button" class="btn" style="flex:1" [disabled]="saving() || !dueDate" (click)="save()">
              @if (saving()) { <span class="spin"></span> } @else { Schedule }
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmFollowUpAddDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() customerId: number | null = null;
  @Input() customerName: string | null = null;
  @Input() interests: CrmTripInterest[] = [];
  /** Pre-selected trip interest (e.g. from a departure's customer row). */
  @Input() interestIdPreset: number | null = null;
  @Input() admins: CrmAdminUser[] = [];
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly quick = QUICK_DAYS;
  readonly today = todayKey();
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  dueDate = '';
  interestId: number | null = null;
  note = '';
  assignedAdminUserId: number | null = null;

  pick(days: number): string {
    return daysFromToday(days);
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.dueDate = daysFromToday(1);
    this.interestId = this.interestIdPreset;
    this.note = '';
    this.assignedAdminUserId = null;
    this.error.set(null);
  }

  save(): void {
    if (!this.customerId || !this.dueDate) return;
    this.saving.set(true);
    this.crm
      .createFollowUp({ customerId: this.customerId, customerTripInterestId: this.interestId, dueDate: this.dueDate, note: this.note.trim() || null, assignedAdminUserId: this.assignedAdminUserId })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.saved.emit();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err, 'Could not schedule the follow-up.'));
        },
      });
  }
}

/**
 * Mark a follow-up done: what happened, optionally the next follow-up date, and optionally move the
 * lead to a new stage (Booked / Completed aren't offered — those follow real bookings).
 */
@Component({
  selector: 'app-crm-followup-complete-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open && followUp) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>Follow-up done · {{ followUp.customerName || 'Customer' }}</h4>
          <div class="fld">
            <span class="lbl">What happened?</span>
            <input class="inp" [(ngModel)]="outcome" maxlength="1000" placeholder="Spoke — wants 3 seats, will pay advance on Friday" />
          </div>
          <div class="fld">
            <span class="lbl">Move {{ followUp.tripTitle ? 'this trip' : 'the customer' }} to</span>
            <select class="inp" [(ngModel)]="newStatus">
              <option [ngValue]="null">Keep as {{ labels[current] }}</option>
              @for (s of stages; track s) {
                @if (s !== current) { <option [ngValue]="s">{{ labels[s] }}</option> }
              }
            </select>
          </div>
          <div class="fld">
            <span class="lbl">Next follow-up (optional)</span>
            <input class="inp" type="date" [(ngModel)]="nextDueDate" [min]="today" />
            <div class="row wrap crm-quick">
              @for (q of quick; track q.days) { <button type="button" class="btn sm ghost" (click)="nextDueDate = pick(q.days)">{{ q.label }}</button> }
              @if (nextDueDate) { <button type="button" class="linkbtn" (click)="nextDueDate = ''">No next follow-up</button> }
            </div>
          </div>
          @if (nextDueDate) {
            <div class="fld">
              <span class="lbl">Next time</span>
              <input class="inp" [(ngModel)]="nextNote" maxlength="1000" placeholder="Remind about the advance" />
            </div>
          }
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="button" class="btn" style="flex:1" [disabled]="saving()" (click)="save()">
              @if (saving()) { <span class="spin"></span> } @else { Mark done }
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmFollowUpCompleteDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() followUp: CrmFollowUp | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly quick = QUICK_DAYS;
  readonly today = todayKey();
  readonly labels = CrmStatusLabels;
  readonly stages = CrmEnquiryStatuses;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  outcome = '';
  newStatus: CrmStatus | null = null;
  nextDueDate = '';
  nextNote = '';

  get current(): CrmStatus {
    return this.followUp?.interestStatus ?? this.followUp?.customerStatus ?? CrmStatus.NewLead;
  }

  pick(days: number): string {
    return daysFromToday(days);
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.outcome = '';
    this.newStatus = null;
    this.nextDueDate = '';
    this.nextNote = '';
    this.error.set(null);
  }

  save(): void {
    if (!this.followUp) return;
    this.saving.set(true);
    this.crm
      .completeFollowUp(this.followUp.crmFollowUpId, {
        outcome: this.outcome.trim() || null,
        nextDueDate: this.nextDueDate || null,
        nextNote: this.nextDueDate ? this.nextNote.trim() || null : null,
        newStatus: this.newStatus,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.saved.emit();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(err, 'Could not complete the follow-up.'));
        },
      });
  }
}

/** Move a follow-up to another day. */
@Component({
  selector: 'app-crm-followup-reschedule-dialog',
  standalone: true,
  imports: [FormsModule, CommonModule],
  template: `
    @if (open && followUp) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>Reschedule · {{ followUp.customerName || 'Customer' }}</h4>
          <p class="note" style="margin:-6px 0 12px">Was due {{ followUp.dueDate | date: 'd MMM y' }}{{ followUp.note ? ' — ' + followUp.note : '' }}</p>
          <div class="fld">
            <span class="lbl">New date</span>
            <input class="inp" type="date" [(ngModel)]="dueDate" [min]="today" />
            <div class="row wrap crm-quick">
              <button type="button" class="btn sm ghost" (click)="dueDate = today">Today</button>
              @for (q of quick; track q.days) { <button type="button" class="btn sm ghost" (click)="dueDate = pick(q.days)">{{ q.label }}</button> }
            </div>
          </div>
          <div class="fld">
            <span class="lbl">Note (optional)</span>
            <input class="inp" [(ngModel)]="note" maxlength="1000" placeholder="Customer asked to call after salary day" />
          </div>
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="button" class="btn" style="flex:1" [disabled]="saving() || !dueDate" (click)="save()">
              @if (saving()) { <span class="spin"></span> } @else { Reschedule }
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmFollowUpRescheduleDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() followUp: CrmFollowUp | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly quick = QUICK_DAYS;
  readonly today = todayKey();
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  dueDate = '';
  note = '';

  pick(days: number): string {
    return daysFromToday(days);
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.dueDate = daysFromToday(1);
    this.note = '';
    this.error.set(null);
  }

  save(): void {
    if (!this.followUp || !this.dueDate) return;
    this.saving.set(true);
    this.crm.rescheduleFollowUp(this.followUp.crmFollowUpId, this.dueDate, this.note.trim() || null).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not reschedule.'));
      },
    });
  }
}
